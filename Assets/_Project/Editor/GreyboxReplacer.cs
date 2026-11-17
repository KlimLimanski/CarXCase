using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GreyboxReplacer : EditorWindow
{
    const string Root = "Assets/_Project/";
    const string Marker = "__ReplacementVisual";
    static readonly string[] Names = { "01_Hudson", "02_Mercer", "03_Fulton", "04_Rosette", "05_Union" };
    readonly GameObject[] buildings = new GameObject[5];
    readonly List<Plan> plans = new List<Plan>();
    GameObject sedan;
    bool replaceBuildings = true, replaceCar = true, uniform = true, interiorOnly = true;
    Vector3 buildingAxes, carAxes;
    float buildingSize = 1, carSize = 1;
    Vector2 scroll;
    string status = "Сначала проверьте сопоставления на открытой сцене.";

    sealed class Plan
    {
        public Transform target;
        public GameObject asset;
        public MeshRenderer[] oldVisuals;
        public Bounds bounds;
        public bool car;
    }

    [MenuItem("Tools/Replace Greyboxes")]
    static void Open() { GetWindow<GreyboxReplacer>("Replace Greyboxes").minSize = new Vector2(600, 540); }

    void OnEnable()
    {
        for (int i = 0; i < Names.Length; i++)
            buildings[i] = Load("Arts/Buildings/Street_Buildings_1Scene/" + Names[i]);
        sedan = Load("Arts/Car/Sedan_2000/Sedan_2000_Rear_Passenger");
    }

    static GameObject Load(string stem)
    {
        foreach (string ext in new[] { ".prefab", ".fbx", ".blend", ".glb" })
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Root + stem + ext);
            if (asset != null) return asset;
        }
        return null;
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Активная сцена", SceneManager.GetActiveScene().name);
        EditorGUILayout.HelpBox("Заменяется геометрия примитивов. Существующие Transform, коллайдеры, камера и компоненты остаются. Материалы берутся из импортированных моделей. Сцена автоматически не сохраняется.", MessageType.Info);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUI.BeginChangeCheck();
        replaceBuildings = EditorGUILayout.Toggle("Заменить Building_*", replaceBuildings);
        for (int i = 0; i < buildings.Length; i++)
            buildings[i] = (GameObject)EditorGUILayout.ObjectField(Names[i], buildings[i], typeof(GameObject), false);
        buildingAxes = EditorGUILayout.Vector3Field("Поворот зданий (градусы)", buildingAxes);
        buildingSize = EditorGUILayout.FloatField("Множитель размера зданий", buildingSize);
        EditorGUILayout.Space();
        replaceCar = EditorGUILayout.Toggle("Заменить Hero_Car", replaceCar);
        sedan = (GameObject)EditorGUILayout.ObjectField("Sedan_2000", sedan, typeof(GameObject), false);
        interiorOnly = EditorGUILayout.Toggle("Только салон", interiorOnly);
        carAxes = EditorGUILayout.Vector3Field("Поворот авто (градусы)", carAxes);
        carSize = EditorGUILayout.FloatField("Множитель размера авто", carSize);
        uniform = EditorGUILayout.Toggle("Сохранять пропорции", uniform);
        EditorGUILayout.HelpBox("В режиме «Только салон» выберите модель салона без внешнего кузова. Оси моделей заранее не предполагаются: задайте поправку вручную. Масштаб рассчитывается по границам выбранных примитивов. Совпадение сидений с персонажем необходимо проверить визуально.", MessageType.Info);
        if (EditorGUI.EndChangeCheck()) plans.Clear();
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("Проверить сопоставления")) Scan();
            foreach (var plan in plans)
                EditorGUILayout.LabelField(HierarchyPath(plan.target) + " → " + AssetDatabase.GetAssetPath(plan.asset), EditorStyles.wordWrappedLabel);
            using (new EditorGUI.DisabledScope(plans.Count == 0))
                if (GUILayout.Button("Заменить грейбоксы (единый Undo)")) Replace();
        }
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(status, MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    bool Scan()
    {
        plans.Clear();
        try
        {
            if (!Positive(buildingSize) || !Positive(carSize)) throw new Exception("Множители должны быть положительными и конечными.");
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) throw new Exception("Откройте сцену.");
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) throw new Exception("Выйдите из Prefab Mode.");
            var nodes = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true)).ToArray();
            if (replaceBuildings)
            {
                int index = 0;
                foreach (var node in nodes.Where(x => x.name.StartsWith("Building_", StringComparison.Ordinal)).OrderBy(HierarchyPath))
                {
                    if (node.Find(Marker) != null) continue;
                    var visuals = node.GetComponentsInChildren<MeshRenderer>(true).Where(IsPrimitive).ToArray();
                    if (visuals.Length == 0) continue;
                    Add(node, buildings[index++ % buildings.Length], visuals, false);
                }
            }
            if (replaceCar)
            {
                var cars = nodes.Where(x => x.name == "Hero_Car").ToArray();
                if (cars.Length > 1) throw new Exception("Найдено несколько Hero_Car — оставьте один целевой объект.");
                if (cars.Length == 1 && cars[0].Find(Marker) == null)
                {
                    var car = cars[0];
                    var visuals = car.GetComponentsInChildren<MeshRenderer>(true)
                        .Where(x => IsPrimitive(x) && IsCarPart(x.transform, car)).ToArray();
                    if (visuals.Length > 0) Add(car, sedan, visuals, true);
                }
            }
            status = plans.Count == 0 ? "Подходящих примитивов нет. Ожидаются Building_* и Hero_Car; уже заменённые объекты пропускаются." : "Запланировано замен: " + plans.Count + ". Здания распределяются по пяти моделям циклически. Проверьте список и настройки.";
            return plans.Count > 0;
        }
        catch (Exception e) { plans.Clear(); status = e.Message; return false; }
    }

    void Add(Transform target, GameObject asset, MeshRenderer[] visuals, bool car)
    {
        if (asset == null) throw new Exception("Модель для " + target.name + " не импортирована. Для .blend нужен рабочий импорт Blender; для .glb — glTF importer. Можно выбрать импортированный prefab/FBX вручную.");
        if (!AssetDatabase.GetAssetPath(asset).StartsWith(Root, StringComparison.Ordinal)) throw new Exception("Модель должна находиться внутри " + Root);
        if (PrefabUtility.IsPartOfPrefabInstance(target)) throw new Exception(target.name + ": сначала распакуйте исходный prefab, чтобы Undo безопасно менял его компоненты.");
        plans.Add(new Plan { target = target, asset = asset, oldVisuals = visuals, bounds = BoundsIn(visuals, target), car = car });
    }

    bool IsCarPart(Transform node, Transform car)
    {
        while (node.parent != null && node.parent != car) node = node.parent;
        if (node.parent != car) return false;
        string n = node.name;
        // Preserve wheel and steering transforms/visuals, including animation references.
        if (n.StartsWith("Wheel_") || n.StartsWith("SteeringWheel_")) return false;
        bool cabin = n == "Dashboard" || n == "Radio" || n == "RearSeat" || n.StartsWith("FrontSeat_") || n.StartsWith("Headrest_");
        return cabin || (!interiorOnly && (n.StartsWith("Car_") || n == "Hood" || n == "Trunk" || n == "Roof" || n.StartsWith("Door_") || n.Contains("Pillar_")));
    }

    static bool IsPrimitive(MeshRenderer renderer)
    {
        var filter = renderer.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null || renderer.GetComponent<TextMesh>() != null) return false;
        string path = AssetDatabase.GetAssetPath(filter.sharedMesh);
        if (!string.IsNullOrEmpty(path) && !path.StartsWith("Library/") && !path.StartsWith("Resources/")) return false;
        return new[] { "Cube", "Cylinder", "Capsule", "Plane", "Sphere", "Quad" }.Contains(filter.sharedMesh.name);
    }

    void Replace()
    {
        if (!Scan()) return;
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Replace Greyboxes");
        try
        {
            var report = new List<string>();
            foreach (var plan in plans) { Apply(plan); report.Add(HierarchyPath(plan.target) + " → " + AssetDatabase.GetAssetPath(plan.asset)); }
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            status = "Заменено: " + plans.Count + ". Проверьте посадку моделей. Ctrl+Z отменяет всю операцию; Ctrl+S сохраняет сцену.";
            Debug.Log(status + "\n" + string.Join("\n", report));
            plans.Clear();
            SceneView.RepaintAll();
        }
        catch (Exception e)
        {
            Undo.RevertAllDownToGroup(group);
            plans.Clear();
            status = "Изменения отменены: " + e.Message;
            Debug.LogException(e);
        }
    }

    void Apply(Plan plan)
    {
        var holder = new GameObject(Marker);
        SceneManager.MoveGameObjectToScene(holder, plan.target.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(holder, "Create replacement holder");
        holder.transform.SetParent(plan.target, false);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(plan.asset, plan.target.gameObject.scene);
        if (model == null) throw new Exception("Не удалось создать модель " + plan.asset.name);
        Undo.RegisterCreatedObjectUndo(model, "Create replacement model");
        model.transform.SetParent(holder.transform, false);
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.Euler(plan.car ? carAxes : buildingAxes) * model.transform.localRotation;

        // Old wheel/steering visuals and their animated transforms stay authoritative.
        // Hide imported wheel assemblies to avoid duplicate static wheels.
        if (plan.car)
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                if (HasWheelAncestor(renderer.transform, model.transform)) renderer.enabled = false;
        var renderers = model.GetComponentsInChildren<Renderer>(true).Where(x => x.enabled && x.gameObject.activeInHierarchy && !(x is ParticleSystemRenderer)).ToArray();
        Bounds source = BoundsIn(renderers, holder.transform);
        Vector3 ratio = new Vector3(Ratio(plan.bounds.size.x, source.size.x), Ratio(plan.bounds.size.y, source.size.y), Ratio(plan.bounds.size.z, source.size.z));
        if (uniform) ratio = Vector3.one * Mathf.Min(ratio.x, Mathf.Min(ratio.y, ratio.z));
        ratio *= plan.car ? carSize : buildingSize;
        holder.transform.localScale = ratio;
        holder.transform.localPosition = plan.bounds.center - Vector3.Scale(source.center, ratio);
        Bounds fitted = BoundsIn(renderers, plan.target);
        holder.transform.localPosition += Vector3.up * (plan.bounds.min.y - fitted.min.y);
        // Keep source collision volumes; imported collision/light/camera rigs are not used.
        foreach (var component in model.GetComponentsInChildren<Component>(true))
            if (component is Collider || component is Rigidbody || component is Camera || component is AudioListener || component is Light)
                Undo.DestroyObjectImmediate(component);
        foreach (var renderer in plan.oldVisuals)
        {
            var filter = renderer.GetComponent<MeshFilter>();
            Undo.DestroyObjectImmediate(renderer);
            if (filter != null) Undo.DestroyObjectImmediate(filter);
        }
    }

    static bool HasWheelAncestor(Transform node, Transform root)
    {
        while (node != null && node != root)
        {
            string n = node.name.ToLowerInvariant();
            if (n.Contains("wheel") || n.Contains("tire") || n.Contains("tyre") || n.Contains("steering")) return true;
            node = node.parent;
        }
        return false;
    }

    static bool Positive(float value) { return value > 0 && !float.IsNaN(value) && !float.IsInfinity(value); }
    static float Ratio(float target, float source)
    {
        if (!Positive(target) || !Positive(source) || source < 0.00001f) throw new Exception("Нулевые/некорректные границы модели. Проверьте импорт и оси.");
        return target / source;
    }

    static Bounds BoundsIn(IEnumerable<Renderer> renderers, Transform space)
    {
        Bounds result = default;
        bool found = false;
        foreach (var renderer in renderers)
        {
            Bounds b = renderer.localBounds;
            Matrix4x4 matrix = space.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 point = matrix.MultiplyPoint3x4(corner);
                if (!found) { result = new Bounds(point, Vector3.zero); found = true; }
                else result.Encapsulate(point);
            }
        }
        if (!found) throw new Exception("Модель не содержит видимой геометрии.");
        return result;
    }

    static string HierarchyPath(Transform node)
    {
        return node.parent == null ? node.name : HierarchyPath(node.parent) + "/" + node.name;
    }
}

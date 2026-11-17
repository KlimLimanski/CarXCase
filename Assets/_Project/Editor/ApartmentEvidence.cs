using System;
using System.IO;
using System.Linq;
using DetectiveGame.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ApartmentEvidence
{
    const string ScenePath = "Assets/_Project/Scenes/01_FatherApartment.unity";
    const string AssetDir = "Assets/_Project/Arts/Apartaments/Evidence/";
    const string MaterialDir = AssetDir + "Materials/";
    const string VisualName = "Evidence_Visual";
    const string TargetName = "Evidence_AimTarget";

    [MenuItem("Tools/Brighton/Apartment: refresh inspectable evidence")]
    public static void ApplyAndSave()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Open 01_FatherApartment in Edit Mode.");
        Apply(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    internal static void Apply(Scene scene)
    {
        if (FatherApartmentScenario.IsInstalled(scene)) return;
        Directory.CreateDirectory(MaterialDir);
        AssetDatabase.Refresh();
        var roots = scene.GetRootGameObjects();
        var nodes = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var clues = roots.SelectMany(r => r.GetComponentsInChildren<ClueObject>(true)).ToArray();
        Transform Find(string name) => nodes.First(t => t && t.name == name);
        ClueObject Clue(string id) => clues.Single(c => c && c.clueId == id);
        Bounds Support(string name) => Find(name).GetComponent<Renderer>().bounds;

        float coffeeTableY = Support("CoffeeTable_Top").max.y;
        Place(Clue("clue_cups"), "Clue_Double_Coffee_Cups", new Vector3(-1.82f, coffeeTableY + .001f, -4.32f), 14,
            "Две чашки. Кофе не допит. Отец был не один.");

        var dining = Support("DiningTable_Top");
        Place(Clue("clue_envelope"), "Clue_Opened_Envelope", new Vector3(dining.center.x + .09f, dining.max.y + .001f, dining.center.z + .10f), -22,
            "Пусто. Ни марки, ни обратного адреса. Кто-то принёс его лично.");
        foreach (string name in new[] { "INT_Envelope_1", "INT_Envelope_2", "Envelope_Address" }) HideOriginals(Find(name));

        var shelf = Support("WallShelf.001");
        Place(Clue("clue_photo"), "Clue_Empty_Photo_Frame", new Vector3(shelf.center.x + .027f, shelf.max.y + .001f, shelf.center.z + .24f), 90,
            "Здесь стояла наша семейная фотография. Рамку оставили, снимок забрали.");

        var dresser = Clue("clue_dresser");
        var dresserBounds = dresser.GetComponent<Collider>().bounds;
        Place(dresser, "Clue_Ransacked_Dresser", new Vector3(dresserBounds.center.x, 0, dresserBounds.center.z), -90,
            "Все ящики выдвинуты. Кто-то здесь что-то искал.", backX: dresserBounds.max.x - .012f);

        Place(Clue("clue_newspaper"), "Clue_Circled_Newspaper", new Vector3(-1.91f, coffeeTableY + .001f, -3.65f), 165,
            "Пожар на складе в Ред-Хуке. Зачем отец обвёл эту заметку?");
        HideOriginals(Find("INT_Newspaper_Second"));

        Place(Clue("clue_bag"), "Clue_Canvas_Bag_With_Keys", new Vector3(-3.61f, .003f, -1.24f), 23,
            "Ключи и бумажник здесь. Отец без них из дома не выходил.");

        var note = Clue("clue_secret_note");
        note.inspectionComment = "«Не говори Лёше». Это почерк Михаила.";

        InstallRecordInspection(Find("RecordPlayer"), Find("RP_Record"));

        var officer = roots.SelectMany(g => g.GetComponentsInChildren<OfficerController>()).Single();
        officer.aimToInspectClues = true;
        officer.hud.showInspectionHud = true;
        var follow = officer.cameraTransform.GetComponent<ThirdPersonCamera>();
        if (follow)
        {
            follow.shoulderOffset = .45f;
            follow.mouseSensitivity = 1.45f;
            follow.orbitSmoothTime = .075f;
            follow.wallSmoothTime = .18f;
        }
    }

    static void Place(ClueObject clue, string assetName, Vector3 position, float yaw, string comment, float? backX = null)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDir + assetName + ".fbx");
        if (!asset) throw new InvalidOperationException($"Missing evidence mesh {assetName}");
        RemoveChild(clue.transform, VisualName);
        HideOriginals(clue.transform);

        var visual = (GameObject)PrefabUtility.InstantiatePrefab(asset, clue.gameObject.scene);
        visual.name = VisualName;
        visual.transform.SetParent(clue.transform, true);
        visual.transform.rotation = Quaternion.Euler(0, yaw, 0);
        var renderers = visual.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
            renderer.sharedMaterials = renderer.sharedMaterials.Select(Finish).ToArray();

        var bounds = BoundsOf(renderers);
        Vector3 measured = new Vector3(backX.HasValue ? bounds.max.x : bounds.center.x, bounds.min.y, bounds.center.z);
        if (backX.HasValue) position.x = backX.Value;
        visual.transform.position += position - measured;
        bounds = BoundsOf(renderers);
        clue.inspectionComment = comment;
        clue.highlightRenderers = renderers;
        clue.targetRenderer = renderers[0];
        clue.highlightColor = new Color(.95f,.80f,.55f);
        foreach (var collider in clue.GetComponentsInChildren<Collider>(true))
            if (collider.isTrigger) collider.enabled = false;
        AddTarget(clue.transform, bounds);
    }

    static Bounds BoundsOf(Renderer[] renderers)
    {
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    static void HideOriginals(Transform root)
    {
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
    }

    static void RemoveChild(Transform host, string name)
    {
        var child = host.Find(name);
        if (child) UnityEngine.Object.DestroyImmediate(child.gameObject);
    }

    static void AddTarget(Transform host, Bounds bounds)
    {
        RemoveChild(host, TargetName);
        var target = new GameObject(TargetName);
        target.transform.SetParent(host, true);
        target.transform.position = bounds.center + Vector3.up * .008f;
        var trigger = target.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = Vector3.Max(bounds.size + new Vector3(.032f, .020f, .032f), new Vector3(.055f,.035f,.055f));
    }

    static void InstallRecordInspection(Transform player, Transform record)
    {
        var renderer = record.GetComponent<Renderer>();
        var bounds = renderer.bounds;
        var body = player.GetComponent<BoxCollider>();
        if (body)
        {
            // The lid is raised. Keep the physical box below the exposed vinyl.
            var baseBounds = body.bounds;
            float bottom = bounds.min.y - .120f;
            float top = bounds.min.y - .012f;
            var localUp = player.InverseTransformDirection(Vector3.up);
            int axis = Mathf.Abs(localUp.x) > .5f ? 0 : Mathf.Abs(localUp.y) > .5f ? 1 : 2;
            var unitAxis = Vector3.zero;
            unitAxis[axis] = 1;
            body.center = player.InverseTransformPoint(new Vector3(baseBounds.center.x, (top + bottom) * .5f, baseBounds.center.z));
            var size = body.size;
            size[axis] = (top - bottom) / player.TransformVector(unitAxis).magnitude;
            body.size = size;
        }

        const string materialPath = MaterialDir + "Inspectable_Vinyl.mat";
        var vinyl = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (!vinyl)
        {
            vinyl = new Material(renderer.sharedMaterial);
            vinyl.SetColor("_EmissionColor", Color.black);
            vinyl.EnableKeyword("_EMISSION");
            AssetDatabase.CreateAsset(vinyl, materialPath);
        }
        renderer.sharedMaterial = vinyl;

        var clue = record.GetComponent<ClueObject>();
        if (!clue) clue = record.gameObject.AddComponent<ClueObject>();
        clue.clueId = "inspect_record_player";
        clue.clueTitle = "Пластинка";
        clue.inspectionComment = "Отец её часто ставил. Даже крышку оставил открытой.";
        clue.addToJournal = false;
        clue.isSuspiciousForBrothers = false;
        clue.targetRenderer = renderer;
        AddTarget(record, bounds);
    }

    static Material Finish(Material imported)
    {
        var name = imported.name;
        var path = MaterialDir + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        var color = imported.HasProperty("_BaseColor") ? imported.GetColor("_BaseColor") : imported.color;
        material.SetColor("_BaseColor", color);
        bool metal = name == "Clue_Brass" || name == "Clue_Steel";
        bool gloss = name == "Clue_Ivory" || name == "Clue_GreenGlaze" || name == "Clue_Coffee";
        material.SetFloat("_Smoothness", metal ? .54f : gloss ? .55f : .23f);
        material.SetFloat("_Metallic", metal ? .52f : 0);
        material.SetFloat("_Cull", 0); // Torn paper and thin folded sheets have visible back faces.
        material.SetColor("_EmissionColor", Color.black);
        material.EnableKeyword("_EMISSION");
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CarXCase.Hero;
using CarXCase.Harbor;
using CarXCase.CameraFX;
using CarXCase.Precinct;
using DetectiveGame.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class HarborSceneBuilder
{
    public const string SceneName = "05_BrightonHarbor";
    const string ScenePath = "Assets/_Project/Scenes/" + SceneName + ".unity";
    const string Art = "Assets/_Project/ArtDirection/Harbor";

    [Serializable] class Data { public Surface[] materials; public Module[] modules; public Placement[] placements; public Lamp[] lights; public Shot[] shots; }
    [Serializable] class Surface { public string name; public float[] color; public float metal, rough, glow; public int grain; }
    [Serializable] class Module { public string name; public Part[] parts; public Shape[] colliders; }
    [Serializable] class Part { public string mat; public float[] vertices, uv; public int[] triangles; }
    [Serializable] class Shape { public float[] p, size; public float yaw; }
    [Serializable] class Placement { public string name, module; public float[] p; public float yaw; }
    [Serializable] class Lamp { public string name, kind; public float[] p; public float intensity, extent; }
    [Serializable] class Shot { public string name; public float[] p, target; public float fov; }
    static readonly Dictionary<string, Texture2D> SurfaceTextures = new();

    static Vector3 At(float[] p) => new Vector3(p[0], p[1], p[2]);
    static string FileName(string name) => name.Replace(' ', '_');

    // Use Blender's build_harbor.py before running this command to regenerate the mesh data.
    [MenuItem("Tools/Harbor/Assemble from Blender geometry")]
    public static void Build()
    {
        const string input = "Temp/HarborGeometry.json";
        if (!File.Exists(input)) throw new FileNotFoundException("Run ArtSource/Harbor/build_harbor.py in Blender first.", input);
        var previous = SceneManager.GetSceneByPath(ScenePath);
        if (EditorApplication.isPlaying || (previous.IsValid() && previous.isDirty))
            throw new InvalidOperationException("Save the Harbor scene and leave Play Mode.");

        var data = JsonUtility.FromJson<Data>(File.ReadAllText(input));
        var originalActive = SceneManager.GetActiveScene();
        bool harborWasOpen = previous.IsValid() && previous.isLoaded;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        bool saved = false;
        try
        {
            SceneManager.SetActiveScene(scene);
            if (harborWasOpen) EditorSceneManager.CloseScene(previous, true);
            Assemble(scene, data);
            saved = EditorSceneManager.SaveScene(scene, ScenePath);
            if (!saved) throw new InvalidOperationException("Could not save the Harbor scene.");
            PrecinctBuilder.AddToBuild(ScenePath, new StringBuilder());
            AssetDatabase.SaveAssets();
        }
        finally
        {
            if (harborWasOpen && !saved && !SceneManager.GetSceneByPath(ScenePath).isLoaded)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            if (originalActive.IsValid() && originalActive.isLoaded)
                SceneManager.SetActiveScene(originalActive);
            if (!harborWasOpen || !saved)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    static void Assemble(Scene scene, Data data)
    {
        SurfaceTextures.Clear();
        foreach (string path in new[] { Art, Art + "/Meshes", Art + "/Materials", Art + "/Prefabs" })
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        var materials = data.materials.ToDictionary(x => x.name, MakeMaterial);
        var prefabs = data.modules.ToDictionary(x => x.name, x => MakePrefab(x, materials));

        var root = new GameObject("Brighton Harbor");
        var groups = new Dictionary<string, Transform>();
        var gateParts = new Dictionary<string, Transform>();
        foreach (var p in data.placements)
        {
            string category = GroupName(p);
            if (!groups.TryGetValue(category, out var parent))
                groups.Add(category, parent = Create(category, root.transform, Vector3.zero));
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[p.module], parent);
            instance.name = p.name;
            instance.transform.SetPositionAndRotation(At(p.p), Quaternion.Euler(0f, p.yaw, 0f));
            if (p.module is "Gate Leaf Left" or "Gate Leaf Right" or "Heavy Gate Chain" or "Rusty Padlock" or "Gate Code Keypad")
                gateParts.Add(p.module, instance.transform);
        }

        var lighting = Create("Lighting and weather", root.transform, Vector3.zero);
        SetLighting(lighting, data.lights);
        var player = CreatePlayer();
        CreateRain(lighting, player.transform.Find("Character_Visual"));
        var views = Create("Reference views", root.transform, Vector3.zero);
        foreach (var shot in data.shots)
        {
            var view = Create(shot.name, views, At(shot.p));
            view.rotation = Quaternion.LookRotation(At(shot.target) - view.position);
            var cam = view.gameObject.AddComponent<Camera>();
            cam.enabled = false;
            cam.fieldOfView = shot.fov;
            ConfigureCamera(cam);
        }
        views.gameObject.SetActive(false);
        ConfigureArrival(player.GetComponent<OfficerController>(), views);

        var gate = Create("Gate keypad interaction", root.transform, new Vector3(.11f, 1.44f, -.32f));
        var keypadReach = gate.gameObject.AddComponent<BoxCollider>();
        keypadReach.size = new Vector3(.90f, 1.2f, .53f);
        keypadReach.isTrigger = true;
        var gateLogic = gate.gameObject.AddComponent<HarborGate>();
        gateLogic.leftLeaf = gateParts["Gate Leaf Left"];
        gateLogic.rightLeaf = gateParts["Gate Leaf Right"];
        var lockedHardware = Create("Chain and lock hardware", root.transform, Vector3.zero);
        gateParts["Heavy Gate Chain"].SetParent(lockedHardware, true);
        gateParts["Rusty Padlock"].SetParent(lockedHardware, true);
        gateLogic.chain = lockedHardware.gameObject;
        gateLogic.player = player.GetComponent<OfficerController>();
        gateLogic.follow = gateLogic.player.cameraTransform.GetComponent<ThirdPersonCamera>();

        CreateInspect(root.transform, "Inspect first freight container", new Vector3(-5.82f, 1.18f, 7.90f),
            "Пломба цела. Похоже, этот контейнер давно не открывали.");
        CreateInspect(root.transform, "Inspect blue freight container", new Vector3(-5.42f, 1.18f, 22.70f),
            "Пусто. Дальше по дороге виден склад.");
        var marker = Create("Warehouse scene transition", root.transform, new Vector3(0f, 1.10f, 46.83f));
        marker.rotation = Quaternion.Euler(0f, 0f, 0f);
        CreateInspect(marker, "Inspect warehouse doors", Vector3.zero,
            "Пирс 3. Это и есть склад.");
        EditorSceneManager.MarkSceneDirty(scene);
    }

    static Transform Create(string name, Transform parent, Vector3 position)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = position;
        return t;
    }

    static string GroupName(Placement p)
    {
        if (p.module == "Pier 3 Warehouse Exterior") return "Warehouse exterior";
        if (p.module == "Harbor Basin" || p.module == "Moored Cargo Ship" ||
            p.module == "Lattice Dock Crane" || p.module == "Distant Harbor Skyline") return "Water and skyline";
        if (p.module == "Quay and Road") return "Quay and asphalt";
        return p.p[2] < 1.15f ? "Entrance and security" : "Cargo road and service yards";
    }

    static Material MakeMaterial(Surface s)
    {
        string path = Art + "/Materials/" + FileName(s.name) + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find(s.name == "Wet Asphalt" ? "Project/Wet Ground" : "Universal Render Pipeline/Lit");
        if (!mat)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        if (s.name == "Wet Asphalt")
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Weather/WetRoad.mat");
            mat.CopyPropertiesFromMaterial(source);
            mat.SetColor("_BaseColor", new Color(.27f, .29f, .32f));
            mat.SetFloat("_Wetness", .77f);
            mat.SetFloat("_PuddleAmount", .36f);
            mat.SetFloat("_PuddleScale", .48f);
            EditorUtility.SetDirty(mat);
            return mat;
        }
        var tint = new Color(s.color[0],s.color[1],s.color[2]);
        mat.SetColor("_BaseColor", tint);
        mat.SetFloat("_Metallic", s.metal);
        mat.SetFloat("_Smoothness", 1f - s.rough);
        if (s.glow > 0f)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", tint * s.glow);
        }
        if (s.grain > 0)
        {
            mat.SetTexture("_BaseMap", Grain(s.grain, false));
            mat.SetTexture("_BumpMap", Grain(s.grain, true));
            mat.SetTextureScale("_BaseMap", Vector2.one * (s.grain is 1 or 2 ? .74f : 1f));
            mat.SetFloat("_BumpScale", s.grain == 5 ? .45f : .26f);
            mat.EnableKeyword("_NORMALMAP");
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static float GrainAt(float u, float v, int type)
    {
        float Sample(int cellsX, int cellsY, int salt)
        {
            float x = u * cellsX, y = v * cellsY;
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float tx = Mathf.SmoothStep(0f, 1f, x - ix), ty = Mathf.SmoothStep(0f, 1f, y - iy);
            float Value(int a, int b)
            {
                uint n = unchecked((uint)((a % cellsX) * 374761393 + (b % cellsY) * 668265263 + salt));
                n = (n ^ (n >> 13)) * 1274126177u;
                return (n & 0xffff) / 32767.5f - 1f;
            }
            return Mathf.Lerp(Mathf.Lerp(Value(ix, iy), Value(ix + 1, iy), tx),
                Mathf.Lerp(Value(ix, iy + 1), Value(ix + 1, iy + 1), tx), ty);
        }
        return .42f * Sample(5, 6, 11 + type) + .36f * Sample(type == 4 ? 32 : 17, type == 4 ? 5 : 17, 127 + type)
            + .22f * Sample(49, 47, 17 + type);
    }

    static Texture2D Grain(int type, bool normal)
    {
        string path = Art + "/Materials/Surface_" + type + (normal ? "_Normal.png" : "_Tone.png");
        if (SurfaceTextures.TryGetValue(path, out var cached)) return cached;
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGB24, false, true);
        var colors = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = x / (float)size;
            float v = y / (float)size;
            float h = GrainAt(u, v, type);
            if (normal)
            {
                const float d = 1f / size;
                float amount = type == 5 ? .37f : type == 1 ? .31f : .20f;
                var n = new Vector3((h - GrainAt(u + d, v, type)) * amount,
                    (h - GrainAt(u, v + d, type)) * amount, 1f).normalized;
                colors[y * size + x] = new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f);
            }
            else
            {
                float value = .84f + h * .16f;
                colors[y * size + x] = new Color(value, value, value);
            }
        }
        tex.SetPixels(colors);
        tex.Apply();
        byte[] image = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(image))
        {
            File.WriteAllBytes(path, image);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.anisoLevel = 8;
                if (normal) importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
        }
        return SurfaceTextures[path] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static GameObject MakePrefab(Module module, IReadOnlyDictionary<string, Material> materials)
    {
        var go = new GameObject(module.name);
        var positions = new List<Vector3>();
        var uv = new List<Vector2>();
        var submeshes = new List<int[]>();
        foreach (var part in module.parts)
        {
            int offset = positions.Count;
            for (int i = 0; i < part.vertices.Length; i += 3)
                positions.Add(new Vector3(part.vertices[i], part.vertices[i + 1], part.vertices[i + 2]));
            for (int i = 0; i < part.uv.Length; i += 2)
                uv.Add(new Vector2(part.uv[i], part.uv[i + 1]));
            submeshes.Add(part.triangles.Select(i => i + offset).ToArray());
        }
        var mesh = new Mesh { name = module.name, indexFormat = positions.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        mesh.SetVertices(positions);
        mesh.SetUVs(0, uv);
        mesh.subMeshCount = submeshes.Count;
        for (int i = 0; i < submeshes.Count; i++) mesh.SetTriangles(submeshes[i], i);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        string path = Art + "/Meshes/" + FileName(module.name) + ".asset";
        var asset = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (asset)
        {
            EditorUtility.CopySerialized(mesh, asset);
            Object.DestroyImmediate(mesh);
        }
        else
        {
            asset = mesh;
            AssetDatabase.CreateAsset(asset, path);
        }
        go.AddComponent<MeshFilter>().sharedMesh = asset;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = module.parts.Select(p => materials[p.mat]).ToArray();
        if (module.name is "Distant Harbor Skyline" or "Moored Cargo Ship" or "Lattice Dock Crane" or "Harbor Basin")
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        foreach (var box in module.colliders)
        {
            var node = Create("Solid collision", go.transform, At(box.p));
            node.localRotation = Quaternion.Euler(0f, box.yaw, 0f);
            node.gameObject.AddComponent<BoxCollider>().size = At(box.size);
        }
        string prefabPath = Art + "/Prefabs/" + FileName(module.name) + ".prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        return prefab;
    }

    static void ConfigureCamera(Camera camera)
    {
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.092f, .124f, .16f);
        camera.nearClipPlane = .05f;
        camera.farClipPlane = 190f;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
    }

    static GameObject CreatePlayer()
    {
        var player = new GameObject("Player");
        player.transform.position = new Vector3(-.82f, .05f, -4.9f);
        var controller = player.AddComponent<CharacterController>();
        controller.radius = .27f;
        controller.height = 1.78f;
        controller.center = Vector3.up * .89f;
        controller.stepOffset = .27f;
        var officer = player.AddComponent<OfficerController>();
        officer.interactionRadius = 2.1f;
        var visual = Create("Character_Visual", player.transform, Vector3.zero);
        var animator = HeroSetup.CreateHero(visual, "Alexey", "Hero_Locomotion.controller");
        var locomotion = visual.gameObject.AddComponent<HeroLocomotion>();
        locomotion.animator = animator;
        var hud = player.AddComponent<SimpleUIHUD>();
        hud.showInspectionHud = true;
        hud.enableJournal = true;
        officer.hud = hud;
        player.AddComponent<CarXCase.Investigation.JournalKeyHint>();

        var camera = new GameObject("Player Camera");
        camera.tag = "MainCamera";
        camera.transform.SetPositionAndRotation(player.transform.position + new Vector3(.44f, 1.73f, -2.46f),
            Quaternion.Euler(2f, 0f, 0f));
        var lens = camera.AddComponent<Camera>();
        lens.fieldOfView = 58f;
        ConfigureCamera(lens);
        camera.AddComponent<AudioListener>();
        var follow = camera.AddComponent<ThirdPersonCamera>();
        follow.target = player.transform;
        officer.cameraTransform = camera.transform;
        return player;
    }

    public static void ConfigureArrival(OfficerController officer, Transform views)
    {
        const string barPath = "Assets/_Project/Scenes/02_Bar.unity";
        var bar = SceneManager.GetSceneByPath(barPath);
        bool preview = !bar.isLoaded;
        if (preview) bar = EditorSceneManager.OpenPreviewScene(barPath);
        try
        {
            var source = bar.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ThirdPersonCamera>(true)).Single();
            var sourcePlayer = source.target.GetComponent<OfficerController>();
            var follow = officer.cameraTransform.GetComponent<ThirdPersonCamera>();
            EditorUtility.CopySerialized(source, follow);
            follow.target = officer.transform;
            officer.walkSpeed = sourcePlayer.walkSpeed;
            officer.runSpeed = sourcePlayer.runSpeed;
            var handheld = follow.GetComponent<CinematicHandheld>();
            if (!handheld) handheld = follow.gameObject.AddComponent<CinematicHandheld>();
            EditorUtility.CopySerialized(source.GetComponent<CinematicHandheld>(), handheld);
            handheld.gameplayCamera = follow;
            handheld.duringExploration = true;
            follow.GetComponent<Camera>().fieldOfView = source.GetComponent<Camera>().fieldOfView;
            var arrival = follow.GetComponent<HarborArrival>();
            if (!arrival) arrival = follow.gameObject.AddComponent<HarborArrival>();
            arrival.overview = views.Find("02 Elevated Overview").GetComponent<Camera>();
            arrival.warehouse = views.Find("04 Warehouse Approach").GetComponent<Camera>();
        }
        finally
        {
            if (preview) EditorSceneManager.ClosePreviewScene(bar);
        }
    }

    static void SetLighting(Transform root, Lamp[] lamps)
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = .016f;
        RenderSettings.fogColor = new Color(.092f, .124f, .16f);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.19f, .24f, .32f);
        RenderSettings.ambientEquatorColor = new Color(.10f, .14f, .21f);
        RenderSettings.ambientGroundColor = new Color(.063f, .069f, .084f);

        var moon = Create("Cool overcast light", root, Vector3.zero).gameObject.AddComponent<Light>();
        moon.transform.rotation = Quaternion.Euler(42f, 137f, 0f);
        moon.type = LightType.Directional;
        moon.intensity = .57f;
        moon.color = new Color(.55f, .69f, .94f);
        moon.GetUniversalAdditionalLightData();
        moon.shadows = LightShadows.Soft;
        moon.shadowStrength = .66f;
        foreach (var entry in lamps)
        {
            var p = At(entry.p);
            var light = Create(entry.name, root, p).gameObject.AddComponent<Light>();
            light.type = entry.kind == "spot" ? LightType.Spot : LightType.Point;
            light.color = new Color(1f, .68f, .38f);
            light.GetUniversalAdditionalLightData();
            light.intensity = entry.intensity;
            light.range = entry.extent;
            light.spotAngle = 109f;
            light.innerSpotAngle = 53f;
            if (light.type == LightType.Spot)
            {
                light.transform.rotation = Quaternion.LookRotation(new Vector3(0f, -1f, entry.name.StartsWith("Warehouse") ? .24f : .08f), Vector3.forward);
                if (p.z < 0 || p.z > 46) light.shadows = LightShadows.Soft;
            }
        }
        var probe = Create("Quay reflections", root, new Vector3(0, 2.1f, 22)).gameObject.AddComponent<ReflectionProbe>();
        probe.size = new Vector3(36, 18, 65);
        probe.resolution = 128;
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
        probe.boxProjection = true;
        probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
        probe.backgroundColor = RenderSettings.fogColor;
        probe.intensity = .75f;
        var roadReflection = Create("Wet road planar reflection", root, Vector3.zero).gameObject
            .AddComponent<CarXCase.Weather.PuddleReflection>();
        roadReflection.planeHeight = .015f;
        roadReflection.resolutionScale = .28f;
        roadReflection.farClip = 92f;

        string path = Art + "/Harbor_Night.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (!profile)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.value = 1.12f;
            bloom.intensity.value = .27f;
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.value = .18f;
            vignette.smoothness.value = .61f;
            profile.Add<Tonemapping>(true).mode.value = TonemappingMode.Neutral;
            foreach (var effect in profile.components) AssetDatabase.AddObjectToAsset(effect, profile);
            EditorUtility.SetDirty(profile);
        }
        var volume = Create("Harbor night grade", root, Vector3.zero).gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;
    }

    static void CreateRain(Transform root, Transform characterVisual)
    {
        // Cover the elevated quay and offshore arrival cameras, including the top of each shot.
        var node = Create("Diagonal harbor rain", root, new Vector3(-12f, 62f, 18f));
        node.rotation = Quaternion.Euler(81f, 0f, -9f);
        var rain = node.gameObject.AddComponent<ParticleSystem>();
        var main = rain.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4.9f, 5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(15f, 20f);
        main.startSize = new ParticleSystem.MinMaxCurve(.009f, .017f);
        main.startColor = new Color(.58f, .67f, .79f, .26f);
        main.maxParticles = 52000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = rain.emission;
        emission.rateOverTime = 9600;
        var shape = rain.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(84f, 80f, .35f);
        var renderer = node.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 5.8f;
        renderer.velocityScale = .019f;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        string path = Art + "/Materials/Harbor_Rain.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, path);
        }
        renderer.sharedMaterial = material;
        node.gameObject.AddComponent<HarborRainImpacts>().characterVisual = characterVisual;
    }

    static void CreateInspect(Transform parent, string name, Vector3 point, string subtitle)
    {
        var node = Create(name, parent, point).gameObject;
        var trigger = node.AddComponent<BoxCollider>();
        trigger.size = new Vector3(.72f, 1.05f, .45f);
        trigger.isTrigger = true;
        var inspect = node.AddComponent<SceneExit>();
        inspect.requiredClues = 0;
        inspect.blockedComment = subtitle;
        inspect.actionText = "Осмотреть";
    }
}

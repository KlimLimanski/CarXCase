using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using CarXCase.Intro;
using CarXCase.HeroCar;

/// Replaces the hero car interior in 00_BrightonDrive_Models with HeroCar_Interior.fbx (built in
/// ArtSource/HeroCar/HeroCar_Interior_v2.blend), creates textured URP materials, live mirrors and gauges.
/// Menu: Tools/Brighton/Hero car..., or write "install capture" / "capture" / "revert" to HeroSetup/herocar_request.txt.
[InitializeOnLoad]
public static class HeroCarInteriorPass
{
    const string Root = "Assets/_Project/Arts/Car/HeroCar/";
    const string Fbx = Root + "HeroCar_Interior.fbx";
    const string TexDir = Root + "Textures/";
    const string MatDir = Root + "Materials/";
    const string ScenePath = "Assets/_Project/Scenes/00_BrightonDrive_Models.unity";
    const string Io = "HeroSetup";
    // Non-uniform scale the old interior used in the scene; it is now baked into the FBX geometry.
    static readonly Vector3 BakedScale = new Vector3(1.0588f, 1.0196f, 1.349f);
    static readonly StringBuilder log = new StringBuilder();

    static HeroCarInteriorPass() { EditorApplication.update += Poll; }

    static void Poll()
    {
        string req = Path.Combine(Io, "herocar_request.txt");
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!File.Exists(req)) return;
        string cmd = File.ReadAllText(req).Trim(); File.Delete(req); Run(cmd);
    }

    [MenuItem("Tools/Brighton/Hero car: install new interior")] static void MenuInstall() => Run("install capture");
    [MenuItem("Tools/Brighton/Hero car: capture review shots")] static void MenuCapture() => Run("capture");
    [MenuItem("Tools/Brighton/Hero car: revert to old interior")] static void MenuRevert() => Run("revert");

    static void Run(string cmd)
    {
        log.Clear(); log.AppendLine("COMMAND " + cmd + " at " + DateTime.Now);
        try
        {
            foreach (var c in cmd.Split(new[] { ' ', ',', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                switch (c.Trim())
                {
                    case "install": Install(); break;
                    case "capture": Capture(); break;
                    case "revert": Revert(); break;
                    default: throw new Exception("Unknown command " + c);
                }
            log.AppendLine("SUCCESS");
        }
        catch (Exception e) { log.AppendLine("ERROR " + e); Debug.LogException(e); }
        Directory.CreateDirectory(Io);
        File.WriteAllText(Path.Combine(Io, "herocar_result.txt"), log.ToString());
        Debug.Log("[HeroCar] " + log);
    }

    // ------------------------------------------------------------------ import settings
    static void ImportSettings()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TexDir.TrimEnd('/') }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            var ti = (TextureImporter)AssetImporter.GetAtPath(p);
            bool normal = p.EndsWith("_Normal.png"), gauge = p.Contains("Gauge_"), dirty = false;
            var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (ti.textureType != type) { ti.textureType = type; dirty = true; }
            var wrap = gauge ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            if (ti.wrapMode != wrap) { ti.wrapMode = wrap; dirty = true; }
            int max = gauge ? 2048 : 1024;
            if (ti.maxTextureSize != max) { ti.maxTextureSize = max; dirty = true; }
            if (ti.anisoLevel != 8) { ti.anisoLevel = 8; dirty = true; }
            if (dirty) ti.SaveAndReimport();
        }
        var mi = AssetImporter.GetAtPath(Fbx) as ModelImporter;
        if (mi == null) throw new Exception("FBX not found: " + Fbx);
        bool d = false;
        if (mi.importTangents != ModelImporterTangents.CalculateMikk) { mi.importTangents = ModelImporterTangents.CalculateMikk; d = true; }
        if (mi.importNormals != ModelImporterNormals.Import) { mi.importNormals = ModelImporterNormals.Import; d = true; }
        if (mi.importCameras || mi.importLights) { mi.importCameras = false; mi.importLights = false; d = true; }
        if (!mi.isReadable) { mi.isReadable = true; d = true; }
        if (d) mi.SaveAndReimport();
    }

    // ------------------------------------------------------------------ materials
    static Texture2D Tex(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + n + ".png");

    static Material Lit(string name, string tex, float tileMeters, Color linearTint, float rough, float metal, float bump)
    {
        Directory.CreateDirectory(MatDir);
        string path = MatDir + name + ".mat";
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); } else m.shader = shader;
        m.SetColor("_BaseColor", linearTint.gamma);
        m.SetFloat("_Smoothness", 1 - rough); m.SetFloat("_Metallic", metal);
        if (tex != null)
        {
            m.SetTexture("_BaseMap", Tex(tex + "_Albedo"));
            m.SetTextureScale("_BaseMap", Vector2.one / tileMeters);
            var nm = Tex(tex + "_Normal");
            if (nm) { m.SetTexture("_BumpMap", nm); m.SetFloat("_BumpScale", bump); m.EnableKeyword("_NORMALMAP"); }
            else { m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP"); }
        }
        else { m.SetTexture("_BaseMap", null); m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP"); }
        m.DisableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Dictionary<string, Material> BuildMaterials()
    {
        var map = new Dictionary<string, Material>();
        void Add(string n, string tex, float tile, Color tint, float rough, float metal, float bump) => map[n] = Lit(n, tex, tile, tint, rough, metal, bump);
        Color g(float v) => new Color(v, v, v, 1);
        Add("HC_Velour", "Velour_Insert", 0.32f, g(1), 0.95f, 0, 1.0f);
        Add("HC_DoorVelour", "Velour_Insert", 0.32f, g(0.85f), 0.95f, 0, 1.0f);
        Add("HC_DashVinyl", "Vinyl_Grain", 0.22f, g(1), 0.70f, 0, 0.8f);
        Add("HC_TrimPlastic", "Plastic_Stipple", 0.18f, g(1), 0.62f, 0, 0.6f);
        Add("HC_DarkPlastic", "Plastic_Stipple", 0.18f, g(0.62f), 0.58f, 0, 0.6f);
        Add("HC_Headliner", "Headliner_Felt", 0.40f, g(1), 0.95f, 0, 0.6f);
        Add("HC_Carpet", "Carpet_Loop", 0.25f, g(1), 1.0f, 0, 1.0f);
        Add("HC_Leather", "Leather_Grip", 0.12f, g(1), 0.55f, 0, 0.8f);
        Add("HC_Webbing", "Webbing", 0.10f, g(1), 0.75f, 0, 0.7f);
        Add("HC_Rubber", null, 1, new Color(0.025f, 0.025f, 0.026f), 0.85f, 0, 0);
        Add("HC_Chrome", null, 1, g(0.55f), 0.35f, 1, 0);
        Add("HC_DarkMetal", null, 1, new Color(0.06f, 0.06f, 0.065f), 0.45f, 1, 0);
        Add("HC_Seam", null, 1, new Color(0.035f, 0.033f, 0.032f), 0.9f, 0, 0);
        Add("HC_Paint", null, 1, new Color(0.055f, 0.065f, 0.06f), 0.4f, 0, 0);
        Add("HC_MirrorHousing", "Plastic_Stipple", 0.18f, g(0.35f), 0.5f, 0, 0.5f);

        var face = Lit("HC_GaugeFace", "Gauge_Cluster", 1, g(1), 0.4f, 0, 0);
        face.SetTexture("_EmissionMap", Tex("Gauge_Cluster_Emission")); face.SetColor("_EmissionColor", Color.white * 1.3f);
        face.EnableKeyword("_EMISSION"); face.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        map["HC_GaugeFace"] = face;

        var needle = Lit("HC_Needle", null, 1, new Color(1f, 0.28f, 0.08f), 0.4f, 0, 0);
        needle.SetColor("_EmissionColor", new Color(1f, 0.25f, 0.06f) * 2.4f); needle.EnableKeyword("_EMISSION");
        map["HC_Needle"] = needle;

        var lens = Lit("HC_GaugeLens", null, 1, new Color(0.02f, 0.02f, 0.02f), 0.04f, 0, 0);
        var lc = lens.GetColor("_BaseColor"); lc.a = 0.14f; lens.SetColor("_BaseColor", lc);
        lens.SetFloat("_Surface", 1); lens.SetFloat("_Blend", 0); lens.SetOverrideTag("RenderType", "Transparent");
        lens.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); lens.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        lens.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); lens.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        lens.SetFloat("_ZWrite", 0); lens.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); lens.renderQueue = (int)RenderQueue.Transparent;
        lens.SetShaderPassEnabled("DepthOnly", false); lens.SetShaderPassEnabled("ShadowCaster", false);
        map["HC_GaugeLens"] = lens;

        map["HC_MirrorGlass"] = MirrorMaterial();
        AssetDatabase.SaveAssets();
        return map;
    }

    static Material MirrorMaterial()
    {
        string path = MatDir + "HC_MirrorGlass.mat";
        var shader = Shader.Find("CarXCase/CarMirror") ?? throw new Exception("Shader CarXCase/CarMirror not compiled");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); } else m.shader = shader;
        return m;
    }

    // ------------------------------------------------------------------ install
    static Transform Deep(Transform r, string n) => r.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n);
    static Bounds RB(Transform root, string n)
    {
        var r = root.GetComponentsInChildren<Renderer>(true).FirstOrDefault(x => x.name == n);
        if (!r) throw new Exception("Renderer " + n + " not found under " + root.name);
        return r.bounds;
    }

    static void Install()
    {
        ImportSettings();
        var scene = EditorSceneManager.OpenScene(ScenePath);
        string backup = "Assets/_Project/Scenes/BeforeHeroCar_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity";
        EditorSceneManager.SaveScene(scene, backup, true);
        log.AppendLine("Backup " + backup);

        var cut = UnityEngine.Object.FindAnyObjectByType<BrightonDriveCutscene>() ?? throw new Exception("BrightonDriveCutscene not found");
        var car = cut.car;
        var old = car.Find("Sedan_Reference_Interior") ?? throw new Exception("Sedan_Reference_Interior not found");
        var prev = car.Find("HeroCar_Interior"); if (prev) UnityEngine.Object.DestroyImmediate(prev.gameObject);

        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
        go.name = "HeroCar_Interior";
        go.transform.SetParent(car, false);
        go.transform.localPosition = old.localPosition;
        go.transform.localRotation = old.localRotation;
        var s = old.localScale;
        go.transform.localScale = new Vector3(s.x / BakedScale.x, s.y / BakedScale.y, s.z / BakedScale.z);

        // Align on parts that exist in both models and were not rebuilt.
        old.gameObject.SetActive(true);
        foreach (var part in new[] { "Dashboard_Lower", "Windshield_Header", "Center_Tunnel" })
        {
            var a = RB(old, part); var b = RB(go.transform, part);
            log.AppendLine($"{part}: old c={a.center:F3} s={a.size:F3} | new c={b.center:F3} s={b.size:F3}");
        }
        var offset = RB(old, "Dashboard_Lower").center - RB(go.transform, "Dashboard_Lower").center;
        go.transform.position += offset;
        log.AppendLine("Alignment offset applied " + offset.ToString("F4"));
        old.gameObject.SetActive(false);

        var map = BuildMaterials();
        var glass = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/BrightonModels/AutomotiveGlass.mat");
        int swapped = 0; var unknown = new HashSet<string>();
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var mm = r.sharedMaterials;
            for (int i = 0; i < mm.Length; i++)
            {
                string n = mm[i] ? mm[i].name : "";
                if (map.TryGetValue(n, out var m)) { mm[i] = m; swapped++; }
                else if (n.Contains("Glass") && glass) { mm[i] = glass; swapped++; }
                else unknown.Add(n);
            }
            r.sharedMaterials = mm;
            if (mm.Any(m => m == glass || m == map["HC_GaugeLens"] || m == map["HC_MirrorGlass"])) r.shadowCastingMode = ShadowCastingMode.Off;
            if (r.name.StartsWith("Simple_Tire") || r.name.StartsWith("Simple_Wheel")) r.enabled = false;
        }
        log.AppendLine("Materials swapped=" + swapped + "; kept from FBX: " + string.Join(", ", unknown));

        foreach (var n in new[] { "HC_Mirror_Rear_Glass", "HC_Mirror_Left_Glass", "HC_Mirror_Right_Glass" })
        {
            var t = Deep(go.transform, n) ?? throw new Exception(n + " missing");
            var cm = t.GetComponent<CarMirror>() ?? t.gameObject.AddComponent<CarMirror>();
            cm.mirrorMaterial = map["HC_MirrorGlass"];
            cm.resolution = n.Contains("Rear") ? 768 : 512;
            cm.enabled = false; cm.enabled = true;
        }

        var gauges = go.GetComponent<CarGauges>() ?? go.AddComponent<CarGauges>();
        gauges.car = car;
        gauges.speedNeedle = Deep(go.transform, "HC_Needle_Speed");
        gauges.tachNeedle = Deep(go.transform, "HC_Needle_Tach");
        gauges.fuelNeedle = Deep(go.transform, "HC_Needle_Fuel");
        gauges.tempNeedle = Deep(go.transform, "HC_Needle_Temp");
        var face = Deep(go.transform, "HC_Gauge_Face");
        var fmesh = face.GetComponent<MeshFilter>().sharedMesh;
        Vector3 nW = face.TransformDirection(fmesh.normals[0]).normalized;
        Vector3 driverEye = car.TransformPoint(new Vector3(-0.43f, 1.6f, -0.06f));
        if (Vector3.Dot(nW, driverEye - face.GetComponent<Renderer>().bounds.center) < 0) nW = -nW;
        gauges.dialNormalInCar = car.InverseTransformDirection(nW);
        log.AppendLine("Dial normal (car space) " + gauges.dialNormalInCar.ToString("F3"));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        log.AppendLine("Installed HeroCar_Interior; old interior disabled (kept for revert). Saved " + scene.path);
    }

    static void Revert()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var car = UnityEngine.Object.FindAnyObjectByType<BrightonDriveCutscene>().car;
        var old = car.Find("Sedan_Reference_Interior"); var neu = car.Find("HeroCar_Interior");
        if (old) old.gameObject.SetActive(true); if (neu) neu.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        log.AppendLine("Reverted to Sedan_Reference_Interior");
    }

    // ------------------------------------------------------------------ review shots
    static void Capture()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);
        var cut = UnityEngine.Object.FindAnyObjectByType<BrightonDriveCutscene>();
        var car = cut.car; var root = car.Find("HeroCar_Interior");
        var g = root ? root.GetComponent<CarGauges>() : null; if (g) g.PreviewNow();
        var main = cut.cameraRig.GetComponent<Camera>();
        CarMirror.RenderAllFor(main); HeroSetup.Shot(main, "herocar_main.png"); log.AppendLine("Capture herocar_main.png");
        Vector3 P(float x, float y, float z) => car.TransformPoint(new Vector3(x, y, z));
        Shot(P(-0.22f, 1.52f, 0.12f), P(-0.43f, 1.27f, 0.68f), "herocar_cluster.png", 42, main);
        Shot(P(0.12f, 1.58f, 0.15f), P(0.025f, 1.72f, 0.65f), "herocar_rear_mirror.png", 38, main);
        Shot(P(-0.40f, 1.42f, 0.30f), P(-0.975f, 1.25f, 0.85f), "herocar_left_mirror.png", 45, main);
        Shot(P(0.35f, 1.45f, 0.25f), P(1.025f, 1.25f, 0.85f), "herocar_right_mirror.png", 45, main);
        Shot(P(0.62f, 1.25f, 0.55f), P(-0.40f, 0.95f, -0.10f), "herocar_seats.png", 65, main);
        Shot(P(0.55f, 1.55f, -0.95f), P(-0.15f, 1.05f, 0.65f), "herocar_wide.png", 70, main);
    }

    static void Shot(Vector3 from, Vector3 at, string file, float fov, Camera like)
    {
        var go = new GameObject("__HeroCarCaptureCam");
        try
        {
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov; cam.nearClipPlane = 0.02f; cam.farClipPlane = 500;
            go.transform.position = from; go.transform.LookAt(at);
            cam.clearFlags = like.clearFlags; cam.backgroundColor = like.backgroundColor; cam.cullingMask = like.cullingMask;
            CarMirror.RenderAllFor(cam);
            HeroSetup.Shot(cam, file);
            log.AppendLine("Capture " + file);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
}

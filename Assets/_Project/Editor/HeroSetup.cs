using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using CarXCase.Hero;

/// Installs the rigged hero (MainHero_Rigged.glb) into the drive cutscene and the apartment.
/// Run from the menu Tools/Hero/..., or by writing a command ("car", "apartment", "all",
/// "capture_car", "capture_apartment") into <project>/HeroSetup/request.txt.
[InitializeOnLoad]
public static class HeroSetup
{
    const string Glb = "Assets/_Project/Arts/Heroes/Main Hero/MainHero_Rigged.glb";
    const string AnimDir = "Assets/_Project/Arts/Heroes/Animations";
    const string CarScene = "Assets/_Project/Scenes/00_BrightonDrive_Models.unity";
    const string FlatScene = "Assets/_Project/Scenes/01_FatherApartment.unity";
    const string IoDir = "HeroSetup";
    const float HeroScale = 0.95f; // model is 1.90 m → 1.80 m
    static readonly StringBuilder log = new StringBuilder();

    static HeroSetup() { EditorApplication.update += Poll; }

    static void Poll()
    {
        string request = Path.Combine(IoDir, "request.txt");
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!File.Exists(request)) return;
        string cmd = File.ReadAllText(request).Trim();
        File.Delete(request);
        Run(cmd);
    }

    [MenuItem("Tools/Hero/Install hero in car scene")] static void MenuCar() => Run("car");
    [MenuItem("Tools/Hero/Install hero in apartment")] static void MenuFlat() => Run("apartment");
    [MenuItem("Tools/Hero/Install hero everywhere")] static void MenuAll() => Run("all");

    public static void RunCommand(string cmd) => Run(cmd);

    static void Run(string cmd)
    {
        log.Clear();
        log.AppendLine("COMMAND " + cmd + " at " + DateTime.Now);
        try
        {
            var current = SceneManager.GetActiveScene();
            if (current.isDirty) EditorSceneManager.SaveScene(current);
            foreach (var c in cmd.Split(new[] { ' ', ',', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                switch (c.Trim())
                {
                    case "anims": EnsureAnimations(); break;
                    case "car": EnsureAnimations(); InstallCar(); break;
                    case "apartment": EnsureAnimations(); InstallApartment(); break;
                    case "all": EnsureAnimations(); InstallCar(); InstallApartment(); break;
                    case "exit": EnsureAnimations(); PrecinctBuilder.SetupApartmentExit(log); break;
                    case "precinct": EnsureAnimations(); PrecinctBuilder.BuildPrecinct(log); break;
                    case "locations": EnsureAnimations(); LocationBuilder.BuildAll(log); break;
                    case "rain": WeatherSetup.Run(log); break;
                    case "playcap_face": PlayCapture.Start(FlatScene, log); break;
                    case "capture_precinct": EditorSceneManager.OpenScene("Assets/_Project/Scenes/02_Precinct.unity"); PrecinctBuilder.CapturePrecinct(log); break;
                    case "capture_car": EditorSceneManager.OpenScene(CarScene); CaptureCar(); break;
                    case "capture_apartment": EditorSceneManager.OpenScene(FlatScene); CaptureApartment(); break;
                    default:
                        if (c.StartsWith("furniture")) { HeroFurniture.Run(c, log); break; }
                        throw new Exception("Unknown command " + c);
                }
            }
            log.AppendLine("SUCCESS");
        }
        catch (Exception e)
        {
            log.AppendLine("ERROR " + e);
            Debug.LogException(e);
        }
        Directory.CreateDirectory(IoDir);
        File.WriteAllText(Path.Combine(IoDir, "result.txt"), log.ToString());
        Debug.Log("[HeroSetup] " + log);
    }

    // ---------------------------------------------------------------- animations
    static readonly Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();

    static void EnsureAnimations()
    {
        AssetDatabase.ImportAsset(Glb, ImportAssetOptions.ForceSynchronousImport);
        var all = AssetDatabase.LoadAllAssetsAtPath(Glb);
        if (all == null || all.Length == 0) throw new Exception("Cannot load " + Glb);
        var src = all.OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToArray();
        log.AppendLine("GLB clips: " + string.Join(", ", src.Select(c => c.name + (c.legacy ? "(legacy)" : ""))));
        if (!Directory.Exists(AnimDir)) { Directory.CreateDirectory(AnimDir); AssetDatabase.Refresh(); }
        clips.Clear();
        foreach (var name in new[] { "Idle", "Walk", "Run", "Drive_Phone" })
        {
            var s = src.FirstOrDefault(c => c.name == name) ?? src.FirstOrDefault(c => c.name.EndsWith(name));
            if (!s) throw new Exception("Clip " + name + " not found in GLB");
            string path = AnimDir + "/Hero_" + name + ".anim";
            var copy = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            // Existing clips can be edited or retargeted from newer Blender actions.
            // Only seed missing clips from the original GLB.
            if (copy)
            {
                clips[name] = copy;
                continue;
            }
            copy = new AnimationClip();
            EditorUtility.CopySerialized(s, copy);
            copy.name = "Hero_" + name;
            copy.legacy = false;
            var settings = AnimationUtility.GetAnimationClipSettings(copy);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(copy, settings);
            AssetDatabase.CreateAsset(copy, path);
            clips[name] = copy;
        }
        var b = AnimationUtility.GetCurveBindings(clips["Walk"]);
        log.AppendLine("Walk bindings=" + b.Length + " sample paths: " + string.Join(" | ", b.Select(x => x.path).Distinct().Take(3)));

        // locomotion controller: Idle (0) → Walk (1.8 m/s) → Run (3 m/s)
        string locoPath = AnimDir + "/Hero_Locomotion.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(locoPath) && AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimDir + "/Hero_Drive.controller"))
        { log.AppendLine("Controllers exist, kept"); return; }
        AssetDatabase.DeleteAsset(locoPath);
        var loco = AnimatorController.CreateAnimatorControllerAtPath(locoPath);
        loco.AddParameter("Speed", AnimatorControllerParameterType.Float);
        loco.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(clips["Idle"], 0f);
        tree.AddChild(clips["Walk"], 1.8f);
        tree.AddChild(clips["Run"], 3f);

        string drivePath = AnimDir + "/Hero_Drive.controller";
        AssetDatabase.DeleteAsset(drivePath);
        var drive = AnimatorController.CreateAnimatorControllerAtPath(drivePath);
        drive.AddMotion(clips["Drive_Phone"]);
        AssetDatabase.SaveAssets();
        log.AppendLine("Controllers created");
    }

    static AnimationClip Clip(string name)
    {
        if (!clips.TryGetValue(name, out var c) || !c) c = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimDir + "/Hero_" + name + ".anim");
        return c;
    }

    // ---------------------------------------------------------------- hero instance
    static Transform Deep(Transform root, string name) =>
        root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

    internal static Animator CreateHero(Transform parent, string name, string controllerFile)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Glb);
        if (!asset) throw new Exception("Rigged hero not imported: " + Glb);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.name = name;
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one * HeroScale;

        // Animator must sit on the object the clip paths are relative to.
        var bindings = AnimationUtility.GetCurveBindings(Clip("Idle"));
        Transform host = null; int best = -1;
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
        {
            int ok = bindings.Count(bd => string.IsNullOrEmpty(bd.path) || t.Find(bd.path));
            if (ok > best) { best = ok; host = t; }
        }
        log.AppendLine("Animator host=" + host.name + " resolved " + best + "/" + bindings.Length + " bindings");
        foreach (var a in go.GetComponentsInChildren<Animation>(true)) UnityEngine.Object.DestroyImmediate(a);
        foreach (var a in go.GetComponentsInChildren<Animator>(true)) if (a.transform != host) UnityEngine.Object.DestroyImmediate(a);
        var anim = host.GetComponent<Animator>() ?? host.gameObject.AddComponent<Animator>();
        anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimDir + "/" + controllerFile);
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        if (!go.GetComponent<HeroFace>()) go.AddComponent<HeroFace>();
        foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            smr.updateWhenOffscreen = true;
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
        if (controllerFile == "Hero_Locomotion.controller") HeroAnimationImport.ApplyIdlePose(anim);
        return anim;
    }

    static string Backup(Scene scene, string tag)
    {
        string path = "Assets/_Project/Scenes/Before" + tag + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity";
        EditorSceneManager.SaveScene(scene, path, true);
        log.AppendLine("Backup " + path);
        return path;
    }

    // ---------------------------------------------------------------- car
    static void InstallCar()
    {
        var scene = EditorSceneManager.OpenScene(CarScene);
        Backup(scene, "Hero");
        var car = GameObject.Find("Hero_Car")?.transform ?? throw new Exception("Hero_Car missing");
        var old = car.Find("Alexey_Driver");
        if (old)
        {
            var oldPhone = old.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Phone_1999");
            var drv = car.Find("Driver");
            if (oldPhone && drv) oldPhone.SetParent(drv, true);
            UnityEngine.Object.DestroyImmediate(old.gameObject);
        }
        foreach (var n in new[] { "IK_Wheel_LeftHand", "IK_LeftElbow_Hint" }) { var t = car.Find(n); if (t) UnityEngine.Object.DestroyImmediate(t.gameObject); }

        var anim = CreateHero(car, "Alexey_Driver", "Hero_Drive.controller");
        var hero = car.Find("Alexey_Driver");
        Clip("Drive_Phone").SampleAnimation(anim.gameObject, 0);
        var face = hero.GetComponent<HeroFace>(); if (face) { face.armGestures = false; face.emotion = 0.3f; face.intensity = 0.7f; }

        var renderers = car.GetComponentsInChildren<Renderer>(true);
        var cushion = renderers.FirstOrDefault(r => r.name == "Driver_Seat_Cushion") ?? throw new Exception("Driver_Seat_Cushion missing");
        var cb = cushion.bounds;
        Vector3 fwd = car.forward, up = car.up, right = car.right;
        Vector3 rearEdge = cb.center - fwd * Vector3.Dot(cb.extents, Abs(fwd));
        Vector3 hipTarget = rearEdge + fwd * HeroTune.hipFromRear + up * (Vector3.Dot(cb.extents, Abs(up)) + HeroTune.hipAboveCushion);
        hipTarget += right * HeroTune.hipSide;
        var hips = Deep(hero, "Hips");
        hero.position += hipTarget - hips.position;
        log.AppendLine("Seat cushion bounds=" + cb + " hips→" + hipTarget);

        // steering wheel rim target for the left hand
        var rim = renderers.Where(r => r.name.StartsWith("SteeringWheel_")).ToArray();
        if (rim.Length >= 6)
        {
            Vector3 c = rim.Aggregate(Vector3.zero, (s, r) => s + r.bounds.center) / rim.Length;
            Vector3 n = Vector3.Cross(rim[0].bounds.center - c, rim[rim.Length / 3].bounds.center - c).normalized;
            if (Vector3.Dot(n, hips.position - c) < 0) n = -n;
            float radius = rim.Average(r => (r.bounds.center - c).magnitude);
            Vector3 wUp = Vector3.ProjectOnPlane(up, n).normalized, wLeft = Vector3.ProjectOnPlane(-right, n).normalized;
            Vector3 dir = (wUp * HeroTune.gripUp + wLeft * HeroTune.gripLeft).normalized;
            var target = new GameObject("IK_Wheel_LeftHand").transform; target.SetParent(car, false);
            target.position = c + dir * (radius + HeroTune.wristOut) + n * HeroTune.wristBack;
            target.rotation = Quaternion.LookRotation(-n, dir);
            var upper = Deep(hero, "L_UpperArm");
            var hint = new GameObject("IK_LeftElbow_Hint").transform; hint.SetParent(car, false);
            hint.position = upper.position - right * 0.35f - up * 0.45f + fwd * 0.1f;
            var ik = hero.gameObject.GetComponent<HeroArmIK>() ?? hero.gameObject.AddComponent<HeroArmIK>();
            ik.upper = upper; ik.lower = Deep(hero, "L_LowerArm"); ik.end = Deep(hero, "L_Hand");
            ik.target = target; ik.hint = hint; ik.alignHandRotation = false; ik.weight = 1;
            HeroArmIK.Solve(ik.upper, ik.lower, ik.end, target.position, hint.position, 1);
            log.AppendLine("Wheel center=" + c + " radius=" + radius.ToString("F3") + " normal=" + n + " handErr=" + (ik.end.position - target.position).magnitude.ToString("F3"));
        }
        else log.AppendLine("WARNING steering wheel rim not found, IK skipped");

        // phone from the old primitive driver goes into the hero's right hand
        var driver = car.Find("Driver");
        if (driver)
        {
            var phone = driver.Find("Phone_1999") ?? MakePhone(driver);
            var hand = Deep(hero, "R_Hand"); var head = Deep(hero, "Head");
            if (phone && hand && head)
            {
                Vector3 hr = hero.right, hu = head.up, hf = head.forward;
                Vector3 ear = head.position + hr * 0.085f * HeroScale + hu * 0.06f + hf * 0.03f;
                Vector3 phoneUp = (hu * 0.75f - hf * 0.4f).normalized;
                Vector3 centre = ear + hf * 0.045f - hu * 0.05f + hr * HeroTune.phoneOut;
                phone.SetParent(hand, true);
                phone.position = centre;
                phone.rotation = Quaternion.LookRotation(Vector3.Cross(hr, phoneUp), phoneUp);
                log.AppendLine("Phone to wrist distance=" + (phone.position - hand.position).magnitude.ToString("F3"));
            }
            driver.gameObject.SetActive(false);
            log.AppendLine("Primitive Driver disabled");
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine("Saved " + scene.path);
        CaptureCar();
    }

    static Transform MakePhone(Transform parent)
    {
        var root = new GameObject("Phone_1999").transform; root.SetParent(parent, false);
        var dark = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Arts/Heroes/Phone_1999.mat");
        if (!dark) { dark = new Material(Shader.Find("Universal Render Pipeline/Lit")); dark.color = new Color(.05f, .055f, .06f); dark.SetFloat("_Smoothness", .55f); AssetDatabase.CreateAsset(dark, "Assets/_Project/Arts/Heroes/Phone_1999.mat"); }
        void Part(PrimitiveType type, string name, Vector3 pos, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(root, false); go.transform.localPosition = pos; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = dark;
        }
        Part(PrimitiveType.Cube, "Body", Vector3.zero, new Vector3(.024f, .13f, .05f));
        Part(PrimitiveType.Cylinder, "Antenna", new Vector3(0, .085f, .015f), new Vector3(.008f, .03f, .008f));
        log.AppendLine("Phone_1999 rebuilt");
        return root;
    }

    static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    static void CaptureCar()
    {
        var car = GameObject.Find("Hero_Car").transform;
        var hero = car.Find("Alexey_Driver");
        if (Camera.main) Shot(Camera.main, "car_main.png");
        var rear = car.GetComponentsInChildren<Camera>(true).FirstOrDefault(c => c.name == "Rear_Passenger_Camera");
        if (rear && rear != Camera.main) Shot(rear, "car_rear_passenger.png");
        if (hero)
        {
            var hips = Deep(hero, "Hips").position;
            DebugShot(hips + car.right * 0.85f + car.up * 0.45f + car.forward * 0.35f, hips + car.up * 0.3f, "car_from_passenger.png", 70);
            DebugShot(hips + car.forward * 0.75f + car.up * 0.55f + car.right * 0.2f, hips + car.up * 0.35f, "car_from_dash.png", 75);
            var head = Deep(hero, "Head").position;
            DebugShot(head + car.right * 0.55f + car.forward * 0.35f + car.up * 0.05f, head, "car_head_closeup.png", 50);
            DebugShot(hips - car.right * 2.2f + car.up * 0.5f, hips + car.up * 0.25f, "car_from_outside_left.png", 45);
        }
    }

    // ---------------------------------------------------------------- apartment
    static void InstallApartment()
    {
        var scene = EditorSceneManager.OpenScene(FlatScene);
        Backup(scene, "HeroFlat");
        var player = GameObject.Find("Player")?.transform ?? throw new Exception("Player missing");
        var visual = player.Find("Character_Visual");
        if (!visual) { visual = new GameObject("Character_Visual").transform; visual.SetParent(player, false); }
        for (int i = visual.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(visual.GetChild(i).gameObject);
        var anim = CreateHero(visual, "Alexey", "Hero_Locomotion.controller");
        var hero = visual.Find("Alexey");
        var loco = hero.gameObject.GetComponent<HeroLocomotion>() ?? hero.gameObject.AddComponent<HeroLocomotion>();
        loco.animator = anim; loco.controller = player.GetComponent<CharacterController>();
        Clip("Idle").SampleAnimation(anim.gameObject, 0);
        ConfigureApartmentFill(player);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine("Saved " + scene.path);
        CaptureApartment();
    }

    internal static void ConfigureApartmentFill(Transform player)
    {
        const int heroFillLayer = 1 << 1;
        var visual = player.Find("Character_Visual");
        if (!visual) return;
        foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            renderer.renderingLayerMask |= heroFillLayer;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
        SetFill("Alexey_Fill", new Vector3(.73f, 1.72f, -.73f), 1.65f);
        SetFill("Alexey_Front_Fill", new Vector3(-.66f, 1.63f, .78f), 1.1f);

        void SetFill(string name, Vector3 position, float intensity)
        {
            var fill = player.Find(name);
            if (!fill)
            {
                fill = new GameObject(name).transform;
                fill.SetParent(player, false);
            }
            fill.localPosition = position;
            var light = fill.GetComponent<Light>();
            if (!light) light = fill.gameObject.AddComponent<Light>();
            light.enabled = true;
            light.type = LightType.Point;
            light.color = new Color(1f, .97f, .91f);
            light.intensity = intensity;
            light.range = 3.1f;
            light.shadows = LightShadows.None;
            light.GetUniversalAdditionalLightData().renderingLayers = heroFillLayer;
        }
    }

    static void CaptureApartment()
    {
        var player = GameObject.Find("Player").transform;
        var hero = player.Find("Character_Visual/Alexey");
        if (Camera.main) Shot(Camera.main, "flat_main.png");
        if (!hero) return;
        var anim = hero.GetComponentInChildren<Animator>();
        Vector3 p = player.position, f = player.forward, r = player.right;
        DebugShot(p + f * 2.0f + Vector3.up * 1.1f, p + Vector3.up * 0.95f, "flat_front_idle.png", 55);
        Clip("Walk").SampleAnimation(anim.gameObject, 0.2f);
        DebugShot(p + r * 2.2f + Vector3.up * 1.0f, p + Vector3.up * 0.9f, "flat_side_walk.png", 55);
        Clip("Idle").SampleAnimation(anim.gameObject, 0);
    }

    // ---------------------------------------------------------------- capture helpers
    internal static void DebugShot(Vector3 from, Vector3 at, string file, float fov)
    {
        var go = new GameObject("__HeroCaptureCam");
        try
        {
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov; cam.nearClipPlane = 0.02f; cam.farClipPlane = 500;
            go.transform.position = from; go.transform.LookAt(at);
            if (Camera.main) { cam.clearFlags = Camera.main.clearFlags; cam.backgroundColor = Camera.main.backgroundColor; }
            Shot(cam, file);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    internal static void Shot(Camera camera, string file)
    {
        Directory.CreateDirectory(IoDir);
        var old = camera.targetTexture; var active = RenderTexture.active;
        var rt = new RenderTexture(1280, 720, 24); var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
            File.WriteAllBytes(Path.Combine(IoDir, file), tex.EncodeToPNG());
            log.AppendLine("Capture " + file);
        }
        finally
        {
            camera.targetTexture = old; RenderTexture.active = active;
            UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}

/// Tunables for the seated pose (metres, car space). Edit and re-run "car" to adjust.
public static class HeroTune
{
    public static float hipFromRear = 0.42f;
    public static float hipAboveCushion = 0.05f;
    public static float hipSide = 0f;
    public static float gripUp = 0.55f, gripLeft = 0.85f;
    public static float wristOut = 0.03f, wristBack = 0.04f;
    public static float phoneOut = 0.02f;
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using CarXCase.Intro;
using CarXCase.Weather;

/// Realistic rain + light fog for 00_BrightonDrive_Models.
/// Menu: Tools/Brighton/Rain: build realistic rain and fog  (or write "rain" to HeroSetup/rain_request.txt).
/// Everything it builds lives under Hero_Car/Rain_System (+ fog cones under the lamps, Hero_Headlights,
/// Rain_Atmosphere_Volume) and is rebuilt from scratch on every run. The scene is backed up once.
[InitializeOnLoad]
public static class RainAtmosphereSetup
{
    const string ScenePath = "Assets/_Project/Scenes/00_BrightonDrive_Models.unity";
    const string Dir = "Assets/_Project/ArtDirection/Rain/";
    const string Io = "HeroSetup";
    static readonly StringBuilder log = new StringBuilder();
    const float WiperShift = 0.07f;   // move wipers to the right (passenger side), metres

    static RainAtmosphereSetup() { EditorApplication.update += Poll; }

    static void Poll()
    {
        string req = Path.Combine(Io, "rain_request.txt");
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!File.Exists(req)) return;
        if (File.Exists(Path.Combine(Io, "herocar_request.txt"))) return; // let the interior install first
        File.Delete(req); Run();
    }

    [MenuItem("Tools/Brighton/Rain: build realistic rain and fog")]
    public static void Run()
    {
        log.Clear(); log.AppendLine("RAIN BUILD " + DateTime.Now);
        try { Build(); log.AppendLine("SUCCESS"); }
        catch (Exception e) { log.AppendLine("ERROR " + e); Debug.LogException(e); }
        Directory.CreateDirectory(Io);
        File.WriteAllText(Path.Combine(Io, "rain_result.txt"), log.ToString());
        Debug.Log("[Rain] " + log);
    }

    // ------------------------------------------------------------------ helpers
    static Shader CheckedShader(string name)
    {
        var sh = Shader.Find(name) ?? throw new Exception("Shader not found / failed to compile: " + name);
        foreach (var m in ShaderUtil.GetShaderMessages(sh)) log.AppendLine($"  {name} {m.severity}: {m.message} (line {m.line})");
        if (ShaderUtil.ShaderHasError(sh)) throw new Exception("Shader has errors: " + name);
        return sh;
    }

    static Material Mat(string name, Shader sh)
    {
        Directory.CreateDirectory(Dir);
        string path = Dir + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); } else m.shader = sh;
        EditorUtility.SetDirty(m); return m;
    }

    static Transform Deep(Transform r, string n) => r ? r.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n) : null;

    static ParticleSystem.MinMaxCurve C(float a, float b) => new ParticleSystem.MinMaxCurve(a, b);

    // ------------------------------------------------------------------ build
    static void Build()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play Mode first");
        var streakSh = CheckedShader("CarXCase/RainStreak");
        var glassSh = CheckedShader("CarXCase/RainOnGlass");
        var coneSh = CheckedShader("CarXCase/FogLightCone");

        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);
        var cut = UnityEngine.Object.FindAnyObjectByType<BrightonDriveCutscene>() ?? throw new Exception("BrightonDriveCutscene not found");
        var car = cut.car;
        if (!car.Find("Rain_System"))
        {
            string backup = "Assets/_Project/Scenes/BeforeRealRain_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity";
            EditorSceneManager.SaveScene(scene, backup, true); log.AppendLine("Backup " + backup);
        }
        var prev = car.Find("Rain_System"); if (prev) UnityEngine.Object.DestroyImmediate(prev.gameObject);
        var root = new GameObject("Rain_System").transform; root.SetParent(car, false);

        // old, flat rain: kept but switched off
        foreach (var n in new[] { "Rain_Particles", "Rain_Right_Street" })
        {
            var o = Deep(car, n) ?? GameObject.Find(n)?.transform;
            if (o) { o.gameObject.SetActive(false); log.AppendLine("Disabled old " + n); }
        }

        // ---------------- materials
        Material Streak(string n, float opacity, float scatter)
        {
            var m = Mat(n, streakSh); m.SetFloat("_Opacity", opacity); m.SetFloat("_ForwardScatter", scatter);
            m.renderQueue = (int)RenderQueue.Transparent; return m;
        }
        var near = Streak("Rain_Near", 0.28f, 1.6f);
        var mid = Streak("Rain_Mid", 0.19f, 1.4f);
        var far = Streak("Rain_Far", 0.075f, 1.0f); far.SetFloat("_SoftDistance", 0.5f);
        var splashM = Streak("Rain_Splash", 0.24f, 1.8f); splashM.SetFloat("_SoftDistance", 0.02f);

        // ---------------- ground plane for collisions / splashes
        var ground = new GameObject("Rain_GroundPlane").transform; ground.SetParent(root, false);
        ground.position = new Vector3(car.position.x, 0.01f, car.position.z); ground.rotation = Quaternion.identity;

        ParticleSystem Splash(Transform parent)
        {
            var go = new GameObject("Splashes"); go.transform.SetParent(parent, false); go.transform.rotation = Quaternion.identity;
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = true; main.playOnAwake = false; main.duration = 1;
            main.startLifetime = C(0.14f, 0.3f); main.startSpeed = C(0.5f, 1.5f); main.startSize = C(0.0025f, 0.0055f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1, 1, 1, 0.6f), new Color(1, 1, 1, 1f));
            main.gravityModifier = 1.3f; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 5000;
            var em = ps.emission; em.rateOverTime = 0; em.SetBursts(new[] { new ParticleSystem.Burst(0, 2, 4) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 38; sh.radius = 0.006f; sh.rotation = new Vector3(-90, 0, 0);
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Stretch; pr.lengthScale = 1.2f; pr.velocityScale = 0.025f; pr.cameraVelocityScale = 0;
            pr.sharedMaterial = splashM; pr.shadowCastingMode = ShadowCastingMode.Off; pr.receiveShadows = false;
            return ps;
        }

        ParticleSystem Layer(string name, Vector3 localCenter, Vector2 boxXZ, float rate, int max, Vector2 width, Vector2 speed, float life, Material mat, float stretch, float splashChance)
        {
            var go = new GameObject(name); go.transform.SetParent(root, false);
            go.transform.localPosition = localCenter; go.transform.localRotation = Quaternion.Euler(90, 0, 0); // local +Z = down
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = true; main.prewarm = true; main.duration = 4; main.playOnAwake = true;
            main.startLifetime = life; main.startSpeed = C(speed.x, speed.y); main.startSize = C(width.x, width.y);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1, 1, 1, 0.55f), new Color(1, 1, 1, 1f));
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = max; main.gravityModifier = 0.15f;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            var em = ps.emission; em.rateOverTime = rate;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(boxXZ.x, boxXZ.y, 0.05f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = C(-1.1f, -0.5f); vel.y = C(0, 0); vel.z = C(-0.25f, 0.25f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.25f; noise.frequency = 0.35f; noise.scrollSpeed = 0.4f; noise.quality = ParticleSystemNoiseQuality.Low;
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Stretch; pr.lengthScale = 2f; pr.velocityScale = stretch; pr.cameraVelocityScale = stretch;
            pr.sharedMaterial = mat; pr.shadowCastingMode = ShadowCastingMode.Off; pr.receiveShadows = false; pr.maxParticleSize = 0.2f;
            if (splashChance > 0)
            {
                var col = ps.collision; col.enabled = true; col.type = ParticleSystemCollisionType.Planes;
                col.SetPlane(0, ground); col.lifetimeLoss = 1; col.bounce = 0; col.dampen = 1; col.radiusScale = 0.01f;
                var splash = Splash(go.transform);
                var sub = ps.subEmitters; sub.enabled = true;
                sub.AddSubEmitter(splash, ParticleSystemSubEmitterType.Collision, ParticleSystemSubEmitterProperties.InheritNothing, splashChance);
            }
            ps.Play(true);
            return ps;
        }
        // car moves +Z at ~4.5 m/s, so emitters are shifted ahead
        Layer("Rain_Near", new Vector3(0, 5.5f, 3.0f), new Vector2(9, 13), 4200, 9000, new Vector2(0.0035f, 0.006f), new Vector2(8.5f, 10.5f), 1.1f, near, 0.017f, 0.4f);
        Layer("Rain_Mid", new Vector3(0, 11f, 22f), new Vector2(30, 52), 16000, 30000, new Vector2(0.006f, 0.011f), new Vector2(9f, 11f), 1.6f, mid, 0.017f, 0.12f);
        Layer("Rain_Far", new Vector3(0, 14f, 105f), new Vector2(46, 110), 7000, 12000, new Vector2(0.02f, 0.035f), new Vector2(9.5f, 11f), 1.45f, far, 0.02f, 0);
        log.AppendLine("Rain layers built (near/mid/far + splashes)");

        // ---------------- rain on glass + wipers
        var interior = car.Find("HeroCar_Interior");
        var wiperPivots = new List<Transform>(); var wiperAxes = new List<Vector3>();
        if (interior)
        {
            var wind = Mat("RainGlass_Windshield", glassSh); wind.SetFloat("_StaticAmount", 0.42f); wind.SetFloat("_StaticSize", 0.009f); wind.SetFloat("_RunningAmount", 0.48f); wind.SetFloat("_EdgeDarken", 0.35f); wind.renderQueue = (int)RenderQueue.Transparent + 1;
            var side = Mat("RainGlass_Side", glassSh); side.renderQueue = (int)RenderQueue.Transparent + 1;
            side.SetFloat("_Wipers", 0); side.SetFloat("_StaticAmount", 0.28f); side.SetFloat("_StaticSize", 0.013f); side.SetFloat("_RunningAmount", 0.35f); side.SetFloat("_EdgeDarken", 0.3f);
            foreach (var n in new[] { "Windshield_Laminated", "Left_Front_Glass", "Right_Front_Glass", "Left_Rear_Glass", "Right_Rear_Glass", "Left_Quarter_Glass", "Right_Quarter_Glass", "Rear_Window_Heated" })
            {
                var t = Deep(interior, n); if (!t) { log.AppendLine("Glass not found: " + n); continue; }
                var old = t.Find("RainOnGlass"); if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
                var go = new GameObject("RainOnGlass"); go.transform.SetParent(t, false);
                go.AddComponent<MeshFilter>().sharedMesh = t.GetComponent<MeshFilter>().sharedMesh;
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = n.StartsWith("Windshield") ? wind : side;
                r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            }
            SetupWipers(car, interior, root, wind, wiperPivots, wiperAxes);
        }
        else log.AppendLine("HeroCar_Interior not found: glass drops and wipers skipped");

        var atmo = root.gameObject.AddComponent<RainAtmosphere>();
        atmo.car = car; atmo.wiperPivots = wiperPivots.ToArray(); atmo.wiperAxes = wiperAxes.ToArray();

        // ---------------- headlights of the hero car
        var heads = car.Find("Hero_Headlights"); if (heads) UnityEngine.Object.DestroyImmediate(heads.gameObject);
        heads = new GameObject("Hero_Headlights").transform; heads.SetParent(car, false);
        var headCone = Mat("FogCone_Headlight", coneSh);
        headCone.SetColor("_Color", new Color(1f, 0.9f, 0.75f)); headCone.SetFloat("_Intensity", 0.035f); headCone.SetFloat("_Falloff", 1.2f);
        var coneMesh = ConeMesh();
        foreach (var x in new[] { -0.62f, 0.62f })
        {
            var g = new GameObject(x < 0 ? "Headlight_L" : "Headlight_R"); g.transform.SetParent(heads, false);
            g.transform.localPosition = new Vector3(x, 0.74f, 2.22f); g.transform.localRotation = Quaternion.Euler(5.5f, x * 2.5f, 0);
            var l = g.AddComponent<Light>(); l.type = LightType.Spot; l.useColorTemperature = false; l.color = new Color(1f, 0.88f, 0.72f);
            l.intensity = 260; l.range = 42; l.spotAngle = 62; l.innerSpotAngle = 32; l.shadows = LightShadows.None;
            Cone(g.transform, coneMesh, headCone, 16f, 26f);
        }

        // ---------------- light shafts under the sodium lamps
        var sodiumCone = Mat("FogCone_Sodium", coneSh);
        sodiumCone.SetColor("_Color", new Color(1f, 0.62f, 0.3f)); sodiumCone.SetFloat("_Intensity", 0.05f); sodiumCone.SetFloat("_Falloff", 1.1f);
        int cones = 0;
        foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(l => l.type == LightType.Spot && l.name == "Sodium_Pool"))
        {
            var old = l.transform.Find("Fog_Cone"); if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
            float down = Mathf.Max(0.2f, -l.transform.forward.y);
            float h = Mathf.Min(l.range * 0.8f, l.transform.position.y / down);
            Cone(l.transform, coneMesh, sodiumCone, h, Mathf.Min(l.spotAngle, 80f)); cones++;
        }
        log.AppendLine("Fog cones under sodium lamps: " + cones);

        // ---------------- fog: light, exponential, cool grey-blue
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.0095f; RenderSettings.fogColor = new Color(0.072f, 0.08f, 0.09f);
        foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c.clearFlags == CameraClearFlags.SolidColor) c.backgroundColor = RenderSettings.fogColor;
        log.AppendLine("Fog: exp2 density 0.0095");

        // ---------------- wet ground: rain is hitting the puddles
        void Wet(string n, float ripple, float speed)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Weather/" + n + ".mat");
            if (!m) return;
            if (m.HasProperty("_RippleStrength")) m.SetFloat("_RippleStrength", ripple);
            if (m.HasProperty("_RainSpeed")) m.SetFloat("_RainSpeed", speed);
            if (m.HasProperty("_RippleScale")) m.SetFloat("_RippleScale", 3.8f);
            if (m.HasProperty("_Wetness")) m.SetFloat("_Wetness", Mathf.Max(m.GetFloat("_Wetness"), 0.8f));
            EditorUtility.SetDirty(m);
        }
        Wet("WetRoad", 0.19f, 1.4f); Wet("WetSidewalk", 0.4f, 1.4f); Wet("WetCurb", 0.25f, 1.4f); Wet("WetPaint", 0.4f, 1.4f);

        // ---------------- planar reflections for puddles / wet asphalt
        var puddleGo = GameObject.Find("Puddle_Reflection");
        if (!puddleGo) puddleGo = new GameObject("Puddle_Reflection");
        var refl = puddleGo.GetComponent<PuddleReflection>() ?? puddleGo.AddComponent<PuddleReflection>();
        refl.planeHeight = 0f;
        foreach (var n in new[] { "WetRoad", "WetSidewalk", "WetCurb", "WetPaint" })
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Weather/" + n + ".mat");
            if (!m || !m.HasProperty("_ReflectionStrength")) continue;
            m.SetFloat("_ReflectionStrength", 1f); m.SetFloat("_WetReflection", n == "WetRoad" ? 0.55f : 0.35f);
            m.SetFloat("_ReflectionBlur", 3f); m.SetFloat("_StreakLength", 0.014f); m.SetFloat("_ReflectionDistort", 0.08f);
            EditorUtility.SetDirty(m);
        }
        log.AppendLine("Puddle planar reflection ready");

        // ---------------- post: bloom for lamp glow in the wet air, light grain, vignette
        var vol = GameObject.Find("Rain_Atmosphere_Volume");
        if (!vol) vol = new GameObject("Rain_Atmosphere_Volume");
        var v = vol.GetComponent<Volume>() ?? vol.AddComponent<Volume>();
        v.isGlobal = true; v.priority = 5; v.weight = 1; v.sharedProfile = Profile();

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine("Saved " + scene.path);
    }

    static void SetupWipers(Transform car, Transform interior, Transform root, Material wind, List<Transform> pivots, List<Vector3> axes)
    {
        var ws = Deep(interior, "Windshield_Laminated");
        var mesh = ws.GetComponent<MeshFilter>().sharedMesh;
        var verts = mesh.vertices; var uvs = mesh.uv;
        Vector3 L(int i) => car.InverseTransformPoint(ws.TransformPoint(verts[i]));
        int iMinX = 0, iMaxX = 0, iMinY = 0, iMaxY = 0;
        for (int i = 0; i < verts.Length; i++)
        {
            if (L(i).x < L(iMinX).x) iMinX = i; if (L(i).x > L(iMaxX).x) iMaxX = i;
            if (L(i).y < L(iMinY).y) iMinY = i; if (L(i).y > L(iMaxY).y) iMaxY = i;
        }
        float a = (uvs[iMaxX].x - uvs[iMinX].x) / (L(iMaxX).x - L(iMinX).x), b = uvs[iMinX].x - a * L(iMinX).x;
        float c = (uvs[iMaxY].y - uvs[iMinY].y) / (L(iMaxY).y - L(iMinY).y), d = uvs[iMinY].y - c * L(iMinY).y;
        Vector2 ToUV(Vector3 local) => new Vector2(a * local.x + b, c * local.y + d);
        Vector3 nrm = car.InverseTransformDirection(ws.TransformDirection(mesh.normals[0])).normalized;
        if (nrm.z < 0) nrm = -nrm;                                  // outward = forward
        Vector3 upG = Vector3.ProjectOnPlane(Vector3.up, nrm).normalized;
        float vScale = 1f / Mathf.Max(0.2f, upG.y);
        log.AppendLine($"Windshield uv fit u={a:F3}x+{b:F3} v={c:F3}y+{d:F3}; normal {nrm:F3}; vScale {vScale:F3}");

        var uvPiv = new List<Vector2>(); float rMax = 0.5f;
        int k = 0;
        foreach (var wn in new[] { "Windshield_Wiper", "Windshield_Wiper.001", "Windshield_Wiper_001" })
        {
            var w = Deep(interior, wn); if (!w || !w.GetComponent<MeshFilter>()) continue;
            var wm = w.GetComponent<MeshFilter>().sharedMesh; var wv = wm.vertices.Select(p => car.InverseTransformPoint(w.TransformPoint(p))).ToArray();
            float minX = wv.Min(p => p.x), maxX = wv.Max(p => p.x);
            Vector3 mean = new Vector3(0, wv.Average(p => p.y), wv.Average(p => p.z));
            // pivot: the end of the blade toward the driver's side (−X), a little past the blade,
            // the whole wiper shifted right (+X) by WiperShift
            var pivotLocal = new Vector3(minX - 0.025f + WiperShift, mean.y, mean.z);
            float bladeLen = (maxX - minX) * 1.05f;
            rMax = Mathf.Max(rMax, bladeLen + 0.06f);
            var pivot = new GameObject("Wiper_Pivot_" + k).transform; pivot.SetParent(root, false);
            pivot.position = car.TransformPoint(pivotLocal); pivot.rotation = car.rotation;
            BuildWiper(pivot, car, nrm, upG, bladeLen);
            w.GetComponent<MeshRenderer>().enabled = false;
            // positive rotation takes the parked blade (+X) up along the glass
            Vector3 axisCar = Vector3.Cross(Vector3.right, upG).normalized;
            pivots.Add(pivot); axes.Add(Quaternion.Inverse(pivot.localRotation) * root.InverseTransformDirection(car.TransformDirection(axisCar)));
            uvPiv.Add(ToUV(pivotLocal)); k++;
            log.AppendLine($"Wiper {wn}: pivot {pivotLocal:F3} uv {ToUV(pivotLocal):F3}");
        }
        if (uvPiv.Count == 0) { wind.SetFloat("_Wipers", 0); log.AppendLine("No wipers found"); return; }
        if (uvPiv.Count == 1) uvPiv.Add(uvPiv[0]);
        wind.SetFloat("_Wipers", 1); wind.SetFloat("_VScale", vScale);
        wind.SetVector("_WiperPivots", new Vector4(uvPiv[0].x, uvPiv[0].y, uvPiv[1].x, uvPiv[1].y));
        wind.SetVector("_WiperRange", new Vector4(0.02f, rMax, 0, 0));
        // shader measures angles in uv space; if u runs opposite to car X the parked direction flips
        if (a > 0) log.AppendLine("WARNING: windshield u runs along +X; wiper clearing may be mirrored");
    }


    static Material WiperMat(string name, Color c, float smooth)
    {
        var m = Mat(name, Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", 0);
        return m;
    }

    // Arm + framed blade + rubber + pivot cap, lying on the glass, blade parked along car +X
    static void BuildWiper(Transform pivot, Transform car, Vector3 nrmCar, Vector3 upCar, float bladeLen)
    {
        var black = WiperMat("Wiper_SatinBlack", new Color(0.035f, 0.035f, 0.038f), 0.38f);
        var rubber = WiperMat("Wiper_Rubber", new Color(0.015f, 0.015f, 0.015f), 0.15f);
        Vector3 n = car.TransformDirection(nrmCar), up = car.TransformDirection(upCar), x = car.TransformDirection(Vector3.right);
        var rot = Quaternion.LookRotation(n, up);   // local X = blade direction, Y = up the glass, Z = out of the glass
        void Part(string name, Vector3 centerAlong, Vector3 size, Material m, PrimitiveType type = PrimitiveType.Cube)
        {
            var g = GameObject.CreatePrimitive(type); g.name = name;
            UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(pivot, false);
            g.transform.SetPositionAndRotation(pivot.position + x * centerAlong.x + up * centerAlong.y + n * centerAlong.z, rot);
            g.transform.localScale = size;
            var r = g.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.On;
        }
        float start = 0.035f, mid = start + bladeLen * 0.5f;
        Part("Blade_Frame", new Vector3(mid, 0, 0.022f), new Vector3(bladeLen, 0.026f, 0.022f), black);
        Part("Blade_Spine", new Vector3(mid, 0, 0.036f), new Vector3(bladeLen * 0.55f, 0.016f, 0.014f), black);
        Part("Blade_Rubber", new Vector3(mid, -0.002f, 0.007f), new Vector3(bladeLen * 0.99f, 0.009f, 0.012f), rubber);
        Part("Arm", new Vector3(mid * 0.5f + 0.01f, 0.004f, 0.048f), new Vector3(mid + 0.03f, 0.02f, 0.016f), black);
        Part("Arm_Clip", new Vector3(mid, 0, 0.044f), new Vector3(0.05f, 0.028f, 0.022f), black);
        var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder); cap.name = "Pivot_Cap";
        UnityEngine.Object.DestroyImmediate(cap.GetComponent<Collider>());
        cap.transform.SetParent(pivot, false);
        cap.transform.SetPositionAndRotation(pivot.position + n * 0.03f, Quaternion.LookRotation(up, n));
        cap.transform.localScale = new Vector3(0.045f, 0.018f, 0.045f);
        cap.GetComponent<Renderer>().sharedMaterial = black;
    }

    static Mesh ConeMesh()
    {
        string path = Dir + "FogCone.asset";
        var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (m) return m;
        m = new Mesh { name = "FogCone" };
        const int seg = 32, rings = 10;
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        for (int r = 0; r <= rings; r++)
        {
            float h = r / (float)rings;
            for (int s = 0; s <= seg; s++)
            {
                float a = s / (float)seg * Mathf.PI * 2;
                v.Add(new Vector3(Mathf.Cos(a) * h, Mathf.Sin(a) * h, h));
                n.Add(new Vector3(Mathf.Cos(a), Mathf.Sin(a), -1).normalized);
                uv.Add(new Vector2(s / (float)seg, h));
            }
        }
        for (int r = 0; r < rings; r++)
            for (int s = 0; s < seg; s++)
            {
                int i0 = r * (seg + 1) + s, i1 = i0 + 1, i2 = i0 + seg + 1, i3 = i2 + 1;
                tri.AddRange(new[] { i0, i2, i1, i1, i2, i3 });
            }
        m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tri, 0); m.RecalculateBounds();
        Directory.CreateDirectory(Dir); AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static void Cone(Transform light, Mesh mesh, Material mat, float length, float angle)
    {
        var g = new GameObject("Fog_Cone"); g.transform.SetParent(light, false);
        float r = length * Mathf.Tan(angle * 0.5f * Mathf.Deg2Rad);
        g.transform.localScale = new Vector3(r, r, length);
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false; mr.lightProbeUsage = LightProbeUsage.Off; mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    static VolumeProfile Profile()
    {
        string path = Dir + "RainAtmosphere_Profile.asset";
        var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (!p) { Directory.CreateDirectory(Dir); p = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(p, path); }
        T Get<T>() where T : VolumeComponent
        {
            if (p.TryGet<T>(out var c)) return c;
            c = p.Add<T>(true); c.name = typeof(T).Name; AssetDatabase.AddObjectToAsset(c, p); return c;
        }
        var bloom = Get<Bloom>(); bloom.active = true;
        bloom.threshold.Override(0.9f); bloom.intensity.Override(0.55f); bloom.scatter.Override(0.78f); bloom.highQualityFiltering.Override(true);
        var vig = Get<Vignette>(); vig.active = true; vig.intensity.Override(0.24f); vig.smoothness.Override(0.45f);
        var grain = Get<FilmGrain>(); grain.active = true; grain.type.Override(FilmGrainLookup.Medium2); grain.intensity.Override(0.12f); grain.response.Override(0.8f);
        EditorUtility.SetDirty(p);
        return p;
    }
}

using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Rain look for the drive cutscene: rusty metal on the elevated railway,
/// wet asphalt / sidewalks with puddles and ripples, falling rain. Command: "rain".
public static class WeatherSetup
{
    const string Dir = "Assets/_Project/Materials/Weather/";
    const string Scene = "Assets/_Project/Scenes/00_BrightonDrive_Models.unity";

    static Texture2D Tex(string file, bool normal)
    {
        string path = Dir + file;
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        if (ti)
        {
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = !normal; ti.wrapMode = TextureWrapMode.Repeat; ti.anisoLevel = 8; ti.mipmapEnabled = true;
            ti.alphaSource = normal ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material Mat(string name, string shader)
    {
        var sh = Shader.Find(shader) ?? throw new Exception("Shader not found / failed to compile: " + shader);
        string path = Dir + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        m.shader = sh; EditorUtility.SetDirty(m); return m;
    }

    static Texture BaseOf(Renderer r)
    {
        var m = r ? r.sharedMaterial : null;
        if (!m) return null;
        if (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap")) return m.GetTexture("_BaseMap");
        if (m.HasProperty("_MainTex")) return m.GetTexture("_MainTex");
        return null;
    }
    static Color ColorOf(Renderer r)
    {
        var m = r ? r.sharedMaterial : null;
        if (m && m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
        if (m && m.HasProperty("_Color")) return m.color;
        return Color.gray;
    }

    public static void Run(StringBuilder log)
    {
        AssetDatabase.ImportAsset(Dir + "RustyMetal.shader", ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset(Dir + "WetGround.shader", ImportAssetOptions.ForceSynchronousImport);
        var rustA = Tex("Rust_BaseColor.png", false);
        var rustN = Tex("Rust_Normal.png", true);
        var groundN = Tex("Ground_DetailNormal.png", true);

        foreach (var sn in new[] { "Project/Wet Ground", "Project/Rusty Metal" })
        {
            var sh = Shader.Find(sn);
            if (!sh) { log.AppendLine("SHADER MISSING " + sn); continue; }
            log.AppendLine("Shader " + sn + " hasError=" + ShaderUtil.ShaderHasError(sh));
            foreach (var m in ShaderUtil.GetShaderMessages(sh)) log.AppendLine("   " + m.severity + ": " + m.message + " line " + m.line);
        }
        var scene = EditorSceneManager.OpenScene(Scene);
        EditorSceneManager.SaveScene(scene, "Assets/_Project/Scenes/BeforeRain_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity", true);
        var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(true)).ToArray();

        // ---------- rusty metal on every element of the elevated railway
        var rust = Mat("RustyMetal", "Project/Rusty Metal");
        rust.SetTexture("_BaseMap", rustA); rust.SetTexture("_NormalMap", rustN);
        rust.SetFloat("_TextureScale", .45f); rust.SetFloat("_Wetness", .45f); rust.SetFloat("_Metallic", .2f);
        int rails = 0;
        foreach (var r in all.Where(r => r.name.StartsWith("Rail_")))
        {
            var mm = r.sharedMaterials; for (int i = 0; i < mm.Length; i++) mm[i] = rust; r.sharedMaterials = mm; rails++;
        }
        log.AppendLine("Rusty metal on railway renderers: " + rails);

        // ---------- wet ground
        Material Ground(string name, Renderer sample, float scale, float puddles, float seed, Texture fallback)
        {
            var m = Mat(name, "Project/Wet Ground");
            var baseTex = BaseOf(sample) ?? fallback;
            m.SetTexture("_BaseMap", baseTex); m.SetColor("_BaseColor", baseTex ? Color.white : ColorOf(sample));
            m.SetTexture("_DetailNormal", groundN); m.SetFloat("_TextureScale", scale);
            m.SetFloat("_PuddleAmount", puddles); m.SetFloat("_PuddleSeed", seed);
            log.AppendLine($"{name}: base={(baseTex ? baseTex.name : "colour " + ColorOf(sample))}");
            return m;
        }
        var road = all.FirstOrDefault(r => r.name == "Road");
        var walk = all.FirstOrDefault(r => r.name.StartsWith("Sidewalk_"));
        var curb = all.FirstOrDefault(r => r.name.StartsWith("Curb_"));
        var line = all.FirstOrDefault(r => r.name.StartsWith("Centerline_"));
        var asphaltTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Materials/BrightonModels/WetAsphalt_BaseColor.png");
        var wetRoad = Ground("WetRoad", road, .25f, .30f, 0, asphaltTex);
        if (road && BaseOf(road) == null && asphaltTex) wetRoad.SetTexture("_BaseMap", asphaltTex);
        var wetWalk = Ground("WetSidewalk", walk, .5f, .26f, 13, null);
        var wetCurb = Ground("WetCurb", curb, .5f, 0, 27, null);
        var wetLine = Ground("WetPaint", line, 1, 0, 0, null);
        wetLine.SetFloat("_WetDarken", .2f);
        int g = 0;
        foreach (var r in all)
        {
            Material m = r.name == "Road" ? wetRoad : r.name.StartsWith("Sidewalk_") ? wetWalk : r.name.StartsWith("Curb_") ? wetCurb : r.name.StartsWith("Centerline_") ? wetLine : null;
            if (!m) continue;
            var mm = r.sharedMaterials; for (int i = 0; i < mm.Length; i++) mm[i] = m; r.sharedMaterials = mm; g++;
        }
        log.AppendLine("Wet ground renderers: " + g);

        // ---------- falling rain that travels with the camera rig
        var cam = Camera.main;
        var old = GameObject.Find("Rain_Particles"); if (old) UnityEngine.Object.DestroyImmediate(old);
        if (cam)
        {
            var go = new GameObject("Rain_Particles");
            go.transform.SetParent(cam.transform.parent ? cam.transform.parent : cam.transform, false);
            go.transform.localPosition = new Vector3(0, 9, 8);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main; main.loop = true; main.startLifetime = 1.1f; main.startSpeed = 0; main.maxParticles = 6000;
            main.startSize = new ParticleSystem.MinMaxCurve(.012f, .02f); main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(.75f, .8f, .85f, .32f); main.prewarm = true;
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-.6f, -.6f); vel.y = new ParticleSystem.MinMaxCurve(-15f, -15f); vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var em = ps.emission; em.rateOverTime = 3500;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(26, .1f, 40);
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Stretch; pr.lengthScale = 2.5f; pr.velocityScale = .04f;
            var pm = Mat("RainDrop", "Universal Render Pipeline/Particles/Unlit");
            pm.SetFloat("_Surface", 1); pm.SetFloat("_Blend", 0);
            pm.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); pm.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            pm.SetFloat("_ZWrite", 0); pm.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); pm.renderQueue = 3000;
            pm.SetColor("_BaseColor", new Color(.8f, .85f, .9f, .35f));
            pr.sharedMaterial = pm; pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // no rain inside the car: particles entering the cabin volume are killed
            var carT = GameObject.Find("Hero_Car")?.transform;
            var visual = carT ? carT.Find("Sedan_2000_Visual") : null;
            if (visual)
            {
                var rr = visual.GetComponentsInChildren<Renderer>(); var b = rr[0].bounds; foreach (var r in rr) b.Encapsulate(r.bounds);
                var oldBox = carT.Find("Rain_Blocker"); if (oldBox) UnityEngine.Object.DestroyImmediate(oldBox.gameObject);
                var box = new GameObject("Rain_Blocker").AddComponent<BoxCollider>();
                box.transform.SetParent(carT, false); box.isTrigger = true;
                box.transform.position = b.center; box.size = b.size + new Vector3(.1f, .3f, .1f);
                var trig = ps.trigger; trig.enabled = true; trig.AddCollider(box);
                trig.inside = ParticleSystemOverlapAction.Kill; trig.enter = ParticleSystemOverlapAction.Kill;
            }
            log.AppendLine("Rain particles under " + go.transform.parent.name);
        }

        // realtime reflection probe travelling with the car: puddles reflect the buildings and the railway
        ReflectionProbe probe = null;
        var carRoot = GameObject.Find("Hero_Car")?.transform;
        if (carRoot)
        {
            var oldProbe = carRoot.Find("Rain_ReflectionProbe"); if (oldProbe) UnityEngine.Object.DestroyImmediate(oldProbe.gameObject);
            probe = new GameObject("Rain_ReflectionProbe").AddComponent<ReflectionProbe>();
            probe.transform.SetParent(carRoot, false); probe.transform.localPosition = new Vector3(0, 2.5f, 18);
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.EveryFrame;
            probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.resolution = 256; probe.boxProjection = true; probe.size = new Vector3(34, 18, 90); probe.center = Vector3.zero;
            probe.importance = 2; probe.hdr = true;
            log.AppendLine("Realtime reflection probe added");
        }

        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        if (probe) probe.RenderProbe();
        log.AppendLine("Saved " + scene.path);

        // ---------- diagnostics: what is actually under the camera?
        if (road) log.AppendLine("Road bounds " + road.bounds + " shader " + road.sharedMaterial.shader.name);
        var probeCar = GameObject.Find("Hero_Car");
        Vector3 pc = probeCar ? probeCar.transform.position : Vector3.zero;
        foreach (var off in new[] { new Vector3(2, 0, 20), new Vector3(5, 0, 30), new Vector3(-3, 0, 15) })
        {
            var origin = pc + off + Vector3.up * 30;
            var hits = Physics.RaycastAll(origin, Vector3.down, 40);
            log.AppendLine("Ray at " + (pc + off) + ": " + string.Join(", ", hits.OrderBy(h => h.distance).Take(3).Select(h => h.collider.name + "@" + h.point.y.ToString("F2") + "[" + (h.collider.GetComponent<Renderer>() ? h.collider.GetComponent<Renderer>().sharedMaterial.name : "-") + "]")));
            foreach (var r in all.Where(r => r && r.bounds.Contains(new Vector3(origin.x, r.bounds.center.y, origin.z)) && r.bounds.max.y < 0.6f && r.bounds.max.y > -0.6f))
                log.AppendLine("   ground candidate " + r.name + " parent=" + (r.transform.parent ? r.transform.parent.name : "-") + " top=" + r.bounds.max.y.ToString("F3") + " enabled=" + r.enabled + "/" + r.gameObject.activeInHierarchy + " mats=" + string.Join(",", r.sharedMaterials.Select(m => m ? m.name + "(" + m.shader.name + ")" : "null")) + " type=" + r.GetType().Name);
        }

        // ---------- previews
        var car = GameObject.Find("Hero_Car");
        Vector3 c0 = car ? car.transform.position : Vector3.zero;
        if (cam) HeroSetup.Shot(cam, "rain_car_view.png");
        HeroSetup.DebugShot(c0 + new Vector3(0.5f, 1.5f, 6), c0 + new Vector3(1.5f, 0, 30), "rain_street.png", 60);
        HeroSetup.DebugShot(c0 + new Vector3(4.5f, .5f, 10), c0 + new Vector3(2, 0, 18), "rain_puddles.png", 60);
        HeroSetup.DebugShot(c0 + new Vector3(2f, 7f, 14), c0 + new Vector3(2, 0, 22), "rain_topdown.png", 60);
        var column = all.FirstOrDefault(r => r.name.StartsWith("Rail_Column_"));
        if (column)
        {
            var b = column.bounds;
            HeroSetup.DebugShot(b.center + new Vector3(4, -b.extents.y * .4f, -6), b.center + Vector3.up * b.extents.y * .3f, "rain_railway.png", 55);
            HeroSetup.DebugShot(b.center + new Vector3(1.2f, 0, -1.5f), b.center, "rain_rust_closeup.png", 45);
        }
    }
}

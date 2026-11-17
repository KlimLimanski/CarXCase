using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class ApartmentNoirLook
{
    const string Folder = "Assets/_Project/ArtDirection/ApartmentNoir/";
    const string ApartmentMaterials = "Assets/_Project/Arts/Apartaments/Materials/";
    const string ScenePath = "Assets/_Project/Scenes/01_FatherApartment.unity";
    const string RigName = "Apartment_Practical_Lighting";

    [MenuItem("Tools/Brighton/Apartment: refresh practical lighting and furniture")]
    public static void ApplyAndSave()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Open 01_FatherApartment in Edit Mode.");
        Apply(scene);
        BakeReflections(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Could not save the father apartment.");
    }

    internal static void Apply(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var apartment = roots.Single(g => g.name == "BrooklynApartment").transform;
        var nodes = roots.SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
        Transform Find(string name) => nodes.First(t => t.name == name);
        var rig = roots.FirstOrDefault(g => g.name == RigName);
        if (!rig) rig = new GameObject(RigName);
        var furniture = Part(rig.transform, "Furniture");
        var lighting = Part(rig.transform, "Practical_Lights");

        var walnut = Finish("Table_Walnut_Varnish", ApartmentMaterials + "OldWalnut.mat", .43f);
        var walnutLegs = Finish("Table_Walnut_Turned", ApartmentMaterials + "OldWalnut.mat", .30f);
        var brass = Finish("Floor_Lamp_Oxidized_Brass", ApartmentMaterials + "DullBrass.mat", .36f);
        var shade = Finish("Floor_Lamp_Warm_Linen", ApartmentMaterials + "Linen.mat", .22f);
        SetEmission(shade, new Color(.56f, .28f, .105f));
        shade.SetFloat("_Cull", (float)CullMode.Off);
        var globes = Finish("Ceiling_Opal_Diffuser", ApartmentMaterials + "LampGlass.mat", .21f);
        SetEmission(globes, new Color(1.07f, .76f, .43f));
        foreach (string name in new[] { "CeilingLight_Assembly", "Shell_BedLight_Globe_0", "Shell_BedLight_Globe_1", "VanityLight_Glass", "Bath_CeilingLightGlobe" })
        {
            var renderer = Find(name).GetComponent<Renderer>();
            renderer.sharedMaterials = renderer.sharedMaterials.Select(m => m && m.name == "LampGlass" || m == globes ? globes : m).ToArray();
        }
        foreach (var table in new[] { "CoffeeTable", "DiningTable" })
            BuildTable(furniture, apartment.Find("Furniture"), table, "", walnut, walnutLegs);
        BuildTable(furniture, Find("Table_Study"), "CoffeeTable", "(Clone)", walnut, walnutLegs);

        var floorLamp = Part(furniture, "Living_Room_Linen_Floor_Lamp");
        floorLamp.position = new Vector3(-.37f, .004f, -4.86f);
        Profile(floorLamp, "Weighted_Brass_Base", brass,
            P(0,0), P(.147f,0), P(.158f,.012f), P(.154f,.023f), P(.133f,.037f), P(.065f,.052f), P(.032f,.061f), P(0,.061f));
        Profile(floorLamp, "Stem_and_Lamp_Holder", brass,
            P(.030f,.048f), P(.027f,.08f), P(.015f,.12f), P(.015f,1.20f), P(.028f,1.226f), P(.028f,1.274f), P(.018f,1.296f));
        Profile(floorLamp, "Linen_Shade", shade, P(.206f,1.253f), P(.192f,1.284f), P(.143f,1.51f), P(.134f,1.525f));
        Profile(floorLamp, "Rolled_Brass_Shade_Binding", brass,
            P(.196f,1.248f), P(.21f,1.254f), P(.207f,1.265f), P(.2f,1.272f), P(.194f,1.26f));

        var note = Find("Secret_Note").GetComponent<MeshFilter>();
        // Leave the paper at the wall end of the bed, with its crease exposed beyond the bed rail.
        note.transform.position = new Vector3(-7.94f, .015f, -2.403f);
        var paper = new Mesh { name = "Creased_Note_Paper" };
        paper.vertices = new[] {
            new Vector3(-.48f,-.04f,-.47f),new Vector3(-.44f,.19f,.44f),new Vector3(.06f,.1f,.46f),
            new Vector3(.48f,-.09f,.27f),new Vector3(.41f,.26f,-.44f),new Vector3(-.03f,-.33f,-.49f),
            new Vector3(-.07f,.25f,.02f),new Vector3(.19f,-.28f,.13f) };
        paper.triangles = new[] {0,1,6,1,2,6,2,7,6,2,3,7,3,4,7,4,5,7,5,6,7,5,0,6};
        paper.uv = paper.vertices.Select(v => new Vector2(v.x + .5f, v.z + .5f)).ToArray();
        paper.RecalculateNormals();
        paper.RecalculateBounds();
        note.sharedMesh = SaveMesh(paper);
        foreach (var renderer in Find("INT_CoffeeMug").GetComponentsInChildren<MeshRenderer>())
            renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
        foreach (var renderer in Find("Porcelain_Bathroom_Basin").GetComponentsInChildren<MeshRenderer>())
            renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;

        var shader = Shader.Find("CarXCase/Interiors/Rain Pane");
        if (!shader) throw new InvalidOperationException("Import ApartmentRainPane.shader before applying the apartment look.");
        var glass = MaterialAsset("Rain_Lit_Sash_Glass", shader);
        glass.SetFloat("_RainIntensity", .43f);
        foreach (var window in new[] { apartment.Find("Architecture"), apartment.Find("Bedroom_Kitchen_Window") })
        {
            window.Find("Window_Glazing").GetComponent<Renderer>().sharedMaterial = glass;
            // Keep the head rail fixed. The visible slats are resized in the mesh so this is repeatable.
            var blind = window.Find("Blind_Assembly").GetComponent<MeshFilter>();
            var source = blind.sharedMesh;
            if (AssetDatabase.GetAssetPath(source).StartsWith(Folder, StringComparison.Ordinal)) continue;
            var exposed = UnityEngine.Object.Instantiate(source);
            exposed.name = "Raised_Venetian_Blind";
            var points = exposed.vertices;
            Vector3 up = blind.transform.InverseTransformDirection(Vector3.up);
            float top = points.Max(v => Vector3.Dot(v, up));
            for (int i = 0; i < points.Length; i++)
                points[i] += up * ((top - Vector3.Dot(points[i], up)) * .17f);
            exposed.SetVertices(points);
            exposed.RecalculateBounds();
            blind.sharedMesh = SaveMesh(exposed);
        }
        EditorUtility.SetDirty(glass);

        var warm = new Color(1f, .79f, .59f);
        var cool = new Color(.59f, .76f, 1f);
        TuneSpot(Find("Ceiling_Warm"), new Vector3(-2.185f, 2.68f, -4.025f), Vector3.down, warm, 1.25f, 4.3f, 128f, 88f, LightShadows.Soft);
        TuneSpot(Find("Ceiling_Warm.001"), new Vector3(-2.185f, 2.68f, -6.463f), Vector3.down, warm, .81f, 4.1f, 122f, 82f, LightShadows.None);
        TuneSpot(Find("Bedroom_Ceiling_Front"), new Vector3(-6.624f, 2.67f, -2.013f), Vector3.down, warm, 1.22f, 4.1f, 126f, 87f, LightShadows.Soft);
        TuneSpot(Find("Bedroom_Ceiling_Back"), new Vector3(-6.624f, 2.67f, -5.635f), Vector3.down, warm, .76f, 4.0f, 120f, 82f, LightShadows.None);
        TuneSpot(Find("Bath_CeilingBounce"), new Vector3(-1.127f, 2.74f, -1.070f), Vector3.down, warm, .77f, 3f, 118f, 80f, LightShadows.None);
        TuneSpot(Find("Window_Overcast"), new Vector3(-.155f, 1.93f, -5.58f), new Vector3(-1, -.27f, .03f),
            cool, 2.24f, 5.25f, 104f, 64f, LightShadows.Soft);
        var existingStreet = Find("Street_Sodium_Living").GetComponent<Light>();
        existingStreet.enabled = false;
        var bedroomStreet = Find("Street_Sodium_Bedroom").GetComponent<Light>();
        bedroomStreet.intensity = 13f;
        bedroomStreet.color = cool;
        var lampPool = Part(lighting, "Floor_Lamp_Table_Pool");
        TuneSpot(lampPool, floorLamp.position + new Vector3(0, 1.23f, 0), new Vector3(-1.42f, -.66f, .85f),
            new Color(1f, .73f, .48f), 2.3f, 3.62f, 104f, 68f, LightShadows.Soft);
        SetupPoint(Part(lighting, "Floor_Lamp_Wall_Bounce"), floorLamp.position + new Vector3(-.06f, 1.59f, 0),
            new Color(1f, .71f, .48f), .13f, 1.6f);
        SetupPoint(Find("Room_Bounce"), new Vector3(-3.06f, 1.64f, -1.49f), new Color(.88f,.83f,.77f), .44f, 3.15f);
        SetupPoint(Find("Kitchen_Bounce"), new Vector3(-2.07f, 1.79f, -5.94f), new Color(.84f,.87f,.91f), .38f, 3.65f);
        SetupPoint(Part(lighting, "Bedroom_Wall_Bounce"), new Vector3(-5.69f, 1.69f, -4.23f), new Color(.84f,.85f,.93f), .36f, 3.8f);
        SetupPoint(Find("Vanity_Warm"), new Vector3(-1.38f, 2.10f, -.27f), warm, .47f, 2.65f);
        var crt = Find("CRT_White_Noise_Light").GetComponent<Light>();
        crt.intensity = 2.60f;
        crt.range = 3.15f;

        var ambient = new Color(.172f, .166f, .159f);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = ambient;
        RenderSettings.reflectionIntensity = .83f;
        Find("InteriorReflectionProbe").GetComponent<ReflectionProbe>().enabled = false;
        ConfigureProbe(rig.transform, "Living_Room_Reflection", new Vector3(-2.2f, 1.41f, -3.83f), new Vector3(4.36f, 2.9f, 4.0f), 3, .86f);
        ConfigureProbe(rig.transform, "Bedroom_Reflection", new Vector3(-6.63f, 1.41f, -4.06f), new Vector3(4.30f, 2.9f, 7.4f), 4, .79f);
        ConfigureProbe(rig.transform, "Bathroom_Reflection", new Vector3(-1.29f, 1.52f, -1.19f), new Vector3(2.14f, 2.86f, 2.25f), 5, .92f);
        var kitchen = nodes.FirstOrDefault(t => t.name == "Kitchen_Box_Reflection");
        if (kitchen) kitchen.GetComponent<ReflectionProbe>().intensity = .86f;

        var grade = Part(rig.transform, "Apartment_Color_Grade").gameObject;
        var volume = grade.GetComponent<Volume>();
        if (!volume) volume = grade.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = -4;
        string profilePath = Folder + "Apartment_Practicals_Grade.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (!profile)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
            AssetDatabase.AddObjectToAsset(profile.Add<ColorAdjustments>(), profile);
            AssetDatabase.AddObjectToAsset(profile.Add<Tonemapping>(), profile);
            AssetDatabase.AddObjectToAsset(profile.Add<Bloom>(), profile);
        }
        profile.TryGet<ColorAdjustments>(out var adjustments);
        adjustments.postExposure.Override(.36f);
        adjustments.contrast.Override(3f);
        adjustments.saturation.Override(-9f);
        profile.TryGet<Tonemapping>(out var tone);
        tone.mode.Override(TonemappingMode.Neutral);
        profile.TryGet<Bloom>(out var bloom);
        bloom.threshold.Override(1.26f);
        bloom.intensity.Override(.087f);
        bloom.scatter.Override(.61f);
        EditorUtility.SetDirty(adjustments);
        EditorUtility.SetDirty(tone);
        EditorUtility.SetDirty(bloom);
        EditorUtility.SetDirty(profile);
        volume.sharedProfile = profile;
        var camera = Find("PlayerCamera").GetComponent<Camera>().GetUniversalAdditionalCameraData();
        camera.renderPostProcessing = true;
        camera.volumeLayerMask |= 1 << grade.layer;
        if (FatherApartmentScenario.IsInstalled(scene)) FatherApartmentScenario.ConfigureLighting(scene);
    }

    internal static void RefreshIfPresent(Scene scene)
    {
        if (scene.path == ScenePath && scene.GetRootGameObjects().Any(g => g.name == RigName))
            Apply(scene);
    }

    static Vector2 P(float radius, float height) => new Vector2(radius, height);

    static Transform Part(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing) return existing;
        var part = new GameObject(name).transform;
        part.SetParent(parent, false);
        return part;
    }

    static Material MaterialAsset(string name, Shader shader)
    {
        string path = Folder + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing) return existing;
        var material = new Material(shader) { name = name };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static Material Finish(string name, string sourcePath, float smoothness)
    {
        var source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
        if (!source) throw new InvalidOperationException("Missing apartment material " + sourcePath);
        var material = MaterialAsset(name, source.shader);
        material.CopyPropertiesFromMaterial(source);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_EnvironmentReflections", 1f);
        material.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        EditorUtility.SetDirty(material);
        return material;
    }

    static void SetEmission(Material material, Color color)
    {
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", color);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
    }

    static void BuildTable(Transform parent, Transform source, string prefix, string suffix, Material finish, Material legs)
    {
        var tabletop = source.Find(prefix + "_Top" + suffix).GetComponent<MeshRenderer>();
        var bounds = tabletop.bounds;
        var table = Part(parent, suffix.Length > 0 ? "Record_Player_Table" : prefix);
        table.position = bounds.center;
        bool longOnZ = bounds.size.z > bounds.size.x;
        table.rotation = Quaternion.Euler(0, longOnZ ? 90 : 0, 0);
        var size = longOnZ ? new Vector3(bounds.size.z, bounds.size.y, bounds.size.x) : bounds.size;
        var top = Part(table, "Rounded_Veneer_Top");
        var meshName = suffix.Length > 0 ? "Record_Table_Rolled_Edge" :
            prefix == "DiningTable" ? "Dining_Table_Rolled_Edge" : "Coffee_Table_Rolled_Edge";
        SetGeometry(top, SaveMesh(Box(meshName, size, .052f, .010f, 0)), finish);
        tabletop.enabled = false;
        var oldLegs = source.Cast<Transform>().Where(t => t.name.StartsWith(prefix + "_Leg", StringComparison.Ordinal)
            && t.name.EndsWith(suffix, StringComparison.Ordinal)).Select(t => t.GetComponent<MeshRenderer>()).ToArray();
        float legHeight = bounds.min.y - oldLegs.Min(r => r.bounds.min.y) + .01f;
        var leg = SaveMesh(Box(prefix + "_Tapered_Leg", new Vector3(.064f, legHeight, .064f), .007f, .006f, .59f));
        for (int i = 0; i < oldLegs.Length; i++)
        {
            var replacement = Part(table, "Tapered_Leg_" + i);
            var center = oldLegs[i].bounds.center;
            center.y = bounds.min.y + .01f - legHeight / 2f;
            replacement.position = center;
            SetGeometry(replacement, leg, legs);
            oldLegs[i].enabled = false;
        }
        foreach (var apron in source.Cast<Transform>().Where(t => t.name.StartsWith(prefix + "_Apron", StringComparison.Ordinal)))
            apron.GetComponent<MeshRenderer>().sharedMaterial = legs;
        var rail = SaveMesh(Box(prefix + "_End_Rail", new Vector3(.038f, .069f, size.z - .125f), .006f, .004f, 0));
        foreach (int side in new[] {-1, 1})
        {
            var end = Part(table, side < 0 ? "Lower_End_Rail_Left" : "Lower_End_Rail_Right");
            end.localPosition = new Vector3(side * (size.x / 2f - .097f), -size.y / 2f - legHeight * .53f, 0);
            SetGeometry(end, rail, legs);
        }
    }

    static Mesh Box(string name, Vector3 size, float corner, float bevel, float taper)
    {
        const int segments = 16;
        Vector3 half = size / 2f;
        var vertices = new Vector3[segments * 6];
        var uv = new Vector2[vertices.Length];
        var triangles = new System.Collections.Generic.List<int>();
        float[] heights = {-half.y, -half.y, -half.y + bevel, half.y - bevel, half.y, half.y};
        for (int ring = 0; ring < heights.Length; ring++)
        {
            float inset = ring is 0 or 1 or 4 or 5 ? bevel : 0;
            float ratio = Mathf.Lerp(taper > 0 ? taper : 1, 1, (heights[ring] + half.y) / size.y);
            for (int i = 0; i < segments; i++)
            {
                int sector = i / 4;
                float a = (sector + i % 4 / 3f) * Mathf.PI / 2f;
                float signX = sector is 0 or 3 ? 1 : -1;
                float signZ = sector is 0 or 1 ? 1 : -1;
                int index = ring * segments + i;
                float x = (signX * (half.x - corner) + (corner - inset * .5f) * Mathf.Cos(a) - signX * inset * .5f) * ratio;
                float z = (signZ * (half.z - corner) + (corner - inset * .5f) * Mathf.Sin(a) - signZ * inset * .5f) * ratio;
                vertices[index] = new Vector3(x, heights[ring], z);
                uv[index] = ring is 0 or 5 ? new Vector2(x / size.x + .5f, z / size.z + .5f) : new Vector2((float)i / segments, (heights[ring] + half.y) / size.y);
                if (ring is < 1 or > 3) continue;
                int next = ring * segments + (i + 1) % segments;
                triangles.AddRange(new[] { index, index + segments, next, next, index + segments, next + segments });
            }
        }
        for (int i = 1; i < segments - 1; i++)
            triangles.AddRange(new[] { 0, i, i+1, 5*segments, 5*segments+i+1, 5*segments+i });
        var mesh = new Mesh { name = name, vertices = vertices, uv = uv, triangles = triangles.ToArray() };
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void Profile(Transform root, string name, Material material, params Vector2[] outline)
    {
        const int sides = 40;
        var vertices = new Vector3[outline.Length * (sides + 1)];
        var uvs = new Vector2[vertices.Length];
        var triangles = new System.Collections.Generic.List<int>();
        for (int ring = 0; ring < outline.Length; ring++)
            for (int side = 0; side <= sides; side++)
            {
                float u = side / (float)sides;
                float angle = u * 2 * Mathf.PI;
                int index = ring * (sides + 1) + side;
                vertices[index] = new Vector3(outline[ring].x * Mathf.Cos(angle), outline[ring].y, outline[ring].x * Mathf.Sin(angle));
                uvs[index] = new Vector2(u, outline[ring].y);
                if (ring < outline.Length - 1 && side < sides)
                    triangles.AddRange(new[] {index,index+sides+1,index+1,index+1,index+sides+1,index+sides+2});
            }
        var mesh = new Mesh { name = "Floor_Lamp_" + name, vertices = vertices, uv = uvs, triangles = triangles.ToArray() };
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        SetGeometry(Part(root, name), SaveMesh(mesh), material);
    }

    static Mesh SaveMesh(Mesh mesh)
    {
        string path = Folder + mesh.name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (!existing)
        {
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
        EditorUtility.CopySerialized(mesh, existing);
        UnityEngine.Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    static void SetGeometry(Transform obj, Mesh mesh, Material material)
    {
        var filter = obj.GetComponent<MeshFilter>();
        if (!filter) filter = obj.gameObject.AddComponent<MeshFilter>();
        var renderer = obj.GetComponent<MeshRenderer>();
        if (!renderer) renderer = obj.gameObject.AddComponent<MeshRenderer>();
        filter.sharedMesh = mesh;
        renderer.sharedMaterial = material;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
    }

    static Light SetupPoint(Transform node, Vector3 position, Color color, float intensity, float range)
    {
        node.position = position;
        var light = node.GetComponent<Light>();
        if (!light) light = node.gameObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.enabled = true;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
        return light;
    }

    static void TuneSpot(Transform node, Vector3 position, Vector3 aim, Color color, float power,
        float range, float angle, float inner, LightShadows shadows)
    {
        var light = SetupPoint(node, position, color, power, range);
        node.rotation = Quaternion.LookRotation(aim, Mathf.Abs(Vector3.Dot(aim.normalized, Vector3.up)) > .99f ? Vector3.forward : Vector3.up);
        light.type = LightType.Spot;
        light.spotAngle = angle;
        light.innerSpotAngle = inner;
        light.shadows = shadows;
        light.shadowBias = .14f;
        light.shadowNormalBias = .27f;
    }

    static void ConfigureProbe(Transform rig, string name, Vector3 position, Vector3 size, int importance, float intensity)
    {
        var node = Part(rig, name);
        node.position = position;
        var probe = node.GetComponent<ReflectionProbe>();
        if (!probe) probe = node.gameObject.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Custom;
        probe.renderDynamicObjects = true;
        probe.boxProjection = true;
        probe.resolution = 128;
        probe.hdr = true;
        probe.size = size;
        probe.nearClipPlane = .055f;
        probe.farClipPlane = 14;
        probe.shadowDistance = 10;
        probe.blendDistance = .40f;
        probe.importance = importance;
        probe.intensity = intensity;
        probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(Folder + name + ".exr");
    }

    internal static void BakeReflections(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var rig = roots.Single(g => g.name == RigName);
        var hero = roots.Single(g => g.name == "Player").GetComponentsInChildren<Renderer>().Where(r => !r.forceRenderingOff).ToArray();
        var cameraFill = roots.Single(g => g.name == "Player").GetComponentsInChildren<Light>().Where(l => l.enabled).ToArray();
        try
        {
            foreach (var renderer in hero) renderer.forceRenderingOff = true;
            foreach (var light in cameraFill) light.enabled = false;
            var kitchen = roots.FirstOrDefault(g => g.name == "Kitchen_Lighting")?.transform.Find("Kitchen_Box_Reflection");
            var probes = rig.GetComponentsInChildren<ReflectionProbe>().ToList();
            if (kitchen) probes.Add(kitchen.GetComponent<ReflectionProbe>());
            foreach (var probe in probes)
            {
                var previous = probe.customBakedTexture;
                string path = probe.name == "Kitchen_Box_Reflection"
                    ? "Assets/_Project/ArtDirection/ApartmentKitchen/Kitchen_Interior.exr"
                    : Folder + probe.name + ".exr";
                try
                {
                    probe.customBakedTexture = null;
                    if (!Lightmapping.BakeReflectionProbe(probe, path))
                        throw new InvalidOperationException("Could not capture " + probe.name);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
                    if (!probe.customBakedTexture)
                        throw new InvalidOperationException("Could not import the reflection cubemap " + path);
                }
                catch
                {
                    probe.customBakedTexture = previous;
                    throw;
                }
            }
        }
        finally
        {
            foreach (var renderer in hero) renderer.forceRenderingOff = false;
            foreach (var light in cameraFill) light.enabled = true;
        }
    }
}

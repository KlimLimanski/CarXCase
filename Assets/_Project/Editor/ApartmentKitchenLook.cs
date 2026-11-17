using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class ApartmentKitchenLook
{
    const string ScenePath = "Assets/_Project/Scenes/01_FatherApartment.unity";
    const string Folder = "Assets/_Project/ArtDirection/ApartmentKitchen/";
    const string Sources = "Assets/_Project/Arts/Apartaments/Materials/";
    const string Fixtures = "Assets/_Project/Arts/Apartaments/Furniture/ApartmentFixtures/Materials/";
    const string ReflectionPath = Folder + "Kitchen_Interior.exr";

    [MenuItem("Tools/Brighton/Apartment: refresh kitchen lighting and reflections")]
    public static void ApplyAndSave()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath)
            throw new InvalidOperationException("Open 01_FatherApartment in Edit Mode.");

        var apartment = scene.GetRootGameObjects().Single(g => g.name == "BrooklynApartment").transform;
        var probe = Apply(apartment);
        ApartmentNoirLook.RefreshIfPresent(scene);
        var player = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Player");
        var visibleHero = player ? player.GetComponentsInChildren<Renderer>().Where(r => !r.forceRenderingOff).ToArray()
                                 : Array.Empty<Renderer>();
        var previousTexture = probe.customBakedTexture;
        try
        {
            foreach (var renderer in visibleHero) renderer.forceRenderingOff = true;
            probe.customBakedTexture = null;
            if (!Lightmapping.BakeReflectionProbe(probe, ReflectionPath))
                throw new InvalidOperationException("Unable to capture the kitchen reflection probe.");
        }
        finally
        {
            foreach (var renderer in visibleHero) renderer.forceRenderingOff = false;
            probe.customBakedTexture = previousTexture;
        }
        AssetDatabase.ImportAsset(ReflectionPath, ImportAssetOptions.ForceSynchronousImport);
        probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(ReflectionPath);
        if (!probe.customBakedTexture)
            throw new InvalidOperationException("The kitchen reflection must import as a cubemap.");

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    internal static ReflectionProbe Apply(Transform apartment)
    {
        var scene = apartment.gameObject.scene;
        var meshes = apartment.GetComponentsInChildren<MeshRenderer>(true);
        var tiles = Finish("Kitchen_Glazed_Wall_Tile", Sources + "KitchenPattern.mat", .99f);
        var floor = Finish("Kitchen_Worn_Floor_Tile", Sources + "KitchenPattern.mat", .64f);
        var tileMask = Map("Tile_Glaze_MetallicSmoothness");
        var tileNormal = Map("Tile_Glaze_Normal", normal: true);
        SetMask(tiles, tileMask);
        SetMask(floor, tileMask);
        SetNormal(tiles, tileNormal, .67f);
        SetNormal(floor, tileNormal, .52f);
        var counter = Finish("Kitchen_Aged_Laminate", Sources + "CounterLaminate.mat", .96f);
        SetMask(counter, Map("Laminate_MetallicSmoothness"));
        counter.SetFloat("_BumpScale", .63f);

        var brushed = Finish("Kitchen_Brushed_Sink_Steel", Fixtures + "Fixture_Satin_Steel.mat", .94f, .82f);
        brushed.color = new Color(.60f, .60f, .57f);
        SetNormal(brushed, Map("Sink_Steel_Normal", normal: true), .24f);
        SetMask(brushed, Map("Sink_Steel_MetallicSmoothness"));
        var chrome = Finish("Kitchen_Polished_Chrome", Fixtures + "Fixture_Chrome.mat", .84f, .88f);
        var enamel = Finish("Kitchen_Gloss_Enamel", Fixtures + "Fixture_Aged_Enamel.mat", .63f);
        var blindPaint = Finish("Kitchen_Painted_Blinds", Sources + "IvoryEnamel.mat", .34f);
        var ovenGlass = Finish("Kitchen_Smoked_Oven_Glass", Sources + "CRTGlass.mat", .82f, .19f);
        var wood = Finish("Kitchen_Satin_Walnut", Sources + "OldWalnut.mat", .43f);

        foreach (var mesh in meshes)
        {
            string name = mesh.name;
            string parent = mesh.transform.parent.name;
            if (name == "Kitchen_Backsplash") mesh.sharedMaterial = tiles;
            else if (name == "Floor_KitchenTile") mesh.sharedMaterial = floor;
            else if (name.StartsWith("Counter_", StringComparison.Ordinal)) mesh.sharedMaterial = counter;
            else if (name.StartsWith("KitchenUpper_", StringComparison.Ordinal) ||
                     name.StartsWith("KitchenBase_", StringComparison.Ordinal) || name == "DiningTable_Top")
                ReplaceSlot(mesh, "OldWalnut", wood);
            else if (name == "Blind_Assembly" || name == "Blinds_Headrail")
                ReplaceSlot(mesh, "IvoryEnamel", blindPaint);
            else if (name == "OvenDoor_Visual")
                ReplaceSlot(mesh, "CRTGlass", ovenGlass);

            if (parent == "Stamped_Kitchen_Sink")
            {
                if (name == "Basin_Rim_and_Bowl") mesh.sharedMaterial = brushed;
                ReplaceSlot(mesh, "Fixture_Chrome", chrome);
            }
            else if (parent == "Cream_Fridge_Case" || parent == "Cream_Fridge_Door" || parent == "Cream_Freezer_Door")
                ReplaceSlot(mesh, "Fixture_Aged_Enamel", enamel);
        }

        var rig = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Kitchen_Lighting");
        if (!rig)
        {
            rig = new GameObject("Kitchen_Lighting");
            SceneManager.MoveGameObjectToScene(rig, scene);
        }
        var oldSpot = UnityEngine.Object.FindObjectsByType<Light>().First(l => l.gameObject.scene == scene && l.name == "Kitchen_UnderCabinet");
        oldSpot.enabled = false;
        var window = UnityEngine.Object.FindObjectsByType<Light>().First(l => l.gameObject.scene == scene && l.name == "Window_Overcast");
        window.transform.position = new Vector3(-.38f, 1.96f, -5.67f);
        window.color = new Color(.61f, .76f, 1f);
        window.intensity = .79f;

        var housing = Finish("Kitchen_Lamp_Aged_Plastic", null, .37f);
        housing.color = new Color(.51f, .44f, .32f);
        var diffuser = Finish("Kitchen_Warm_Fluorescent_Diffuser", null, .46f);
        diffuser.color = new Color(.88f, .79f, .57f);
        diffuser.EnableKeyword("_EMISSION");
        diffuser.SetColor("_EmissionColor", new Color(2.75f, 1.93f, 1.19f));
        diffuser.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        PlaceLamp(rig.transform, "Right", -1.18f, housing, diffuser, .85f);
        PlaceLamp(rig.transform, "Left", -2.54f, housing, diffuser, .94f);
        PlaceTrim(rig.transform, "Sink_Front_Rail", new Vector3(-1.77f, .982f, -6.859f),
            new Vector3(.91f, .032f, .034f), wood);

        var gradeTransform = Part(rig.transform, "Kitchen_Grade");
        var volume = gradeTransform.GetComponent<Volume>();
        if (!volume) volume = gradeTransform.gameObject.AddComponent<Volume>();
        volume.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        volume.transform.position = new Vector3(-2.17f, 1.42f, -6.55f);
        var zone = volume.GetComponent<BoxCollider>();
        if (!zone) zone = volume.gameObject.AddComponent<BoxCollider>();
        zone.isTrigger = true;
        zone.excludeLayers = ~0;
        zone.size = new Vector3(4.30f, 2.84f, 2.03f);
        volume.isGlobal = false;
        volume.priority = 5;
        volume.blendDistance = 1.63f;
        volume.sharedProfile = Grade();
        // Clear the sample project effects when the camera is outside the kitchen.
        var baselineTransform = Part(rig.transform, "Apartment_PostFX_Baseline");
        var baseline = baselineTransform.GetComponent<Volume>();
        if (!baseline) baseline = baselineTransform.gameObject.AddComponent<Volume>();
        baseline.gameObject.layer = volume.gameObject.layer;
        baseline.isGlobal = true;
        baseline.priority = -10;
        baseline.sharedProfile = Baseline();
        var camera = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>()).First(c => c.CompareTag("MainCamera"));
        var cameraData = camera.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = true;
        cameraData.volumeLayerMask |= 1 << volume.gameObject.layer;

        var probeTransform = Part(rig.transform, "Kitchen_Box_Reflection");
        probeTransform.position = new Vector3(-2.08f, 1.41f, -6.44f);
        var probe = probeTransform.GetComponent<ReflectionProbe>();
        if (!probe) probe = probeTransform.gameObject.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Custom;
        probe.resolution = 256;
        probe.hdr = true;
        probe.boxProjection = true;
        probe.renderDynamicObjects = true;
        probe.nearClipPlane = .055f;
        probe.farClipPlane = 18f;
        probe.shadowDistance = 12f;
        probe.size = new Vector3(4.37f, 2.96f, 2.88f);
        probe.center = new Vector3(-.095f, .02f, .24f);
        probe.blendDistance = .54f;
        probe.importance = 5;
        probe.intensity = 1.04f;
        probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(ReflectionPath);
        return probe;
    }

    static void PlaceLamp(Transform parent, string side, float x, Material housing, Material diffuser, float intensity)
    {
        var lamp = Part(parent, "Under_Cabinet_" + side);
        lamp.position = new Vector3(x, 1.792f, RecessedLampDepth);
        PlaceTrim(lamp, "Housing", new Vector3(0f, .003f, 0f), new Vector3(.67f, .033f, .088f), housing);
        PlaceTrim(lamp, "Diffuser", new Vector3(0f, -.015f, .002f), new Vector3(.60f, .018f, .067f), diffuser);
        var source = Part(lamp, "Light");
        source.localPosition = new Vector3(0f, -.065f, .025f);
        source.rotation = Quaternion.LookRotation(new Vector3(0f, -1f, -.29f), Vector3.forward);
        var light = source.GetComponent<Light>();
        if (!light) light = source.gameObject.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = new Color(1f, .84f, .63f);
        light.intensity = intensity;
        light.range = 2.45f;
        light.innerSpotAngle = 86f;
        light.spotAngle = 134f;
        light.shadows = LightShadows.None;
    }

    const float RecessedLampDepth = -7.36f;

    public static void RecessExistingLamps(Transform rig)
    {
        foreach (string name in new[] { "Under_Cabinet_Right", "Under_Cabinet_Left" })
        {
            var fixture = rig.Find(name);
            var position = fixture.position;
            position.z = RecessedLampDepth;
            fixture.position = position;
        }
    }

    static void PlaceTrim(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    {
        var mesh = parent.Find(name);
        if (!mesh)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>());
            item.name = name;
            mesh = item.transform;
            mesh.SetParent(parent, false);
        }
        mesh.localPosition = position;
        mesh.localScale = size;
        var renderer = mesh.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    static Transform Part(Transform parent, string name)
    {
        var item = parent.Find(name);
        if (item) return item;
        item = new GameObject(name).transform;
        item.SetParent(parent, false);
        return item;
    }

    static VolumeProfile Grade()
    {
        var profile = LoadProfile("Kitchen_Grade");
        var bloom = Override<Bloom>(profile);
        bloom.threshold.Override(1.25f);
        bloom.intensity.Override(.16f);
        bloom.scatter.Override(.58f);
        bloom.clamp.Override(5.0f);
        bloom.maxIterations.Override(4);
        var grade = Override<ColorAdjustments>(profile);
        grade.postExposure.Override(.11f);
        grade.contrast.Override(5f);
        grade.saturation.Override(-5f);
        return profile;
    }

    static VolumeProfile Baseline()
    {
        var profile = LoadProfile("Apartment_PostFX_Baseline");
        Override<Tonemapping>(profile).mode.Override(TonemappingMode.None);
        Override<Bloom>(profile).intensity.Override(0f);
        Override<Vignette>(profile).intensity.Override(0f);
        return profile;
    }

    static VolumeProfile LoadProfile(string name)
    {
        string path = Folder + name + ".asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile) return profile;
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);
        return profile;
    }

    static T Override<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (!profile.TryGet<T>(out var value))
        {
            value = profile.Add<T>();
            AssetDatabase.AddObjectToAsset(value, profile);
        }
        EditorUtility.SetDirty(value);
        EditorUtility.SetDirty(profile);
        return value;
    }

    static Material Finish(string name, string sourcePath, float smoothness, float metal = 0f)
    {
        string path = Folder + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material)
        {
            var source = string.IsNullOrEmpty(sourcePath) ? null : AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
            material = source ? new Material(source) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", metal);
        material.SetFloat("_EnvironmentReflections", 1f);
        material.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        EditorUtility.SetDirty(material);
        return material;
    }

    static Texture2D Map(string name, bool normal = false)
    {
        string path = Folder + name + ".png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer) throw new FileNotFoundException("Missing kitchen surface map.", path);
        var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        if (importer.textureType != type || importer.sRGBTexture || importer.anisoLevel != 8)
        {
            importer.textureType = type;
            importer.sRGBTexture = false;
            importer.anisoLevel = 8;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void SetMask(Material material, Texture2D map)
    {
        material.SetTexture("_MetallicGlossMap", map);
        material.EnableKeyword("_METALLICSPECGLOSSMAP");
    }

    static void SetNormal(Material material, Texture2D map, float strength)
    {
        material.SetTexture("_BumpMap", map);
        material.SetFloat("_BumpScale", strength);
        material.EnableKeyword("_NORMALMAP");
    }

    static void ReplaceSlot(MeshRenderer mesh, string sourceName, Material replacement)
    {
        var slots = mesh.sharedMaterials;
        int index = Array.FindIndex(slots, material => material && material.name == sourceName);
        if (index < 0) return;
        slots[index] = replacement;
        mesh.sharedMaterials = slots;
    }
}

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ApartmentFurnishings
{
    const string ScenePath = "Assets/_Project/Scenes/01_FatherApartment.unity";
    const string AssetDir = "Assets/_Project/Arts/Apartaments/Furniture/ApartmentDetails/";
    const string FurnitureDir = "Assets/_Project/Arts/Apartaments/Furniture/";
    const string MaterialDir = AssetDir + "Materials/";
    const string WallpaperTextures = "Assets/_Project/Arts/Apartaments/Shell/Textures/";
    const string WallpaperMaterial = "Assets/_Project/Arts/Apartaments/Shell/Materials/Shell_Wallpaper.mat";

    [MenuItem("Tools/Brighton/Apartment: refresh furniture and rugs")]
    public static void ApplyAndSave()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath)
            throw new InvalidOperationException("Open 01_FatherApartment in Edit Mode first.");

        var apartment = scene.GetRootGameObjects().First(g => g.name == "BrooklynApartment").transform;
        Apply(apartment);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    internal static void Apply(Transform apartment)
    {
        var wallpaper = AssetDatabase.LoadAssetAtPath<Material>(WallpaperMaterial);
        var paperColor = ImportMap(WallpaperTextures + "Wallpaper_Textured_Albedo.png", false, false);
        var paperNormal = ImportMap(WallpaperTextures + "Wallpaper_Textured_Normal.png", true, false);
        if (!wallpaper || !paperColor || !paperNormal)
            throw new InvalidOperationException("The textured wallpaper maps and shell material must be imported first.");

        wallpaper.SetTexture("_BaseMap", paperColor);
        wallpaper.SetTexture("_BumpMap", paperNormal);
        wallpaper.SetFloat("_BumpScale", .68f);
        wallpaper.EnableKeyword("_NORMALMAP");
        EditorUtility.SetDirty(wallpaper);

        Directory.CreateDirectory(MaterialDir);
        AssetDatabase.Refresh();
        var previous = apartment.Find("Apartment_Furnishings");
        var group = new GameObject("Apartment_Furnishings");
        group.transform.SetParent(apartment, false);
        try
        {
            var cabinet = Place(group.transform, "Walnut_Cabinet", new Vector3(-7.80f, 0, -.26f), .94f,
                "Cabinet_Top", "Shallow_Drawer", Vector3.back);
            var cabinetBounds = BoundsOf(cabinet);
            var hitbox = cabinet.AddComponent<BoxCollider>();
            hitbox.center = cabinet.transform.InverseTransformPoint(new Vector3(cabinetBounds.center.x, cabinetBounds.min.y + .44f, cabinetBounds.center.z));
            hitbox.size = new Vector3(cabinetBounds.size.x, .88f, cabinetBounds.size.z) / Mathf.Abs(cabinet.transform.lossyScale.y);

            Place(group.transform, "Brass_Coat_Rack", new Vector3(-4.293f, 1.47f, -1.02f), .69f,
                "Peg_Rail_Backplate", "Curved_Coat_Hook", Vector3.right);
            AddRug(group.transform, "Rug_Bedroom_Indigo", new Vector3(-6.51f, .004f, -5.90f), 2.10f,
                new Color(.10f,.15f,.16f));
            AddRug(group.transform, "Rug_Bedside_Runner", new Vector3(-6.10f, .004f, -1.94f), .78f,
                new Color(.18f,.12f,.08f));
            InstallLivingRoomModels(apartment, group.transform);
        }
        catch
        {
            UnityEngine.Object.DestroyImmediate(group);
            throw;
        }

        if (previous) UnityEngine.Object.DestroyImmediate(previous.gameObject);
        ApartmentRoomRepairs.RefreshTextiles(apartment);
        var oldCarpet = GameObject.Find("Bedroom_Furniture")?.transform.Find("Carpet");
        if (oldCarpet) oldCarpet.gameObject.SetActive(false);
    }

    public static void InstallLivingRoomModels(Transform apartment, Transform group)
    {
        var originalSofa = apartment.Find("Furniture/Sofa_Assembly");
        var throwRenders = apartment.Find("Props/Sofa_DrapedThrow").GetComponentsInChildren<Renderer>();
        var proxy = apartment.GetComponentsInChildren<BoxCollider>().Single(c => c.name == "COL_Sofa");
        var sofaAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FurnitureDir + "Sofa_Dad.glb");
        var carpetAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FurnitureDir + "Carpet_Dad.glb");
        if (!sofaAsset || !carpetAsset)
            throw new InvalidOperationException("Import the living room sofa and carpet first.");

        var originalBounds = BoundsOf(originalSofa.gameObject);
        var sofa = PlaceLivingRoomModel(group, sofaAsset,
            new Vector3(originalBounds.center.x, apartment.position.y, originalBounds.center.z), -90f);
        var bounds = BoundsOf(sofa);
        var carpet = PlaceLivingRoomModel(group, carpetAsset, new Vector3(-1.51f, .004f, -3.87f), 0f);
        foreach (var renderer in carpet.GetComponentsInChildren<Renderer>())
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Keep the sofa's collision object and fit it to the replacement mesh.
        var localBounds = new Bounds(proxy.transform.InverseTransformPoint(bounds.center), Vector3.zero);
        for (int corner = 0; corner < 8; corner++)
        {
            var point = new Vector3((corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
            localBounds.Encapsulate(proxy.transform.InverseTransformPoint(point));
        }
        proxy.center = localBounds.center;
        proxy.size = localBounds.size;
        foreach (var renderer in originalSofa.GetComponentsInChildren<Renderer>().Concat(throwRenders))
            renderer.enabled = false;

        var originalRug = group.Find("Rug_Dining_Wine");
        if (originalRug) UnityEngine.Object.DestroyImmediate(originalRug.gameObject);
    }

    static GameObject PlaceLivingRoomModel(Transform parent, GameObject asset, Vector3 floorCenter, float yaw)
    {
        var existing = parent.Find(asset.name);
        var instance = existing ? existing.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        var t = instance.transform;
        t.localScale = asset.transform.localScale / parent.lossyScale.x;
        t.SetPositionAndRotation(floorCenter, Quaternion.Euler(0f, yaw, 0f));
        var bounds = BoundsOf(instance);
        t.position += floorCenter - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        return instance;
    }

    static GameObject Place(Transform parent, string name, Vector3 floorCenter, float width,
        string widthPart, string frontPart = null, Vector3 facing = default)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDir + name + ".fbx");
        if (!asset) throw new InvalidOperationException($"Missing imported furniture asset {name}");
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent.gameObject.scene);
        instance.transform.SetParent(parent, false);
        var renderers = instance.GetComponentsInChildren<MeshRenderer>();
        var reference = renderers.First(r => r.name.StartsWith(widthPart, StringComparison.Ordinal));
        instance.transform.localScale *= width / reference.bounds.size.x;
        if (frontPart != null)
        {
            var front = renderers.Where(r => r.name.StartsWith(frontPart, StringComparison.Ordinal)).ToArray();
            var center = front.Aggregate(Vector3.zero, (sum, r) => sum + r.bounds.center) / front.Length;
            var direction = Vector3.ProjectOnPlane(center - reference.bounds.center, Vector3.up);
            instance.transform.Rotate(Vector3.up, Vector3.SignedAngle(direction, facing, Vector3.up), Space.World);
        }
        var bounds = BoundsOf(instance);
        instance.transform.position += floorCenter - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        foreach (var renderer in renderers)
            renderer.sharedMaterials = renderer.sharedMaterials.Select(m => Remap(m, name)).ToArray();
        return instance;
    }

    static void AddRug(Transform parent, string name, Vector3 position, float width, Color bindingColor)
    {
        var pile = Material(name, Color.white, .12f);
        var albedo = ImportMap(AssetDir + name + "_Albedo.png", false, true);
        var normal = ImportMap(AssetDir + name + "_Normal.png", true, true);
        if (!albedo || !normal) throw new InvalidOperationException($"Missing woven texture maps for {name}");
        pile.SetTexture("_BaseMap", albedo);
        pile.SetTexture("_BumpMap", normal);
        pile.SetFloat("_BumpScale", .75f);
        pile.EnableKeyword("_NORMALMAP");
        Material(name + "_Binding", bindingColor, .09f);
        var fringe = Material(name + "_Fringe", new Color(.47f,.39f,.28f), .08f);
        fringe.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        Place(parent, name, position, width, name + "_Pile");
    }

    static Material Remap(Material original, string assetName)
    {
        if (!original) throw new InvalidOperationException($"Unassigned material in {assetName}");
        switch (original.name)
        {
            case "Details_Walnut": return AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Arts/Apartaments/Materials/OldWalnut.mat");
            case "Details_Brass": return Material(original.name, new Color(.31f,.20f,.087f), .60f, .62f);
            case "Details_Shadow": return Material(original.name, new Color(.038f,.026f,.018f), .18f);
            case "Details_BookGreen": return Material(original.name, new Color(.10f,.20f,.16f), .15f);
            case "Details_BookWine": return Material(original.name, new Color(.24f,.085f,.072f), .16f);
            case "Details_PageEdges": return Material(original.name, new Color(.50f,.43f,.31f), .05f);
            case "Details_Ceramic": return Material(original.name, new Color(.25f,.37f,.39f), .68f);
            default:
                var fabric = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + original.name + ".mat");
                if (!fabric) throw new InvalidOperationException($"Unexpected material {original.name} in {assetName}");
                return fabric;
        }
    }

    static Bounds BoundsOf(GameObject model)
    {
        var renderers = model.GetComponentsInChildren<Renderer>();
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    static Material Material(string name, Color color, float smoothness, float metallic = 0)
    {
        var path = MaterialDir + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(material);
        return material;
    }

    static Texture2D ImportMap(string path, bool normal, bool clamp)
    {
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return null;
        var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        var wrap = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
        if (importer.textureType != type || importer.wrapMode != wrap || importer.anisoLevel != 8 || importer.npotScale != TextureImporterNPOTScale.None)
        {
            importer.textureType = type;
            importer.wrapMode = wrap;
            importer.anisoLevel = 8;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}

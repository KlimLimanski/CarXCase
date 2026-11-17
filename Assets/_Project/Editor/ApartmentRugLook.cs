using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ApartmentRugLook
{
    const string Folder = "Assets/_Project/ArtDirection/ApartmentRugs/";
    const string Finish = "Faded_Medallion";

    public static void Apply(Transform apartment)
    {
        var furnishings = apartment.Find("Apartment_Furnishings");
        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + Finish + "_Albedo.png");
        var fabric = Fabric(Finish, albedo, Color.white, .13f, 1.12f);
        RestoreLivingRug(furnishings.Find("Carpet_Dad"));

        foreach (string rugName in new[] { "Rug_Bedroom_Indigo", "Rug_Bedside_Runner" })
        {
            var pile = furnishings.Find(rugName).GetComponentsInChildren<MeshRenderer>()
                .Single(x => x.name.EndsWith("_Pile"));
            ApplyMaterial(pile, fabric);
        }

        var bath = apartment.GetComponentsInChildren<MeshRenderer>().Single(x => x.name=="BathMat");
        FitBathMatUV(bath.GetComponent<MeshFilter>());
        ApplyMaterial(bath, fabric);
    }

    static void RestoreLivingRug(Transform rug)
    {
        foreach (var renderer in rug.GetComponentsInChildren<MeshRenderer>())
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(renderer);
            if (!source) throw new InvalidOperationException("The living room rug has lost its model reference.");
            renderer.sharedMaterials = source.sharedMaterials;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
    }

    static void FitBathMatUV(MeshFilter filter)
    {
        // The old linen used repeating, centered UVs. A bordered rug needs the full 0–1 area.
        var path = Folder + "BathMat_Bordered_UV.asset";
        var fitted = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (!fitted)
        {
            fitted = UnityEngine.Object.Instantiate(filter.sharedMesh);
            fitted.name = "Bath Mat Bordered UV";
            var uv = fitted.uv;
            var min = new Vector2(uv.Min(p=>p.x), uv.Min(p=>p.y));
            var size = new Vector2(uv.Max(p=>p.x), uv.Max(p=>p.y)) - min;
            for (int i=0;i<uv.Length;i++)
                uv[i] = new Vector2((uv[i].x-min.x)/size.x,(uv[i].y-min.y)/size.y);
            fitted.uv = uv;
            fitted.RecalculateTangents();
            AssetDatabase.CreateAsset(fitted,path);
        }
        filter.sharedMesh = fitted;
        PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
    }

    static void ApplyMaterial(MeshRenderer renderer, Material fabric)
    {
        renderer.sharedMaterial = fabric;
        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
    }

    static Material Fabric(string name, Texture albedo, Color color, float smoothness, float strength)
    {
        var normalPath = Folder + name + "_Normal.png";
        var import = (TextureImporter)AssetImporter.GetAtPath(normalPath);
        if (!import || !albedo) throw new InvalidOperationException("Missing rug textures for " + name);
        if (import.textureType != TextureImporterType.NormalMap || import.anisoLevel != 8)
        {
            import.textureType = TextureImporterType.NormalMap;
            import.convertToNormalmap = false;
            import.anisoLevel = 8;
            import.SaveAndReimport();
        }
        var path = Folder + name + ".mat";
        var fabric = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!fabric)
        {
            fabric = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(fabric, path);
        }
        fabric.SetTexture("_BaseMap", albedo);
        fabric.SetColor("_BaseColor", color);
        fabric.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
        fabric.SetFloat("_BumpScale", strength);
        fabric.SetFloat("_Metallic", 0f);
        fabric.SetFloat("_Smoothness", smoothness);
        fabric.EnableKeyword("_NORMALMAP");
        EditorUtility.SetDirty(fabric);
        return fabric;
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CarXCase.Precinct;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// Transfers the supplied Blender interior into the existing story scene.
public static class LombardLocationImport
{
    const string Root = "Assets/_Project/Arts/Lombard/Unity/";
    [Serializable] class Manifest { public Mat[] materials; public Lamp[] lights; }
    [Serializable] class Maps { public string baseColor, normal, roughness, metallic; }
    [Serializable] class Mat
    {
        public string name, file;
        public float[] color, emission;
        public float metallic, roughness, emissionStrength, alpha, transmission;
        public Maps textures;
    }
    [Serializable] class Lamp
    {
        public string name, type;
        public float energy;
        public float[] color, position, rotation;
    }

    static Color ColorOf(float[] v) => new Color(v[0], v[1], v[2], v.Length > 3 ? v[3] : 1);

    static Texture2D Texture(string relative, bool normal = false)
    {
        if (string.IsNullOrEmpty(relative)) return null;
        var path = Root + relative;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer) return null;
        if (normal && importer.textureType != TextureImporterType.NormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    [MenuItem("Tools/Locations/Install supplied Lombard interior")]
    public static void Install()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        AssetDatabase.Refresh();
        var data = JsonUtility.FromJson<Manifest>(File.ReadAllText(Root + "ImportManifest.json"));
        Directory.CreateDirectory(Root + "Materials");
        var materials = new Dictionary<string, Material>();
        foreach (var item in data.materials)
        {
            var path = Root + "Materials/" + item.file + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            var colorTexture = Texture(item.textures.baseColor);
            mat.SetColor("_BaseColor", colorTexture ? Color.white : ColorOf(item.color).gamma);
            mat.SetTexture("_BaseMap", colorTexture);
            mat.SetFloat("_Metallic", item.metallic);
            mat.SetFloat("_Smoothness", 1 - item.roughness);
            var normal = Texture(item.textures.normal, true);
            if (normal) { mat.SetTexture("_BumpMap", normal); mat.EnableKeyword("_NORMALMAP"); }
            if (item.emissionStrength > 0)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", ColorOf(item.emission) * Mathf.Min(item.emissionStrength, 3));
            }
            if (item.transmission > .1f || item.alpha < .95f || item.name == "GlassPane")
            {
                var color = mat.GetColor("_BaseColor"); color.a = .16f; mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Surface", 1); mat.SetFloat("_SrcBlend", (int)BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); mat.SetFloat("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); mat.renderQueue = 3000;
                mat.SetOverrideTag("RenderType", "Transparent");
            }
            EditorUtility.SetDirty(mat);
            materials[item.name] = mat;
        }
        var importer = (ModelImporter)AssetImporter.GetAtPath(Root + "Models/Lombard.fbx");
        foreach (var item in materials)
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), item.Key), item.Value);
        importer.SaveAndReimport();

        const string scenePath = "Assets/_Project/Scenes/04_Pawnshop.unity";
        var backup = "Review/LocationImport_20261117/04_Pawnshop_Greybox.unity";
        if (!File.Exists(backup)) File.Copy(scenePath, backup);
        var scene = EditorSceneManager.OpenScene(scenePath);
        if (GameObject.Find("Lombard_Imported")) throw new InvalidOperationException("Interior is already installed.");
        var story = GameObject.Find("04_Pawnshop_Greybox");
        var cut = UnityEngine.Object.FindAnyObjectByType<ScriptedCutscene>();
        var talk = UnityEngine.Object.FindAnyObjectByType<Interrogation>();
        var npc = talk.suspect;
        Undo.RecordObject(story, "Install Lombard");
        npc.transform.SetParent(story.transform, true);
        foreach (var name in new[] { "Architecture", "Props", "Lighting" })
        {
            var old = story.transform.Find(name);
            if (old) Undo.DestroyObjectImmediate(old.gameObject);
        }
        story.name = "Lombard_Story";
        var environment = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Models/Lombard.fbx"));
        Undo.RegisterCreatedObjectUndo(environment, "Import Lombard"); environment.name = "Lombard_Imported";
        foreach (var renderer in environment.GetComponentsInChildren<MeshRenderer>())
        {
            // Large architectural and furniture meshes block movement and the orbit camera.
            if (renderer.bounds.size.y > .35f || renderer.name.StartsWith("Floor") || renderer.name.StartsWith("Ceiling"))
            {
                var mesh = renderer.GetComponent<MeshFilter>();
                if (mesh && mesh.sharedMesh) renderer.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh.sharedMesh;
            }
        }
        var lights = new GameObject("Lombard_Lighting"); Undo.RegisterCreatedObjectUndo(lights, "Import lights");
        foreach (var item in data.lights)
        {
            var go = new GameObject(item.name); go.transform.SetParent(lights.transform);
            go.transform.position = new Vector3(item.position[0], item.position[2], item.position[1]);
            var light = go.AddComponent<Light>(); light.type = LightType.Point;
            light.color = ColorOf(item.color); light.intensity = item.type == "AREA" ? 3.2f : item.energy / 14;
            light.range = item.type == "AREA" ? 6 : 4.5f;
            light.shadows = item.type == "AREA" ? LightShadows.None : LightShadows.Soft;
            light.shadowBias = .03f; light.shadowNormalBias = .15f;
        }
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.12f, .13f, .16f);
        RenderSettings.ambientEquatorColor = new Color(.085f, .075f, .06f);
        RenderSettings.ambientGroundColor = new Color(.04f, .035f, .03f);
        RenderSettings.skybox = null;
        RenderSettings.fog = false;
        npc.transform.SetPositionAndRotation(new Vector3(2.8f, 0, 6.65f), Quaternion.Euler(0, 180, 0));
        var end = new Vector3(2.8f, 0, 4.5f);
        cut.door = null;
        cut.path[0].SetPositionAndRotation(new Vector3(0, 0, -6.5f), Quaternion.identity);
        cut.path[1].SetPositionAndRotation(end, Quaternion.identity);
        var heroLook = story.transform.Find("Look_HeroEnd"); heroLook.position = end + Vector3.up * 1.55f;
        var npcLook = story.transform.Find("Look_Sema"); npcLook.position = npc.transform.position + Vector3.up * 1.6f;
        var wide = story.transform.Find("Shot_SemaPOV"); wide.position = new Vector3(0, 2, -6.9f); wide.LookAt(new Vector3(1, 1.5f, 5.6f));
        var reverse = story.transform.Find("Shot_OverSema"); reverse.position = new Vector3(3.65f, 1.7f, 6.4f); reverse.LookAt(heroLook);
        cut.shots = new[]
        {
            new ScriptedCutscene.Shot { start = 0, anchor = wide, lookAt = npcLook, fov = 57 },
            new ScriptedCutscene.Shot { start = cut.walkStart + 1.4f, followOffset = new Vector3(.4f, 1.7f, -1.6f), fov = 55 },
            new ScriptedCutscene.Shot { start = cut.Duration - 2.8f, anchor = reverse, lookAt = heroLook, fov = 40 }
        };
        Place(talk.camOnSuspect, new Vector3(2.1f, 1.65f, 4.2f), npcLook.position);
        Place(talk.camOnHero, new Vector3(3.65f, 1.7f, 6.4f), heroLook.position);
        Place(talk.camWide, new Vector3(.8f, 1.9f, 5.4f), new Vector3(2.8f, 1.3f, 5.6f));
        var additional = cut.shotCamera.GetUniversalAdditionalCameraData(); additional.renderPostProcessing = true;
        cut.shotCamera.nearClipPlane = .04f; cut.shotCamera.farClipPlane = 60;
        cut.Evaluate(0);
        var probeGo = new GameObject("Lombard_Reflection"); probeGo.transform.position = new Vector3(0, 1.8f, 0);
        var probe = probeGo.AddComponent<ReflectionProbe>(); probe.size = new Vector3(10, 4, 16);
        probe.mode = ReflectionProbeMode.Realtime; probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        probe.resolution = 128; probe.boxProjection = true;
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("Installed Lombard interior: " + environment.GetComponentsInChildren<MeshRenderer>().Length + " renderers, " + materials.Count + " materials, story preserved.");
    }

    static void Place(Transform target, Vector3 position, Vector3 look)
    { target.position = position; target.LookAt(look); }
}

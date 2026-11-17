using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CarXCase.Intro;

[InitializeOnLoad]
public static class BrightonStreetRepair
{
    static BrightonStreetRepair() { EditorApplication.update += Poll; }

    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode ||
            !File.Exists("Temp/BrightonStreetRepair.request")) return;
        File.Delete("Temp/BrightonStreetRepair.request");
        try { Repair(); }
        catch (Exception e)
        {
            File.WriteAllText("Logs/BrightonStreetRepair.txt", e.ToString());
            Debug.LogException(e);
        }
    }

    [MenuItem("Tools/Greybox/Widen Road + Add Oncoming Traffic")]
    public static void Repair()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.name != "00_BrightonDrive")
        {
            EditorSceneManager.OpenScene("Assets/Scenes/00_BrightonDrive.unity");
            scene = SceneManager.GetActiveScene();
        }

        var street = GameObject.Find("Brighton_Street");
        if (street == null) throw new Exception("Brighton_Street not found in scene.");

        var streetT = street.transform;
        var road = streetT.Find("Road");
        if (road == null) throw new Exception("Road object not found.");

        float currentWidth = road.localScale.x;
        float targetWidth = 16f;
        float delta = Mathf.Max(0, (targetWidth - currentWidth) / 2f);

        if (delta > 0.01f)
        {
            road.localScale = new Vector3(targetWidth, road.localScale.y, road.localScale.z);

            List<Transform> children = new List<Transform>();
            foreach (Transform child in streetT) children.Add(child);

            foreach (var item in children)
            {
                string n = item.name;
                float side = Mathf.Sign(item.localPosition.x);

                if (n.StartsWith("Building_"))
                {
                    var facade = item.Find("Facade");
                    if (facade != null) side = Mathf.Sign(facade.position.x);
                    item.position += Vector3.right * side * delta;
                }
                else if (n.StartsWith("Sidewalk_"))
                {
                    item.position += Vector3.right * side * delta;
                    var sc = item.localScale;
                    item.localScale = new Vector3(Mathf.Max(sc.x, 4.5f), sc.y, sc.z);
                }
                else if (n.StartsWith("Curb_") || n.StartsWith("Rail_Column_") ||
                         n.StartsWith("ParkedCar_") || n.StartsWith("Father_House_") ||
                         n == "HouseNumber")
                {
                    item.position += Vector3.right * side * delta;
                }
                else if (n.StartsWith("Rail_Crossbeam_"))
                {
                    item.localScale += Vector3.right * delta * 2f;
                }
            }
        }

        var shader = Shader.Find("CarXCase/World Text Depth");
        if (shader != null && shader.isSupported)
        {
            foreach (var label in UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
            {
                if (label.gameObject.scene != scene) continue;
                var renderer = label.GetComponent<Renderer>();
                string fontName = label.font != null ? label.font.name : "default";
                string matPath = "Assets/Materials/BrightonIntro/TextDepth_" + fontName + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (material == null)
                {
                    material = new Material(shader);
                    AssetDatabase.CreateAsset(material, matPath);
                }
                material.shader = shader;
                material.mainTexture = label.font != null
                    ? label.font.material.mainTexture
                    : renderer.sharedMaterial.mainTexture;
                material.renderQueue = 2450;
                renderer.sharedMaterial = material;
                EditorUtility.SetDirty(material);
            }
        }

        var existing = GameObject.Find("Oncoming_Traffic");
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing);

        var trafficRoot = new GameObject("Oncoming_Traffic").transform;

        var blue = LoadOrCreateMat("CarPaint", new Color(.20f, .29f, .35f));
        var red = LoadOrCreateMat("CarPaint_Red", new Color(.45f, .12f, .10f));
        var yellow = LoadOrCreateMat("CarPaint_Yellow", new Color(.72f, .62f, .18f));
        var white = LoadOrCreateMat("CarPaint_White", new Color(.78f, .76f, .72f));
        var green = LoadOrCreateMat("CarPaint_Green", new Color(.15f, .32f, .18f));
        var glass = LoadOrCreateMat("Window_Oncoming", new Color(.20f, .31f, .36f));
        var trim = LoadOrCreateMat("Trim_Oncoming", new Color(.07f, .08f, .09f));

        var vehicles = new List<BrightonDriveCutscene.OncomingVehicle>();

        float oncomingX = -2.2f;

        var sedan1 = BuildSedan(trafficRoot, "Oncoming_Sedan_1", blue, glass, trim, oncomingX, 120f);
        vehicles.Add(new BrightonDriveCutscene.OncomingVehicle
        {
            root = sedan1, spawnTime = 5f, speed = 12f, startZ = 120f, endZ = -60f
        });

        var van1 = BuildVan(trafficRoot, "Oncoming_Van_1", white, glass, trim, oncomingX, 280f);
        vehicles.Add(new BrightonDriveCutscene.OncomingVehicle
        {
            root = van1, spawnTime = 18f, speed = 10f, startZ = 280f, endZ = -60f
        });

        var taxi1 = BuildTaxi(trafficRoot, "Oncoming_Taxi_1", yellow, glass, trim, oncomingX, 380f);
        vehicles.Add(new BrightonDriveCutscene.OncomingVehicle
        {
            root = taxi1, spawnTime = 32f, speed = 14f, startZ = 380f, endZ = -60f
        });

        var sedan2 = BuildSedan(trafficRoot, "Oncoming_Sedan_2", red, glass, trim, oncomingX - 0.3f, 440f);
        vehicles.Add(new BrightonDriveCutscene.OncomingVehicle
        {
            root = sedan2, spawnTime = 45f, speed = 11f, startZ = 440f, endZ = -60f
        });

        var van2 = BuildVan(trafficRoot, "Oncoming_Van_2", green, glass, trim, oncomingX, 520f);
        vehicles.Add(new BrightonDriveCutscene.OncomingVehicle
        {
            root = van2, spawnTime = 60f, speed = 9f, startZ = 520f, endZ = -60f
        });

        var sedan3 = BuildSedan(trafficRoot, "Oncoming_Sedan_3", white, glass, trim, oncomingX + 0.2f, 560f);
        vehicles.Add(new BrightonDriveCutscene.OncomingVehicle
        {
            root = sedan3, spawnTime = 78f, speed = 13f, startZ = 560f, endZ = -60f
        });

        var taxi2 = BuildTaxi(trafficRoot, "Oncoming_Taxi_2", yellow, glass, trim, oncomingX, 580f);
        vehicles.Add(new BrightonDriveCutscene.OncomingVehicle
        {
            root = taxi2, spawnTime = 95f, speed = 10f, startZ = 580f, endZ = -60f
        });

        var director = UnityEngine.Object.FindAnyObjectByType<BrightonDriveCutscene>();
        if (director != null)
        {
            director.oncomingVehicles = vehicles;
            EditorUtility.SetDirty(director);
        }

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/00_BrightonDrive.unity");
        AssetDatabase.SaveAssets();

        string log = "Road widened to " + targetWidth + " (delta=" + delta + ")\n";
        log += "Oncoming vehicles: " + vehicles.Count + "\n";
        log += "Text labels fixed: shader applied\n";
        File.WriteAllText("Logs/BrightonStreetRepair.txt", log);
        Debug.Log("[Brighton] Road widened, text fixed, " + vehicles.Count + " oncoming cars added. Press Play.");
    }

    static Transform BuildSedan(Transform parent, string name, Material body, Material glass, Material trim, float x, float z)
    {
        var root = new GameObject(name).transform;
        root.SetParent(parent, false);
        root.position = new Vector3(x, 0, z);
        root.rotation = Quaternion.Euler(0, 180, 0);

        MakeBox(root, "Body", new Vector3(0, .55f, 0), new Vector3(1.7f, .65f, 4.2f), body);
        MakeBox(root, "Cabin", new Vector3(0, 1.15f, -.15f), new Vector3(1.55f, .6f, 2.0f), glass);
        MakeBox(root, "Bumper_F", new Vector3(0, .35f, 2.2f), new Vector3(1.6f, .2f, .15f), trim);
        MakeBox(root, "Bumper_R", new Vector3(0, .35f, -2.2f), new Vector3(1.6f, .2f, .15f), trim);

        root.gameObject.SetActive(false);
        return root;
    }

    static Transform BuildVan(Transform parent, string name, Material body, Material glass, Material trim, float x, float z)
    {
        var root = new GameObject(name).transform;
        root.SetParent(parent, false);
        root.position = new Vector3(x, 0, z);
        root.rotation = Quaternion.Euler(0, 180, 0);

        MakeBox(root, "Body", new Vector3(0, .75f, 0), new Vector3(1.8f, 1.1f, 4.8f), body);
        MakeBox(root, "Cabin", new Vector3(0, 1.7f, 1.2f), new Vector3(1.65f, .8f, 1.6f), glass);
        MakeBox(root, "Cargo", new Vector3(0, 1.7f, -.6f), new Vector3(1.75f, .8f, 2.8f), body);
        MakeBox(root, "Bumper_F", new Vector3(0, .35f, 2.5f), new Vector3(1.7f, .22f, .15f), trim);

        root.gameObject.SetActive(false);
        return root;
    }

    static Transform BuildTaxi(Transform parent, string name, Material body, Material glass, Material trim, float x, float z)
    {
        var root = new GameObject(name).transform;
        root.SetParent(parent, false);
        root.position = new Vector3(x, 0, z);
        root.rotation = Quaternion.Euler(0, 180, 0);

        MakeBox(root, "Body", new Vector3(0, .55f, 0), new Vector3(1.7f, .65f, 4.4f), body);
        MakeBox(root, "Cabin", new Vector3(0, 1.15f, -.1f), new Vector3(1.55f, .6f, 2.1f), glass);
        MakeBox(root, "TaxiSign", new Vector3(0, 1.52f, .15f), new Vector3(.4f, .15f, .2f), body);
        MakeBox(root, "Bumper_F", new Vector3(0, .35f, 2.3f), new Vector3(1.6f, .2f, .15f), trim);
        MakeBox(root, "Bumper_R", new Vector3(0, .35f, -2.3f), new Vector3(1.6f, .2f, .15f), trim);

        root.gameObject.SetActive(false);
        return root;
    }

    static void MakeBox(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
    }

    static Material LoadOrCreateMat(string name, Color color)
    {
        string path = "Assets/Materials/BrightonIntro/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = color;
        mat.SetFloat("_Smoothness", .15f);
        EditorUtility.SetDirty(mat);
        return mat;
    }
}

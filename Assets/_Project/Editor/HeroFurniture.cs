using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DetectiveGame.Core;

/// Furnishes the bedroom half of the apartment side wing (Apartment_AdditionalRooms)
/// with the new models from Assets/_Project/Arts/Apartaments/Furniture.
public static class HeroFurniture
{
    const string Dir = "Assets/_Project/Arts/Apartaments/Furniture/";
    const string FlatScene = "Assets/_Project/Scenes/01_FatherApartment.unity";

    public static void Run(string cmd, StringBuilder log)
    {
        var scene = EditorSceneManager.OpenScene(FlatScene);
        EditorSceneManager.SaveScene(scene, "Assets/_Project/Scenes/BeforeFurniture_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity", true);
        var wing = GameObject.Find("Apartment_AdditionalRooms")?.transform ?? throw new Exception("Apartment_AdditionalRooms missing");
        var fb = wing.Find("Floor_Extension").GetComponent<Renderer>().bounds;
        var db = wing.Find("Wall_StudyDivider").GetComponent<Renderer>().bounds;
        float floorY = fb.max.y;
        float xMin = fb.min.x + 0.075f;            // inner face of the outer west wall
        float xMax = fb.max.x - 0.075f - 0.09f;    // inner face of the wall with the doorway
        float zMax = fb.max.z;                     // inner face of the front wall
        float zDiv = db.max.z;                     // bedroom side of the divider
        log.AppendLine($"Bedroom area x[{xMin:F2},{xMax:F2}] z[{zDiv:F2},{zMax:F2}] floor {floorY:F2}");

        var oldWardrobe = wing.Find("Wardrobe_Storage");
        if (oldWardrobe) { oldWardrobe.gameObject.SetActive(false); log.AppendLine("Wardrobe_Storage copy disabled"); }
        var prev = wing.Find("Bedroom_Furniture"); if (prev) UnityEngine.Object.DestroyImmediate(prev.gameObject);
        var room = new GameObject("Bedroom_Furniture").transform; room.SetParent(wing, false);

        float bedZ = zMax - 1.75f;
        var bed = Place(room, "Bed_Single", 90);
        Put(bed, xMin + 0.02f, floorY, bedZ, alignMinX: true);
        var bb = WorldBounds(bed);

        var stand = Place(room, "Nightstand_Lamp", 90);
        var sb = WorldBounds(stand);
        Put(stand, xMin + 0.03f, floorY, bb.min.z - 0.06f - sb.extents.z, alignMinX: true);

        var shelf = Place(room, "Wall_Shelf", 90);
        Put(shelf, xMin + 0.005f, floorY + 1.35f, bedZ, alignMinX: true, collider: false);

        var wardrobe = Place(room, "Wardrobe_Open", 180);
        var wb = WorldBounds(wardrobe);
        Put(wardrobe, xMin + 1.5f, floorY, zMax - 0.02f - wb.extents.z);

        var dresser = Place(room, "Dresser_Ransacked", -90);
        var rb = WorldBounds(dresser);
        Put(dresser, xMax - 0.02f - rb.extents.x, floorY, zDiv + 0.06f + rb.extents.z);

        var carpet = Place(room, "Carpet", 0);
        Put(carpet, xMin + 2.25f, floorY + 0.004f, bedZ + 0.1f, collider: false);

        // the ransacked dresser is an inspectable clue
        var clue = dresser.gameObject.AddComponent<ClueObject>();
        clue.clueId = "clue_dresser";
        clue.clueTitle = "Комод в спальне";
        clue.clueDescription = "Все ящики выдвинуты, вещи выброшены наружу. Отец так не оставил бы — здесь что-то искали.";
        clue.targetRenderer = dresser.GetComponentInChildren<Renderer>();

        var light = new GameObject("Bedroom_Lamp_Light").AddComponent<Light>();
        light.transform.SetParent(room, false);
        var lb = WorldBounds(stand);
        light.transform.position = new Vector3(lb.center.x + 0.05f, lb.max.y - 0.22f, lb.center.z);
        light.type = LightType.Point; light.color = new Color(1f, .78f, .52f); light.intensity = 1.2f; light.range = 3.5f;
        light.shadows = LightShadows.Soft;

        foreach (var t in new[] { bed, stand, shelf, wardrobe, dresser, carpet })
            log.AppendLine("  " + t.name + " bounds " + WorldBounds(t));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine("Saved " + scene.path);

        Vector3 c = new Vector3((xMin + xMax) / 2, floorY, (zDiv + zMax) / 2);
        HeroSetup.DebugShot(new Vector3(xMax - 0.3f, floorY + 1.7f, zMax - 0.3f), c + Vector3.up * 0.5f, "flat_bedroom_1.png", 75);
        HeroSetup.DebugShot(new Vector3(xMin + 0.4f, floorY + 1.6f, zDiv + 0.3f), c + Vector3.up * 0.5f, "flat_bedroom_2.png", 75);
        HeroSetup.DebugShot(new Vector3(xMax + 1.2f, floorY + 1.6f, zMax - 0.85f), new Vector3(xMax - 1f, floorY + 0.8f, zMax - 0.9f), "flat_bedroom_door.png", 70);
    }

    static Transform Place(Transform parent, string model, float yaw)
    {
        string path = Dir + model + ".glb";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new Exception("Model not imported: " + path);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.name = model;
        go.transform.rotation = Quaternion.Euler(0, yaw, 0);
        foreach (var t in go.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
        return go.transform;
    }

    static void Put(Transform t, float x, float y, float z, bool alignMinX = false, bool collider = true)
    {
        var b = WorldBounds(t);
        float dx = alignMinX ? x - b.min.x : x - b.center.x;
        t.position += new Vector3(dx, y - b.min.y, z - b.center.z);
        if (!collider) return;
        var box = t.gameObject.AddComponent<BoxCollider>();
        var lb = LocalBounds(t);
        box.center = lb.center; box.size = lb.size;
    }

    public static Bounds WorldBounds(Transform t)
    {
        var rr = t.GetComponentsInChildren<Renderer>();
        var b = rr[0].bounds; foreach (var r in rr.Skip(1)) b.Encapsulate(r.bounds); return b;
    }

    static Bounds LocalBounds(Transform root)
    {
        bool first = true; Bounds lb = default;
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
        {
            var mb = mf.sharedMesh.bounds; var m = root.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                var p = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                if (first) { lb = new Bounds(p, Vector3.zero); first = false; } else lb.Encapsulate(p);
            }
        }
        return lb;
    }
}

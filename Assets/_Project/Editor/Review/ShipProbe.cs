using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Review-only: measures the moored ship and the water around it (Review/ship.txt, Review/ship_old.png).
/// Trigger: <project>/Review/shipprobe.request. Never saves.
[InitializeOnLoad]
public static class ShipProbe
{
    const string Harbor = "Assets/_Project/Scenes/05_BrightonHarbor.unity";
    static ShipProbe() { EditorApplication.update += Tick; }

    static void Tick()
    {
        string request = Path.Combine(HeroReviewCapture.Folder, "shipprobe.request");
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || !File.Exists(request)) return;
        File.Delete(request);
        var log = new StringBuilder();
        try { Run(log); } catch (System.Exception e) { log.AppendLine("ERROR " + e); }
        File.WriteAllText(Path.Combine(HeroReviewCapture.Folder, "ship.txt"), log.ToString());
    }

    public static void Run(StringBuilder log)
    {
        var active = SceneManager.GetActiveScene();
        if (active.path != Harbor)
        {
            if (active.isDirty) { log.AppendLine("ABORT dirty scene"); return; }
            EditorSceneManager.OpenScene(Harbor, OpenSceneMode.Single);
        }
        var ship = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == "Moored Cargo Ship");
        if (!ship) { log.AppendLine("no ship"); return; }
        log.AppendLine($"ship pos={ship.position} rot={ship.eulerAngles} scale={ship.lossyScale} parent={(ship.parent ? ship.parent.name : "-")}");
        var mf = ship.GetComponent<MeshFilter>();
        if (mf && mf.sharedMesh) log.AppendLine($"mesh {mf.sharedMesh.name} local bounds {mf.sharedMesh.bounds.center} size {mf.sharedMesh.bounds.size} subs {mf.sharedMesh.subMeshCount} verts {mf.sharedMesh.vertexCount}");
        var r = ship.GetComponent<Renderer>();
        if (r)
        {
            log.AppendLine($"world bounds {r.bounds.center} size {r.bounds.size} min {r.bounds.min} max {r.bounds.max}");
            foreach (var m in r.sharedMaterials) log.AppendLine($"  mat {m.name} {m.shader.name}");
        }
        foreach (var w in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            string n = w.name.ToLowerInvariant();
            if (n.Contains("water") || n.Contains("basin") || n.Contains("sea") || n.Contains("вод"))
                log.AppendLine($"water? {w.name} parent={(w.transform.parent ? w.transform.parent.name : "-")} bounds {w.bounds.center} size {w.bounds.size}");
        }
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name.Contains("Painted vessel name") || t.name.Contains("mooring")).Take(12))
            log.AppendLine($"near {t.name} pos {t.position}");
        if (r)
        {
            var b = r.bounds; var cam = Camera.main;
            var shots = new System.Collections.Generic.List<Texture2D>
            {
                HeroReviewCapture.Render(cam, b.center + new Vector3(b.size.x * .2f, 6f, -b.size.z * .9f - 12f), b.center, 55f, 640, 360),
                HeroReviewCapture.Render(cam, b.center + new Vector3(b.size.x * .9f + 15f, 4f, 0f), b.center, 55f, 640, 360),
            };
            log.AppendLine("sheet " + HeroReviewCapture.Sheet("ship_old", shots, 2));
        }
    }

    public static void Batch()
    {
        var log = new StringBuilder();
        try { Run(log); } catch (System.Exception e) { log.AppendLine("ERROR " + e); }
        File.WriteAllText(Path.Combine(HeroReviewCapture.Folder, "ship.txt"), log.ToString());
    }
}

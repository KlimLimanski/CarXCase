using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Review-only: reports the hangar doors and films the outside one (Review/door.txt, Review/door_*.png).
/// Trigger: <project>/Review/door.request. Never saves the scene.
[InitializeOnLoad]
public static class DoorProbe
{
    const string Harbor = "Assets/_Project/Scenes/05_BrightonHarbor.unity";
    static DoorProbe() { EditorApplication.update += Tick; }

    static void Tick()
    {
        string request = Path.Combine(HeroReviewCapture.Folder, "door.request");
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || !File.Exists(request)) return;
        File.Delete(request);
        var log = new StringBuilder();
        try { Run(log); } catch (System.Exception e) { log.AppendLine("ERROR " + e); }
        File.WriteAllText(Path.Combine(HeroReviewCapture.Folder, "door.txt"), log.ToString());
    }

    /// Batch entry: -executeMethod DoorProbe.CapturePadlock
    public static void CapturePadlock()
    {
        var log = new StringBuilder();
        try
        {
            EditorSceneManager.OpenScene(Harbor, OpenSceneMode.Single);
            foreach (var door in Object.FindObjectsByType<HangarDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!door.LeadsInside) continue;
                var dest = new SerializedObject(door).FindProperty("destination").objectReferenceValue as Transform;
                Vector3 into0 = dest.position - door.transform.position; into0.y = 0; into0.Normalize();
                var ray0 = new Ray(door.transform.position - into0 * .6f, into0);
                foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                    if (r.enabled && r.bounds.IntersectRay(ray0, out float d0) && d0 < 3f)
                        log.AppendLine($"  ray hits {r.name} ({r.GetType().Name}) parent={(r.transform.parent ? r.transform.parent.name : "-")} d={d0:F3} size={r.bounds.size} min={r.bounds.min}");
                foreach (var h in Physics.RaycastAll(ray0, 3f, ~0, QueryTriggerInteraction.Ignore))
                    log.AppendLine($"  phys {h.collider.name} d={h.distance:F3} p={h.point}");
                var padlock = HangarPadlock.Build(door.transform, dest);
                Vector3 p = padlock.Point, n = padlock.Outward, side = Vector3.Cross(Vector3.up, n), up = Vector3.up;
                log.AppendLine($"padlock at {padlock.transform.position} normal {n} door {door.transform.position}");
                var cam = Camera.main;
                var shots = new System.Collections.Generic.List<Texture2D>
                {
                    HeroReviewCapture.Render(cam, p + n * .62f + side * .28f + up * .12f, p - up * .08f, 50f, 480, 300),
                    HeroReviewCapture.Render(cam, p + n * .35f + side * .05f + up * .02f, p - up * .03f, 40f, 480, 300),
                    HeroReviewCapture.Render(cam, p + n * 2.2f + up * .4f, p - up * .3f, 55f, 480, 300),
                    HeroReviewCapture.Render(cam, p + n * 5f + side * 1.5f + up * .6f, p - up * .4f, 55f, 480, 300),
                };
                log.AppendLine("sheet " + HeroReviewCapture.Sheet("padlock", shots, 2));
            }
        }
        catch (System.Exception e) { log.AppendLine("ERROR " + e); }
        File.WriteAllText(Path.Combine(HeroReviewCapture.Folder, "padlock.txt"), log.ToString());
    }

    static void Run(StringBuilder log)
    {
        var active = SceneManager.GetActiveScene();
        if (active.path != Harbor)
        {
            if (active.isDirty) { log.AppendLine("ABORT dirty scene"); return; }
            EditorSceneManager.OpenScene(Harbor, OpenSceneMode.Single);
        }
        foreach (var door in Object.FindObjectsByType<HangarDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(door);
            bool inside = so.FindProperty("leadsInside").boolValue;
            var t = door.transform;
            log.AppendLine($"DOOR {t.name} inside={inside} pos={t.position} rot={t.eulerAngles} scale={t.lossyScale} parent={(t.parent ? t.parent.name : "-")}");
            foreach (var c in door.GetComponentsInChildren<Collider>(true)) log.AppendLine($"  collider {c.name} {c.GetType().Name} trigger={c.isTrigger} bounds={c.bounds.center}/{c.bounds.size}");
            if (!inside) continue;
            var near = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(r => r.enabled && Vector3.Distance(r.bounds.ClosestPoint(t.position), t.position) < 2.5f)
                .OrderBy(r => Vector3.Distance(r.bounds.center, t.position)).Take(25);
            foreach (var r in near) log.AppendLine($"  near {r.name} parent={(r.transform.parent ? r.transform.parent.name : "-")} center={r.bounds.center} size={r.bounds.size}");
            Vector3 p = t.position, f = t.forward, right = t.right, up = Vector3.up;
            var cam = Camera.main;
            var shots = new[]
            {
                HeroReviewCapture.Render(cam, p + f * 3.2f + up * 1.5f, p + up * 1.1f, 50f, 480, 300),
                HeroReviewCapture.Render(cam, p - f * 3.2f + up * 1.5f, p + up * 1.1f, 50f, 480, 300),
                HeroReviewCapture.Render(cam, p + right * 3f + up * 1.4f, p + up * 1.1f, 50f, 480, 300),
                HeroReviewCapture.Render(cam, p - right * 3f + up * 1.4f, p + up * 1.1f, 50f, 480, 300),
            };
            log.AppendLine("sheet " + HeroReviewCapture.Sheet("door_" + t.name.Replace(' ', '_'), shots.ToList(), 2));
        }
        log.AppendLine("DONE");
    }
}

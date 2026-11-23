using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Review-only: poses the hangar cast at the key cutscene moments in edit mode, films each pose
/// from two angles into Review/hangar_*.png and writes Review/hangar.txt with floor / collision checks.
/// Nothing is saved: the harbour scene is reopened without saving and the previous scene restored.
/// Trigger: create <project>/Review/hangar.request (or Tools/Review/Hangar poses).
[InitializeOnLoad]
public static class HangarReview
{
    const string Harbor = "Assets/_Project/Scenes/05_BrightonHarbor.unity";
    static string Dir => HeroReviewCapture.Folder;

    sealed class Pose { public string name; public Action apply; public Func<(Vector3 eye, Vector3 look)[]> views; }

    static List<Pose> poses;
    static int index;
    static double settleUntil;
    static string returnTo;
    static readonly StringBuilder report = new();
    static readonly List<Texture2D> frames = new();
    static HangarFinale finale;
    static HangarStaging staging;
    static Transform kostya, player;
    static GameObject rescue, late;

    static HangarReview() { EditorApplication.update += Poll; }

    [MenuItem("Tools/Review/Hangar poses")] static void Menu() => Begin();

    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (poses != null) { Step(); return; }
        string request = Path.Combine(Dir, "hangar.request");
        if (!File.Exists(request)) return;
        File.Delete(request);
        Begin();
    }

    static void Begin()
    {
        report.Clear();
        Directory.CreateDirectory(Dir);
        var active = SceneManager.GetActiveScene();
        if (active.isDirty) { Finish("ABORT: the open scene has unsaved changes; nothing was touched."); return; }
        returnTo = active.path;
        EditorSceneManager.OpenScene(Harbor, OpenSceneMode.Single);
        finale = UnityEngine.Object.FindAnyObjectByType<HangarFinale>(FindObjectsInactive.Include);
        if (!finale) { Finish("ABORT: no HangarFinale in " + Harbor); return; }
        staging = finale.GetComponent<HangarStaging>();
        kostya = finale.kostya; rescue = finale.rescue; late = finale.late;
        var officer = UnityEngine.Object.FindAnyObjectByType<DetectiveGame.Core.OfficerController>(FindObjectsInactive.Include);
        player = officer ? officer.transform : null;
        rescue.SetActive(true); late.SetActive(false);
        staging.Initialize(rescue.transform, kostya);
        staging.ResetActors();
        if (player)
        {
            player.position = finale.transform.TransformPoint(new Vector3(-.05f, .10f, 5.35f));
            var to = kostya.position - player.position; to.y = 0;
            player.rotation = Quaternion.LookRotation(to);
        }
        report.AppendLine("hangar root " + V(finale.transform.position) + " rot " + V(finale.transform.eulerAngles));
        report.AppendLine("kostya " + V(kostya.position) + " player " + (player ? V(player.position) : "none") + " father " + V(rescue.transform.position));
        Build();
        index = 0;
        settleUntil = EditorApplication.timeSinceStartup + .35;
    }

    static void Build()
    {
        Vector3 mark = staging.KostyaMark;
        Quaternion facing = Quaternion.Euler(0, -155f, 0);
        (Vector3, Vector3)[] OnKostya()
        {
            Vector3 k = kostya.position, f = kostya.forward, s = kostya.right, u = Vector3.up;
            return new[] { (k + f * 2.1f + s * .8f + u * 1.25f, k + u * .85f), (k + s * 2.4f - f * .2f + u * 1.0f, k + u * .75f) };
        }
        (Vector3, Vector3)[] OnFather()
        {
            Vector3 p = rescue.transform.position, u = Vector3.up;
            Vector3 toA = player ? player.position - p : rescue.transform.forward; toA.y = 0; toA.Normalize();
            Vector3 s = Vector3.Cross(u, toA);
            return new[] { (p + toA * 1.6f + s * .5f + u * 1.15f, p + u * .8f), (p + s * 1.3f + toA * .3f + u * .95f, p + u * .7f) };
        }
        (Vector3, Vector3)[] OnPlayer()
        {
            Vector3 p = player.position, u = Vector3.up;
            return new[] { (p + player.right * 2.6f + u * 1.1f, p + u * .5f), (p + player.forward * 2.4f + player.right * .6f + u * 1.2f, p + u * .6f) };
        }
        void Talk() { kostya.localPosition = mark; kostya.localRotation = facing; staging.Conversation(1f, .28f); }
        poses = new List<Pose>
        {
            new() { name = "father_seated", apply = () => staging.Father(0, 0), views = OnFather },
            new() { name = "father_struggle", apply = () => staging.Father(1, 0), views = OnFather },
            new() { name = "turn_half", apply = () => staging.Turn(.5f), views = OnKostya },
            new() { name = "turn_end", apply = () => staging.Turn(1f), views = OnKostya },
            new() { name = "talk", apply = Talk, views = OnKostya },
            new() { name = "reach", apply = () => { Talk(); staging.Reach(1f); }, views = OnKostya },
            new() { name = "fall_35", apply = () => staging.Subside(true, .35f, mark, facing), views = OnKostya },
            new() { name = "fall_70", apply = () => staging.Subside(true, .7f, mark, facing), views = OnKostya },
            new() { name = "fall_end", apply = () => staging.Subside(true, 1f, mark, facing), views = OnKostya },
            new() { name = "restraint_35", apply = () => staging.Subside(false, .35f, mark, facing), views = OnKostya },
            new() { name = "restraint_70", apply = () => staging.Subside(false, .7f, mark, facing), views = OnKostya },
            new() { name = "restraint_end", apply = () => { staging.Subside(false, 1f, mark, facing); staging.SetCuff(true); }, views = OnKostya },
            new() { name = "offer_wrists", apply = () => { Talk(); staging.OfferWrists(1f); staging.SetCuff(true); }, views = OnKostya },
            new() { name = "father_released", apply = () => { staging.Bindings(false); staging.DropRope(1f); staging.Father(1, 1); }, views = OnFather },
        };
        if (player)
            poses.Add(new Pose
            {
                name = "alexey_fall",
                apply = () =>
                {
                    var visual = player.Find("Character_Visual");
                    if (visual) visual.rotation = Quaternion.AngleAxis(-82f, player.right) * Quaternion.LookRotation(player.forward);
                },
                views = OnPlayer
            });
    }

    static void Step()
    {
        try
        {
            var pose = poses[index];
            pose.apply();   // re-applied every tick so clip cross-fades settle in edit mode
            if (EditorApplication.timeSinceStartup < settleUntil) return;
            Measure(pose.name);
            var cam = Camera.main;
            foreach (var (eye, look) in pose.views())
            {
                Texture2D tex = null;
                HeroReviewCapture.WithBakedSkins(finale.transform.root, () =>
                    HeroReviewCapture.WithBakedSkins(player ? player : finale.transform, () =>
                        tex = HeroReviewCapture.Render(cam, eye, look, 50f, 480, 300)));
                frames.Add(tex);
            }
            index++;
            if (index % 4 == 0 || index == poses.Count)
            {
                int sheet = (index - 1) / 4;
                report.AppendLine("sheet " + HeroReviewCapture.Sheet("hangar_" + sheet, new List<Texture2D>(frames), 2));
                frames.Clear();
            }
            if (index >= poses.Count) { Finish("DONE"); return; }
            settleUntil = EditorApplication.timeSinceStartup + .35;
        }
        catch (Exception e) { Finish("ERROR " + e); }
    }

    /// Lowest bone vs the floor, and bones buried in scenery colliders.
    static void Measure(string name)
    {
        var line = new StringBuilder(name + ":");
        foreach (var (who, root) in new[] { ("kostya", kostya), ("father", rescue.transform), ("alexey", player) })
        {
            if (!root) continue;
            var skin = root.GetComponentInChildren<SkinnedMeshRenderer>();
            if (!skin) continue;
            float floor = finale.transform.position.y;
            float lowest = float.MaxValue; string lowBone = "";
            var buried = new HashSet<string>();
            foreach (var bone in skin.bones)
            {
                if (!bone) continue;
                if (bone.position.y < lowest) { lowest = bone.position.y; lowBone = bone.name; }
                foreach (var hit in Physics.OverlapSphere(bone.position, .045f, ~0, QueryTriggerInteraction.Ignore))
                {
                    var t = hit.transform;
                    if (t.IsChildOf(root) || (player && t.IsChildOf(player)) || t.IsChildOf(kostya) || t.IsChildOf(rescue.transform)) continue;
                    buried.Add(bone.name + ">" + hit.name);
                }
            }
            line.Append($" {who}: lowest {lowBone} {lowest - floor:F2}m above root floor");
            if (buried.Count > 0) line.Append(" INSIDE [" + string.Join(", ", buried) + "]");
            line.Append(';');
        }
        report.AppendLine(line.ToString());
    }

    static void Finish(string status)
    {
        report.AppendLine(status);
        File.WriteAllText(Path.Combine(Dir, "hangar.txt"), report.ToString());
        poses = null;
        foreach (var f in frames) if (f) UnityEngine.Object.DestroyImmediate(f);
        frames.Clear();
        if (!string.IsNullOrEmpty(returnTo)) EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
        else if (SceneManager.GetActiveScene().path == Harbor) EditorSceneManager.OpenScene(Harbor, OpenSceneMode.Single);
        returnTo = null;
    }

    static string V(Vector3 v) => $"({v.x:F2},{v.y:F2},{v.z:F2})";
}

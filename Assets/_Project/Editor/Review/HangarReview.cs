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
    static HangarProps props;

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
        foreach (var root in new[] { kostya, rescue.transform, late.transform })
            foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                report.AppendLine($"  skin {Where(r.transform)} active={r.gameObject.activeInHierarchy} mesh={(r.sharedMesh ? r.sharedMesh.name : "-")} root={(r.rootBone ? r.rootBone.name : "-")} bones={r.bones.Length} firstBone={(r.bones.Length > 0 && r.bones[0] ? Where(r.bones[0]) : "-")} shapes={(r.sharedMesh ? r.sharedMesh.blendShapeCount : 0)}");
        var so = new SerializedObject(staging);
        foreach (var field in new[] { "looseRope", "wristCuff" })
        {
            var t = so.FindProperty(field).objectReferenceValue as Transform;
            if (!t) { report.AppendLine("  " + field + " none"); continue; }
            var mf = t.GetComponentInChildren<MeshFilter>(true); var mr = t.GetComponentInChildren<MeshRenderer>(true);
            report.AppendLine($"  {field} {Where(t)} mesh={(mf && mf.sharedMesh ? mf.sharedMesh.name + " v" + mf.sharedMesh.vertexCount : "-")} mat={(mr && mr.sharedMaterial ? mr.sharedMaterial.name + "/" + mr.sharedMaterial.shader.name : "-")} scale={V(t.lossyScale)} size={(mr ? V(mr.bounds.size) : "-")}");
        }
        int ropes = 0;
        foreach (Transform part in rescue.transform)
            if (part.name == "Rope" && ropes++ < 2)
            {
                var mf = part.GetComponent<MeshFilter>(); var mr = part.GetComponent<MeshRenderer>();
                report.AppendLine($"  Rope piece mesh={(mf && mf.sharedMesh ? mf.sharedMesh.name + " v" + mf.sharedMesh.vertexCount : "-")} mat={(mr && mr.sharedMaterial ? mr.sharedMaterial.name + "/" + mr.sharedMaterial.shader.name : "-")} local={V(part.localPosition)} scale={V(part.localScale)}");
            }
        report.AppendLine("  rope pieces " + ropes + " chair=" + (rescue.transform.Find("Chair") ? "yes" : "no"));
        foreach (Transform child in rescue.transform) if (child.name != "Rope") report.AppendLine("  rescue child " + child.name);
        report.AppendLine("kostya " + V(kostya.position) + " player " + (player ? V(player.position) : "none") + " father " + V(rescue.transform.position));
        props = finale.gameObject.AddComponent<HangarProps>();
        props.Setup(staging, kostya, rescue.transform);
        if (player) props.SetHero(player.GetComponent<DetectiveGame.Core.OfficerController>());
        SnapshotHero();
        if (player)
        {
            foreach (var skin in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                report.AppendLine($"  hero skin {Where(skin.transform)} active={skin.gameObject.activeInHierarchy} bones={skin.bones.Length} first={(skin.bones.Length > 0 && skin.bones[0] ? Where(skin.bones[0]) : "-")}");
            foreach (var t in player.GetComponentsInChildren<Transform>(true)) if (t.name == "R_Hand") report.AppendLine("  R_Hand at " + Where(t));
        }
        staging.ResetActors();
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
            return new[] { (k + f * 2.1f + s * .8f + u * 1.25f, k + u * .85f), (k + s * 2.4f - f * .2f + u * 1.0f, k + u * .75f), (k + u * 3.4f - f * .9f, k - f * .5f) };
        }
        (Vector3, Vector3)[] OnFather()
        {
            Vector3 p = rescue.transform.position, u = Vector3.up;
            Vector3 toA = player ? player.position - p : rescue.transform.forward; toA.y = 0; toA.Normalize();
            Vector3 s = Vector3.Cross(u, toA);
            return new[] { (p + toA * 1.6f + s * .5f + u * 1.15f, p + u * .8f), (p + s * 1.3f + toA * .3f + u * .95f, p + u * .7f), (p + toA * .45f + s * .35f + u * 1.25f, p + u * .82f) };
        }
        (Vector3, Vector3)[] OnPlayer()
        {
            Vector3 p = player.position, u = Vector3.up;
            return new[] { (p + player.right * 2.6f + u * 1.1f, p + u * .5f), (p + player.forward * 2.4f + player.right * .6f + u * 1.2f, p + u * .6f), (p + u * 3.2f - player.forward * .6f, p - player.forward * .8f) };
        }
        (Vector3, Vector3)[] OnBoth()
        {
            Vector3 a = player.position, k = kostya.position, u = Vector3.up, mid = (a + k) * .5f;
            Vector3 axis = (k - a); axis.y = 0; axis.Normalize();
            Vector3 side = Vector3.Cross(u, axis);
            return new[] { (mid + side * 2.2f + u * 1.25f, mid + u * 1f), (a - axis * .9f + side * .45f + u * 1.55f, k + u * 1.1f), (a + axis * .9f - side * .3f + u * 1.45f, a + u * 1.2f) };
        }
        void Talk() { kostya.localPosition = mark; kostya.localRotation = facing; staging.Conversation(1f, .28f); }
        void Hero(System.Action act) { RestoreHero(); props.ResetAll(); act(); props.Apply(); }
        poses = new List<Pose>
        {
            new() { name = "father_rope", apply = () => { staging.Bindings(true); staging.Father(0, 0); }, views = OnFather },
            new() { name = "alexey_aim", apply = () => { Talk(); Hero(() => props.AimImmediate(1f, kostya)); }, views = OnBoth },
            new() { name = "alexey_low_ready", apply = () => { Talk(); Hero(() => props.AimImmediate(.5f, kostya)); }, views = OnBoth },
            new() { name = "kostya_draws", apply = () => { Talk(); staging.Reach(1f); Hero(() => { props.AimImmediate(1f, kostya); props.ArmKostya(true); }); }, views = OnBoth },
            new() { name = "fall_35", apply = () => { Hero(() => { }); staging.Subside(true, .35f, mark, facing); }, views = OnKostya },
            new() { name = "fall_end", apply = () => staging.Subside(true, 1f, mark, facing), views = OnKostya },
            new() { name = "restraint_20", apply = () => staging.Subside(false, .2f, mark, facing), views = OnKostya },
            new() { name = "restraint_45", apply = () => staging.Subside(false, .45f, mark, facing), views = OnKostya },
            new() { name = "restraint_end", apply = () => { staging.Subside(false, 1f, mark, facing); staging.SetCuff(true); staging.UpdateCuff(); }, views = OnKostya },
            new() { name = "offer_wrists", apply = () => { Talk(); staging.OfferWrists(1f); staging.SetCuff(true); staging.UpdateCuff(); }, views = OnKostya },
            new() { name = "alexey_collapse_30", apply = () => Hero(() => props.Collapse(.3f)), views = OnPlayer },
            new() { name = "alexey_collapse_60", apply = () => Hero(() => props.Collapse(.6f)), views = OnPlayer },
            new() { name = "alexey_collapse_end", apply = () => Hero(() => props.Collapse(1f)), views = OnPlayer },
            new() { name = "father_released", apply = () => { Hero(() => { }); staging.SetCuff(false); staging.Bindings(false); staging.DropRope(1f); staging.Father(1, 1); }, views = OnFather },
        };
    }

    static readonly Dictionary<Transform, (Vector3, Quaternion)> heroRest = new();

    static void SnapshotHero()
    {
        heroRest.Clear();
        if (player) foreach (var t in player.GetComponentsInChildren<Transform>(true)) heroRest[t] = (t.localPosition, t.localRotation);
    }

    static void RestoreHero()
    {
        foreach (var pair in heroRest) if (pair.Key) pair.Key.SetLocalPositionAndRotation(pair.Value.Item1, pair.Value.Item2);
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
                // The pose has been re-applied for several editor frames, so the skinning is current.
                Texture2D tex = null;
                if (player) HeroReviewCapture.WithBakedSkins(player, () => tex = HeroReviewCapture.Render(cam, eye, look, 50f, 480, 300));
                else tex = HeroReviewCapture.Render(cam, eye, look, 50f, 480, 300);
                frames.Add(tex);
            }
            index++;
            if (index % 4 == 0 || index == poses.Count)
            {
                int sheet = (index - 1) / 4;
                report.AppendLine("sheet " + HeroReviewCapture.Sheet("hangar_" + sheet, new List<Texture2D>(frames), 3));
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
            float floor = FloorBelow(skin.bounds.center);
            float lowest = float.MaxValue; string lowBone = "";
            var buried = new HashSet<string>();
            foreach (var bone in skin.bones)
            {
                if (!bone || bone.name == "root") continue;
                if (bone.position.y < lowest) { lowest = bone.position.y; lowBone = bone.name; }
                foreach (var hit in Physics.OverlapSphere(bone.position, .045f, ~0, QueryTriggerInteraction.Ignore))
                {
                    var t = hit.transform;
                    if (t.IsChildOf(root) || (player && t.IsChildOf(player)) || t.IsChildOf(kostya) || t.IsChildOf(rescue.transform)) continue;
                    buried.Add(bone.name + ">" + hit.name);
                }
            }
            var pelvisBone = Array.Find(skin.bones, b => b && (b.name == "pelvis" || b.name == "Hips" || b.name.EndsWith("Hips")));
            line.Append($" {who}: floor {floor:F2} lowest {lowBone} {lowest - floor:+0.00;-0.00}m pelvis {(pelvisBone ? (pelvisBone.position.y - floor).ToString("F2") : "?")} skinMinY {skin.bounds.min.y - floor:+0.00;-0.00}");
            if (buried.Count > 0) line.Append(" INSIDE [" + string.Join(", ", buried) + "]");
            line.Append(';');
        }
        report.AppendLine(line.ToString());
    }

    static float FloorBelow(Vector3 point)
    {
        float best = finale.transform.position.y;
        float top = float.MinValue;
        foreach (var hit in Physics.RaycastAll(new Vector3(point.x, point.y + 2f, point.z), Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore))
        {
            var t = hit.transform;
            if (t.IsChildOf(kostya) || t.IsChildOf(rescue.transform) || (player && t.IsChildOf(player))) continue;
            if (!hit.collider.name.Contains("slab") && !hit.collider.name.ToLower().Contains("floor")) continue;
            top = Mathf.Max(top, hit.point.y);
        }
        return top > float.MinValue ? top : best;
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

    static string Where(Transform t) => t.parent ? Where(t.parent) + "/" + t.name : t.name;

    static string V(Vector3 v) => $"({v.x:F2},{v.y:F2},{v.z:F2})";
}

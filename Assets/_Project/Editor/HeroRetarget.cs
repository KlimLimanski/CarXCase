using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Builds Hero_Walk.anim / Hero_Run.anim from the retarget baked in Blender
/// (ArtSource/Hero/hero_retarget_work.blend, actions Walk_RT / Run_RT on the game rig Armature.001).
/// Blender writes, per frame, every bone's world-space rotation relative to its rest pose and the hips position,
/// already converted to Unity axes (Unity = -bx, bz, -by): Animations/Source/Hero_{Walk,Run}_retarget.json.
/// Here those deltas are put on top of the glb rig's rest pose, hips travel is removed, the loop is closed and the
/// lowest point of the mesh is put on the floor. Existing .anim assets are overwritten in place (GUIDs kept).
/// Menu: Tools/Hero/Retarget walk and run (direct)  or  HeroSetup/heroretarget_request.txt
[InitializeOnLoad]
public static class HeroRetarget
{
    const string AnimDir = "Assets/_Project/Arts/Heroes/Animations/";
    const string SrcDir = AnimDir + "Source/";
    const string Model = "Assets/_Project/Arts/Heroes/Main Hero/MainHero_Rigged.glb";

    [Serializable] class Data { public int fps; public string[] bones; public float lift; public int frameCount; public float[] restHead; public float[] rot; public float[] hips; }

    static HeroRetarget() { EditorApplication.update += Poll; }
    static void Poll()
    {
        string req = "HeroSetup/heroretarget_request.txt";
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!File.Exists(req)) return;
        File.Delete(req); Run();
    }

    [MenuItem("Tools/Hero/Retarget walk and run (direct)")]
    public static void Run()
    {
        var log = new StringBuilder("RETARGET " + DateTime.Now + "\n");
        try
        {
            foreach (var name in new[] { "Walk", "Run" })
            {
                var clip = Bake(name, log);
                Save(clip, name);
                UnityEngine.Object.DestroyImmediate(clip);
            }
            AssetDatabase.SaveAssets();
            log.AppendLine("SUCCESS");
        }
        catch (Exception e) { log.AppendLine("ERROR " + e); Debug.LogException(e); }
        Directory.CreateDirectory("HeroSetup");
        File.WriteAllText("HeroSetup/retarget_result.txt", log.ToString());
        Debug.Log("[HeroRetarget] " + log);
        HeroAnimReview.Review();
    }

    static AnimationClip Bake(string name, StringBuilder log)
    {
        string jsonPath = SrcDir + "Hero_" + name + "_retarget.json";
        var d = JsonUtility.FromJson<Data>(File.ReadAllText(jsonPath));
        int n = d.bones.Length, frames = d.frameCount - 1;
        if (d.rot.Length != d.frameCount * n * 4 || d.hips.Length != d.frameCount * 3) throw new Exception("Bad data in " + jsonPath);
        float length = frames / (float)d.fps;

        var tgt = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
        try
        {
            tgt.hideFlags = HideFlags.HideAndDontSave;
            tgt.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); tgt.transform.localScale = Vector3.one;
            var all = tgt.GetComponentsInChildren<Transform>(true);
            var t = d.bones.Select(b => all.FirstOrDefault(x => x.name == b) ?? throw new Exception("Bone not found: " + b)).ToArray();

            // axis check: Blender rest heads converted to Unity must land on the glb bones
            float worst = 0;
            for (int i = 0; i < n; i++) worst = Mathf.Max(worst, (t[i].position - new Vector3(d.restHead[i * 3], d.restHead[i * 3 + 1], d.restHead[i * 3 + 2])).magnitude);
            log.AppendLine($"{name}: {d.frameCount} frames, {length:F2}s, rest match {worst * 100:F1} cm, lift {d.lift:F3}");
            if (worst > 0.03f) throw new Exception("Blender and Unity rigs do not line up (" + worst + " m), axis conversion is off");

            var rest = t.Select(x => x.rotation).ToArray();
            Vector3 hipsRest = t[0].position;
            var rot = new Quaternion[n, frames + 1];
            var off = new Vector3[frames + 1];
            for (int f = 0; f <= frames; f++)
            {
                for (int i = 0; i < n; i++)
                {
                    int k = (f * n + i) * 4;
                    var delta = new Quaternion(d.rot[k], d.rot[k + 1], d.rot[k + 2], d.rot[k + 3]);
                    t[i].rotation = delta * rest[i];          // parents come first in the list
                }
                for (int i = 0; i < n; i++) rot[i, f] = t[i].localRotation;
                off[f] = new Vector3(d.hips[f * 3], d.hips[f * 3 + 1], d.hips[f * 3 + 2]) - hipsRest;
            }
            // no travel in place: remove the average horizontal offset and any drift across the cycle
            var mean = Vector3.zero; for (int f = 0; f < frames; f++) mean += off[f]; mean /= frames; mean.y = 0;
            var drift = off[frames] - off[0];
            for (int f = 0; f <= frames; f++) off[f] -= mean + drift * ((float)f / frames - 0.5f) + Vector3.down * d.lift;

            var clip = new AnimationClip { name = "Hero_" + name, frameRate = d.fps };
            for (int i = 0; i < n; i++)
            {
                var corr = Quaternion.Inverse(rot[i, frames]) * rot[i, 0];
                for (int f = 0; f <= frames; f++) rot[i, f] *= Quaternion.Slerp(Quaternion.identity, corr, (float)f / frames);
                for (int f = 1; f <= frames; f++) if (Quaternion.Dot(rot[i, f - 1], rot[i, f]) < 0) rot[i, f] = new Quaternion(-rot[i, f].x, -rot[i, f].y, -rot[i, f].z, -rot[i, f].w);
                string path = AnimationUtility.CalculateTransformPath(t[i], tgt.transform);
                for (int a = 0; a < 4; a++)
                    Curve(clip, path, "m_LocalRotation." + "xyzw"[a], Enumerable.Range(0, frames + 1).Select(f => new Keyframe(length * f / frames, rot[i, f][a])).ToArray());
                if (i == 0)
                {
                    var hl = Enumerable.Range(0, frames + 1).Select(f => t[0].parent.InverseTransformPoint(hipsRest + off[f])).ToArray();
                    for (int a = 0; a < 3; a++)
                        Curve(clip, path, "m_LocalPosition." + "xyz"[a], Enumerable.Range(0, frames + 1).Select(f => new Keyframe(length * f / frames, hl[f][a])).ToArray());
                }
            }
            clip.EnsureQuaternionContinuity();
            var st = AnimationUtility.GetAnimationClipSettings(clip); st.loopTime = true; st.startTime = 0; st.stopTime = length;
            AnimationUtility.SetAnimationClipSettings(clip, st);
            log.AppendLine($"  hips y {off.Min(o => o.y):F3}..{off.Max(o => o.y):F3}");
            return clip;
        }
        finally { UnityEngine.Object.DestroyImmediate(tgt); }
    }

    static void Curve(AnimationClip clip, string path, string prop, Keyframe[] keys)
    {
        var c = new AnimationCurve(keys);
        for (int i = 0; i < keys.Length; i++) { AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto); AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto); }
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), prop), c);
    }

    static void Save(AnimationClip baked, string name)
    {
        string path = AnimDir + "Hero_" + name + ".anim";
        string backup = SrcDir + "Hero_" + name + "_before_retarget.anim";
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (!existing) { existing = new AnimationClip(); AssetDatabase.CreateAsset(existing, path); }
        else if (!AssetDatabase.LoadAssetAtPath<AnimationClip>(backup)) AssetDatabase.CopyAsset(path, backup);
        EditorUtility.CopySerialized(baked, existing); existing.name = "Hero_" + name;
        EditorUtility.SetDirty(existing);
    }
}

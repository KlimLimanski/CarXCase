using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// Brings an action authored in Blender (exported as an armature-only FBX into Performance/Source) onto one of
/// the hangar models as a plain .anim, the format HangarStaging samples. The Blender export and the model FBX
/// may disagree on bone axes, so the motion is carried over as world-space rotation from the rest pose, bone by
/// bone, and written as local rotations of the model's own bones. Bones that do not move are left out, so the
/// clip only drives what it animates.
/// Blender convention: frame 0 of the exported action is the rest pose (Unity takes the FBX default pose from
/// the frame the file was exported on), the motion starts at frame 1.
public static class HangarClipImport
{
    public static string Retarget(string sourceFbx, string modelFbx, string animPath, bool pelvisPosition = false, int firstFrame = 1)
    {
        AssetDatabase.ImportAsset(sourceFbx, ImportAssetOptions.ForceSynchronousImport);
        if (AssetImporter.GetAtPath(sourceFbx) is ModelImporter importer)
        {
            bool dirty = importer.animationType != ModelImporterAnimationType.Generic || importer.materialImportMode != ModelImporterMaterialImportMode.None ||
                         importer.animationCompression != ModelImporterAnimationCompression.Off;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            if (dirty) importer.SaveAndReimport();
        }
        var source = AssetDatabase.LoadAllAssetsAtPath(sourceFbx).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
        var a = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(sourceFbx));
        var b = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelFbx));
        // Both stay as imported: an armature-only FBX has the armature itself as its root, with the axis
        // conversion on it, and the clip keys that root too.
        try
        {
            var A = a.GetComponentsInChildren<Transform>().GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
            var B = b.GetComponentsInChildren<Transform>().GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
            var aLocal = A.ToDictionary(k => k.Key, k => (k.Value.localPosition, k.Value.localRotation));
            var bLocal = B.ToDictionary(k => k.Key, k => (k.Value.localPosition, k.Value.localRotation));
            var aRest = A.ToDictionary(k => k.Key, k => k.Value.rotation);
            var bRest = B.ToDictionary(k => k.Key, k => k.Value.rotation);
            var skin = b.GetComponentInChildren<SkinnedMeshRenderer>();
            var order = b.GetComponentsInChildren<Transform>().Where(t => skin.bones.Contains(t) && t.name != "root" && A.ContainsKey(t.name)).ToList();
            var curves = order.ToDictionary(t => t.name, t => new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() });
            var position = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
            var last = new Dictionary<string, Quaternion>();
            Transform pelvisA = A.TryGetValue("pelvis", out var pa) ? pa : null, pelvisB = B.TryGetValue("pelvis", out var pb) ? pb : null;
            Vector3 pelvisRestA = pelvisA ? pelvisA.position : Vector3.zero, pelvisRestB = pelvisB ? pelvisB.position : Vector3.zero;
            int frames = Mathf.RoundToInt(source.length * 30f) + 1 - firstFrame;
            for (int f = 0; f < frames; f++)
            {
                float sampleAt = Mathf.Min(source.length, (f + firstFrame) / 30f);
                float t = f / 30f;
                foreach (var kv in A) kv.Value.SetLocalPositionAndRotation(aLocal[kv.Key].Item1, aLocal[kv.Key].Item2);
                foreach (var kv in B) kv.Value.SetLocalPositionAndRotation(bLocal[kv.Key].Item1, bLocal[kv.Key].Item2);
                source.SampleAnimation(a, sampleAt);
                if (pelvisPosition && pelvisA && pelvisB)
                {
                    pelvisB.position = pelvisRestB + (pelvisA.position - pelvisRestA);
                    var lp = pelvisB.localPosition;
                    position[0].AddKey(t, lp.x); position[1].AddKey(t, lp.y); position[2].AddKey(t, lp.z);
                }
                foreach (var bone in order) bone.rotation = A[bone.name].rotation * Quaternion.Inverse(aRest[bone.name]) * bRest[bone.name];
                foreach (var bone in order)
                {
                    var q = bone.localRotation;
                    if (last.TryGetValue(bone.name, out var previous) && Quaternion.Dot(previous, q) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                    last[bone.name] = q;
                    var c = curves[bone.name];
                    c[0].AddKey(t, q.x); c[1].AddKey(t, q.y); c[2].AddKey(t, q.z); c[3].AddKey(t, q.w);
                }
            }
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(animPath);
            if (!clip) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, animPath); }
            clip.ClearCurves();
            clip.frameRate = 30;
            int moving = 0;
            foreach (var bone in order)
            {
                var c = curves[bone.name];
                if (!c.Any(curve => curve.keys.Max(k => k.value) - curve.keys.Min(k => k.value) > .002f)) continue;
                moving++;
                string path = AnimationUtility.CalculateTransformPath(bone, b.transform);
                clip.SetCurve(path, typeof(Transform), "localRotation.x", c[0]);
                clip.SetCurve(path, typeof(Transform), "localRotation.y", c[1]);
                clip.SetCurve(path, typeof(Transform), "localRotation.z", c[2]);
                clip.SetCurve(path, typeof(Transform), "localRotation.w", c[3]);
            }
            if (pelvisPosition && pelvisB)
            {
                string path = AnimationUtility.CalculateTransformPath(pelvisB, b.transform);
                clip.SetCurve(path, typeof(Transform), "localPosition.x", position[0]);
                clip.SetCurve(path, typeof(Transform), "localPosition.y", position[1]);
                clip.SetCurve(path, typeof(Transform), "localPosition.z", position[2]);
            }
            clip.EnsureQuaternionContinuity();
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
            return $"{animPath}: {moving} bones, {frames} frames, {clip.length:F2}s";
        }
        finally
        {
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// Bakes Mixamo's humanoid motion onto the existing generic gameplay rig.
/// Keeping the clip assets in place preserves the references used by the scenes and cutscenes.
/// The source FBX import settings and the locomotion controller are enforced here as well,
/// so running the menu item again always produces the same clips.
public static class HeroAnimationImport
{
    const string Heroes = "Assets/_Project/Arts/Heroes/";
    const string Animations = Heroes + "Animations/";
    const string Model = Heroes + "Main Hero/MainHero_Rigged.glb";
    const string ControllerPath = Animations + "Hero_Locomotion.controller";
    const int SampleRate = 30;
    /// Must match HeroSetup.HeroScale: the scene hero is scaled from 1.90 m to 1.80 m.
    const float HeroScale = 0.95f;

    public const string SpeedParameter = "Speed";
    public const string DirectionParameter = "Direction";

    /// Ground speeds (m/s) of OfficerController.walkSpeed / runSpeed in the apartment and precinct.
    static readonly float[] Thresholds = { 0f, 1.8f, 3f };

    static readonly (string clip, string file)[] Sources =
    {
        ("Idle", "Standing W_Briefcase Idle.fbx"),
        ("Walk", "main char Walking.fbx"),
        ("Run", "main_char_Running.fbx"),
    };

    static readonly string[] HumanNames =
    {
        "Hips", "Spine", "Chest", "UpperChest", "Neck", "Head",
        "LeftShoulder", "LeftUpperArm", "LeftLowerArm", "LeftHand",
        "RightShoulder", "RightUpperArm", "RightLowerArm", "RightHand",
        "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "LeftToes",
        "RightUpperLeg", "RightLowerLeg", "RightFoot", "RightToes"
    };

    static readonly string[] TargetNames =
    {
        "Hips", "Spine", "Chest", "UpperChest", "Neck", "Head",
        "L_Shoulder", "L_UpperArm", "L_LowerArm", "L_Hand",
        "R_Shoulder", "R_UpperArm", "R_LowerArm", "R_Hand",
        "L_UpperLeg", "L_LowerLeg", "L_Foot", "L_Toes",
        "R_UpperLeg", "R_LowerLeg", "R_Foot", "R_Toes"
    };

    [MenuItem("Tools/Hero/Import Mixamo idle, walk and run")]
    public static void Import() => ImportClips(includeWalk: true);

    public static void ImportIdleAndRun() => ImportClips(includeWalk: false);

    static void ImportClips(bool includeWalk)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Import animations in Edit Mode.");

        foreach (var (_, file) in Sources) ConfigureSource(Animations + "Source/" + file);

        var baked = new List<(int index, (AnimationClip clip, float groundSpeed) motion)>();
        try
        {
            for (int i = 0; i < Sources.Length; i++)
            {
                if (!includeWalk && Sources[i].clip == "Walk") continue;
                baked.Add((i, Bake(Sources[i].clip, Sources[i].file)));
            }

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            var state = controller.layers[0].stateMachine.defaultState;
            var tree = state.motion as BlendTree;
            if (!tree || tree.children.Length != 3)
                throw new InvalidOperationException("Expected the hero's locomotion blend tree.");

            var children = tree.children;
            foreach (var (i, motion) in baked)
            {
                var (clip, groundSpeed) = motion;
                children[i].motion = Save(clip);
                children[i].threshold = Thresholds[i];
                children[i].timeScale = groundSpeed > .1f
                    ? Mathf.Clamp(Thresholds[i] / groundSpeed, .75f, 1.30f) : 1f;
                Debug.Log($"[HeroAnimationImport] {clip.name}, {clip.length:F2}s, estimated stride speed {groundSpeed:F2} m/s, playback ×{children[i].timeScale:F2}");
            }

            Undo.RecordObject(tree, "Tune Mixamo locomotion");
            tree.blendParameter = SpeedParameter;
            tree.useAutomaticThresholds = false;
            tree.children = children;
            EditorUtility.SetDirty(tree);
            ConfigureController(controller, state);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            foreach (var (_, motion) in baked) UnityEngine.Object.DestroyImmediate(motion.clip);
        }
    }

    /// Makes the FBX import settings explicit so a reimport samples exactly the same humanoid curves.
    static void ConfigureSource(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (!importer) throw new InvalidOperationException("Missing animation source " + path);
        bool changed = importer.clipAnimations.Length == 0;
        if (importer.animationType != ModelImporterAnimationType.Human)
            throw new InvalidOperationException(path + " must be imported as Humanoid.");
        if (importer.animationCompression != ModelImporterAnimationCompression.Off)
        {
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            changed = true;
        }
        if (!importer.resampleCurves) { importer.resampleCurves = true; changed = true; }

        var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
        foreach (var clip in clips)
        {
            changed |= clip.loopTime || clip.loopPose || clip.lockRootRotation || clip.lockRootHeightY ||
                       clip.lockRootPositionXZ || clip.keepOriginalOrientation || !clip.keepOriginalPositionY ||
                       clip.keepOriginalPositionXZ || clip.mirror;
            // Loop closure and ground contact are solved by the bake below, not by the importer.
            clip.loopTime = false;
            clip.loopPose = false;
            clip.lockRootRotation = false;
            clip.lockRootHeightY = false;
            clip.lockRootPositionXZ = false;
            clip.keepOriginalOrientation = false;
            clip.keepOriginalPositionY = true;
            clip.keepOriginalPositionXZ = false;
            clip.mirror = false;
        }
        if (!changed) return;
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        Debug.Log("[HeroAnimationImport] Fixed import settings of " + path);
    }

    /// "Direction" lets the locomotion state play backwards for scripted back-steps.
    static void ConfigureController(AnimatorController controller, AnimatorState state)
    {
        if (controller.parameters.All(p => p.name != DirectionParameter))
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = DirectionParameter,
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1f
            });
        Undo.RecordObject(state, "Hero locomotion direction");
        state.speed = 1f;
        state.speedParameter = DirectionParameter;
        state.speedParameterActive = true;
        EditorUtility.SetDirty(state);
        EditorUtility.SetDirty(controller);
    }

    public static void ApplyIdlePose(Animator animator)
    {
        var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(Animations + "Hero_Idle.anim");
        if (!idle) throw new InvalidOperationException("Hero_Idle is missing.");
        if (!animator) throw new ArgumentNullException(nameof(animator));

        var transforms = AnimationUtility.GetCurveBindings(idle).Select(binding => binding.path)
            .Distinct().Select(path => string.IsNullOrEmpty(path) ? animator.transform : animator.transform.Find(path))
            .Where(t => t).ToArray();
        Undo.RecordObjects(transforms, "Apply hero idle pose");
        Undo.RecordObject(animator, "Apply hero locomotion rig");
        // The scene uses transform curves baked against this GLB's own hierarchy.
        animator.avatar = null;
        animator.applyRootMotion = false;
        idle.SampleAnimation(animator.gameObject, 0);
        foreach (var transform in transforms) PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
    }

    static Avatar BuildTargetAvatar(GameObject root)
    {
        var description = new HumanDescription
        {
            human = HumanNames.Select((name, i) => new HumanBone
            {
                humanName = name,
                boneName = TargetNames[i],
                limit = new HumanLimit { useDefaultValues = true }
            }).ToArray(),
            skeleton = root.GetComponentsInChildren<Transform>().Select(t => new SkeletonBone
            {
                name = t.name,
                position = t.localPosition,
                rotation = t.localRotation,
                scale = t.localScale
            }).ToArray(),
            upperArmTwist = .5f, lowerArmTwist = .5f,
            upperLegTwist = .5f, lowerLegTwist = .5f,
            armStretch = .05f, legStretch = .05f
        };
        var avatar = AvatarBuilder.BuildHumanAvatar(root, description);
        if (avatar && avatar.isValid && avatar.isHuman) return avatar;
        if (avatar) UnityEngine.Object.DestroyImmediate(avatar);
        throw new InvalidOperationException("Could not map the GLB hero's skeleton.");
    }

    static (AnimationClip clip, float groundSpeed) Bake(string name, string file)
    {
        string sourcePath = Animations + "Source/" + file;
        var sourceClip = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<AnimationClip>()
            .FirstOrDefault(clip => !clip.name.StartsWith("__preview", StringComparison.Ordinal));
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
        if (!sourceClip || !sourceClip.humanMotion || sourceClip.length <= 0 || !model)
            throw new InvalidOperationException("Missing model or Humanoid animation " + sourcePath);

        var target = UnityEngine.Object.Instantiate(model);
        target.hideFlags = HideFlags.HideAndDontSave;
        target.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        Avatar avatar = null;
        var baked = new AnimationClip { name = "Hero_" + name, frameRate = SampleRate };
        try
        {
            avatar = BuildTargetAvatar(target);
            var animator = target.GetComponent<Animator>();
            if (!animator) animator = target.AddComponent<Animator>();
            animator.runtimeAnimatorController = null;
            animator.avatar = avatar;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var transforms = target.GetComponentsInChildren<Transform>();
            var bones = TargetNames.Select(n => transforms.Single(t => t.name == n)).ToArray();
            var hips = bones[0];
            Vector3 restHips = hips.position;
            var rig = bones.ToDictionary(bone => bone.name);
            var bindRotations = bones.ToDictionary(bone => bone, bone => bone.rotation);
            var leftHandRest = rig["L_Hand"].localRotation;
            var rightHandRest = rig["R_Hand"].localRotation;
            var headBindLocal = rig["Head"].localRotation;
            int frames = Mathf.Max(1, Mathf.RoundToInt(sourceClip.length * SampleRate));
            var rotations = new Quaternion[bones.Length, frames + 1];
            var positions = new Vector3[frames + 1];

            void Record(int frame)
            {
                for (int i = 0; i < bones.Length; i++) rotations[i, frame] = bones[i].localRotation;
                positions[frame] = hips.position;
            }

            SampleHumanoid(animator, sourceClip, frames, Record);

            for (int i = 0; i < bones.Length; i++)
            {
                Quaternion closure = Quaternion.Inverse(rotations[i, frames]) * rotations[i, 0];
                for (int frame = 0; frame <= frames; frame++)
                    rotations[i, frame] *= Quaternion.Slerp(Quaternion.identity, closure, (float)frame / frames);
            }
            Vector3 drift = positions[frames] - positions[0];
            Vector3 average = Vector3.zero;
            for (int i = 0; i <= frames; i++)
            {
                positions[i] -= drift * ((float)i / frames);
                if (i < frames) average += positions[i] / frames;
            }
            var horizontalOffset = Vector3.ProjectOnPlane(average - restHips, Vector3.up);
            for (int i = 0; i <= frames; i++) positions[i] -= horizontalOffset;

            var feet = new[] { rig["L_Foot"], rig["R_Foot"] };
            var toes = new[] { rig["L_Toes"], rig["R_Toes"] };
            bool idle = name == "Idle";
            bool run = name == "Run";
            int firstLeg = Array.IndexOf(TargetNames, "L_UpperLeg");
            Vector3 idleStance = positions[0];
            var root = target.transform;

            void Pose(int frame)
            {
                for (int i = 0; i < bones.Length; i++)
                    bones[i].localRotation = rotations[i, idle && (i == 0 || i >= firstLeg) ? 0 : frame];
                hips.position = idle ? idleStance : positions[frame];
                if (idle || run) StabilizeUpperBody(rig, bindRotations, idle);
                if (run)
                {
                    // A compact stride: the leading foot lands closer under the hips, on its own side.
                    FitRunningStep(rig["L_UpperLeg"], rig["L_LowerLeg"], feet[0], -.13f);
                    FitRunningStep(rig["R_UpperLeg"], rig["R_LowerLeg"], feet[1], .13f);
                    SteerLimb(rig["L_UpperArm"], rig["L_Hand"], -.28f, .235f, .44f, .8f);
                    SteerLimb(rig["R_UpperArm"], rig["R_Hand"], .28f, .235f, .44f, .8f);
                    rig["L_Hand"].localRotation = Quaternion.Slerp(rig["L_Hand"].localRotation, leftHandRest, .35f);
                    rig["R_Hand"].localRotation = Quaternion.Slerp(rig["R_Hand"].localRotation, rightHandRest, .35f);
                }
            }

            // First pass: the posture bias of the source (a dropped shoulder, a tilted head)
            // and the neutral arm pose of the run cycle.
            float shoulderBias = 0, headBias = 0, headPitchBias = 0, headYawBias = 0;
            Vector4 leftArmSum = Vector4.zero, rightArmSum = Vector4.zero;
            for (int frame = 0; frame < frames; frame++)
            {
                Pose(frame);
                shoulderBias += ShoulderRoll(rig, root) / frames;
                headBias += HeadRoll(rig["Head"], root) / frames;
                headPitchBias += HeadPitch(rig["Head"], root) / frames;
                headYawBias += HeadYaw(rig["Head"], root) / frames;
                leftArmSum = Accumulate(leftArmSum, rig["L_UpperArm"].localRotation);
                rightArmSum = Accumulate(rightArmSum, rig["R_UpperArm"].localRotation);
            }
            Quaternion leftArmMean = Normalize(leftArmSum), rightArmMean = Normalize(rightArmSum);

            var footPositions = new Vector3[feet.Length, frames + 1];
            var mesh = new Mesh();
            var renderer = target.GetComponentInChildren<SkinnedMeshRenderer>();
            float[] lowest = new float[frames + 1];
            var vertices = new List<Vector3>();
            try
            {
                for (int frame = 0; frame <= frames; frame++)
                {
                    Pose(frame);
                    // Idle holds the shoulders level and damps their sway; locomotion keeps its swing
                    // but loses the constant lean baked into the source.
                    float shoulderRoll = ShoulderRoll(rig, root);
                    // The arms keep hanging the way they did; only the shoulder joints move.
                    Quaternion leftArm = rig["L_UpperArm"].rotation, rightArm = rig["R_UpperArm"].rotation;
                    LevelShoulders(rig, root, idle ? shoulderBias + .6f * (shoulderRoll - shoulderBias) : shoulderBias);
                    if (idle) Breathe(rig, root, 2f * Mathf.PI * 2f * frame / frames);
                    rig["L_UpperArm"].rotation = leftArm;
                    rig["R_UpperArm"].rotation = rightArm;
                    // Arms hang clear of the thighs with soft elbows; palms face the legs.
                    // (The source idle holds a briefcase in the right hand; without it both arms hang alike.)
                    foreach (var arm in new[] { "L", "R" })
                    {
                        float side = arm == "L" ? -1f : 1f;
                        if (idle) RelaxArm(rig, bindRotations, arm, root, 13f, .295f * side, .285f, .32f, .3f, 1f);
                        else if (run) RelaxArm(rig, bindRotations, arm, root, 0f, 0f, 0f, 0f, 0f, .7f);
                        else RelaxArm(rig, bindRotations, arm, root, 9f, .30f * side, .295f, .36f, .4f, .9f);
                    }
                    if (run)
                    {
                        // Moderate arm swing around the cycle's neutral pose.
                        rig["L_UpperArm"].localRotation = Quaternion.Slerp(leftArmMean, rig["L_UpperArm"].localRotation, .95f);
                        rig["R_UpperArm"].localRotation = Quaternion.Slerp(rightArmMean, rig["R_UpperArm"].localRotation, .95f);
                    }
                    OpenPosture(rig, root, idle ? 1f : run ? .8f : .9f);
                    float headRoll = HeadRoll(rig["Head"], root);
                    var head = rig["Head"];
                    head.rotation = Quaternion.AngleAxis(idle ? headRoll * .9f : headBias, root.forward) * head.rotation;
                    if (idle)
                    {
                        // A calm, level gaze: slightly down, centred, keeping a little of the source's glances.
                        float pitch = HeadPitch(head, root), yaw = HeadYaw(head, root);
                        float pitchFix = (IdleGazePitch + .6f * (pitch - headPitchBias)) - pitch;
                        float yawFix = .5f * (yaw - headYawBias) - yaw;
                        rig["Neck"].rotation = Quaternion.AngleAxis(.4f * pitchFix, root.right) * rig["Neck"].rotation;
                        head.rotation = Quaternion.AngleAxis(yawFix, root.up) *
                                        Quaternion.AngleAxis(.6f * pitchFix, root.right) * head.rotation;
                    }

                    // The face is skinned across Neck/Head: bending the head on the neck squashes
                    // the chin and changes the features. Keep the head close to its bind pose on the
                    // neck and let the neck carry the tilt, so the face reads the same in every clip.
                    {
                        Quaternion headWorld = rig["Head"].rotation;
                        Quaternion headLocal = Quaternion.Slerp(headBindLocal, rig["Head"].localRotation, .2f);
                        rig["Neck"].rotation = headWorld * Quaternion.Inverse(headLocal);
                        rig["Head"].localRotation = headLocal;
                    }
                    for (int i = 0; i < bones.Length; i++) rotations[i, frame] = bones[i].localRotation;
                    positions[frame] = hips.position;
                    for (int i = 0; i < feet.Length; i++) footPositions[i, frame] = toes[i].position;
                    renderer.BakeMesh(mesh);
                    mesh.GetVertices(vertices);
                    var meshToWorld = renderer.transform.localToWorldMatrix;
                    lowest[frame] = vertices.Min(vertex => meshToWorld.MultiplyPoint3x4(vertex).y);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }

            var sortedHeights = (float[])lowest.Clone();
            Array.Sort(sortedHeights);
            float runningGround = sortedHeights[Mathf.FloorToInt(frames * .08f)];
            for (int i = 0; i <= frames; i++)
            {
                int previous = (i + frames - 1) % frames;
                int next = (i + 1) % frames;
                // Walking and idle have continuous contact. Leave the running flight phases intact.
                float ground = name == "Run" ? runningGround
                    : (lowest[previous] + 2 * lowest[i] + lowest[next]) * .25f;
                positions[i] = hips.parent.InverseTransformPoint(positions[i] - Vector3.up * ground);
            }
            for (int bone = 0; bone < bones.Length; bone++)
            {
                string path = AnimationUtility.CalculateTransformPath(bones[bone], target.transform);
                for (int axis = 0; axis < 4; axis++)
                    SetCurve(baked, path, "m_LocalRotation." + "xyzw"[axis],
                        Enumerable.Range(0, frames + 1).Select(f => new Keyframe(
                            sourceClip.length * f / frames, rotations[bone, f][axis])).ToArray());
            }
            string hipsPath = AnimationUtility.CalculateTransformPath(hips, target.transform);
            for (int axis = 0; axis < 3; axis++)
                SetCurve(baked, hipsPath, "m_LocalPosition." + "xyz"[axis],
                    Enumerable.Range(0, frames + 1).Select(f => new Keyframe(
                        sourceClip.length * f / frames, positions[f][axis])).ToArray());

            baked.EnsureQuaternionContinuity();
            var settings = AnimationUtility.GetAnimationClipSettings(baked);
            settings.loopTime = true;
            settings.startTime = 0;
            settings.stopTime = sourceClip.length;
            AnimationUtility.SetAnimationClipSettings(baked, settings);
            // Ground speed of the planted toes, in the scene's scale.
            float speed = name == "Idle" ? 0 : HeroScale * EstimateSpeed(footPositions, sourceClip.length / frames);
            Debug.Log($"[HeroAnimationImport] {name}: removed shoulder lean {shoulderBias:F1}°, head tilt {headBias:F1}°");
            return (baked, speed);
        }
        catch
        {
            UnityEngine.Object.DestroyImmediate(baked);
            throw;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(target);
            if (avatar) UnityEngine.Object.DestroyImmediate(avatar);
        }
    }

    /// Positive when the left shoulder is higher than the right one (degrees).
    static float ShoulderRoll(Dictionary<string, Transform> rig, Transform root)
    {
        Vector3 left = root.InverseTransformPoint(rig["L_UpperArm"].position);
        Vector3 right = root.InverseTransformPoint(rig["R_UpperArm"].position);
        return Mathf.Atan2(left.y - right.y, Mathf.Max(.01f, right.x - left.x)) * Mathf.Rad2Deg;
    }

    /// Idle head pitch (degrees, positive looks down).
    const float IdleGazePitch = 2f;

    /// Positive when the head tilts forward / looks down (degrees).
    static float HeadPitch(Transform head, Transform root)
    {
        Vector3 up = root.InverseTransformDirection(head.up);
        return Mathf.Atan2(up.z, up.y) * Mathf.Rad2Deg;
    }

    /// Positive when the face turns towards the hero's right (degrees).
    static float HeadYaw(Transform head, Transform root)
    {
        Vector3 forward = root.InverseTransformDirection(head.forward);
        return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
    }

    /// Positive when the head leans towards the hero's right shoulder (degrees).
    static float HeadRoll(Transform head, Transform root)
    {
        Vector3 up = root.InverseTransformDirection(head.up);
        return Mathf.Atan2(up.x, up.y) * Mathf.Rad2Deg;
    }

    static void LevelShoulders(Dictionary<string, Transform> rig, Transform root, float roll)
    {
        // Rolling both clavicles about the body's forward axis lowers the high side and lifts the low one.
        // 0.36 m between the shoulder joints, 0.135 m clavicles.
        float angle = Mathf.Asin(Mathf.Clamp(.36f * Mathf.Tan(roll * Mathf.Deg2Rad) / .27f, -.5f, .5f)) * Mathf.Rad2Deg;
        foreach (var clavicle in new[] { rig["L_Shoulder"], rig["R_Shoulder"] })
            clavicle.rotation = Quaternion.AngleAxis(angle, root.forward) * clavicle.rotation;
    }

    /// One breath: the upper chest opens, the shoulders rise slightly, the head stays on target.
    static void Breathe(Dictionary<string, Transform> rig, Transform root, float phase)
    {
        // Quick inhale, slower exhale.
        float t = Mathf.Repeat(phase / (2f * Mathf.PI), 1f);
        float breath = t < .4f ? Mathf.SmoothStep(0f, 1f, t / .4f) : Mathf.SmoothStep(1f, 0f, (t - .4f) / .6f);
        Vector3 side = root.right, forward = root.forward;
        rig["Chest"].rotation = Quaternion.AngleAxis(-.5f * breath, side) * rig["Chest"].rotation;
        rig["UpperChest"].rotation = Quaternion.AngleAxis(-1.1f * breath, side) * rig["UpperChest"].rotation;
        rig["L_Shoulder"].rotation = Quaternion.AngleAxis(-1.6f * breath, forward) * rig["L_Shoulder"].rotation;
        rig["R_Shoulder"].rotation = Quaternion.AngleAxis(1.6f * breath, forward) * rig["R_Shoulder"].rotation;
        rig["Neck"].rotation = Quaternion.AngleAxis(1.0f * breath, side) * rig["Neck"].rotation;
        rig["Head"].rotation = Quaternion.AngleAxis(.6f * breath, side) * rig["Head"].rotation;
    }

    /// Bends the elbow a little, swings the arm out so the wrist clears the thigh (lateral
    /// target in metres, 0 = keep), and turns the hand so the palm faces the leg with a relaxed flex.
    static void RelaxArm(Dictionary<string, Transform> rig, Dictionary<Transform, Quaternion> bind, string s,
        Transform root, float elbowBend, float center, float inner, float outer, float motionWeight, float handWeight)
    {
        var upper = rig[s + "_UpperArm"]; var lower = rig[s + "_LowerArm"]; var hand = rig[s + "_Hand"];
        float side = s == "L" ? -1f : 1f;
        Quaternion handRotation = hand.rotation;
        if (elbowBend > 0) lower.rotation = Quaternion.AngleAxis(-elbowBend, root.right) * lower.rotation;
        if (center != 0) SteerLimb(upper, hand, center, inner, outer, motionWeight);
        hand.rotation = handRotation;

        // Hand axes in the bind (T) pose: along the arm, palm down.
        Vector3 bindAlong = root.right * side;
        Quaternion b = bind[hand];
        Vector3 alongLocal = Quaternion.Inverse(b) * bindAlong;
        Vector3 palmLocal = Quaternion.Inverse(b) * -root.up;
        Vector3 along = (hand.position - lower.position).normalized;
        Vector3 palm = Vector3.ProjectOnPlane(-side * root.right * .9f - root.forward * .2f, along).normalized;
        Quaternion target = Quaternion.LookRotation(along, palm) * Quaternion.Inverse(Quaternion.LookRotation(alongLocal, palmLocal));
        target = Quaternion.AngleAxis(9f, Vector3.Cross(along, palm)) * target;
        hand.rotation = Quaternion.Slerp(hand.rotation, target, handWeight);
    }

    /// Undoes the hunched, squeezed look of the retarget: the chest lifts, the clavicles drop and
    /// open back, the neck straightens so the head sits over the shoulders. The arms keep their aim.
    static void OpenPosture(Dictionary<string, Transform> rig, Transform root, float weight)
    {
        Vector3 side = root.right, forward = root.forward, up = root.up;
        Quaternion leftArm = rig["L_UpperArm"].rotation, rightArm = rig["R_UpperArm"].rotation;
        Quaternion head = rig["Head"].rotation;
        rig["Chest"].rotation = Quaternion.AngleAxis(-2f * weight, side) * rig["Chest"].rotation;
        rig["UpperChest"].rotation = Quaternion.AngleAxis(-3f * weight, side) * rig["UpperChest"].rotation;
        // Shoulders down (away from the ears) and slightly back.
        rig["L_Shoulder"].rotation = Quaternion.AngleAxis(4.5f * weight, forward) * Quaternion.AngleAxis(-4f * weight, up) * rig["L_Shoulder"].rotation;
        rig["R_Shoulder"].rotation = Quaternion.AngleAxis(-4.5f * weight, forward) * Quaternion.AngleAxis(4f * weight, up) * rig["R_Shoulder"].rotation;
        rig["L_UpperArm"].rotation = leftArm;
        rig["R_UpperArm"].rotation = rightArm;
        // Neck up and back so the head is not pushed forward; the face keeps its direction.
        rig["Neck"].rotation = Quaternion.AngleAxis(-4f * weight, side) * rig["Neck"].rotation;
        rig["Head"].rotation = Quaternion.Slerp(rig["Head"].rotation, head, .85f);
    }

    static Vector4 Accumulate(Vector4 sum, Quaternion q)
    {
        var v = new Vector4(q.x, q.y, q.z, q.w);
        if (sum != Vector4.zero && Vector4.Dot(sum, v) < 0) v = -v;
        return sum + v;
    }

    static Quaternion Normalize(Vector4 sum)
    {
        sum.Normalize();
        return new Quaternion(sum.x, sum.y, sum.z, sum.w);
    }

    static void StabilizeUpperBody(Dictionary<string, Transform> rig,
        Dictionary<Transform, Quaternion> bindRotations, bool idle)
    {
        var chest = rig["UpperChest"];
        var neck = rig["Neck"];
        var head = rig["Head"];
        // Measure all three against the GLB bind orientation before adjusting their parents.
        Quaternion chestPose = LimitSway(chest.rotation, bindRotations[chest],
            idle ? new Vector3(.68f, .55f, .38f) : new Vector3(.91f, .57f, .45f));
        Quaternion neckPose = LimitSway(neck.rotation, bindRotations[neck],
            idle ? new Vector3(.78f, .40f, .34f) : new Vector3(.83f, .46f, .37f));
        Quaternion headPose = LimitSway(head.rotation, bindRotations[head],
            idle ? new Vector3(.34f, .20f, .23f) : new Vector3(.63f, .24f, .31f));
        chest.rotation = chestPose;
        neck.rotation = neckPose;
        head.rotation = headPose;
    }

    static Quaternion LimitSway(Quaternion animated, Quaternion bind, Vector3 weight)
    {
        Vector3 angles = (animated * Quaternion.Inverse(bind)).eulerAngles;
        return Quaternion.Euler(
            Mathf.DeltaAngle(0f, angles.x) * weight.x,
            Mathf.DeltaAngle(0f, angles.y) * weight.y,
            Mathf.DeltaAngle(0f, angles.z) * weight.z) * bind;
    }

    static void FitRunningStep(Transform thigh, Transform shin, Transform foot, float lane)
    {
        Vector3 hip = thigh.position;
        Vector3 knee = shin.position;
        Vector3 ankle = foot.position;
        Vector3 target = ankle;
        float side = Mathf.Sign(lane);
        target.x = side * Mathf.Clamp(side * Mathf.Lerp(lane, ankle.x, .26f), .11f, .22f);
        target.z = Mathf.Lerp(hip.z, ankle.z, StrideScale);
        target.y -= Mathf.Max(0f, ankle.y - .21f) * .22f;

        // Keep the animated knee's bend direction while preserving both bone lengths.
        Vector3 axis = (target - hip).normalized;
        Vector3 bend = Vector3.ProjectOnPlane(knee - hip, axis).normalized;
        // The source swings the knees across the midline; aim them forward and a touch outward instead.
        Vector3 straight = Vector3.ProjectOnPlane(Vector3.forward + Vector3.right * (side * .12f), axis).normalized;
        bend = Vector3.Slerp(bend.sqrMagnitude < .01f ? straight : bend, straight, .7f).normalized;
        float thighLength = Vector3.Distance(hip, knee);
        float shinLength = Vector3.Distance(knee, ankle);
        float reach = Mathf.Clamp(Vector3.Distance(hip, target),
            Mathf.Abs(thighLength - shinLength) + .002f, thighLength + shinLength - .002f);
        float kneeAlong = (thighLength * thighLength - shinLength * shinLength + reach * reach) / (2f * reach);
        float kneeBend = Mathf.Sqrt(Mathf.Max(0f, thighLength * thighLength - kneeAlong * kneeAlong));
        Vector3 desiredKnee = hip + axis * kneeAlong + bend * kneeBend;
        Quaternion footOrientation = foot.rotation;
        thigh.rotation = Quaternion.FromToRotation(knee - hip, desiredKnee - hip) * thigh.rotation;
        shin.rotation = Quaternion.FromToRotation(foot.position - shin.position, hip + axis * reach - shin.position) * shin.rotation;
        foot.rotation = footOrientation;

        // No pigeon toes: turn an inward-pointing foot back towards straight ahead.
        Vector3 toes = Vector3.ProjectOnPlane(foot.rotation * Vector3.forward, Vector3.up);
        if (toes.sqrMagnitude > .0001f)
        {
            float yaw = Mathf.Atan2(toes.x, toes.z) * Mathf.Rad2Deg;
            float inward = -side * yaw;
            if (inward > -4f) foot.rotation = Quaternion.AngleAxis(side * (inward + 4f) * .8f, Vector3.up) * foot.rotation;
        }
    }

    /// Share of the source stride kept by the run (front-to-back foot travel).
    const float StrideScale = .92f;

    static void SteerLimb(Transform joint, Transform end, float center, float inner, float outer, float motionWeight)
    {
        float side = Mathf.Sign(center);
        Vector3 aim = end.position;
        aim.x = side * Mathf.Clamp(side * Mathf.Lerp(center, aim.x, motionWeight), inner, outer);
        Quaternion endOrientation = end.rotation;
        joint.rotation = Quaternion.FromToRotation(end.position - joint.position, aim - joint.position) * joint.rotation;
        end.rotation = endOrientation;
    }

    static void SampleHumanoid(Animator target, AnimationClip clip, int frames, Action<int> record)
    {
        var graph = PlayableGraph.Create("Mixamo retarget");
        try
        {
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(false);
            AnimationPlayableOutput.Create(graph, "Hero", target).SetSourcePlayable(playable);
            graph.Play();
            for (int frame = 0; frame <= frames; frame++)
            {
                playable.SetTime(clip.length * frame / frames);
                graph.Evaluate(0);
                record(frame);
            }
        }
        finally { graph.Destroy(); }
    }

    static float EstimateSpeed(Vector3[,] positions, float deltaTime)
    {
        float speed = EstimateSpeed(positions, deltaTime, .015f);
        return speed > 0 ? speed : EstimateSpeed(positions, deltaTime, .035f);
    }

    static float EstimateSpeed(Vector3[,] positions, float deltaTime, float contactBand)
    {
        var speeds = new List<float>();
        int frames = positions.GetLength(1);
        for (int foot = 0; foot < positions.GetLength(0); foot++)
        {
            float bottom = Enumerable.Range(0, frames).Min(i => positions[foot, i].y);
            for (int frame = 1; frame < frames; frame++)
            {
                var a = positions[foot, frame - 1];
                var b = positions[foot, frame];
                float speed = (a.z - b.z) / deltaTime;
                // Only frames where the toes are planted: the heel strike and toe-off move slower.
                if (Mathf.Max(a.y, b.y) < bottom + contactBand && speed > .5f) speeds.Add(speed);
            }
        }
        if (speeds.Count < 4) return 0;
        speeds.Sort();
        return speeds[speeds.Count / 2];
    }

    static void SetCurve(AnimationClip clip, string path, string property, Keyframe[] keys)
    {
        var curve = new AnimationCurve(keys);
        for (int i = 0; i < keys.Length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
        }
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
    }

    static AnimationClip Save(AnimationClip baked)
    {
        string path = Animations + baked.name + ".anim";
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (!existing)
        {
            existing = new AnimationClip();
            AssetDatabase.CreateAsset(existing, path);
        }
        Undo.RecordObject(existing, "Import " + baked.name);
        EditorUtility.CopySerialized(baked, existing);
        EditorUtility.SetDirty(existing);
        return existing;
    }
}

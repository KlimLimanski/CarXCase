using System.Collections.Generic;
using UnityEngine;

public sealed class HangarStaging : MonoBehaviour
{
    [SerializeField] Transform looseRope;
    [SerializeField] Transform wristCuff;

    [SerializeField] AnimationClip seated, conversation, pivot, backwardFall, restraint;
    [SerializeField] AnimationClip heroAimPose;
    [SerializeField] TextAsset heroGripData;
    public AnimationClip HeroAimPose => heroAimPose;
    public TextAsset HeroGripData => heroGripData;
    [Tooltip("Kostya reaches under the left lapel and draws (Blender: Source~/Kostya draw.blend).")]
    [SerializeField] AnimationClip draw;
    [Tooltip("The father after the shot: head raised, then sinking (Blender: Source~/Fyodor death.blend).")]
    [SerializeField] AnimationClip fatherDeath;
    Actor lateRig;
    public float DeathDuration => fatherDeath ? fatherDeath.length : 2.5f;

    /// The late father model, used when Kostya shoots him in front of Alexey.
    public void InitializeLate(Transform lateFather)
    {
        var model = lateFather ? lateFather.Find("Hangar Late") : null;
        if (model) lateRig = new Actor(model);
    }

    /// 0: head still up as the picture comes back from black, 1: slumped, the late model's own pose.
    public void FatherDeath(float progress)
    {
        if (lateRig == null || !fatherDeath) return;
        lateRig.Sample(fatherDeath, Mathf.Clamp01(progress) * fatherDeath.length, 1f, true);
    }
    public float TurnDuration => pivot.length;
    public float DrawDuration => draw ? draw.length : .5f;
    /// First frame where the prop is clear of the coat.
    public const float RevealMoment = .51f;
    public float DrawProgress { get; private set; }
    public float FallDuration(bool shot) => shot ? backwardFall.length : restraint.length;
    float performanceStart;
    Transform kostya, father;
    Actor fatherRig, kostyaRig;
    Vector3 kostyaMark, ropeStart, ropeEnd;
    Quaternion ropeRest;
    readonly List<GameObject> bindings = new();
    bool cuffed;
    readonly Dictionary<bool, Vector3> clearance = new();

    public Vector3 KostyaMark => kostyaMark;
    public Transform KostyaBone(string name) => kostyaRig.Get(name);
    public Transform FatherBone(string name) => fatherRig.Has(name) ? fatherRig.Get(name) : null;
    public Material RopeMaterial => looseRope ? looseRope.GetComponentInChildren<Renderer>(true).sharedMaterial : null;
    public Material CuffMaterial => wristCuff ? wristCuff.GetComponentInChildren<Renderer>(true).sharedMaterial : null;

    /// Extra binding pieces (the rebuilt rope) that appear and disappear together with the original ones.
    public void AddBinding(GameObject piece) => bindings.Add(piece);

    public void Initialize(Transform liveFather, Transform brother)
    {
        kostya = brother;
        father = liveFather;
        fatherRig = new Actor(liveFather.Find("Hangar Rescue"));
        kostyaRig = new Actor(kostya.Find("Hangar Kostya"));
        kostyaMark = kostya.localPosition;
        {   // arms hang at rest: the hand continues the forearm, the thumb points forward
            var wrist = kostyaRig.Get("hand.R");
            handAxis = Quaternion.Inverse(wrist.rotation) * (wrist.position - kostyaRig.Get("forearm.R").position).normalized;
            thumbAxis = Quaternion.Inverse(wrist.rotation) * Vector3.ProjectOnPlane(kostya.forward, wrist.rotation * handAxis).normalized;
        }
        foreach (Transform part in liveFather)
            if (part.name == "Rope") bindings.Add(part.gameObject);
        if (looseRope)
        {
            ropeEnd = looseRope.localPosition;
            ropeRest = looseRope.localRotation;
            ropeStart = ropeEnd + new Vector3(-.24f, .82f, .27f);
        }
    }

    public void ResetActors()
    {
        performanceStart = Time.time;
        DrawProgress = 0f;
        kostya.localPosition = kostyaMark;
        kostya.localRotation = Quaternion.identity;
        fatherRig.Reset();
        kostyaRig.Reset();
        Bindings(true);
        SetCuff(false);
    }

    public void Father(float effort, float release)
    {
        fatherRig.Sample(seated, Mathf.Repeat(Time.time - performanceStart, seated.length), .8f);
        fatherRig.Offset("neck", Mathf.Lerp(7f, -3f, effort), effort * 6f, 0);
        fatherRig.Offset("head", Mathf.Lerp(4f, -5f, effort), effort * 8f + release * 3f, 0);
        fatherRig.Offset("forearm.R", -9f * release, 3f * release, 0);
    }

    public void Turn(float progress)
    {
        kostya.localRotation = Quaternion.Euler(0, -155f * Mathf.SmoothStep(0, 1, progress), 0);
        kostya.localPosition = kostyaMark;
        kostyaRig.Sample(pivot, progress * pivot.length);
    }

    public void Conversation(float gesture, float concern)
    {
        float time = Mathf.Repeat((Time.time - performanceStart) * .68f, conversation.length);
        DialoguePose(time, gesture);
        kostyaRig.Offset("head", concern * 7f, concern * 13f, 0);
    }

    float lastGesture, lastDialogueTime;

    void DialoguePose(float time, float gesture)
    {
        lastDialogueTime = time;
        lastGesture = gesture;
        kostyaRig.Sample(conversation, time, .18f);
        float phase = time / conversation.length * Mathf.PI * 2f;
        DialogueArm(".R", 1f, gesture * (.5f - .5f * Mathf.Cos(phase)));
        DialogueArm(".L", -1f, gesture * (.5f + .5f * Mathf.Cos(phase)) * .52f);
    }

    void DialogueArm(string side, float sign, float lift)
    {
        string upper = "upper_arm" + side, lower = "forearm" + side, hand = "hand" + side;
        kostyaRig.RelaxArm(upper, lower, hand);
        Vector3 target = new Vector3(sign * (.304f + .012f * lift),
            .925f + .18f * lift, .065f + .19f * lift);
        kostyaRig.Solve(upper, lower, hand, kostya.TransformPoint(target),
            kostya.TransformDirection(new Vector3(sign * .13f, -.52f, -.95f)), kostya.right * sign);
    }

    /// The cross draw: right hand under the left lapel while the left hand opens the coat, the gun comes out
    /// and the arm extends. The last part bends the arm so the barrel ends on `target`.
    public void Draw(float progress, Transform target, float targetHeight = 1.15f)
    {
        DrawProgress = progress = Mathf.Clamp01(progress);
        if (draw) kostyaRig.Sample(draw, progress * draw.length, .32f);
        else DialoguePose(lastDialogueTime, lastGesture);

        // Keep the body shift from the clip and ease each sleeve along a separate hand path.
        kostyaRig.RelaxArm("upper_arm.R", "forearm.R", "hand.R");
        kostyaRig.RelaxArm("upper_arm.L", "forearm.L", "hand.L");
        float reachCoat = Ease(.02f, .36f, progress);
        float withdraw = Ease(.37f, .64f, progress);
        float aim = Ease(.59f, .96f, progress);
        if (reachCoat <= 0f) return;
        var shoulder = kostyaRig.Get("upper_arm.R");
        var elbow = kostyaRig.Get("forearm.R");
        var wrist = kostyaRig.Get("hand.R");
        Vector3 goal = target ? target.position + Vector3.up * targetHeight : shoulder.position + kostya.forward;
        Vector3 dir = (goal - shoulder.position).normalized;
        float reach = (Vector3.Distance(shoulder.position, elbow.position) + Vector3.Distance(elbow.position, wrist.position)) * .88f;
        Quaternion restWrist = wrist.rotation;
        Vector3 handTarget = Vector3.Lerp(wrist.position,
            kostya.TransformPoint(new Vector3(-.065f, 1.21f, .165f)), reachCoat);
        handTarget = Vector3.Lerp(handTarget, kostya.TransformPoint(new Vector3(.105f, 1.22f, .32f)), withdraw);
        handTarget = Vector3.Lerp(handTarget, shoulder.position + dir * reach, aim);
        var bend = Vector3.Lerp((elbow.position - shoulder.position).normalized,
            kostya.TransformDirection(new Vector3(.48f, -.84f, -.12f)), reachCoat);
        kostyaRig.Solve("upper_arm.R", "forearm.R", "hand.R", handTarget, bend);

        float openCoat = Ease(.04f, .27f, progress) * (1f - Ease(.48f, .81f, progress));
        if (openCoat > 0f)
        {
            var left = kostyaRig.Get("hand.L");
            Vector3 lapel = kostya.TransformPoint(new Vector3(-.185f, 1.26f, .185f));
            kostyaRig.Solve("upper_arm.L", "forearm.L", "hand.L", Vector3.Lerp(left.position, lapel, openCoat),
                kostya.TransformDirection(new Vector3(-.5f, -.85f, -.22f)));
        }
        var gripBasis = Quaternion.LookRotation(handAxis, thumbAxis);
        var lifted = Quaternion.LookRotation((goal - wrist.position).normalized, Vector3.up) * Quaternion.Inverse(gripBasis);
        var tucked = Quaternion.LookRotation(kostya.TransformDirection(new Vector3(.16f, -.78f, .61f)),
            kostya.TransformDirection(new Vector3(-.16f, .60f, .78f))) * Quaternion.Inverse(gripBasis);
        wrist.rotation = Quaternion.Slerp(Quaternion.Slerp(restWrist, tucked, reachCoat), lifted, aim);
        kostyaRig.Offset("head", 0f, Vector3.SignedAngle(kostya.forward, Vector3.ProjectOnPlane(dir, Vector3.up), Vector3.up) * .35f * aim, 0f);
    }

    Vector3 handAxis = Vector3.down, thumbAxis = Vector3.forward;
    /// Direction from wrist to fingers and of the thumb, in the hand bone's space (taken from the rest pose).
    public Vector3 HandAxis => kostyaRig.Get("hand.R").rotation * handAxis;
    public Vector3 ThumbAxis => kostyaRig.Get("hand.R").rotation * thumbAxis;

    public void Reach(float amount)
    {
        // Hold the same underlying dialogue frame while the decision timer runs.
        DialoguePose(lastDialogueTime, lastGesture);
        var target = kostya.TransformPoint(new Vector3(-.045f, 1.31f, .145f));
        var hand = kostyaRig.Get("hand.R");
        kostyaRig.Solve("upper_arm.R", "forearm.R", "hand.R",
            Vector3.Lerp(hand.position, target, amount), kostya.TransformDirection(new Vector3(.9f, -.45f, .23f)));
    }


    public void Subside(bool shot, float progress, Vector3 start, Quaternion facing)
    {
        var clip = shot ? backwardFall : restraint;
        Vector3 shift = Clearance(shot, start, facing);
        // Ease away from the father's chair while going down, so no limb passes through him or the chair.
        kostya.localPosition = start + shift * Ease(0f, .7f, progress);
        kostya.localRotation = facing;
        kostyaRig.Sample(clip, progress * clip.length, 1f, progress >= 1f);
        if (!shot)
        {
            // Going down on command: hands on the back of the head first, then behind the back for the cuffs.
            float onHead = Ease(.03f, .22f, progress) * (1f - Ease(.5f, .7f, progress));
            if (onHead > 0f)
            {
                HandsOnHead(".R", 1f, onHead);
                HandsOnHead(".L", -1f, onHead);
            }
            if (progress > .7f)
            {
                float amount = Ease(.7f, 1f, progress);
                HandsBehind(".R", .045f, amount);
                HandsBehind(".L", -.045f, amount);
            }
        }
    }

    /// How far Kostya's mark must slide (in his parent's space) so the whole fall / takedown clears
    /// the father and his chair. Worked out once per clip from sampled poses.
    Vector3 Clearance(bool shot, Vector3 start, Quaternion facing)
    {
        if (clearance.TryGetValue(shot, out var known)) return known;
        var clip = shot ? backwardFall : restraint;
        Vector3 away = kostya.parent.TransformPoint(start) - father.position;
        away.y = 0f;
        away = away.sqrMagnitude > .001f ? away.normalized : kostya.right;

        var obstacles = new List<Vector3> { father.position + Vector3.up * .45f };
        foreach (var name in new[] { "pelvis", "spine", "spine.003", "head", "thigh.L", "thigh.R", "shin.L", "shin.R", "foot.L", "foot.R", "hand.L", "hand.R" })
            if (fatherRig.Has(name)) obstacles.Add(fatherRig.Get(name).position);

        Vector3 savedPosition = kostya.localPosition;
        Quaternion savedRotation = kostya.localRotation;
        kostya.localPosition = start;
        kostya.localRotation = facing;
        float need = 0f;
        const float gap = .34f;
        for (float t = .1f; t <= 1.001f; t += .1f)
        {
            kostyaRig.Sample(clip, t * clip.length, 1f, true);
            foreach (var bone in kostyaRig.All)
            {
                if (bone.name == "root") continue;
                foreach (var obstacle in obstacles)
                {
                    for (float s = need; s < 1.4f; s += .02f)
                    {
                        Vector3 d = bone.position + away * s - obstacle;
                        d.y = 0f;
                        if (d.magnitude >= gap || Mathf.Abs(bone.position.y - obstacle.y) > .7f) { need = Mathf.Max(need, s); break; }
                    }
                }
            }
        }
        kostya.localPosition = savedPosition;
        kostya.localRotation = savedRotation;
        Vector3 local = kostya.parent.InverseTransformDirection(away) * need;
        local.y = 0f;
        clearance[shot] = local;
        return local;
    }

    void HandsOnHead(string side, float sign, float amount)
    {
        var head = kostyaRig.Get("head");
        Quaternion frame = head.rotation * Quaternion.Inverse(kostyaRig.RestOrientation("head"));
        Vector3 behind = head.position + frame * new Vector3(sign * .075f, .05f, -.085f);
        Vector3 target = Vector3.Lerp(kostyaRig.Get("hand" + side).position, behind, amount);
        kostyaRig.Solve("upper_arm" + side, "forearm" + side, "hand" + side, target,
            frame * new Vector3(sign, .15f, .25f));
    }

    public void OfferWrists(float t)
    {
        foreach (var side in new[] { ".L", ".R" })
        {
            float sign = side == ".R" ? 1 : -1;
            var target = kostya.TransformPoint(new Vector3(sign * .05f, 1.10f, .31f));
            var hand = kostyaRig.Get("hand" + side);
            kostyaRig.Solve("upper_arm" + side, "forearm" + side, "hand" + side,
                Vector3.Lerp(hand.position, target, t), kostya.TransformDirection(new Vector3(sign, -.5f, -.12f)));
        }
    }

    void HandsBehind(string side, float lateral, float amount)
    {
        var pelvis = kostyaRig.Get("pelvis");
        Quaternion body = pelvis.rotation * Quaternion.Inverse(kostyaRig.RestOrientation("pelvis"));
        Vector3 behind = pelvis.position + body * new Vector3(lateral, .105f, -.17f);
        Vector3 target = Vector3.Lerp(kostyaRig.Get("hand" + side).position, behind, amount);
        kostyaRig.Solve("upper_arm" + side, "forearm" + side, "hand" + side, target,
            body * new Vector3(Mathf.Sign(lateral), 0, -.45f));
    }

    public void Bindings(bool tied)
    {
        foreach (var binding in bindings) binding.SetActive(tied);
        if (looseRope) looseRope.gameObject.SetActive(!tied);
    }

    public void DropRope(float t)
    {
        if (!looseRope) return;
        float fall = Mathf.Clamp01(t * 1.16f);
        looseRope.localPosition = Vector3.Lerp(ropeStart, ropeEnd, fall * fall) +
            Vector3.up * (.045f * Mathf.Sin(Mathf.PI * Ease(.84f, 1f, t)));
        looseRope.localRotation = Quaternion.Slerp(Quaternion.Euler(65, -44, 28) * ropeRest,
            ropeRest, Ease(0, .92f, t));
    }

    public void SetCuff(bool visible)
    {
        if (!wristCuff) return;
        cuffed = visible;
        wristCuff.gameObject.SetActive(visible);
        UpdateCuff();
    }

    /// Keeps the linked cuffs locked between Kostya's wrists while his arms move. Call after posing.
    public void UpdateCuff()
    {
        if (!cuffed || !wristCuff) return;
        Vector3 Wrist(string side)
        {
            var hand = kostyaRig.Get("hand" + side);
            var forearm = kostyaRig.Get("forearm" + side);
            return hand.position - (hand.position - forearm.position).normalized * .025f;
        }
        Vector3 left = Wrist(".L"), right = Wrist(".R");
        Vector3 link = right - left;
        Vector3 forearmAxis = (kostyaRig.Get("hand.R").position - kostyaRig.Get("forearm.R").position).normalized;
        if (link.sqrMagnitude < .0001f) link = Vector3.Cross(forearmAxis, Vector3.up);
        // The mesh's chain runs along its local X; the cuff rings open around the forearm direction.
        Vector3 up = Vector3.ProjectOnPlane(forearmAxis, link.normalized);
        wristCuff.SetPositionAndRotation((left + right) * .5f,
            Quaternion.LookRotation(Vector3.Cross(link.normalized, up.sqrMagnitude > .0001f ? up.normalized : Vector3.up), up.sqrMagnitude > .0001f ? up.normalized : Vector3.up));
    }

    static float Ease(float start, float end, float value) =>
        Mathf.SmoothStep(0, 1, Mathf.InverseLerp(start, end, value));

    sealed class Actor
    {
        readonly Transform space;
        readonly Dictionary<string, Transform> bones = new();
        readonly Dictionary<string, Quaternion> rest = new();
        readonly GameObject modelRoot;
        readonly Dictionary<string, Quaternion> previous = new();
        readonly Vector3 restPelvis;
        Vector3 previousPelvis;
        float transitionStart;
        AnimationClip current;
        readonly Dictionary<string, Quaternion> orientations = new();

        public Actor(Transform model)
        {
            space = model.parent;
            modelRoot = model.gameObject;
            var skin = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
            foreach (var bone in skin.bones)
            {
                bones.Add(bone.name, bone);
                rest.Add(bone.name, bone.localRotation);
                previous.Add(bone.name, bone.localRotation);
                orientations.Add(bone.name, Quaternion.Inverse(space.rotation) * bone.rotation);
            }
            restPelvis = bones["pelvis"].localPosition;
        }

        public Transform Get(string name) => bones[name];
        public bool Has(string name) => bones.ContainsKey(name);
        public IEnumerable<Transform> All => bones.Values;
        public void RelaxArm(string upper, string lower, string hand)
        {
            bones[upper].localRotation = rest[upper];
            bones[lower].localRotation = rest[lower];
            bones[hand].localRotation = rest[hand];
        }
        public Quaternion RestOrientation(string name) => orientations[name];
        public void Reset()
        {
            current = null;
            RestoreRest();
        }

        void RestoreRest()
        {
            bones["pelvis"].localPosition = restPelvis;
            foreach (var pair in bones) pair.Value.localRotation = rest[pair.Key];
        }

        public void Sample(AnimationClip clip, float seconds, float strength = 1f, bool snap = false)
        {
            var pelvis = bones["pelvis"];
            if (current != clip)
            {
                foreach (var pair in bones) previous[pair.Key] = pair.Value.localRotation;
                previousPelvis = pelvis.localPosition;
                transitionStart = Time.time;
                current = clip;
            }
            RestoreRest();
            clip.SampleAnimation(modelRoot, seconds);
            float blend = snap ? 1f : Ease(0, .20f, Time.time - transitionStart);
            foreach (var pair in bones)
            {
                var rotation = Quaternion.Slerp(rest[pair.Key], pair.Value.localRotation, strength);
                pair.Value.localRotation = Quaternion.Slerp(previous[pair.Key], rotation, blend);
            }
            pelvis.localPosition = Vector3.Lerp(previousPelvis, pelvis.localPosition, blend);
        }

        public void Offset(string name, float pitch, float yaw, float roll)
        {
            Transform bone = bones[name];
            Quaternion basis = Quaternion.Inverse(bone.parent.rotation) * space.rotation;
            bone.localRotation = basis * Quaternion.Euler(pitch, yaw, roll) * Quaternion.Inverse(basis) * bone.localRotation;
        }

        public void Solve(string upperName, string lowerName, string tipName, Vector3 target, Vector3 bend, Vector3 outward = default)
        {
            Transform upper = bones[upperName], lower = bones[lowerName], tip = bones[tipName];
            float a = Vector3.Distance(upper.position, lower.position);
            float b = Vector3.Distance(lower.position, tip.position);
            Vector3 delta = target - upper.position;
            float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + .001f, a + b - .001f);
            Vector3 direction = delta.normalized;
            float projection = (a * a + distance * distance - b * b) / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(0, a * a - projection * projection));
            Vector3 radial = Vector3.ProjectOnPlane(bend, direction).normalized * height;
            Vector3 lateral = Vector3.ProjectOnPlane(outward, direction);
            if (lateral.sqrMagnitude > .0001f)
            {
                float shift = Mathf.Min(height, Vector3.Dot(radial, lateral.normalized) + .02f / lateral.magnitude);
                Vector3 tangent = Vector3.ProjectOnPlane(radial, lateral.normalized).normalized;
                radial = lateral.normalized * shift + tangent * Mathf.Sqrt(Mathf.Max(0, height * height - shift * shift));
            }
            var knee = direction * projection + radial;
            upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, knee) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(tip.position - lower.position,
                upper.position + direction * distance - lower.position) * lower.rotation;
        }
    }
}

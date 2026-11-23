using System.Collections.Generic;
using UnityEngine;

public sealed class HangarStaging : MonoBehaviour
{
    [SerializeField] Transform looseRope;
    [SerializeField] Transform wristCuff;

    [SerializeField] AnimationClip seated, conversation, pivot, backwardFall, restraint;
    public float TurnDuration => pivot.length;
    public float FallDuration(bool shot) => shot ? backwardFall.length : restraint.length;
    float performanceStart;
    Transform kostya;
    Actor fatherRig, kostyaRig;
    Vector3 kostyaMark, ropeStart, ropeEnd;
    Quaternion ropeRest;
    readonly List<GameObject> bindings = new();

    public Vector3 KostyaMark => kostyaMark;

    public void Initialize(Transform liveFather, Transform brother)
    {
        kostya = brother;
        fatherRig = new Actor(liveFather.Find("Hangar Rescue"));
        kostyaRig = new Actor(kostya.Find("Hangar Kostya"));
        kostyaMark = kostya.localPosition;
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
        float time = Mathf.Repeat(Time.time - performanceStart, conversation.length);
        kostyaRig.Sample(conversation, time, Mathf.Lerp(.32f, .93f, gesture));
        kostyaRig.Offset("head", concern * 7f, concern * 13f, 0);
    }

    public void Reach(float amount)
    {
        // Hold the same underlying dialogue frame while the decision timer runs.
        if (amount <= 0f) reachTime = Mathf.Repeat(Time.time - performanceStart, conversation.length);
        kostyaRig.Sample(conversation, reachTime, .4f);
        var target = kostya.TransformPoint(new Vector3(-.045f, 1.31f, .145f));
        var hand = kostyaRig.Get("hand.R");
        kostyaRig.Solve("upper_arm.R", "forearm.R", "hand.R",
            Vector3.Lerp(hand.position, target, amount), kostya.TransformDirection(new Vector3(.9f, -.45f, .23f)));
    }

    float reachTime;

    public void Subside(bool shot, float progress, Vector3 start, Quaternion facing)
    {
        var clip = shot ? backwardFall : restraint;
        kostya.localPosition = start;
        kostya.localRotation = facing;
        kostyaRig.Sample(clip, progress * clip.length, 1f, progress >= 1f);
        if (!shot && progress > .7f)
        {
            float amount = Ease(.7f, 1f, progress);
            HandsBehind(".R", .045f, amount);
            HandsBehind(".L", -.045f, amount);
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
        wristCuff.gameObject.SetActive(visible);
        if (!visible) return;
        var hand = kostyaRig.Get("hand.R");
        var forearm = kostyaRig.Get("forearm.R");
        Vector3 axis = (hand.position - forearm.position).normalized;
        wristCuff.SetPositionAndRotation(hand.position - axis * .023f, Quaternion.FromToRotation(Vector3.up, axis));
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

        public void Solve(string upperName, string lowerName, string tipName, Vector3 target, Vector3 bend)
        {
            Transform upper = bones[upperName], lower = bones[lowerName], tip = bones[tipName];
            float a = Vector3.Distance(upper.position, lower.position);
            float b = Vector3.Distance(lower.position, tip.position);
            Vector3 delta = target - upper.position;
            float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + .001f, a + b - .001f);
            Vector3 direction = delta.normalized;
            float projection = (a * a + distance * distance - b * b) / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(0, a * a - projection * projection));
            var knee = direction * projection + Vector3.ProjectOnPlane(bend, direction).normalized * height;
            upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, knee) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(tip.position - lower.position,
                upper.position + direction * distance - lower.position) * lower.rotation;
        }
    }
}

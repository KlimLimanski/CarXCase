using System.Collections;
using CarXCase.Hero;
using UnityEngine;

namespace CarXCase.Precinct
{
    /// Restrained conversation acting for a standing humanoid NPC, layered over its idle clip:
    /// looks at the partner, shifts weight from foot to foot (feet stay planted), breathes, nods while
    /// listening, and makes a rare, simple gesture when the line calls for it: the forearm lifts a little
    /// from the hanging arm (a denial, a question, a calming word) or the shoulders shrug.
    /// Every layer fades in and out, so speaking, listening and waiting blend into each other.
    /// Needs the IK pass on the Animator layer.
    [DefaultExecutionOrder(9050)]
    [RequireComponent(typeof(Animator))]
    public sealed class ConversationActing : MonoBehaviour
    {
        public enum Mode { Off, Waiting, Listening, Speaking }
        enum Gesture { None, Lift, SmallLift, Shrug }

        [Tooltip("Face that articulates this character's lines.")] public HeroFace face;
        [Range(0f, 1f)] public float lookWeight = .85f;
        [Tooltip("Sideways hip travel of a weight shift, metres.")] public float weightShift = .022f;
        [Tooltip("Elbow bend of a simple hand lift, degrees.")] public float liftAngle = 24f;
        [Tooltip("How much of the gaze a scripted glance takes (0..1).")] [Range(0f, 1f)] public float glanceWeight = .4f;

        Animator animator;
        Transform chest, neck, head, lShoulder, rShoulder, lUpper, rUpper, lLower, rLower;
        Transform partner, partnerHead, glance;
        HeroFace partnerFace;
        Mode mode = Mode.Off;
        float engage, engageVel;
        Vector3 lookPos; Vector3 lookVel; bool lookInit;
        float shift, shiftVel, shiftTarget, shiftTimer;
        float awayTimer, awayLeft; Vector3 awayOffset;
        float glanceFrom = -1f, glanceUntil;

        // gesture
        Gesture gesture; float gestureAt = -1f, gestureLength; float gestureW, gestureVel;
        // head reactions
        float nod, nodVel, nodTarget; float nodTimer;
        float shakeStart = -10f; float dropUntil; float drop, dropVel;
        float lean, leanVel, leanTarget;
        float tilt, tiltVel;
        float shrug, shrugVel;
        bool partnerAccentLatch;
        string currentLine = "";
        float lineStart;

        static readonly int Standing = Animator.StringToHash("Base Layer.Standing");
        static readonly int Walking = Animator.StringToHash("Base Layer.Walking");

        public static IEnumerator WalkTo(Transform actor, Animator animator, Vector3 destination, float speed)
        {
            destination.y = actor.position.y;
            Vector3 delta = destination - actor.position;
            if (delta.sqrMagnitude < .001f) yield break;
            Quaternion facing = Quaternion.LookRotation(delta);
            while (Quaternion.Angle(actor.rotation, facing) > 1f)
            {
                actor.rotation = Quaternion.RotateTowards(actor.rotation, facing, 280f * Time.deltaTime);
                yield return NoirMenu.GameSession.NextStoryFrame;
            }
            animator.CrossFadeInFixedTime(Walking, .2f);
            while (actor.position != destination)
            {
                actor.position = Vector3.MoveTowards(actor.position, destination, speed * Time.deltaTime);
                yield return NoirMenu.GameSession.NextStoryFrame;
            }
            animator.CrossFadeInFixedTime(Standing, .2f);
        }

        public Mode Current => mode;

        void Awake()
        {
            animator = GetComponent<Animator>();
            chest = animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest);
            neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            lShoulder = animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
            rShoulder = animator.GetBoneTransform(HumanBodyBones.RightShoulder);
            lUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            rUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            lLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            rLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            if (!face) face = GetComponent<HeroFace>();
        }

        // ------------------------------------------------------------------ API

        public void Engage(Transform listener)
        {
            partner = listener;
            partnerFace = null; partnerHead = null;
            if (listener)
            {
                foreach (var f in listener.GetComponentsInChildren<HeroFace>())
                    if (f.receiveHeroDialogue) { partnerFace = f; break; }
                partnerHead = partnerFace ? partnerFace.HeadBone : null;
                if (!partnerHead)
                    foreach (var t in listener.GetComponentsInChildren<Transform>())
                        if (t.name == "Head") { partnerHead = t; break; }
            }
            if (mode == Mode.Off) mode = Mode.Waiting;
        }

        public void Speak(string line, float shownFor, Transform glanceTarget = null, float glanceStart = 0f)
        {
            mode = Mode.Speaking;
            currentLine = line ?? "";
            lineStart = Time.time;
            if (face)
            {
                face.Speak(line, shownFor);
                face.emotion = Mathf.MoveTowards(face.emotion, 0f, .5f);
            }
            glance = glanceTarget;
            glanceFrom = glanceTarget ? Time.time + glanceStart : -1f;
            glanceUntil = glanceFrom + 1.1f;
            PlanGesture(currentLine, shownFor);
            PlanHead(currentLine);
        }

        /// The partner talks: stop speaking, watch him, react to the tone of the line.
        public void Listen(string partnerLine)
        {
            if (face) face.StopSpeaking();
            mode = Mode.Listening;
            currentLine = "";
            gesture = Gesture.None;
            string l = (partnerLine ?? "").ToLowerInvariant();
            bool harsh = l.Contains("!") || l.Contains("разнес") || l.Contains("ареста") || l.Contains("соучастник") || l.Contains("проверк") || l.Contains("понт");
            bool soft = l.Contains("отец") || l.Contains("отца") || l.Contains("семь") || l.Contains("брат") || l.Contains("очередь") || l.Contains("одного");
            leanTarget = harsh ? -1f : soft ? .5f : 0f;   // back from a threat, a little toward a plea
            if (face) face.emotion = harsh ? -.7f : soft ? .25f : -.15f;
            nodTimer = Random.Range(.8f, 1.6f);
        }

        /// Nobody talks (a choice is on screen): keep eye contact, idle quietly.
        public void Wait()
        {
            if (mode == Mode.Off) return;
            if (face) face.StopSpeaking();
            mode = Mode.Waiting;
            leanTarget = 0f;
        }

        public void Disengage(bool immediate = false)
        {
            mode = Mode.Off;
            if (face) { face.StopSpeaking(); face.emotion = 0f; }
            gesture = Gesture.None;
            leanTarget = 0f;
            glance = null;
            if (immediate)
            {
                engage = engageVel = 0f;
                gestureW = shrug = nod = drop = lean = shift = 0f;
            }
        }

        // ------------------------------------------------------------------ planning

        void PlanGesture(string line, float shownFor)
        {
            string l = line.ToLowerInvariant();
            gesture = Gesture.None;
            float at = -1f;
            var track = face ? face.CurrentLine : null;
            float When(string key) => track != null ? track.TimeOf(key) : -1f;

            if ((at = When("клянусь")) >= 0f || (at = When("я не был")) >= 0f || (at = When("думаешь, мне")) >= 0f ||
                (at = When("тише")) >= 0f || (at = When("не делай")) >= 0f || (at = When("не бери")) >= 0f ||
                (at = When("уходи")) >= 0f || (at = When("пошёл")) >= 0f || (at = When("я ухожу")) >= 0f)
                gesture = Gesture.Lift;
            else if ((at = When("может, просто")) >= 0f || (at = When("не знаю")) >= 0f || (at = When("рассосётся")) >= 0f || (at = When("и что с того")) >= 0f)
                gesture = Gesture.Shrug;
            else if (l.Contains("?") && l.Length > 30)
            {
                gesture = Gesture.SmallLift;
                at = track != null ? Mathf.Max(0f, track.TimeOfChar(l.LastIndexOf('?')) - .9f) : shownFor * .5f;
            }
            else if (l.Length > 90 && Random.value < .5f)
            {
                gesture = Gesture.SmallLift;
                at = shownFor * Random.Range(.25f, .45f);
            }
            if (gesture == Gesture.None) return;
            gestureAt = Time.time + Mathf.Max(0f, at - .25f);
            gestureLength = gesture == Gesture.Shrug ? 1.1f : 1.5f;
        }

        void PlanHead(string line)
        {
            var track = face ? face.CurrentLine : null;
            string l = line.ToLowerInvariant();
            shakeStart = -10f;
            float t = track != null ? track.TimeOf("не ") : -1f;
            if (t < 0f && track != null) t = track.TimeOf("нет");
            if (t >= 0f && (l.Contains("не был") || l.Contains("не делай") || l.Contains("не бери") || l.Contains("не знаю") || l.Contains("нет")))
                shakeStart = Time.time + t;
            dropUntil = l.Contains("чёрт с тобой") || l.Contains("не хотел") ? Time.time + 1.4f : 0f;
            leanTarget = l.Contains("!") ? .6f : 0f;
        }

        // ------------------------------------------------------------------ IK pass

        void Update()
        {
            float dt = Time.deltaTime;
            engage = Mathf.SmoothDamp(engage, mode == Mode.Off ? 0f : 1f, ref engageVel, mode == Mode.Off ? .45f : .35f);
            if (engage < .002f && mode == Mode.Off) engage = 0f;

            // weight shift: settle on one foot, then the other, every few seconds
            shiftTimer -= dt;
            if (shiftTimer <= 0f)
            {
                shiftTimer = Random.Range(4.5f, 8.5f);
                shiftTarget = Random.Range(.45f, 1f) * (shiftTarget > 0f ? -1f : 1f);
            }
            shift = Mathf.SmoothDamp(shift, shiftTarget * engage, ref shiftVel, 1.1f);

            // speaking characters glance away briefly; listeners keep eye contact more
            awayTimer -= dt;
            if (awayTimer <= 0f)
            {
                awayTimer = mode == Mode.Listening ? Random.Range(4f, 7f) : Random.Range(3f, 5.5f);
                awayLeft = mode == Mode.Off ? 0f : Random.Range(.6f, 1.1f);
                awayOffset = new Vector3(Random.Range(-.45f, .45f), Random.Range(-.45f, -.2f), 0f);
            }
            awayLeft -= dt;

            // gesture envelope
            float gestureTarget = 0f;
            if (gesture != Gesture.None && gestureAt > 0f && mode == Mode.Speaking)
            {
                float g = Time.time - gestureAt;
                gestureTarget = g < 0f || g > gestureLength ? 0f : 1f;
                if (g > gestureLength + .8f) gesture = Gesture.None;
            }
            gestureW = Mathf.SmoothDamp(gestureW, gestureTarget * engage, ref gestureVel, gestureTarget > gestureW ? .28f : .42f);

            // listening: small nods on the partner's stressed words
            if (mode == Mode.Listening && partnerFace && partnerFace.IsSpeaking)
            {
                nodTimer -= dt;
                bool pulse = partnerFace.IsVoiced && partnerFace.MouthOpen > .6f;
                if (pulse && !partnerAccentLatch && nodTimer <= 0f)
                {
                    nodTarget = Random.Range(.6f, 1f);
                    nodTimer = Random.Range(1.6f, 3.2f);
                }
                partnerAccentLatch = pulse;
            }
            nodTarget = Mathf.MoveTowards(nodTarget, 0f, dt * 2.2f);
            nod = Mathf.SmoothDamp(nod, nodTarget, ref nodVel, .18f);
            drop = Mathf.SmoothDamp(drop, Time.time < dropUntil ? 1f : 0f, ref dropVel, .4f);
            lean = Mathf.SmoothDamp(lean, leanTarget * engage, ref leanVel, .6f);
            float shrugTarget = gesture == Gesture.Shrug ? gestureW : 0f;
            shrug = Mathf.SmoothDamp(shrug, shrugTarget, ref shrugVel, .15f);
            if (mode == Mode.Listening && partnerFace && !partnerFace.IsSpeaking) leanTarget = Mathf.MoveTowards(leanTarget, 0f, dt * .5f);
        }

        void OnAnimatorIK(int layer)
        {
            if (engage <= 0f) return;
            Vector3 fwd = transform.forward, right = transform.right;

            // ---- weight shift with planted feet
            var lFoot = animator.GetIKPosition(AvatarIKGoal.LeftFoot);
            var rFoot = animator.GetIKPosition(AvatarIKGoal.RightFoot);
            var lFootRot = animator.GetIKRotation(AvatarIKGoal.LeftFoot);
            var rFootRot = animator.GetIKRotation(AvatarIKGoal.RightFoot);
            animator.bodyPosition += right * (shift * weightShift) - Vector3.up * (Mathf.Abs(shift) * .006f) - fwd * (lean * .012f);
            animator.bodyRotation = Quaternion.AngleAxis(-shift * 1.6f, fwd) * animator.bodyRotation;
            Plant(AvatarIKGoal.LeftFoot, lFoot, lFootRot);
            Plant(AvatarIKGoal.RightFoot, rFoot, rFootRot);

            // ---- gaze
            Vector3 target = LookTarget();
            if (!lookInit) { lookPos = target; lookInit = true; }
            lookPos = Vector3.SmoothDamp(lookPos, target, ref lookVel, .22f);
            animator.SetLookAtWeight(lookWeight * engage, .18f, .85f, 0f, .55f);
            animator.SetLookAtPosition(lookPos);

        }

        void Plant(AvatarIKGoal goal, Vector3 position, Quaternion rotation)
        {
            animator.SetIKPositionWeight(goal, engage);
            animator.SetIKRotationWeight(goal, engage);
            animator.SetIKPosition(goal, position);
            animator.SetIKRotation(goal, rotation);
        }

        Vector3 LookTarget()
        {
            Vector3 eye = head ? head.position : transform.position + Vector3.up * 1.6f;
            Vector3 target = partnerHead ? partnerHead.position + Vector3.up * .05f
                : partner ? partner.position + Vector3.up * 1.6f
                : eye + transform.forward * 2f;
            if (glance && Time.time >= glanceFrom && Time.time < glanceUntil)
                return Vector3.Lerp(target, glance.position, glanceWeight); // a quick look, not a stare
            if (awayLeft > 0f && mode != Mode.Off)
            {   // a short look down/aside, e.g. at the counter, then back to the partner
                Vector3 toward = (target - eye).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, toward).normalized;
                return target + side * awayOffset.x + Vector3.up * awayOffset.y;
            }
            if (drop > .05f) target += Vector3.down * (.5f * drop);
            return target;
        }

        // ------------------------------------------------------------------ bones after the Animator

        void LateUpdate()
        {
            if (engage <= 0f) return;
            Vector3 right = transform.right, fwd = transform.forward;
            float t = Time.time;
            // breathing and a lean toward / away from the partner
            if (chest)
            {
                float breath = Mathf.Sin(t * 1.55f) * .55f;
                chest.rotation = Quaternion.AngleAxis((breath + lean * 2.2f) * engage, right) * chest.rotation;
            }
            // simple gesture: the forearm lifts a little from the hanging arm, the upper arm follows slightly
            if (gestureW > .001f && gesture != Gesture.None)
            {
                float lift = (gesture == Gesture.SmallLift ? .6f : gesture == Gesture.Shrug ? .45f : 1f) * liftAngle * gestureW;
                if (rUpper) rUpper.rotation = Quaternion.AngleAxis(-lift * .22f, right) * rUpper.rotation;
                if (rLower) rLower.rotation = Quaternion.AngleAxis(-lift, right) * rLower.rotation;
                if (gesture == Gesture.Shrug)
                {
                    if (lUpper) lUpper.rotation = Quaternion.AngleAxis(-lift * .22f, right) * lUpper.rotation;
                    if (lLower) lLower.rotation = Quaternion.AngleAxis(-lift, right) * lLower.rotation;
                }
            }
            // shrug
            if (shrug > .001f)
            {
                if (lShoulder) lShoulder.rotation = Quaternion.AngleAxis(-9f * shrug, fwd) * lShoulder.rotation;
                if (rShoulder) rShoulder.rotation = Quaternion.AngleAxis(9f * shrug, fwd) * rShoulder.rotation;
                if (head) head.rotation = Quaternion.AngleAxis(4f * shrug, fwd) * head.rotation;
            }
            if (!head) return;
            // listening nods, head shake on a denial, head drop on a resigned line, listening tilt
            float shake = 0f;
            float s = t - shakeStart;
            if (s > 0f && s < .75f) shake = Mathf.Sin(s * Mathf.PI * 4f) * 4.2f * Mathf.Sin(s / .75f * Mathf.PI);
            tilt = Mathf.SmoothDamp(tilt, mode == Mode.Listening ? 2.5f : 0f, ref tiltVel, .5f);
            head.rotation = Quaternion.AngleAxis(shake * engage, Vector3.up)
                * Quaternion.AngleAxis((nod * 5f + drop * 7f) * engage, right)
                * Quaternion.AngleAxis(tilt * engage, fwd) * head.rotation;
            if (neck) neck.rotation = Quaternion.AngleAxis(nod * 2f * engage, right) * neck.rotation;
        }
    }
}

using CarXCase.Hero;
using DetectiveGame.Core;
using UnityEngine;

namespace CarXCase.Precinct
{
    /// Seated acting for the woman at the far end of the counter (procedural rig, no Animator).
    /// Every frame the pose is rebuilt from the rest pose: pelvis stays on the stool, hands rest on the
    /// thighs through arm IK, the spine breathes and shifts weight, the head looks where her attention is.
    /// She notices Alexey when he comes near, turns to him in the conversation, articulates her own lines
    /// (procedural jaw and lips), nods while he talks and settles back to her drink afterwards.
    [DefaultExecutionOrder(9100)]
    public sealed class BarWitnessActor : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Arm
        {
            public Transform upper, lower, twist, hand;
            [Tooltip("Wrist target on the thigh, local to this object.")] public Vector3 wristTarget;
            [Tooltip("Where the elbow points, local to this object.")] public Vector3 pole;
            [Tooltip("Direction of the fingers when resting, local.")] public Vector3 fingerDir = new Vector3(0, -.3f, -1);
            [Tooltip("Palm normal when resting, local.")] public Vector3 palmNormal = Vector3.down;
            [Tooltip("Finger direction in the bind pose, local.")] public Vector3 restFinger = Vector3.down;
            [Tooltip("Palm normal in the bind pose, local.")] public Vector3 restPalm = Vector3.left;

            [System.NonSerialized] public Quaternion upperRest, lowerRest, twistRest, handRest;
            [System.NonSerialized] public Vector3 restUpperAim, restForeAim, restHinge;
            [System.NonSerialized] public float upperLength, foreLength;
            [System.NonSerialized] public Vector3 lastHinge;
        }

        [Header("Rig")]
        public Transform hips;
        public Transform spine, chest, neck, head;
        public Arm left = new Arm(), right = new Arm();
        public FaceVisemes visemes;
        [Header("Attention")]
        [Tooltip("What she looks at when alone (her glass on the counter).")] public Transform idleLook;
        public float noticeDistance = 3.2f;
        [Header("Face")]
        [Range(0f, 1f)] public float jawAmount = .7f;
        [Range(0f, 1.5f)] public float lipAmount = 1f;

        static readonly Vector3 Up = Vector3.up;
        static readonly Vector3 Fwd = Vector3.back;          // the mesh faces local -Z
        static readonly Vector3 PitchAxis = Vector3.left;    // + leans/looks down
        Quaternion[] rest;
        Transform[] posed;

        readonly SpeechMouth speech = new SpeechMouth();
        Transform hero, heroHead;
        HeroFace heroFace;
        bool conversation, listening;
        float attention, attentionVel;            // 0 = own thoughts, 1 = looking at Alexey
        float noticeTimer, glanceLeft;
        bool heroNear;
        float yaw, yawVel, pitch, pitchVel;
        float turn, turnVel;                       // torso turned toward Alexey
        float sway, swayVel, swayTarget, swayTimer;
        float nod, nodVel, nodTarget, nodTimer;
        float tilt, tiltVel, tiltTarget;
        float awayLeft, awayTimer;
        bool accentLatch, heroAccentLatch;
        float seed;

        public bool InConversation => conversation;

        void Awake()
        {
            seed = Random.value * 50f;
            posed = new[] { spine, chest, neck, head, left.upper, left.lower, left.twist, left.hand, right.upper, right.lower, right.twist, right.hand };
            rest = new Quaternion[posed.Length];
            for (int i = 0; i < posed.Length; i++) if (posed[i]) rest[i] = posed[i].localRotation;
            CacheArm(left);
            CacheArm(right);
            swayTimer = Random.Range(2f, 5f);
            noticeTimer = 0f;
        }

        void CacheArm(Arm a)
        {
            if (!a.upper || !a.lower || !a.hand) return;
            Quaternion inv = Quaternion.Inverse(transform.rotation);
            a.upperRest = inv * a.upper.rotation;
            a.lowerRest = inv * a.lower.rotation;
            a.twistRest = a.twist ? inv * a.twist.rotation : Quaternion.identity;
            a.handRest = inv * a.hand.rotation;
            Vector3 s = Local(a.upper.position), e = Local(a.lower.position), w = Local(a.hand.position);
            a.upperLength = Vector3.Distance(s, e);
            a.foreLength = Vector3.Distance(e, w);
            a.restUpperAim = (e - s).normalized;
            a.restForeAim = (w - e).normalized;
            a.restHinge = Vector3.Cross(a.restUpperAim, Fwd).normalized;
            a.lastHinge = a.restHinge;
        }

        Vector3 Local(Vector3 world) => transform.InverseTransformPoint(world);

        void Start()
        {
            var officer = FindAnyObjectByType<OfficerController>();
            if (officer) hero = officer.transform;
            foreach (var f in HeroFace.All)
                if (f && f.receiveHeroDialogue) { heroFace = f; heroHead = f.HeadBone; break; }
        }

        // ------------------------------------------------------------------ conversation API

        public void BeginConversation(Transform alexey)
        {
            if (alexey) hero = alexey;
            if (!heroFace)
                foreach (var f in HeroFace.All)
                    if (f && f.receiveHeroDialogue) { heroFace = f; heroHead = f.HeadBone; break; }
            conversation = true;
            listening = false;
            awayTimer = Random.Range(3f, 5f);
        }

        public void Speak(string line, float shownFor)
        {
            listening = false;
            speech.Begin(line, shownFor);
            tiltTarget = line != null && line.Contains("?") ? -3.5f : 1.5f;
        }

        /// Alexey talks: she stops, watches him and nods now and then.
        public void Listen()
        {
            speech.Stop();
            listening = true;
            tiltTarget = 3f;
            nodTimer = Random.Range(.7f, 1.4f);
        }

        /// The player is choosing an answer: she waits, eyes on him.
        public void Wait()
        {
            speech.Stop();
            listening = false;
            tiltTarget = 2f;
        }

        public void EndConversation()
        {
            speech.Stop();
            conversation = listening = false;
            tiltTarget = 0f;
            noticeTimer = Random.Range(1.6f, 2.4f); // keeps looking at him for a moment, then back to the glass
            glanceLeft = noticeTimer;
        }

        void OnDisable()
        {
            speech.ResetFace();
            if (visemes) visemes.Apply(0f, 0f, 0f);
        }

        // ------------------------------------------------------------------ per frame

        void LateUpdate()
        {
            if (rest == null) return;
            float dt = Time.deltaTime, t = Time.time + seed;
            for (int i = 0; i < posed.Length; i++) if (posed[i]) posed[i].localRotation = rest[i];

            UpdateAttention(dt);
            speech.Tick(dt);
            if (visemes) visemes.Apply(speech.Open * jawAmount, speech.Wide * lipAmount, speech.Round * lipAmount);

            // ---- reactions
            if (speech.Accent > .6f && !accentLatch) { nodTarget = Mathf.Max(nodTarget, .7f); accentLatch = true; }
            if (speech.Accent < .2f) accentLatch = false;
            if (listening && heroFace && heroFace.IsSpeaking)
            {
                nodTimer -= dt;
                bool pulse = heroFace.IsVoiced && heroFace.MouthOpen > .55f;
                if (pulse && !heroAccentLatch && nodTimer <= 0f)
                {
                    nodTarget = Random.Range(.6f, 1f);
                    nodTimer = Random.Range(1.8f, 3.4f);
                }
                heroAccentLatch = pulse;
            }
            nodTarget = Mathf.MoveTowards(nodTarget, 0f, dt * 2.4f);
            nod = Mathf.SmoothDamp(nod, nodTarget, ref nodVel, .16f);
            tilt = Mathf.SmoothDamp(tilt, conversation ? tiltTarget : 0f, ref tiltVel, .6f);

            // ---- weight shift: she settles slowly from one side to the other
            swayTimer -= dt;
            if (swayTimer <= 0f) { swayTimer = Random.Range(5f, 9f); swayTarget = Random.Range(.4f, 1f) * (swayTarget > 0f ? -1f : 1f); }
            sway = Mathf.SmoothDamp(sway, swayTarget, ref swayVel, 1.4f);

            float breath = Mathf.Sin(t * 1.45f);
            bool speaking = speech.Active;
            float talkSway = speaking ? (Mathf.PerlinNoise(t * .9f, seed) - .5f) * 3f : 0f;

            // ---- torso (pelvis stays on the stool)
            turn = Mathf.SmoothDamp(turn, conversation ? Mathf.Clamp(yaw * .35f, -16f, 16f) : 0f, ref turnVel, .7f);
            if (spine) spine.localRotation = rest[0] * Rot(2.5f + breath * .25f, turn * .45f, sway * 1.3f);
            if (chest) chest.localRotation = rest[1] * Rot(1.5f + breath * .6f + (conversation ? 1.5f : 0f), turn * .55f, -sway * .7f);

            // ---- head and neck: what she looks at, plus nods and tilts
            float rest_ = (yaw - turn);
            if (neck) neck.localRotation = rest[2] * Rot(-pitch * .4f + nod * 2f - 1f, rest_ * .4f, tilt * .4f);
            if (head) head.localRotation = rest[3] * Rot(-pitch * .6f + nod * 5f - 3f, rest_ * .6f + talkSway, tilt * .6f + talkSway * .3f);

            // ---- hands on the thighs
            SolveArm(left, -1f);
            SolveArm(right, 1f);
        }

        /// pitch (+ = forward/down), yaw (+ = to her right), roll in degrees, in her own frame.
        static Quaternion Rot(float pitchDeg, float yawDeg, float rollDeg) =>
            Quaternion.AngleAxis(yawDeg, Up) * Quaternion.AngleAxis(pitchDeg, PitchAxis) * Quaternion.AngleAxis(rollDeg, Fwd);

        void UpdateAttention(float dt)
        {
            Vector3 heroPoint = heroHead ? heroHead.position : hero ? hero.position + Vector3.up * 1.6f : Vector3.zero;
            bool heroValid = hero && hero.gameObject.activeInHierarchy;
            Vector3 eye = head ? head.position : transform.position + Vector3.up * 1.5f;
            float distance = heroValid ? Vector3.Distance(Flat(eye), Flat(heroPoint)) : 99f;
            Vector3 heroDir = transform.InverseTransformDirection(heroPoint - eye);
            float heroYaw = Vector3.SignedAngle(Fwd, Vector3.ProjectOnPlane(heroDir, Up), Up);

            // noticing: a glance when he comes close, then now and then while he stays near
            bool near = heroValid && distance < noticeDistance && Mathf.Abs(heroYaw) < 115f;
            if (near && !heroNear) { glanceLeft = Random.Range(2f, 3f); noticeTimer = glanceLeft + Random.Range(4f, 7f); }
            heroNear = near;
            if (!conversation)
            {
                glanceLeft -= dt;
                noticeTimer -= dt;
                if (near && noticeTimer <= 0f) { glanceLeft = Random.Range(1.4f, 2.4f); noticeTimer = glanceLeft + Random.Range(5f, 9f); }
            }
            bool watching = heroValid && (conversation || glanceLeft > 0f);

            // while she talks she sometimes looks down at her glass for a second
            if (conversation && speech.Active)
            {
                awayTimer -= dt;
                if (awayTimer <= 0f) { awayLeft = Random.Range(.7f, 1.1f); awayTimer = Random.Range(3.5f, 6f); }
            }
            awayLeft -= dt;
            if (awayLeft > 0f) watching = false;

            attention = Mathf.SmoothDamp(attention, watching ? 1f : 0f, ref attentionVel, .25f);
            Vector3 idle = idleLook ? idleLook.position : transform.TransformPoint(new Vector3(0f, 1.1f, -.75f));
            Vector3 target = Vector3.Lerp(idle, heroPoint, watching ? 1f : 0f);
            Vector3 d = transform.InverseTransformDirection(target - eye);
            float wantYaw = Mathf.Clamp(Vector3.SignedAngle(Fwd, Vector3.ProjectOnPlane(d, Up), Up), -75f, 75f);
            float wantPitch = Mathf.Clamp(Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg, -30f, 18f);
            float smooth = conversation ? .3f : .45f;
            yaw = Mathf.SmoothDampAngle(yaw, wantYaw, ref yawVel, smooth);
            pitch = Mathf.SmoothDampAngle(pitch, wantPitch, ref pitchVel, smooth);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        void SolveArm(Arm a, float side)
        {
            if (!a.upper || !a.lower || !a.hand || a.upperLength <= 0f) return;
            Quaternion root = transform.rotation;
            Vector3 s = Local(a.upper.position);
            Vector3 w = a.wristTarget;
            float la = a.upperLength, lb = a.foreLength;
            Vector3 d = w - s;
            float dist = Mathf.Clamp(d.magnitude, .02f, la + lb - .002f);
            Vector3 dir = d.normalized;
            Vector3 poleDir = Vector3.ProjectOnPlane(a.pole - s, dir).normalized;
            if (poleDir.sqrMagnitude < .5f) poleDir = Vector3.ProjectOnPlane(Vector3.back, dir).normalized;
            float cos = Mathf.Clamp((la * la + dist * dist - lb * lb) / (2f * la * dist), -1f, 1f);
            Vector3 elbow = s + Quaternion.AngleAxis(Mathf.Acos(cos) * Mathf.Rad2Deg, Vector3.Cross(dir, poleDir)) * dir * la;
            Vector3 wrist = s + dir * dist;

            Vector3 upperAim = (elbow - s).normalized, foreAim = (wrist - elbow).normalized;
            Vector3 hinge = Vector3.Cross(upperAim, foreAim);
            hinge = hinge.sqrMagnitude > 1e-5f ? hinge.normalized : a.lastHinge;
            a.lastHinge = hinge;

            Quaternion upper = Frame(upperAim, hinge) * Quaternion.Inverse(Frame(a.restUpperAim, a.restHinge)) * a.upperRest;
            Quaternion fore = Frame(foreAim, hinge) * Quaternion.Inverse(Frame(a.restForeAim, a.restHinge)) * a.lowerRest;
            a.upper.rotation = root * upper;
            a.lower.rotation = root * fore;

            // the forearm rolls the palm down onto the thigh; the twist bone carries it so the wrist does not wrap
            Vector3 palm = a.palmNormal.normalized;
            Quaternion carried = fore * Quaternion.Inverse(a.lowerRest);
            Vector3 carriedPalm = carried * a.restPalm.normalized;
            float roll = Vector3.SignedAngle(Vector3.ProjectOnPlane(carriedPalm, foreAim), Vector3.ProjectOnPlane(palm, foreAim), foreAim);
            if (a.twist) a.twist.rotation = root * (Quaternion.AngleAxis(roll, foreAim) * carried * a.twistRest);
            Quaternion hand = Frame(a.fingerDir.normalized, -palm) * Quaternion.Inverse(Frame(a.restFinger.normalized, -a.restPalm.normalized)) * a.handRest;
            a.hand.rotation = root * hand;
        }

        static Quaternion Frame(Vector3 aim, Vector3 up) => Quaternion.LookRotation(aim, up);

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            foreach (var a in new[] { left, right })
            {
                Gizmos.DrawWireSphere(transform.TransformPoint(a.wristTarget), .015f);
                Gizmos.DrawLine(transform.TransformPoint(a.wristTarget), transform.TransformPoint(a.wristTarget + a.fingerDir.normalized * .15f));
                Gizmos.DrawWireSphere(transform.TransformPoint(a.pole), .02f);
            }
        }
    }
}

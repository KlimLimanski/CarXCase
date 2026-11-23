using System.Collections.Generic;
using UnityEngine;

namespace CarXCase.Hero
{
    /// Subtle procedural acting on top of the Animator:
    /// speech articulation (jaw and lips) driven by the subtitle text or a voice clip, soft brow
    /// expressions, slow head drift with accents on stressed syllables, light hand beats.
    /// The face returns to neutral as soon as a line ends, is skipped or is interrupted.
    /// Runs after the Animator and before HeroArmIK.
    [DefaultExecutionOrder(9000)]
    public sealed class HeroFace : MonoBehaviour
    {
        public static readonly List<HeroFace> All = new List<HeroFace>();

        /// Generic talking without a known line (old cutscenes). Lines with text should use SpeakAsHero.
        public static void SetTalking(bool on)
        {
            foreach (var f in All)
                if (f && f.receiveHeroDialogue)
                {
                    f.talking = on;
                    if (!on) f.StopSpeaking();
                }
        }

        /// -1 = frown, 0 = neutral, +1 = raised brows
        public static void SetEmotion(float e) { foreach (var f in All) if (f && f.receiveHeroDialogue) f.emotion = Mathf.Clamp(e, -1, 1); }

        /// Alexey says a line: the mouth follows the text over the time it stays on screen (or the voice clip).
        public static void SpeakAsHero(string line, float shownFor, AudioClip voice = null)
        {
            foreach (var f in All) if (f && f.receiveHeroDialogue) f.Speak(line, shownFor, voice);
        }

        /// The current hero line ended, was skipped or interrupted: back to the neutral face.
        public static void StopHero()
        {
            foreach (var f in All) if (f && f.receiveHeroDialogue) f.StopSpeaking();
        }

        public bool talking;
        public bool receiveHeroDialogue = true;
        [SerializeField] SkinnedMeshRenderer mouthMesh;
        [SerializeField] string mouthShape = "JawOpen";
        [Tooltip("Procedural lip shapes (and jaw shape) for this head. Optional.")]
        [SerializeField] FaceVisemes visemes;
        [Range(-1, 1)] public float emotion;
        public bool armGestures = true;
        [Range(0, 2)] public float intensity = 1;
        [Tooltip("Jaw opening on a full syllable, degrees.")] public float maxJawAngle = 5.5f;
        [Tooltip("Weight (0..1) of the mouth blend shape on a full syllable.")]
        [Range(0, 1)] public float maxMouthShape = 1f;
        [Tooltip("Lip spreading/rounding on top of the jaw.")]
        [Range(0, 1.5f)] public float lipAmount = 1f;
        [Tooltip("Brow travel, metres.")] public float browTravel = 0.0022f;

        Transform jaw, browL, browR, head, neck, chest, rArm, rFore, root;
        Quaternion jawRest;
        float blinkAt;
        int mouthShapeIndex = -1;
        readonly SpeechMouth speech = new SpeechMouth();
        AudioSource voice;

        /// Applies an offset on top of whatever the Animator wrote this frame.
        /// If the Animator did not touch the bone (constant curve), the previous offset is undone first,
        /// so offsets never accumulate.
        sealed class Layer
        {
            readonly Transform t; Quaternion lastSet, lastBase; bool has;
            public Layer(Transform t) { this.t = t; }
            public bool Valid => t;
            public void Begin()
            {
                if (!t) return;
                if (has && Quaternion.Angle(t.localRotation, lastSet) < 0.01f) t.localRotation = lastBase;
                lastBase = t.localRotation;
            }
            public void End() { if (!t) return; lastSet = t.localRotation; has = true; }
            public Transform T => t;
        }
        Layer lHead, lNeck, lChest, lArm, lFore;
        Vector3 browLBase, browRBase;
        float open, openVel, emo, talkBlend, seed;
        // generic speech generator (no text)
        float sylTimer, sylTarget, sylLeftInWord; float accent, accentVel;
        bool accentLatch;
        Vector3 headOff, headOffVel; float gesture, gestureVel, gestureTarget, gestureTimer;
        int spokenPhrases;

        public bool IsSpeaking => speech.Active || talking;
        /// True only while syllables are being spoken (false in pauses).
        public bool IsVoiced => speech.Voiced || (talking && sylTarget > 0f);
        public SpeechTrack CurrentLine => speech.Active ? speech.Track : null;
        public float LineTime => speech.Elapsed;
        public float MouthOpen => open;
        public Transform HeadBone => head;

        public void SetSpeech(bool speaking)
        {
            if (speaking && !talking) Kick();
            talking = speaking;
            if (!speaking) StopSpeaking();
        }

        /// Says one line. The mouth articulates the text over the time it is shown, or follows the clip.
        public void Speak(string line, float shownFor, AudioClip clip = null)
        {
            if (string.IsNullOrWhiteSpace(line) || line.Trim() == "…")
            {
                StopSpeaking();
                return;
            }
            if (!speech.Active) Kick();
            AudioSource source = null;
            if (clip)
            {
                source = Voice();
                source.clip = clip;
                source.Play();
                shownFor = Mathf.Max(shownFor, clip.length + .2f);
            }
            else if (voice && voice.isPlaying) voice.Stop();
            speech.Begin(line, shownFor, source);
        }

        public void StopSpeaking()
        {
            speech.Stop();
            if (voice && voice.isPlaying) voice.Stop();
        }

        void Kick()
        {
            if (++spokenPhrases % 2 != 1) return;
            gestureTarget = .68f;
            gestureTimer = 1.35f;
            accent = .7f;
        }

        AudioSource Voice()
        {
            if (voice) return voice;
            voice = (head ? head : transform).gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = .8f;
            voice.minDistance = 1.5f;
            return voice;
        }

        void Awake() => BindRig();

        void BindRig()
        {
            root = transform;
            var hierarchy = GetComponentsInChildren<Transform>(true);
            Transform F(string n) { foreach (var t in hierarchy) if (t.name == n) return t; return null; }
            jaw = F("Jaw"); browL = F("Brow_L"); browR = F("Brow_R"); head = F("Head"); neck = F("Neck"); chest = F("UpperChest");
            rArm = F("R_UpperArm"); rFore = F("R_LowerArm");
            var rig = GetComponentInChildren<Animator>(true);
            if (rig && rig.isHuman && rig.avatar && rig.avatar.isValid)
            {
                head ??= rig.GetBoneTransform(HumanBodyBones.Head);
                neck ??= rig.GetBoneTransform(HumanBodyBones.Neck);
                chest ??= rig.GetBoneTransform(HumanBodyBones.UpperChest) ?? rig.GetBoneTransform(HumanBodyBones.Chest);
                rArm ??= rig.GetBoneTransform(HumanBodyBones.RightUpperArm);
                rFore ??= rig.GetBoneTransform(HumanBodyBones.RightLowerArm);
            }
            if (jaw) jawRest = jaw.localRotation;
            if (!visemes && receiveHeroDialogue)
            {
                foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (!FaceVisemes.HasMouthShapes(renderer.sharedMesh)) continue;
                    visemes = GetComponent<FaceVisemes>();
                    if (!visemes) visemes = gameObject.AddComponent<FaceVisemes>();
                    visemes.body = renderer;
                    break;
                }
            }
            if (visemes) visemes.Build();
            if (mouthMesh && mouthMesh.sharedMesh)
                mouthShapeIndex = mouthMesh.sharedMesh.GetBlendShapeIndex(mouthShape);
            lHead = new Layer(head); lNeck = new Layer(neck); lChest = new Layer(chest); lArm = new Layer(rArm); lFore = new Layer(rFore);
            if (browL) browLBase = browL.localPosition;
            if (browR) browRBase = browR.localPosition;
            seed = Random.value * 100;
            blinkAt = Time.time + Random.Range(1.6f, 4f);
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }

        void OnDisable()
        {
            All.Remove(this);
            speech.ResetFace();
            if (voice) voice.Stop();
            open = openVel = 0f;
            talking = false;
            ApplyMouth(0f, 0f, 0f);
            if (visemes) visemes.ApplyExpression(0f, 0f);
            if (browL) browL.localPosition = browLBase;
            if (browR) browR.localPosition = browRBase;
        }

        void NextSyllable()
        {
            if (sylLeftInWord <= 0)
            {   // gap between words (sometimes a longer breath)
                sylLeftInWord = Random.Range(1, 4);
                sylTarget = 0;
                sylTimer = Random.value < .2f ? Random.Range(.35f, .6f) : Random.Range(.08f, .18f);
                return;
            }
            sylLeftInWord--;
            sylTarget = Random.Range(.35f, 1f);
            sylTimer = Random.Range(.11f, .2f);
            if (sylTarget > .85f) accent = 1; // stressed syllable: small head accent
        }

        void ApplyMouth(float jawOpen, float wide, float round)
        {
            if (receiveHeroDialogue && visemes && visemes.HasAuthoredMouth) jawOpen *= .52f;
            if (jaw)
                jaw.localRotation = visemes && visemes.HasAuthoredMouth
                    ? jawRest
                    : jawRest * Quaternion.Euler(jawOpen * maxJawAngle, 0, 0);
            if (visemes && visemes.Ready)
            {
                visemes.Apply(jawOpen, wide * lipAmount, round * lipAmount);
                if (visemes.HasJaw) return;
            }
            if (mouthShapeIndex >= 0 && mouthMesh)
                mouthMesh.SetBlendShapeWeight(mouthShapeIndex, jawOpen * maxMouthShape * 100f);
        }

        void LateUpdate()
        {
            if (lHead == null) BindRig();
            float dt = Time.deltaTime, t = Time.time + seed, k = intensity;
            lHead.Begin(); lNeck.Begin(); lChest.Begin(); lArm.Begin(); lFore.Begin();

            speech.Tick(dt);
            bool generic = talking && !speech.Active;
            bool speaking = speech.Active || talking;
            talkBlend = Mathf.MoveTowards(talkBlend, speaking ? 1 : 0, dt * 3);
            emo = Mathf.Lerp(emo, emotion, 1 - Mathf.Exp(-dt * 2));

            // ---- jaw and lips
            float want;
            if (generic)
            {
                sylTimer -= dt;
                if (sylTimer <= 0) NextSyllable();
                // alternate open/close inside a syllable: open for the first 60%, close after
                want = sylTarget * (sylTimer > .06f ? 1 : .25f);
            }
            else
            {
                sylTarget = 0; sylLeftInWord = 0; sylTimer = 0;
                want = speech.Open;
                // stressed syllables give a small nod and brow lift
                if (speech.Accent > .6f && !accentLatch) { accent = Mathf.Max(accent, .9f); accentLatch = true; }
                if (speech.Accent < .2f) accentLatch = false;
            }
            open = Mathf.SmoothDamp(open, want, ref openVel, generic ? .055f : .025f);
            if (!speaking && open < .002f) { open = 0f; openVel = 0f; }
            ApplyMouth(open, generic ? 0f : speech.Wide, generic ? 0f : speech.Round);

            // ---- brows: expression + rare lift on accents
            accent = Mathf.SmoothDamp(accent, 0, ref accentVel, .25f);
            float up = (emo * .9f + accent * .35f * talkBlend + Mathf.Sin(t * .35f) * .08f) * browTravel;
            if (browL) browL.localPosition = browLBase + Vector3.up * up + Vector3.right * (-emo * browTravel * .25f);
            if (browR) browR.localPosition = browRBase + Vector3.up * up + Vector3.right * (emo * browTravel * .25f);
            if (visemes && !receiveHeroDialogue)
            {
                float phase = (Time.time - blinkAt) / .17f;
                if (phase > 1f) blinkAt = Time.time + Random.Range(2f, 5f);
                float blink = phase > 0f && phase <= 1f ? Mathf.Sin(phase * Mathf.PI) : 0f;
                visemes.ApplyExpression(blink, emo * .8f + accent * .28f * talkBlend);
            }

            // ---- head: slow drift, gentle accent nods while talking
            Vector3 drift = new Vector3((Mathf.PerlinNoise(t * .18f, seed) - .5f) * 3f, (Mathf.PerlinNoise(t * .15f, seed + 9) - .5f) * 5f, (Mathf.PerlinNoise(t * .12f, seed + 17) - .5f) * 2.5f);
            Vector3 speechNod = new Vector3(accent * 2.2f - emo * 1.5f, 0, 0) * talkBlend;
            headOff = Vector3.SmoothDamp(headOff, drift + speechNod, ref headOffVel, .35f);
            if (head) head.localRotation *= Quaternion.Euler(headOff * k);
            if (neck) neck.localRotation *= Quaternion.Euler(headOff * .35f * k);

            // ---- chest: barely noticeable lean while talking
            if (chest) chest.rotation = Quaternion.AngleAxis(talkBlend * 1.2f * k, root.right) * chest.rotation;

            // ---- right hand: occasional slow beat gesture while talking
            if (!armGestures) { Finish(); return; }
            gestureTimer -= dt;
            if (gestureTimer <= 0) { gestureTimer = Random.Range(1.2f, 2.6f); gestureTarget = speaking && Random.value < .55f ? Random.Range(.5f, 1f) : 0; }
            gesture = Mathf.SmoothDamp(gesture, speaking ? gestureTarget : 0, ref gestureVel, .45f);
            if (rArm) rArm.rotation = Quaternion.AngleAxis(-10f * gesture * k, root.right) * rArm.rotation;
            if (rFore) rFore.rotation = Quaternion.AngleAxis((-28f - accent * 6f) * gesture * k, root.right) * rFore.rotation;
            Finish();
        }

        void Finish() { lHead.End(); lNeck.End(); lChest.End(); lArm.End(); lFore.End(); }
    }
}

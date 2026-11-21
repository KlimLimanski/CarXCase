using System.Collections.Generic;
using UnityEngine;

namespace CarXCase.Hero
{
    /// Subtle procedural acting on top of the Animator:
    /// speech-like jaw motion (syllables, words, pauses), soft brow expressions,
    /// slow head drift with accents on stressed syllables, light hand beats.
    /// Runs after the Animator and before HeroArmIK.
    [DefaultExecutionOrder(9000)]
    public sealed class HeroFace : MonoBehaviour
    {
        public static readonly List<HeroFace> All = new List<HeroFace>();
        public static void SetTalking(bool on) { foreach (var f in All) if (f && f.receiveHeroDialogue) f.talking = on; }
        /// -1 = frown, 0 = neutral, +1 = raised brows
        public static void SetEmotion(float e) { foreach (var f in All) if (f && f.receiveHeroDialogue) f.emotion = Mathf.Clamp(e, -1, 1); }

        public bool talking;
        public bool receiveHeroDialogue = true;
        [SerializeField] SkinnedMeshRenderer mouthMesh;
        [SerializeField] string mouthShape = "JawOpen";
        [Range(-1, 1)] public float emotion;
        public bool armGestures = true;
        [Range(0, 2)] public float intensity = 1;
        [Tooltip("Jaw opening on a full syllable, degrees.")] public float maxJawAngle = 5.5f;
        [Tooltip("Brow travel, metres.")] public float browTravel = 0.0022f;

        Transform jaw, browL, browR, head, neck, chest, rArm, rFore, root;
        Quaternion jawRest;
        int mouthShapeIndex = -1;

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
        // speech generator
        float sylTimer, sylTarget, sylLeftInWord; float accent, accentVel;
        Vector3 headOff, headOffVel; float gesture, gestureVel, gestureTarget, gestureTimer;

        void Awake()
        {
            root = transform;
            Transform F(string n) { foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == n) return t; return null; }
            jaw = F("Jaw"); browL = F("Brow_L"); browR = F("Brow_R"); head = F("Head"); neck = F("Neck"); chest = F("UpperChest");
            rArm = F("R_UpperArm"); rFore = F("R_LowerArm");
            if (jaw) jawRest = jaw.localRotation;
            if (mouthMesh && mouthMesh.sharedMesh)
                mouthShapeIndex = mouthMesh.sharedMesh.GetBlendShapeIndex(mouthShape);
            lHead = new Layer(head); lNeck = new Layer(neck); lChest = new Layer(chest); lArm = new Layer(rArm); lFore = new Layer(rFore);
            if (browL) browLBase = browL.localPosition;
            if (browR) browRBase = browR.localPosition;
            seed = Random.value * 100;
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

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

        void LateUpdate()
        {
            float dt = Time.deltaTime, t = Time.time + seed, k = intensity;
            lHead.Begin(); lNeck.Begin(); lChest.Begin(); lArm.Begin(); lFore.Begin();
            talkBlend = Mathf.MoveTowards(talkBlend, talking ? 1 : 0, dt * 3);
            emo = Mathf.Lerp(emo, emotion, 1 - Mathf.Exp(-dt * 2));

            // ---- jaw
            if (talking) { sylTimer -= dt; if (sylTimer <= 0) NextSyllable(); }
            else { sylTarget = 0; sylLeftInWord = 0; sylTimer = 0; }
            // alternate open/close inside a syllable: open for the first 60%, close after
            float want = sylTarget * (sylTimer > .06f ? 1 : .25f);
            open = Mathf.SmoothDamp(open, want, ref openVel, .055f);
            if (jaw) jaw.localRotation = jawRest * Quaternion.Euler(open * maxJawAngle, 0, 0);
            if (mouthShapeIndex >= 0) mouthMesh.SetBlendShapeWeight(mouthShapeIndex, open * 100f);

            // ---- brows: expression + rare lift on accents
            accent = Mathf.SmoothDamp(accent, 0, ref accentVel, .25f);
            float up = (emo * .9f + accent * .35f * talkBlend + Mathf.Sin(t * .35f) * .08f) * browTravel;
            if (browL) browL.localPosition = browLBase + Vector3.up * up + Vector3.right * (-emo * browTravel * .25f);
            if (browR) browR.localPosition = browRBase + Vector3.up * up + Vector3.right * (emo * browTravel * .25f);

            // ---- head: slow drift, gentle accent nods while talking
            Vector3 drift = new Vector3((Mathf.PerlinNoise(t * .18f, seed) - .5f) * 3f, (Mathf.PerlinNoise(t * .15f, seed + 9) - .5f) * 5f, (Mathf.PerlinNoise(t * .12f, seed + 17) - .5f) * 2.5f);
            Vector3 speech = new Vector3(accent * 2.2f - emo * 1.5f, 0, 0) * talkBlend;
            headOff = Vector3.SmoothDamp(headOff, drift + speech, ref headOffVel, .35f);
            if (head) head.localRotation *= Quaternion.Euler(headOff * k);
            if (neck) neck.localRotation *= Quaternion.Euler(headOff * .35f * k);

            // ---- chest: barely noticeable lean while talking
            if (chest) chest.rotation = Quaternion.AngleAxis(talkBlend * 1.2f * k, root.right) * chest.rotation;

            // ---- right hand: occasional slow beat gesture while talking
            if (!armGestures) { Finish(); return; }
            gestureTimer -= dt;
            if (gestureTimer <= 0) { gestureTimer = Random.Range(1.2f, 2.6f); gestureTarget = talking && Random.value < .55f ? Random.Range(.5f, 1f) : 0; }
            gesture = Mathf.SmoothDamp(gesture, talking ? gestureTarget : 0, ref gestureVel, .45f);
            if (rArm) rArm.rotation = Quaternion.AngleAxis(-10f * gesture * k, root.right) * rArm.rotation;
            if (rFore) rFore.rotation = Quaternion.AngleAxis((-28f - accent * 6f) * gesture * k, root.right) * rFore.rotation;
            Finish();
        }

        void Finish() { lHead.End(); lNeck.End(); lChest.End(); lArm.End(); lFore.End(); }
    }
}

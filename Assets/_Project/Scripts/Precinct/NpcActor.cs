using UnityEngine;

namespace CarXCase.Precinct
{
    /// Cheap "acting" for greybox characters: a simple face (eyes, brows, mouth) that blinks and talks,
    /// breathing, head motion and hand gestures while talking, fidgeting under stress.
    public sealed class NpcActor : MonoBehaviour
    {
        public Transform head, torso;
        public bool talking;
        [Range(0, 100)] public float stress;
        public Color faceColor = new Color(.08f, .06f, .05f);

        Vector3 headPos, torsoScale; Quaternion headRot; float seed;
        Transform mouth, eyeL, eyeR, browL, browR, armL, armR;
        Quaternion armLRot, armRRot; Vector3 mouthScale, eyeScale;
        float open, openVel, nextBlink, blink, talkBlend;

        void Start()
        {
            if (!head) head = transform.Find("Head");
            if (!torso) torso = transform.Find("Torso");
            armL = transform.Find("Arm_L"); armR = transform.Find("Arm_R");
            if (armL) armLRot = armL.localRotation; if (armR) armRRot = armR.localRotation;
            if (head) { headPos = head.localPosition; headRot = head.localRotation; BuildFace(); }
            if (torso) torsoScale = torso.localScale;
            seed = Random.value * 10; nextBlink = Time.time + Random.Range(1f, 4f);
        }

        void BuildFace()
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); mat.color = faceColor;
            Transform Part(string n, PrimitiveType type, Vector3 p, Vector3 s)
            {
                var go = GameObject.CreatePrimitive(type); go.name = n; Destroy(go.GetComponent<Collider>());
                go.GetComponent<Renderer>().sharedMaterial = mat;
                go.transform.SetParent(head, false); go.transform.localPosition = p; go.transform.localScale = s;
                return go.transform;
            }
            // head is a unit sphere scaled per axis: positions are in its local space (radius 0.5, +Z = face)
            eyeL = Part("Eye_L", PrimitiveType.Sphere, new Vector3(-.19f, .1f, .44f), new Vector3(.12f, .09f, .06f));
            eyeR = Part("Eye_R", PrimitiveType.Sphere, new Vector3(.19f, .1f, .44f), new Vector3(.12f, .09f, .06f));
            browL = Part("Brow_L", PrimitiveType.Cube, new Vector3(-.19f, .25f, .45f), new Vector3(.24f, .04f, .04f));
            browR = Part("Brow_R", PrimitiveType.Cube, new Vector3(.19f, .25f, .45f), new Vector3(.24f, .04f, .04f));
            mouth = Part("Mouth", PrimitiveType.Cube, new Vector3(0, -.22f, .46f), new Vector3(.26f, .035f, .04f));
            mouthScale = mouth.localScale; eyeScale = eyeL.localScale;
        }

        void Update()
        {
            float t = Time.time + seed, dt = Time.deltaTime;
            talkBlend = Mathf.MoveTowards(talkBlend, talking ? 1 : 0, dt * 4);
            float breath = Mathf.Sin(t * (1.6f + stress * 0.02f)) * (0.01f + stress * 0.0002f);
            if (torso) torso.localScale = new Vector3(torsoScale.x * (1 + breath), torsoScale.y, torsoScale.z * (1 + breath));

            if (head)
            {
                float nod = talkBlend * (Mathf.Sin(t * 6.5f) * 2.5f * Mathf.PerlinNoise(t, seed) + Mathf.Sin(t * 2.3f) * 4f) + Mathf.Sin(t * .7f) * 2f;
                float shake = stress > 65 ? Mathf.Sin(t * 23f) * (stress - 65) * 0.06f : 0;
                float look = (Mathf.PerlinNoise(t * .3f, seed) - .5f) * 14f + shake + Mathf.Sin(t * 1.9f) * 3f * talkBlend;
                head.localRotation = headRot * Quaternion.Euler(nod - stress * .04f, look, Mathf.Sin(t * .8f) * 2f);
                head.localPosition = headPos + Vector3.up * breath * 2;
            }

            // mouth: syllables while talking, tight line under stress
            float target = talking ? Mathf.Max(0, Mathf.Sin(t * 14f + Mathf.Sin(t * 3f) * 2f)) * (Mathf.PerlinNoise(t * 1.2f, seed) > .3f ? 1 : .1f) : 0;
            open = Mathf.SmoothDamp(open, target, ref openVel, .05f);
            if (mouth) mouth.localScale = new Vector3(mouthScale.x * (1 - stress * .002f), mouthScale.y + open * .12f, mouthScale.z);

            // blink
            if (Time.time > nextBlink) { blink = 1; nextBlink = Time.time + Random.Range(2f, 5f) * (stress > 60 ? .5f : 1); }
            blink = Mathf.MoveTowards(blink, 0, dt * 8);
            float lid = 1 - Mathf.Sin(blink * Mathf.PI) * .9f;
            if (eyeL) eyeL.localScale = new Vector3(eyeScale.x, eyeScale.y * lid, eyeScale.z);
            if (eyeR) eyeR.localScale = new Vector3(eyeScale.x, eyeScale.y * lid, eyeScale.z);

            // brows: worried (inner up) as stress grows, raised when talking with emphasis
            float worry = Mathf.Clamp01((stress - 30) / 60f);
            float lift = talkBlend * Mathf.Max(0, Mathf.PerlinNoise(t * .8f, seed + 5) - .5f) * .12f;
            if (browL) { browL.localPosition = new Vector3(-.19f, .25f + lift, .45f); browL.localRotation = Quaternion.Euler(0, 0, -18f * worry); }
            if (browR) { browR.localPosition = new Vector3(.19f, .25f + lift, .45f); browR.localRotation = Quaternion.Euler(0, 0, 18f * worry); }

            // hands: gestures while talking, fidget under stress
            float g = talkBlend * Mathf.Clamp01((Mathf.PerlinNoise(t * .6f, seed + 9) - .3f) * 2.5f);
            float fidget = stress > 55 ? Mathf.Sin(t * 9f) * (stress - 55) * .08f : 0;
            if (armR) armR.localRotation = armRRot * Quaternion.Euler(-35f * g * (.7f + .3f * Mathf.Sin(t * 6f)) + fidget, 0, 0);
            if (armL) armL.localRotation = armLRot * Quaternion.Euler(-15f * g * Mathf.PerlinNoise(t * .5f, seed + 2) - fidget, 0, 0);
        }
    }
}

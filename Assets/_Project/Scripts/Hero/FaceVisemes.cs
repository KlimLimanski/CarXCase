using UnityEngine;

namespace CarXCase.Hero
{
    /// Procedural mouth shapes for heads that only have a jaw (or nothing): spreads and rounds the lips
    /// and, when asked, opens the lower face around a hinge. Shapes are added to a runtime copy of the
    /// skinned mesh (the source asset is not touched) unless the mesh already carries them.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class FaceVisemes : MonoBehaviour
    {
        public const string WideShape = "Mouth_Wide";
        public const string RoundShape = "Mouth_Round";
        public const string JawShape = "Mouth_JawOpen";

        [System.Serializable]
        public struct Anchors
        {
            [Tooltip("Centre of the lip line, mesh (bind-pose) space.")] public Vector3 mouth;
            [Tooltip("Direction the face looks, mesh space.")] public Vector3 forward;
            [Tooltip("Up of the head, mesh space.")] public Vector3 up;
            [Tooltip("Half of the mouth width, metres.")] public float halfWidth;
            [Tooltip("Mesh units per metre (1 for most meshes, 0.01 for a centimetre mesh scaled up by its bones).")] public float unitsPerMetre;
            [Tooltip("Lip spread at full weight, metres.")] public float wide;
            [Tooltip("Lip rounding at full weight, metres.")] public float round;
            [Tooltip("Lip protrusion when rounding, metres.")] public float protrude;
            [Tooltip("Build a jaw opening shape (for meshes without a jaw bone or jaw blend shape).")] public bool buildJaw;
            [Tooltip("Jaw hinge, mesh space.")] public Vector3 jawPivot;
            [Tooltip("Jaw rotation at full weight, degrees.")] public float jawAngle;
            [Tooltip("Distance from the lip line down to the bottom of the chin, metres.")] public float chinDepth;

            public static Anchors Default => new Anchors
            {
                forward = Vector3.forward, up = Vector3.up, halfWidth = .025f, unitsPerMetre = 1f,
                wide = .0032f, round = .0038f, protrude = .0028f, jawAngle = 6f, chinDepth = .04f
            };
        }

        public SkinnedMeshRenderer body;
        public Anchors anchors = Anchors.Default;
        [Tooltip("Raise the neutral lips locally by this distance, in metres.")] public float mouthLift;
        [Tooltip("Existing jaw blend shape on the mesh (e.g. JawOpen). Empty = use the built one, if any.")]
        public string jawShape = "";
        [Range(0f, 1f), Tooltip("Weight of the jaw shape on a fully open syllable.")] public float maxJaw = 1f;

        int jawIndex = -1, wideIndex = -1, roundIndex = -1;
        int blinkIndex = -1, browsUpIndex = -1, browsKnitIndex = -1;
        float jawWeight, wideWeight, roundWeight, blinkWeight, browsUpWeight, browsKnitWeight;
        Mesh runtimeMesh;
        bool built;

        public bool Ready => built;
        public bool HasJaw => jawIndex >= 0;
        public bool HasAuthoredMouth { get; private set; }

        public static bool HasMouthShapes(Mesh mesh) => mesh &&
            mesh.GetBlendShapeIndex(JawShape) >= 0 && mesh.GetBlendShapeIndex(WideShape) >= 0 &&
            mesh.GetBlendShapeIndex(RoundShape) >= 0;

        void Awake() => Build();

        public void Build()
        {
            if (built || !body || !body.sharedMesh) return;
            var source = body.sharedMesh;
            var mesh = source;
            HasAuthoredMouth = HasMouthShapes(source);
            if (source.GetBlendShapeIndex(WideShape) < 0 || mouthLift != 0f)
            {
                if (!source.isReadable)
                {
                    Debug.LogWarning($"[FaceVisemes] {source.name} is not readable; lips stay on the jaw only.", this);
                    return;
                }
                runtimeMesh = Instantiate(source);
                runtimeMesh.name = source.name + " (mouth)";
                if (mouthLift != 0f) LiftMouth(runtimeMesh, anchors, mouthLift);
                AddShapes(runtimeMesh, anchors);
                body.sharedMesh = runtimeMesh;
                mesh = runtimeMesh;
            }
            wideWeight = FindShape(mesh, WideShape, out wideIndex);
            roundWeight = FindShape(mesh, RoundShape, out roundIndex);
            jawWeight = FindShape(mesh, string.IsNullOrEmpty(jawShape) ? JawShape : jawShape, out jawIndex);
            blinkWeight = FindShape(mesh, "Face_Blink", out blinkIndex);
            browsUpWeight = FindShape(mesh, "Face_BrowsUp", out browsUpIndex);
            browsKnitWeight = FindShape(mesh, "Face_BrowsKnit", out browsKnitIndex);
            built = true;
            Apply(0f, 0f, 0f);
        }

        static float FindShape(Mesh mesh, string shape, out int index)
        {
            index = mesh.GetBlendShapeIndex(shape);
            return index < 0 ? 0f : mesh.GetBlendShapeFrameWeight(index, mesh.GetBlendShapeFrameCount(index) - 1);
        }

        /// jaw, wide and round in 0..1.
        public void Apply(float jaw, float wide, float round)
        {
            if (!built || !body) return;
            if (jawIndex >= 0) body.SetBlendShapeWeight(jawIndex, Mathf.Clamp01(jaw) * maxJaw * jawWeight);
            if (wideIndex >= 0) body.SetBlendShapeWeight(wideIndex, Mathf.Clamp(wide, 0f, 1.5f) * wideWeight);
            if (roundIndex >= 0) body.SetBlendShapeWeight(roundIndex, Mathf.Clamp(round, 0f, 1.5f) * roundWeight);
        }

        public void ApplyExpression(float blink, float brow)
        {
            if (!built || !body) return;
            if (blinkIndex >= 0) body.SetBlendShapeWeight(blinkIndex, Mathf.Clamp01(blink) * blinkWeight);
            if (browsUpIndex >= 0) body.SetBlendShapeWeight(browsUpIndex, Mathf.Clamp01(brow) * browsUpWeight);
            if (browsKnitIndex >= 0) body.SetBlendShapeWeight(browsKnitIndex, Mathf.Clamp01(-brow) * browsKnitWeight);
        }

        void OnDisable()
        {
            Apply(0f, 0f, 0f);
            ApplyExpression(0f, 0f);
        }

        void OnDestroy()
        {
            if (runtimeMesh) Destroy(runtimeMesh);
        }

        static float Smooth(float from, float to, float x)
        {
            float t = Mathf.Clamp01((x - from) / (to - from));
            return t * t * (3f - 2f * t);
        }

        static void LiftMouth(Mesh mesh, Anchors a, float lift)
        {
            float k = a.unitsPerMetre > 0f ? a.unitsPerMetre : 1f;
            Vector3 front = a.forward.normalized;
            Vector3 up = Vector3.ProjectOnPlane(a.up, front).normalized;
            Vector3 right = Vector3.Cross(up, front);
            Vector3 neutral = a.mouth - up * (lift * k);
            var vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 d = (vertices[i] - neutral) / k;
                float x = Mathf.Abs(Vector3.Dot(d, right));
                float y = Vector3.Dot(d, up);
                float z = Vector3.Dot(d, front);
                float width = Smooth(.057f, .026f, x);
                float height = Smooth(-.046f, -.006f, y) * Smooth(.023f, .002f, y);
                float surface = Smooth(-.037f, -.008f, z);
                vertices[i] += up * (lift * k * width * height * surface);
            }
            mesh.vertices = vertices;
        }

        /// Adds Mouth_Wide, Mouth_Round and (optionally) Mouth_JawOpen to a readable mesh.
        public static void AddShapes(Mesh mesh, Anchors a)
        {
            var vertices = mesh.vertices;
            int n = vertices.Length;
            float k = a.unitsPerMetre <= 0f ? 1f : a.unitsPerMetre;
            Vector3 f = a.forward.normalized;
            Vector3 u = Vector3.ProjectOnPlane(a.up, f).normalized;
            Vector3 r = Vector3.Cross(u, f);
            float hw = Mathf.Max(a.halfWidth, .005f) * k;
            var wide = new Vector3[n];
            var round = new Vector3[n];
            var jaw = a.buildJaw ? new Vector3[n] : null;
            var hinge = Quaternion.AngleAxis(a.jawAngle, r);

            for (int i = 0; i < n; i++)
            {
                Vector3 d = vertices[i] - a.mouth;
                float x = Vector3.Dot(d, r), y = Vector3.Dot(d, u), z = Vector3.Dot(d, f);

                // lips: an ellipse around the lip line on the facial surface
                float front = Smooth(-.03f * k, -.008f * k, z);
                if (front > 0f)
                {
                    float ex = x / (hw * 1.9f), ey = y / (.02f * k);
                    float w = Smooth(1f, .25f, ex * ex + ey * ey) * front;
                    if (w > 0f)
                    {
                        float side = Mathf.Clamp(x / hw, -1.3f, 1.3f);
                        float vertical = Mathf.Clamp(y / (.012f * k), -1f, 1f);
                        wide[i] = (r * side * a.wide - f * Mathf.Abs(side) * a.wide * .35f - u * vertical * a.wide * .25f) * (k * w);
                        round[i] = (-r * side * a.round + f * a.protrude * (1f - Mathf.Min(1f, Mathf.Abs(side)) * .6f)) * (k * w);
                    }
                }

                if (jaw != null)
                {   // lower lip, chin and jaw rotate down around the hinge; nothing above the lip line moves
                    float below = Smooth(.0006f * k, -.005f * k, y);
                    float chin = Smooth(-(a.chinDepth + .045f) * k, -(a.chinDepth + .01f) * k, y);
                    float face = Smooth(-.075f * k, -.04f * k, z);
                    float side = Smooth(hw * 3.2f, hw * 2.2f, Mathf.Abs(x));
                    float wj = below * chin * face * side;
                    if (wj > 0f)
                    {
                        Vector3 p = vertices[i];
                        Vector3 q = a.jawPivot + hinge * (p - a.jawPivot);
                        jaw[i] = (q - p) * wj;
                    }
                }
            }

            if (mesh.GetBlendShapeIndex(WideShape) < 0) mesh.AddBlendShapeFrame(WideShape, 100f, wide, null, null);
            if (mesh.GetBlendShapeIndex(RoundShape) < 0) mesh.AddBlendShapeFrame(RoundShape, 100f, round, null, null);
            if (jaw != null && mesh.GetBlendShapeIndex(JawShape) < 0) mesh.AddBlendShapeFrame(JawShape, 100f, jaw, null, null);
        }
    }
}

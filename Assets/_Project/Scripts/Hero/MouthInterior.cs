using UnityEngine;

namespace CarXCase.Hero
{
    /// A mouth for heads whose lips are one welded surface: a thin dark opening with a hint of the upper
    /// teeth, laid on the lip line and parted by the speech articulation of HeroFace. Closed, it has no
    /// height and is not visible; it opens together with the jaw, so the lips read as parting.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]          // after HeroFace has written this frame's mouth opening
    public sealed class MouthInterior : MonoBehaviour
    {
        public HeroFace face;
        public FaceVisemes visemes;
        public Material material;
        [Tooltip("Opening height on a fully open syllable, metres.")] public float maxHeight = .0068f;
        [Range(.5f, 1f), Tooltip("Width of the opening relative to the lip line.")] public float width = .8f;
        [Tooltip("How far in front of the lips the opening sits, metres.")] public float lift = .0011f;
        [Range(0f, 1f), Tooltip("Share of the opening above the lip line (the upper lip moves less).")] public float upperShare = .36f;

        const int Columns = 11;
        static readonly Color Teeth = new Color(.62f, .58f, .52f);
        static readonly Color Throat = new Color(.055f, .018f, .016f);
        static readonly Color Tongue = new Color(.16f, .055f, .05f);

        Transform head;
        Vector3[] lip;           // lip line, head space
        Vector3 upLocal, backLocal;
        Mesh mesh;
        Vector3[] vertices;
        GameObject holder;
        float shown;

        void LateUpdate()
        {
            if (!face) return;
            if (lip == null && !Build()) return;
            float open = Mathf.Clamp01(face.MouthOpen);
            shown = Mathf.Max(open, Mathf.MoveTowards(shown, open, Time.deltaTime * 12f));
            bool visible = shown > .015f;
            if (holder.activeSelf != visible) holder.SetActive(visible);
            if (!visible) return;

            float h = maxHeight * shown;
            float teeth = Mathf.Min(.0016f, h * upperShare * .9f);
            for (int c = 0; c < Columns; c++)
            {
                float t = c / (Columns - 1f) * 2f - 1f;
                float profile = Mathf.Sqrt(Mathf.Max(0f, 1f - t * t));          // lens: closed at the corners
                Vector3 p = lip[c] + backLocal * (1f - profile) * .0012f;
                Vector3 top = p + upLocal * (h * upperShare * profile);
                Vector3 bottom = p - upLocal * (h * (1f - upperShare) * profile);
                vertices[c * 3] = top;
                vertices[c * 3 + 1] = top - upLocal * (teeth * profile);
                vertices[c * 3 + 2] = bottom;
            }
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }

        bool Build()
        {
            if (!visemes) visemes = GetComponentInChildren<FaceVisemes>(true);
            if (!visemes || !visemes.body || !visemes.body.sharedMesh) return false;
            if (FaceVisemes.HasMouthShapes(visemes.body.sharedMesh))
            {
                enabled = false;
                return false;
            }
            head = face.HeadBone;
            if (!head) return false;
            var body = visemes.body;
            var a = visemes.anchors;
            float k = a.unitsPerMetre <= 0f ? 1f : a.unitsPerMetre;
            Vector3 f = a.forward.normalized;
            Vector3 u = Vector3.ProjectOnPlane(a.up, f).normalized;
            Vector3 r = Vector3.Cross(u, f);
            float hw = Mathf.Max(a.halfWidth, .005f) * k;

            var bind = body.sharedMesh.vertices;
            var baked = new Mesh();
            body.BakeMesh(baked, true);
            var posed = baked.vertices;
            Destroy(baked);
            var toWorld = body.transform.localToWorldMatrix;

            // frontmost skin along the lip line, one sample per column
            var front = new Vector3[Columns];
            var depth = new float[Columns];
            for (int c = 0; c < Columns; c++) depth[c] = float.NegativeInfinity;
            for (int i = 0; i < bind.Length; i++)
            {
                Vector3 d = bind[i] - a.mouth;
                float x = Vector3.Dot(d, r), y = Vector3.Dot(d, u), z = Vector3.Dot(d, f);
                if (Mathf.Abs(y) > .0045f * k || z < -.016f * k || Mathf.Abs(x) > hw * width * 1.12f) continue;
                float column = (x / (hw * width) + 1f) * .5f * (Columns - 1);
                int c = Mathf.RoundToInt(column);
                if (c < 0 || c >= Columns || Mathf.Abs(column - c) > .45f) continue;
                if (z > depth[c]) { depth[c] = z; front[c] = toWorld.MultiplyPoint3x4(posed[i]); }
            }
            Vector3 worldForward = body.transform.TransformDirection(f).normalized;
            Vector3 worldUp = body.transform.TransformDirection(u).normalized;
            Vector3 worldRight = body.transform.TransformDirection(r).normalized;
            Vector3 centre = toWorld.MultiplyPoint3x4(a.mouth);
            // fill columns the scan missed by interpolating their neighbours
            for (int c = 0; c < Columns; c++)
            {
                if (!float.IsNegativeInfinity(depth[c])) continue;
                float t = c / (Columns - 1f) * 2f - 1f;
                front[c] = centre + worldRight * (t * hw * width / k * body.transform.lossyScale.x);
            }

            lip = new Vector3[Columns];
            for (int c = 0; c < Columns; c++) lip[c] = head.InverseTransformPoint(front[c] + worldForward * lift);
            upLocal = head.InverseTransformVector(worldUp);
            backLocal = head.InverseTransformVector(-worldForward);

            holder = new GameObject("Mouth interior");
            holder.transform.SetParent(head, false);
            holder.layer = body.gameObject.layer;
            mesh = new Mesh { name = "Mouth interior" };
            mesh.MarkDynamic();
            vertices = new Vector3[Columns * 3];
            var colors = new Color[Columns * 3];
            var tris = new int[(Columns - 1) * 12];
            for (int c = 0; c < Columns; c++)
            {
                colors[c * 3] = Teeth;
                colors[c * 3 + 1] = Throat;
                colors[c * 3 + 2] = Tongue;
                if (c == Columns - 1) continue;
                int o = (c - 0) * 12, v = c * 3, n = v + 3;
                // teeth band, then the dark of the mouth (both windings: the strip is seen from the front only)
                tris[o] = v; tris[o + 1] = n; tris[o + 2] = v + 1;
                tris[o + 3] = v + 1; tris[o + 4] = n; tris[o + 5] = n + 1;
                tris[o + 6] = v + 1; tris[o + 7] = n + 1; tris[o + 8] = v + 2;
                tris[o + 9] = v + 2; tris[o + 10] = n + 1; tris[o + 11] = n + 2;
            }
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = tris;
            // face the strip towards the viewer in front of the head
            Vector3 normal = head.InverseTransformDirection(worldForward);
            var normals = new Vector3[vertices.Length];
            for (int i = 0; i < normals.Length; i++) normals[i] = normal;
            mesh.normals = normals;
            holder.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = holder.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            holder.SetActive(false);

            // which winding faces the viewer depends on the head axes: flip if the strip faces into the head
            Vector3 e1 = head.TransformPoint(lip[Columns / 2 + 1]) - head.TransformPoint(lip[Columns / 2]);
            Vector3 e2 = -worldUp;
            if (Vector3.Dot(Vector3.Cross(e1, e2), worldForward) < 0f)   // Unity: cross(b-a, c-a) is the visible side
            {
                for (int i = 0; i < tris.Length; i += 3) (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
                mesh.triangles = tris;
            }
            return true;
        }

        void OnDisable()
        {
            shown = 0f;
            if (holder) holder.SetActive(false);
        }

        void OnDestroy()
        {
            if (mesh) Destroy(mesh);
            if (holder) Destroy(holder);
        }
    }
}

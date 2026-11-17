using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CarXCase.Weather
{
    /// Planar reflection of the street for puddles and wet asphalt ("Project/Wet Ground").
    /// A hidden camera sits at the main camera mirrored under the road plane, clips everything below
    /// the road (oblique near plane) and renders into a mip-mapped texture. The ground shader projects
    /// each pixel through this camera, so the lookup is exact for a flat road, including lamps and the
    /// elevated railway that are outside the main camera's frame.
    [ExecuteAlways]
    public sealed class PuddleReflection : MonoBehaviour
    {
        [Tooltip("Высота асфальта (м)")] public float planeHeight = 0f;
        [Range(0.25f, 1f)] public float resolutionScale = 0.5f;
        public float farClip = 230f;
        [Tooltip("Чуть шире основного кадра, чтобы рябь не вылезала за край отражения")]
        public float fovPadding = 1.12f;
        public bool shadowsInReflection = false;
        [Tooltip("Рендерить отражение и вне Play Mode (видно в Game view)")]
        public bool renderInEditMode = false;

        static readonly int TexId = Shader.PropertyToID("_PuddleReflectionTex");
        static readonly int VpId = Shader.PropertyToID("_PuddleReflectionVP");
        static readonly int OnId = Shader.PropertyToID("_PuddleReflectionOn");

        Camera cam; RenderTexture rt;

        void OnDisable()
        {
            Shader.SetGlobalFloat(OnId, 0);
            if (cam) DestroyImmediate(cam.gameObject);
            if (rt) { rt.Release(); DestroyImmediate(rt); }
        }

        void Ensure(Camera main)
        {
            int w = Mathf.Max(64, Mathf.RoundToInt(main.pixelWidth * resolutionScale));
            int h = Mathf.Max(64, Mathf.RoundToInt(main.pixelHeight * resolutionScale));
            if (!rt || rt.width != w || rt.height != h)
            {
                if (rt) { rt.Release(); DestroyImmediate(rt); }
                rt = new RenderTexture(w, h, 24, RenderTextureFormat.DefaultHDR)
                { name = "PuddleReflection_RT", hideFlags = HideFlags.DontSave, useMipMap = true, autoGenerateMips = true, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
                if (cam) cam.targetTexture = rt;
            }
            if (cam) return;
            var go = new GameObject("PuddleReflection_Camera") { hideFlags = HideFlags.HideAndDontSave };
            cam = go.AddComponent<Camera>();
            cam.targetTexture = rt; cam.allowHDR = true; cam.allowMSAA = false;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false; data.requiresColorTexture = false; data.requiresDepthTexture = false;
            data.antialiasing = AntialiasingMode.None;
        }

        void LateUpdate()
        {
            bool active = Application.isPlaying || renderInEditMode;
            var main = Camera.main;
            if (!active || !main) { Shader.SetGlobalFloat(OnId, 0); if (cam) cam.enabled = false; return; }
            Ensure(main);
            cam.enabled = true;
            cam.depth = main.depth - 5;
            cam.GetComponent<UniversalAdditionalCameraData>().renderShadows = shadowsInReflection;
            cam.cullingMask = main.cullingMask; cam.clearFlags = main.clearFlags; cam.backgroundColor = main.backgroundColor;

            // camera mirrored under the plane (orientation is not mirrored: the shader uses a projective lookup)
            var mt = main.transform;
            Vector3 eye = mt.position; eye.y = 2f * planeHeight - eye.y;
            Vector3 f = mt.forward; f.y = -f.y;
            Vector3 u = mt.up; u.y = -u.y;
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(f, u));
            cam.fieldOfView = Mathf.Min(170f, main.fieldOfView * fovPadding);
            cam.aspect = main.aspect;
            cam.nearClipPlane = Mathf.Max(0.01f, main.nearClipPlane);
            cam.farClipPlane = farClip;
            cam.ResetProjectionMatrix();

            // keep only what is above the road
            var view = cam.worldToCameraMatrix;
            Vector3 cp = view.MultiplyPoint(new Vector3(0, planeHeight + 0.01f, 0));
            Vector3 cn = view.MultiplyVector(Vector3.up).normalized;
            cam.projectionMatrix = cam.CalculateObliqueMatrix(new Vector4(cn.x, cn.y, cn.z, -Vector3.Dot(cp, cn)));

            Shader.SetGlobalTexture(TexId, rt);
            Shader.SetGlobalMatrix(VpId, cam.projectionMatrix * cam.worldToCameraMatrix);
            Shader.SetGlobalFloat(OnId, 1);
        }
    }
}

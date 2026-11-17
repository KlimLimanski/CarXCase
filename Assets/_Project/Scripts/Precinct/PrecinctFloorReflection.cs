using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CarXCase.Precinct
{
    /// Renders a planar reflection for the polished vinyl in the precinct.
    [DefaultExecutionOrder(350)]
    public sealed class PrecinctFloorReflection : MonoBehaviour
    {
        [SerializeField] Camera source;
        [SerializeField] Renderer[] overlays;
        [SerializeField] float planeHeight;
        [SerializeField, Range(.25f, .75f)] float resolutionScale = .5f;
        [SerializeField, Min(128)] int maxWidth = 960;

        static readonly int ReflectionTexture = Shader.PropertyToID("_FloorReflectionTex");
        static readonly int ReflectionMatrix = Shader.PropertyToID("_FloorReflectionVP");
        static readonly int ReflectionAvailable = Shader.PropertyToID("_FloorReflectionAvailable");

        Camera reflectionCamera;
        RenderTexture reflectionTexture;
        MaterialPropertyBlock properties;
        UniversalRenderPipeline.SingleCameraRequest request;

        public void Configure(Camera camera, Renderer[] surfaces, float height)
        {
            source = camera;
            overlays = surfaces;
            planeHeight = height;
        }

        void LateUpdate()
        {
            if (!source || !source.isActiveAndEnabled || overlays == null || source.transform.position.y <= planeHeight + .04f)
            {
                SetAvailable(false);
                return;
            }

            EnsureResources();
            ConfigureCamera();
            request.destination = reflectionTexture;
            if (!RenderPipeline.SupportsRenderRequest(reflectionCamera, request))
            {
                SetAvailable(false);
                return;
            }

            bool previousCulling = GL.invertCulling;
            try
            {
                SetAvailable(false);
                GL.invertCulling = !previousCulling;
                RenderPipeline.SubmitRenderRequest(reflectionCamera, request);
                reflectionTexture.GenerateMips();
            }
            finally
            {
                GL.invertCulling = previousCulling;
            }

            SetAvailable(true);
        }

        void EnsureResources()
        {
            if (!reflectionCamera)
            {
                var cameraObject = new GameObject("Precinct Floor Reflection Camera") { hideFlags = HideFlags.HideAndDontSave };
                reflectionCamera = cameraObject.AddComponent<Camera>();
                reflectionCamera.enabled = false;

                var data = reflectionCamera.GetUniversalAdditionalCameraData();
                data.renderShadows = false;
                data.renderPostProcessing = false;
                data.requiresColorTexture = false;
                data.requiresDepthTexture = false;
                data.antialiasing = AntialiasingMode.None;

                request = new UniversalRenderPipeline.SingleCameraRequest();
                properties = new MaterialPropertyBlock();
            }

            int width = Mathf.Max(64, Mathf.Min(maxWidth, Mathf.RoundToInt(source.pixelWidth * resolutionScale)));
            int height = Mathf.Max(64, Mathf.RoundToInt(width / Mathf.Max(.01f, source.aspect)));
            if (reflectionTexture && reflectionTexture.width == width && reflectionTexture.height == height) return;

            ReleaseTexture();
            reflectionTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf)
            {
                name = "Precinct Floor Reflection",
                hideFlags = HideFlags.HideAndDontSave,
                useMipMap = true,
                autoGenerateMips = false,
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 1
            };
            reflectionTexture.Create();
        }

        void ConfigureCamera()
        {
            reflectionCamera.cullingMask = source.cullingMask;
            reflectionCamera.clearFlags = CameraClearFlags.SolidColor;
            reflectionCamera.backgroundColor = new Color(.022f, .027f, .033f);
            reflectionCamera.allowHDR = true;
            reflectionCamera.allowMSAA = false;
            reflectionCamera.useOcclusionCulling = false;
            reflectionCamera.nearClipPlane = source.nearClipPlane;
            reflectionCamera.farClipPlane = source.farClipPlane;
            reflectionCamera.aspect = source.aspect;
            reflectionCamera.fieldOfView = source.fieldOfView;

            var mirror = Matrix4x4.identity;
            mirror.m11 = -1;
            mirror.m13 = 2 * planeHeight;
            var view = source.transform;
            reflectionCamera.transform.SetPositionAndRotation(
                mirror.MultiplyPoint(view.position),
                Quaternion.LookRotation(mirror.MultiplyVector(view.forward), mirror.MultiplyVector(view.up)));
            reflectionCamera.worldToCameraMatrix = source.worldToCameraMatrix * mirror;
            reflectionCamera.projectionMatrix = source.projectionMatrix;

            // The reflected camera sits below the vinyl. Keep geometry above the floor plane.
            var worldToView = reflectionCamera.worldToCameraMatrix;
            var point = worldToView.MultiplyPoint(new Vector3(0, planeHeight + .012f, 0));
            var normal = worldToView.MultiplyVector(Vector3.up).normalized;
            var clipPlane = new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(point, normal));
            reflectionCamera.projectionMatrix = reflectionCamera.CalculateObliqueMatrix(clipPlane);
            reflectionCamera.ResetCullingMatrix();
        }

        void SetAvailable(bool available)
        {
            if (properties == null || overlays == null) return;

            Matrix4x4 projection = available
                ? GL.GetGPUProjectionMatrix(reflectionCamera.projectionMatrix, true) * reflectionCamera.worldToCameraMatrix
                : Matrix4x4.identity;
            foreach (var overlay in overlays)
            {
                if (!overlay) continue;
                overlay.GetPropertyBlock(properties);
                properties.SetFloat(ReflectionAvailable, available ? 1 : 0);
                properties.SetTexture(ReflectionTexture, available ? reflectionTexture : Texture2D.blackTexture);
                properties.SetMatrix(ReflectionMatrix, projection);
                overlay.SetPropertyBlock(properties);
            }
        }

        void OnDisable()
        {
            SetAvailable(false);
            ReleaseTexture();
            if (reflectionCamera) Destroy(reflectionCamera.gameObject);
            reflectionCamera = null;
        }

        void ReleaseTexture()
        {
            if (!reflectionTexture) return;
            reflectionTexture.Release();
            Destroy(reflectionTexture);
            reflectionTexture = null;
        }
    }
}

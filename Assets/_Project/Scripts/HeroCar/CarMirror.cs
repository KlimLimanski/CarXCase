using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CarXCase.HeroCar
{
    /// Physically correct planar mirror for car mirrors.
    /// A hidden camera sits at the viewer's position reflected across the glass plane, looks
    /// perpendicular to the glass and uses an off-axis frustum whose near plane IS the glass,
    /// so the housing and everything behind the mirror are clipped. The glass shader projects
    /// its own world position through that camera to sample the reflection.
    [ExecuteAlways]
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class CarMirror : MonoBehaviour
    {
        public Material mirrorMaterial;
        [Tooltip("Камера, для которой строится отражение. Пусто = Camera.main")]
        public Camera viewer;
        [Min(64)] public int resolution = 512;
        public float farClip = 250f;
        public Color tint = new Color(0.78f, 0.80f, 0.82f, 1f);
        [Tooltip("Clip at the surface itself so nearby subjects remain visible.")]
        public bool clipAtGlass;
        [Min(.002f)] public float surfaceBias = .002f;
        [Min(0)] public float visibleDistance;
        [Tooltip("Optional panes sharing this capture. The projection mesh spans the entire group.")]
        public Renderer[] sharedPanes;

        static readonly List<CarMirror> all = new List<CarMirror>();
        Camera cam; RenderTexture rt; MaterialPropertyBlock mpb; MeshRenderer rend;
        Vector3 localCenter, localNormal; Vector3[] localCorners;
        readonly Plane[] viewerFrustum = new Plane[6];
        bool UsesSharedPanes => sharedPanes != null && sharedPanes.Length > 0;

        public static void RenderAllFor(Camera v) { foreach (var m in all) if (m && m.isActiveAndEnabled) m.RenderFor(v, true); }

        void OnEnable()
        {
            all.Add(this);
            var mesh = GetComponent<MeshFilter>().sharedMesh;
            if (!mesh || !mirrorMaterial) return;
            var vs = mesh.vertices; var ns = mesh.normals;
            localCenter = mesh.bounds.center;
            localNormal = ns.Length > 0 ? ns[0].normalized : Vector3.forward;
            localCorners = vs;
            Vector3 size = mesh.bounds.size;
            if (clipAtGlass)
            {
                // Imported glass can carry smoothed vertex normals from its bevel.
                // For a flat mirror the thinnest mesh axis defines its plane.
                if (size.x <= size.y && size.x <= size.z) localNormal = Vector3.right;
                else if (size.y <= size.z) localNormal = Vector3.up;
                else localNormal = Vector3.forward;
            }
            float longest = Mathf.Max(size.x, size.y, size.z);
            float shorter = Mathf.Max(.01f, size.x + size.y + size.z - longest - Mathf.Min(size.x, size.y, size.z));
            int shortPixels = Mathf.Clamp(Mathf.RoundToInt(resolution * shorter / longest), 64, resolution);
            var worldBounds = GetComponent<MeshRenderer>().bounds;
            bool portrait = clipAtGlass && worldBounds.size.y >= Mathf.Max(worldBounds.size.x, worldBounds.size.z);
            int rw = portrait ? shortPixels : resolution, rh = portrait ? resolution : shortPixels;
            rt = new RenderTexture(rw, rh, 24, RenderTextureFormat.DefaultHDR) { name = name + "_RT", hideFlags = HideFlags.DontSave, antiAliasing = 2 };
            var go = new GameObject(name + "_ReflectionCam") { hideFlags = HideFlags.HideAndDontSave };
            cam = go.AddComponent<Camera>();
            cam.targetTexture = rt; cam.depth = -50; cam.allowHDR = true; cam.allowMSAA = true;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderShadows = false; data.renderPostProcessing = false; data.requiresDepthTexture = true; data.requiresColorTexture = true;
            cam.enabled = Application.isPlaying && visibleDistance <= 0;
            rend = GetComponent<MeshRenderer>();
            if (!UsesSharedPanes && rend.sharedMaterial != mirrorMaterial) rend.sharedMaterial = mirrorMaterial;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mpb = new MaterialPropertyBlock();
        }

        void OnDisable()
        {
            all.Remove(this);
            if (cam) DestroyImmediate(cam.gameObject);
            if (rt) { rt.Release(); DestroyImmediate(rt); }
            if (UsesSharedPanes)
            {
                foreach (var pane in sharedPanes)
                    if (pane) pane.SetPropertyBlock(null);
            }
            else if (rend) rend.SetPropertyBlock(null);
        }

        void LateUpdate()
        {
            if (!Application.isPlaying || !cam) return;
            var source = viewer ? viewer : Camera.main;
            if (!source) { cam.enabled = false; return; }
            if (visibleDistance > 0)
            {
                GeometryUtility.CalculateFrustumPlanes(source, viewerFrustum);
                cam.enabled = rend.bounds.SqrDistance(source.transform.position) < visibleDistance * visibleDistance
                    && GeometryUtility.TestPlanesAABB(viewerFrustum, rend.bounds);
                if (!cam.enabled) return;
            }
            else cam.enabled = true;
            RenderFor(source, false);
        }

        void RenderFor(Camera v, bool renderNow)
        {
            if (!cam || !v || localCorners == null) return;
            Vector3 p = transform.TransformPoint(localCenter);
            Vector3 n = transform.TransformDirection(localNormal).normalized;
            Vector3 eye = v.transform.position;
            float side = Vector3.Dot(eye - p, n);
            if (side < 0) { n = -n; side = -side; }           // glass faces the viewer
            if (side < 0.004f) return;
            Vector3 virtualEye = eye - 2f * side * n;            // viewer mirrored behind the glass
            Vector3 up = Vector3.ProjectOnPlane(transform.parent ? transform.parent.up : Vector3.up, n);
            if (up.sqrMagnitude < 1e-4f)
                up = Vector3.ProjectOnPlane(Mathf.Abs(n.y) > .95f ? Vector3.forward : Vector3.up, n);
            cam.transform.SetPositionAndRotation(virtualEye, Quaternion.LookRotation(n, up));
            float near = (clipAtGlass ? side : 2f * side) + surfaceBias;
            var view = cam.worldToCameraMatrix;
            float l = float.MaxValue, rr = float.MinValue, b = float.MaxValue, t = float.MinValue;
            foreach (var c in localCorners)
            {
                Vector3 q = view.MultiplyPoint(transform.TransformPoint(c));
                float s = near / Mathf.Max(1e-4f, -q.z);
                l = Mathf.Min(l, q.x * s); rr = Mathf.Max(rr, q.x * s); b = Mathf.Min(b, q.y * s); t = Mathf.Max(t, q.y * s);
            }
            cam.nearClipPlane = near; cam.farClipPlane = farClip;
            cam.projectionMatrix = Matrix4x4.Frustum(l, rr, b, t, near, farClip);
            cam.cullingMask = v.cullingMask; cam.clearFlags = v.clearFlags; cam.backgroundColor = v.backgroundColor;
            if (UsesSharedPanes)
            {
                foreach (var pane in sharedPanes)
                    if (pane) ApplyReflection(pane);
            }
            else ApplyReflection(rend);
            if (renderNow) cam.Render();
        }

        void ApplyReflection(Renderer pane)
        {
            pane.GetPropertyBlock(mpb);
            mpb.SetTexture("_MirrorTex", rt); mpb.SetColor("_Tint", tint);
            mpb.SetMatrix("_MirrorVP", cam.projectionMatrix * cam.worldToCameraMatrix);
            pane.SetPropertyBlock(mpb);
        }
    }
}

using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// Review-only helper: renders frames (edit or play mode) and packs them into contact sheets.
public static class HeroReviewCapture
{
    public static readonly string Folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Review")) + Path.DirectorySeparatorChar;

    public static Texture2D Render(Camera source, Vector3 position, Vector3 lookAt, float fov, int width, int height)
    {
        var go = new GameObject("__ReviewCam") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var cam = go.AddComponent<Camera>();
            if (source) cam.CopyFrom(source);
            cam.enabled = false;
            cam.fieldOfView = fov;
            cam.nearClipPlane = .03f;
            go.transform.position = position;
            go.transform.LookAt(lookAt);
            return RenderCamera(cam, width, height);
        }
        finally { Object.DestroyImmediate(go); }
    }

    public static Texture2D RenderCamera(Camera cam, int width, int height)
    {
        var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        var previousTarget = cam.targetTexture;
        var previousActive = RenderTexture.active;
        try
        {
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            return tex;
        }
        finally
        {
            cam.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(rt);
        }
    }

    public static string Sheet(string name, IList<Texture2D> frames, int columns)
    {
        int w = frames[0].width, h = frames[0].height;
        int rows = Mathf.CeilToInt(frames.Count / (float)columns);
        var sheet = new Texture2D(w * columns, h * rows, TextureFormat.RGB24, false);
        sheet.SetPixels32(new Color32[sheet.width * sheet.height]);
        for (int i = 0; i < frames.Count; i++)
            sheet.SetPixels((i % columns) * w, (rows - 1 - i / columns) * h, w, h, frames[i].GetPixels());
        sheet.Apply();
        Directory.CreateDirectory(Folder);
        string path = Folder + name + ".png";
        File.WriteAllBytes(path, sheet.EncodeToPNG());
        Object.DestroyImmediate(sheet);
        foreach (var f in frames) Object.DestroyImmediate(f);
        return path;
    }

    /// Renders with skinned meshes baked from the current bone transforms
    /// (Camera.Render alone may reuse the skinning computed on the previous editor frame).
    public static void WithBakedSkins(Transform root, System.Action render)
    {
        var temps = new List<GameObject>();
        var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>();
        try
        {
            foreach (var skin in skins)
            {
                var mesh = new Mesh();
                skin.BakeMesh(mesh, true);
                var go = new GameObject("__Baked") { hideFlags = HideFlags.HideAndDontSave };
                go.layer = skin.gameObject.layer;
                go.transform.SetPositionAndRotation(skin.transform.position, skin.transform.rotation);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = skin.sharedMaterials;
                mr.renderingLayerMask = skin.renderingLayerMask;
                mr.shadowCastingMode = skin.shadowCastingMode;
                temps.Add(go);
                skin.forceRenderingOff = true;
            }
            render();
        }
        finally
        {
            foreach (var skin in skins) skin.forceRenderingOff = false;
            foreach (var go in temps) { Object.DestroyImmediate(go.GetComponent<MeshFilter>().sharedMesh); Object.DestroyImmediate(go); }
        }
    }
}

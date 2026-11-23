using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// Review-only, Play Mode: poses one hangar moment over several frames and films it from fixed angles into
/// Assets/_Project/QA_Temp~ (a folder Unity ignores). Used to check the draw, the mouth, the father's death
/// and the cuffing frame by frame without playing the whole ending.
public static class HangarMomentCapture
{
    public const string Folder = "Assets/_Project/QA_Temp~/";

    public sealed class Context
    {
        public HangarFinale finale;
        public HangarStaging staging;
        public HangarProps props;
        public Transform kostya, player;
    }

    /// Runs `pose(context, progress)` for each progress value; after `settle` frames films every view.
    public static void Run(string prefix, float[] progress, Action<Context, float> pose, Func<Context, (Vector3 eye, Vector3 look)[]> views, int settle = 6, int width = 560, int height = 640, float fov = 38f)
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode only");
        var c = Prepare();
        var go = new GameObject("QA capture camera");
        var cam = go.AddComponent<Camera>();
        cam.enabled = false; cam.fieldOfView = fov; cam.nearClipPlane = .03f;
        var main = Camera.main;
        if (main) cam.CopyFrom(main);
        cam.enabled = false; cam.fieldOfView = fov; cam.nearClipPlane = .03f;
        var rt = new RenderTexture(width, height, 24);
        int step = 0, wait = 0;
        EditorApplication.CallbackFunction tick = null;
        tick = () =>
        {
            if (!EditorApplication.isPlaying || step >= progress.Length)
            {
                EditorApplication.update -= tick;
                if (go) UnityEngine.Object.Destroy(go);
                return;
            }
            pose(c, progress[step]);
            if (++wait < settle) return;
            wait = 0;
            var v = views(c);
            for (int i = 0; i < v.Length; i++)
            {
                go.transform.position = v[i].eye;
                go.transform.LookAt(v[i].look);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                File.WriteAllBytes(Folder + prefix + "_" + step + "_" + i + ".png", tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                RenderTexture.active = null;
            }
            step++;
        };
        EditorApplication.update += tick;
    }

    public static Context Prepare()
    {
        var c = new Context { finale = UnityEngine.Object.FindAnyObjectByType<HangarFinale>() };
        c.staging = c.finale.GetComponent<HangarStaging>();
        c.props = c.finale.GetComponent<HangarProps>();
        c.kostya = c.finale.kostya;
        var officer = UnityEngine.Object.FindAnyObjectByType<DetectiveGame.Core.OfficerController>();
        c.player = officer.transform;
        if (officer.TryGetComponent<CharacterController>(out var cc)) cc.enabled = false;
        c.player.position = c.finale.transform.TransformPoint(new Vector3(-.05f, .10f, 5.35f));
        Vector3 to = c.kostya.position - c.player.position; to.y = 0f;
        c.player.rotation = Quaternion.LookRotation(to);
        c.props.SetHero(officer);
        return c;
    }

    public static (Vector3, Vector3)[] KostyaViews(Context c)
    {
        Vector3 k = c.kostya.position, f = c.kostya.forward, s = c.kostya.right, u = Vector3.up;
        return new[] { (k + f * 1.9f - s * .35f + u * 1.45f, k + u * 1.2f), (k - s * 1.7f + f * .7f + u * 1.4f, k + u * 1.2f) };
    }

    /// Over Alexey's shoulder, the way the dialogue camera frames Kostya.
    public static (Vector3, Vector3)[] GameViews(Context c)
    {
        Vector3 a = c.player.position, k = c.kostya.position, u = Vector3.up;
        Vector3 axis = k - a; axis.y = 0; axis.Normalize();
        Vector3 side = Vector3.Cross(u, axis);
        return new[] { (a - axis * .6f + side * .55f + u * 1.65f, k + u * 1.25f), ((a + k) * .5f + side * 2.3f + u * 1.3f, (a + k) * .5f + u * 1.0f) };
    }
}

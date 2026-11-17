using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CarXCase.Hero;

/// Enters Play Mode, films the hero's face while he "talks", writes PNGs to HeroSetup/ and exits Play Mode.
/// Command: "playcap_face" (HeroSetup/request.txt).
[InitializeOnLoad]
public static class PlayCapture
{
    const string Key = "CarXCase.PlayCapture.Stage";
    static double start; static int shot;
    static readonly float[] times = { 1.5f, 1.65f, 1.8f, 1.95f, 2.1f, 2.6f, 3.4f, 4.2f };

    static PlayCapture() { EditorApplication.update += Tick; }

    public static void Start(string scenePath, StringBuilder log)
    {
        EditorSceneManager.OpenScene(scenePath);
        EditorPrefs.SetInt(Key, 1);
        PlayerSettings.runInBackground = true;
        start = 0; shot = 0;
        log.AppendLine("Entering Play Mode to capture " + scenePath);
        EditorApplication.isPlaying = true;
    }

    static void Tick()
    {
        if (EditorPrefs.GetInt(Key, 0) != 1 || !EditorApplication.isPlaying) return;
        if (start == 0) { start = EditorApplication.timeSinceStartup; shot = 0; }
        float t = (float)(EditorApplication.timeSinceStartup - start);
        HeroFace.SetTalking(true);
        HeroFace.SetEmotion(t < 3f ? 0.5f : -0.8f);
        if (shot < times.Length && t >= times[shot])
        {
            var face = Object.FindAnyObjectByType<HeroFace>();
            if (face)
            {
                var head = FindDeep(face.transform, "Head");
                var root = face.transform;
                Vector3 hp = head.position + Vector3.up * 0.06f;
                HeroSetup.DebugShot(hp + root.forward * 0.55f + root.right * 0.12f, hp, $"face_{shot}.png", 35);
                if (shot == times.Length - 1) HeroSetup.DebugShot(root.position + root.forward * 2.4f + Vector3.up * 1.3f, root.position + Vector3.up * 1.1f, "face_body.png", 50);
            }
            shot++;
        }
        if (shot >= times.Length || t > 12)
        {
            EditorPrefs.SetInt(Key, 0); start = 0;
            Directory.CreateDirectory("HeroSetup");
            File.WriteAllText("HeroSetup/playcap.txt", "DONE shots=" + shot + " at " + System.DateTime.Now);
            EditorApplication.isPlaying = false;
        }
    }

    static Transform FindDeep(Transform r, string n)
    {
        foreach (var t in r.GetComponentsInChildren<Transform>(true)) if (t.name == n) return t;
        return r;
    }
}

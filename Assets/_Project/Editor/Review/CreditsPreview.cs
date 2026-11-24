using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Review-only: opens the harbour, enters Play Mode and rolls the end credits straight away.
/// Trigger: Tools/Review/Show end credits, or create <project>/Review/credits.request.
[InitializeOnLoad]
public static class CreditsPreview
{
    const string Harbor = "Assets/_Project/Scenes/05_BrightonHarbor.unity";
    const string Key = "CarXCase.CreditsPreview.Pending";

    static CreditsPreview() { EditorApplication.update += Tick; }

    [MenuItem("Tools/Review/Show end credits")] static void Menu() => Begin();

    static void Begin()
    {
        if (EditorApplication.isPlaying) { EditorPrefs.SetBool(Key, true); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(Harbor, OpenSceneMode.Single);
        EditorPrefs.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }

    static double ready;

    static void Tick()
    {
        string request = Path.Combine(HeroReviewCapture.Folder, "credits.request");
        if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && File.Exists(request))
        {
            File.Delete(request);
            Begin();
            return;
        }
        if (!EditorPrefs.GetBool(Key, false) || !EditorApplication.isPlaying) return;
        if (SceneManager.GetActiveScene().path != Harbor) return;
        var finale = Object.FindAnyObjectByType<HangarFinale>();
        if (!finale) return;
        if (ready == 0) { ready = EditorApplication.timeSinceStartup + 2.0; return; }   // let the scene settle
        if (EditorApplication.timeSinceStartup < ready) return;
        ready = 0;
        EditorPrefs.SetBool(Key, false);
        typeof(HangarFinale).GetMethod("DebugPlay", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(finale, new object[] { "credits", false });
    }
}

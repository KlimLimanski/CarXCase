using System;
using System.IO;
using UnityEngine;

namespace NoirMenu
{
    [Serializable]
    public class SaveData
    {
        public Casebook.CaseState investigation;
        public string scene;
        public Vector3 playerPosition;
        public float playTime;
        public string savedAt;
        public JournalEntry[] clues;
        public string[] conclusions;
        public string decision;
        public bool skipPrecinctIntro;
    }

    [Serializable]
    public class JournalEntry
    {
        public string id, title, description, thought;
    }

    /// <summary>
    /// Single-slot save file in Application.persistentDataPath.
    /// The game scene writes it; the main menu reads it for "Continue".
    /// </summary>
    public static class SaveSystem
    {
        const string FileName = "savegame.json";

        /// <summary>Save that the next game scene should restore (null = new game).</summary>
        public static SaveData PendingLoad;

        #if UNITY_EDITOR
        // Automated integration checks use an isolated save directory.
        public static string SaveDirectoryOverride;
#endif
        public static string FilePath => Path.Combine(
#if UNITY_EDITOR
            SaveDirectoryOverride ??
#endif
            Application.persistentDataPath, FileName);

        public static bool HasSave()
        {
            var data = Load();
            return data != null && GameSession.IsStoryScene(data.scene)
                && Application.CanStreamedLevelBeLoaded(data.scene);
        }

        public static SaveData Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
                return data != null && !string.IsNullOrEmpty(data.scene) ? data : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SaveSystem] Could not read save: " + e.Message);
                return null;
            }
        }

        public static void Write(SaveData data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            data.savedAt = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
            if (File.Exists(FilePath)) File.Replace(tmp, FilePath, FilePath + ".bak");
            else File.Move(tmp, FilePath);
        }

        public static void Delete()
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
            if (File.Exists(FilePath + ".bak")) File.Delete(FilePath + ".bak");
        }
    }
}

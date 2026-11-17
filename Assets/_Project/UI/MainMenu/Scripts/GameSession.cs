using System;
using System.Linq;
using DetectiveGame.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NoirMenu
{
    /// Scene checkpoints retain investigation progress; arrival cinematics replay on continue.
    public sealed class GameSession : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            var go = new GameObject("StoryCheckpoints");
            DontDestroyOnLoad(go);
            go.AddComponent<GameSession>();
        }

        public static bool IsStoryScene(string scene) => scene == "00_BrightonDrive_Models"
            || scene == "01_FatherApartment" || scene == "02_Precinct"
            || scene == "03_Bar" || scene == "04_Pawnshop" || scene == "05_Mikhail";

        public static void StartNewGame()
        {
            SaveSystem.PendingLoad = null;
            ClueJournal.Clear();
            SaveSystem.Delete();
        }

        void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single || !IsStoryScene(scene.name)) return;
            var saved = SaveSystem.PendingLoad;
            if (saved != null && saved.scene == scene.name)
            {
                ClueJournal.Clear();
                if (saved.clues != null)
                    foreach (var clue in saved.clues)
                        ClueJournal.Collect(clue.id, clue.title, clue.description, clue.thought);
                if (saved.conclusions != null)
                    foreach (var id in saved.conclusions) ClueJournal.AddConclusion(id);
                ClueJournal.Decision = saved.decision;
                ClueJournal.SkipPrecinctIntro = saved.skipPrecinctIntro;
                SaveSystem.PendingLoad = null;
            }
            WriteCheckpoint(scene.name);
        }

        static void WriteCheckpoint(string scene)
        {
            try
            {
                SaveSystem.Write(new SaveData
                {
                    scene = scene,
                    clues = ClueJournal.Entries.Select(e => new JournalEntry
                    { id = e.id, title = e.title, description = e.description, thought = e.thought }).ToArray(),
                    conclusions = ClueJournal.Conclusions.ToArray(),
                    decision = ClueJournal.Decision,
                    skipPrecinctIntro = ClueJournal.SkipPrecinctIntro
                });
            }
            catch (Exception error) { Debug.LogWarning("[Checkpoints] " + error.Message); }
        }
    }
}

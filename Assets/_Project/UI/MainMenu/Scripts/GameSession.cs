using System;
using System.Linq;
using DetectiveGame.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

namespace NoirMenu
{
    /// Scene checkpoints retain investigation progress; arrival cinematics replay on continue.
    public sealed class GameSession : MonoBehaviour
    {
        public static bool Restoring { get; private set; }
        public static void SaveCurrent()
        {
            var scene = SceneManager.GetActiveScene().name;
            if (!Restoring && IsStoryScene(scene)) WriteCheckpoint(scene);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            var go = new GameObject("StoryCheckpoints");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<GameSession>();
            CarXCase.Investigation.InvestigationIntegration.Ensure();
        }

        public static bool IsStoryScene(string scene) => scene == "00_BrightonDrive_Models"
            || scene == "01_FatherApartment" || scene == "02_Precinct"
            || scene == "03_Bar" || scene == "04_Pawnshop" || scene == "05_Mikhail"
            || scene == "02_Bar" || scene == "03_Pawnshop" || scene == "04_Precinct"
            || scene == "05_BrightonHarbor";

        public static void StartNewGame()
        {
            SaveSystem.PendingLoad = null;
            Restoring = true;
            ClueJournal.Clear();
            SaveSystem.Delete();
            Restoring = false;
        }

        const string MenuScene = "00_MainMenu";
        static GameSession instance;
        Scene suspendedScene;
        bool resuming;
        float previousSpeed;
        bool audioWasPaused, cursorWasVisible;
        CursorLockMode previousCursor;
        readonly System.Collections.Generic.List<Behaviour> suspendedOutputs = new();
        public static bool Paused => instance && instance.suspendedScene.IsValid();
        public static readonly WaitWhile NextStoryFrame = new WaitWhile(() => Paused);
        GUIStyle menuHint;

        void OpenMenu()
        {
            SaveCurrent();
            suspendedScene = SceneManager.GetActiveScene();
            previousSpeed = Time.timeScale;
            audioWasPaused = AudioListener.pause;
            previousCursor = Cursor.lockState;
            cursorWasVisible = Cursor.visible;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            SuspendOutputs<AudioListener>();
            SuspendOutputs<Canvas>();
            SuspendOutputs<UnityEngine.EventSystems.EventSystem>();
            SceneManager.LoadSceneAsync(MenuScene, LoadSceneMode.Additive);
        }

        void SuspendOutputs<T>(Scene except = default) where T : Behaviour
        {
            foreach (var output in FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                if (!output.enabled || output.gameObject.scene == except) continue;
                suspendedOutputs.Add(output);
                output.enabled = false;
            }
        }

        public static void Resume()
        {
            if (!Paused || instance.resuming) return;
            instance.resuming = true;
            SceneManager.SetActiveScene(instance.suspendedScene);
            SceneManager.UnloadSceneAsync(MenuScene).completed += _ => instance.ReleasePause(true);
        }

        void ReleasePause(bool resume)
        {
            suspendedScene = default;
            resuming = false;
            Time.timeScale = previousSpeed;
            AudioListener.pause = audioWasPaused;
            foreach (var output in suspendedOutputs)
                if (output) output.enabled = true;
            suspendedOutputs.Clear();
            if (resume)
            {
                Cursor.lockState = previousCursor;
                Cursor.visible = cursorWasVisible;
            }
        }

        void Update()
        {
            if (Paused || SceneManager.GetActiveScene().name == MenuScene) return;
            if (Keyboard.current == null || !Keyboard.current.tabKey.wasPressedThisFrame) return;

            OpenMenu();
        }

        void OnGUI()
        {
            if (Paused || Event.current.type != EventType.Repaint || SceneManager.GetActiveScene().name == MenuScene) return;
            menuHint ??= new GUIStyle(GameTypography.Label) { alignment = TextAnchor.UpperRight };
            menuHint.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * .017f), 13, 28);
            var rect = new Rect(0, 14, Screen.width - 18, menuHint.fontSize * 2);
            const string hint = "Выйти в меню — Tab";
            var previous = GUI.color;
            GUI.color = Color.black;
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), hint, menuHint);
            GUI.color = Color.white;
            GUI.Label(rect, hint, menuHint);
            GUI.color = previous;
        }

        void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive && scene.name == MenuScene && Paused)
            {
                // Keep the paused game visible until the menu camera is ready to render.
                SuspendOutputs<Camera>(scene);
                SceneManager.SetActiveScene(scene);
            }
            if (mode == LoadSceneMode.Single && suspendedOutputs.Count > 0) ReleasePause(false);
            if (mode != LoadSceneMode.Single || !IsStoryScene(scene.name)) return;
            Restoring = true;
            CarXCase.Investigation.InvestigationIntegration.Close();
            var saved = SaveSystem.PendingLoad;
            if (saved != null && saved.scene == scene.name)
            {
                ClueJournal.Clear();
                CarXCase.Investigation.InvestigationIntegration.Restore(saved.investigation, saved.clues);
                if (saved.clues != null)
                    foreach (var clue in saved.clues)
                        ClueJournal.Collect(clue.id, clue.title, clue.description, clue.thought);
                if (saved.conclusions != null)
                    foreach (var id in saved.conclusions) ClueJournal.AddConclusion(id);
                ClueJournal.Decision = saved.decision;
                ClueJournal.SkipPrecinctIntro = saved.skipPrecinctIntro;
                SaveSystem.PendingLoad = null;
            }
            Restoring = false;
            if (scene.name == "02_Bar") ClueJournal.AddConclusion("route_visited_bar");
            if (scene.name == "03_Pawnshop" && ClueJournal.Has("route_visited_bar"))
                ClueJournal.AddConclusion(HangarFinale.LateRoute);
            CarXCase.Investigation.InvestigationIntegration.Synchronize();
            WriteCheckpoint(scene.name);
        }

        static void WriteCheckpoint(string scene)
        {
            try
            {
                SaveSystem.Write(new SaveData
                {
                    investigation = CarXCase.Investigation.InvestigationIntegration.State,
                    scene = scene,
                    clues = ClueJournal.Entries.Select(e => new JournalEntry
                    { id = e.id, title = e.title, description = e.description, thought = e.thought }).ToArray(),
                    conclusions = ClueJournal.Conclusions.ToArray(),
                    decision = ClueJournal.Decision,
                    skipPrecinctIntro = ClueJournal.SkipPrecinctIntro
                });
            }
            catch (Exception error) { Debug.LogWarning("[Checkpoints] " + error.Message); throw; }
        }
    }
}

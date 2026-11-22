using System;
using System.Linq;
using Casebook;
using DetectiveGame.Core;
using CarXCase.Precinct;
using NoirMenu;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace CarXCase.Investigation
{
    /// Adapts the game's existing discovery/progression API to the physical diary.
    /// ClueJournal remains the authority for discoveries and plot conditions.
    public sealed class InvestigationIntegration : MonoBehaviour
    {
        static InvestigationIntegration instance;
        CasebookController desk;
        InvestigationCase catalog;
        DeductionBoard board;
        bool syncing;
        public static bool IsOpen => instance && instance.desk && instance.desk.View != DeskView.Closed;
        public static CaseState State => instance ? instance.desk.Session?.State : null;
        public static CasebookController Controller => instance ? instance.desk : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        public static void Ensure()
        {
            if (instance) return;
            var source = Resources.Load<InvestigationCase>("CarXInvestigation");
            if (!source) { Debug.LogError("Missing CarXInvestigation catalog. Run Tools/Investigation/Rebuild game catalog."); return; }
            var go = new GameObject("Investigation — diary and board");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<InvestigationIntegration>();
            instance.catalog = Instantiate(source);
            instance.desk = go.AddComponent<CasebookController>();
            instance.desk.standaloneInput = false;
            instance.Initialize(null);
            ClueJournal.Changed += instance.JournalChanged;
            ClueJournal.Cleared += instance.ClearState;
            instance.desk.HintUnlocked += instance.Unlock;
            instance.desk.ViewChanged += instance.ViewChanged;
            SceneManager.activeSceneChanged += instance.SceneChanged;
        }

        void Initialize(CaseState state)
        {
            desk.Initialize(catalog, state: state, persist: _ => GameSession.SaveCurrent());
            desk.BoardActionAvailable = () => board && board.DecisionReady;
            desk.BoardAction = () => { if (board) board.OpenDecision(); };
            desk.WorldBoardDismissed = () => { if (board) board.OpenDecision(); };
        }
        void SceneChanged(Scene oldScene, Scene nextScene) => desk.Close();
        void ClearState() => Initialize(null);
        void OnDestroy()
        {
            ClueJournal.Changed -= JournalChanged;
            ClueJournal.Cleared -= ClearState;
            SceneManager.activeSceneChanged -= SceneChanged;
            if (catalog) Destroy(catalog);
            if (instance == this) instance = null;
        }
        void OnApplicationQuit() { if (desk && desk.Session != null) desk.Session.Save(); }

        void Upsert(string id, string title, string description, string thought)
        {
            var entry = catalog.Find(id);
            if (entry == null)
            {
                entry = new Casebook.Entry { id = id };
                catalog.entries.Add(entry);
            }
            entry.title = title;
            entry.observation = description;
            entry.narrator = thought ?? "";
        }

        public static void Restore(CaseState state, JournalEntry[] discoveries)
        {
            Ensure();
            if (!instance) return;
            // Preserve runtime-authored clues too, including those not in the editor catalog yet.
            foreach (var entry in discoveries ?? Array.Empty<JournalEntry>())
                instance.Upsert(entry.id, entry.title, entry.description, entry.thought);
            instance.Initialize(state);
        }

        void JournalChanged()
        {
            Synchronize();
            if (!GameSession.Restoring) desk.Session.Save();
        }
        public static void Synchronize()
        {
            Ensure();
            if (!instance || instance.syncing) return;
            instance.syncing = true;
            try
            {
                foreach (var entry in ClueJournal.Entries)
                {
                    instance.Upsert(entry.id, entry.title, entry.description, entry.thought);
                    instance.desk.Collect(entry.id);
                }
                RevealDerived();
            }
            finally { instance.syncing = false; }
        }
        /// People, places and timeline pages follow from the clues they are built on.
        static void RevealDerived()
        {
            var desk = instance.desk;
            string keep = desk.selection;
            bool any = false;
            foreach (var entry in instance.catalog.entries)
            {
                if (!JournalWindow.Visible(entry) || !entry.Derived || desk.Session.Get(entry.id) != null) continue;
                if (entry.revealedBy.All(id => desk.Session.Get(id) != null || ClueJournal.Has(id)))
                    any |= desk.Collect(entry.id);
            }
            // The clue that triggered them stays the open page; the toast still announces the newest record.
            if (any && !string.IsNullOrEmpty(keep)) desk.Select(keep);
        }

        void Unlock(Casebook.Deduction deduction)
        {
            ClueJournal.AddConclusion(deduction.id);
            desk.Notify("Новый вывод в дневнике: " + deduction.title);
        }

        public static bool HandleInput(SimpleUIHUD hud)
        {
            Ensure();
            if (!instance || !hud || !hud.isActiveAndEnabled || !GameSession.IsStoryScene(SceneManager.GetActiveScene().name)) return false;
            if (Pressed((KeyCode)State.journalKey)) { instance.desk.OpenJournal(); return true; }

            return false;
        }
        static bool Pressed(KeyCode key)
        {
            if (Keyboard.current == null) return false;
            return Enum.TryParse<Key>(key.ToString(), true, out var inputKey)
                && inputKey != Key.None && Keyboard.current[inputKey].wasPressedThisFrame;
        }
        public static void Close() { if (instance && instance.desk) instance.desk.Close(); }
        public static void RegisterBoard(DeductionBoard value) { Ensure(); if (instance) instance.board = value; }
        public static void UnregisterBoard(DeductionBoard value) { if (instance && instance.board == value) instance.board = null; }
        /// At the physical board: the camera glides up to it and the board's own surface becomes the interface.
        public static void OpenBoard(DeductionBoard value)
        {
            RegisterBoard(value);
            if (!instance) return;
            if (value && value.surface)
            {
                var follow = Camera.main ? Camera.main.GetComponent<ThirdPersonCamera>() : null;
                if (follow && follow.isActiveAndEnabled)
                {
                    // Frame the whole board (cork, conclusions and tabs) with a small margin, square on.
                    var lens = follow.GetComponent<Camera>();
                    float vertical = Mathf.Tan((lens ? lens.fieldOfView : 50f) * .5f * Mathf.Deg2Rad);
                    float horizontal = vertical * (lens ? lens.aspect : 16f / 9f);
                    float distance = Mathf.Max(value.surfaceSize.y * .5f / vertical, value.surfaceSize.x * .5f / horizontal) * 1.1f;
                    Vector3 eye = value.surface.position + value.surface.forward * distance;
                    follow.BeginStation(eye, value.surface.position);
                    instance.station = follow;
                }
                instance.desk.OpenBoardOn(value.surface, value.surfaceSize);
            }

        }

        ThirdPersonCamera station;
        void ViewChanged(DeskView view)
        {
            // Leaving the board (or switching to the diary pages) hands the camera back smoothly.
            if (view == DeskView.Closed && station) { station.EndShot(); station = null; }
        }
    }
}

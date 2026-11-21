using System;
using System.IO;
using System.Linq;
using Casebook;
using CarXCase.Apartment;
using CarXCase.Investigation;
using CarXCase.Precinct;
using DetectiveGame.Core;
using NoirMenu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace CarXCase.Editor
{
    /// Real-scene smoke test. Never writes the player's save slot.
    [InitializeOnLoad]
    public static class InvestigationIntegrationCheck
    {
        const string Armed = "CarX.InvestigationCheck";
        static int stage;
        static double started, next;
        static string log = "";
        static readonly string output = Environment.GetEnvironmentVariable("CARX_INVESTIGATION_TEST_SAVE");
        static InvestigationIntegrationCheck()
        {
            if (!string.IsNullOrEmpty(output)) SaveSystem.SaveDirectoryOverride = output;
            EditorApplication.update += Tick;
        }
        public static void Run()
        {
            if (string.IsNullOrEmpty(output)) throw new Exception("Set CARX_INVESTIGATION_TEST_SAVE to an isolated test directory.");
            Directory.CreateDirectory(output);
            SaveSystem.Delete();
            InvestigationCatalogBuilder.Rebuild();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/01_FatherApartment.unity");
            SessionState.SetBool(Armed, true);
            EditorApplication.isPlaying = true;
        }
        public static void Resume()
        {
            if (string.IsNullOrEmpty(output)) throw new Exception("Set isolated save directory.");
            File.Copy(Path.Combine(output, "verified-save.json"), SaveSystem.FilePath, true);
            var saved=SaveSystem.Load();saved.skipPrecinctIntro=true;SaveSystem.Write(saved);
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainMenu.unity");
            SessionState.SetBool("CarX.InvestigationResume", true);
            SessionState.SetBool(Armed, true);
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes = singleton.GetProperty("instance").GetValue(null);
            var groupMethod = sizesType.GetMethod("GetGroup");
            var group = groupMethod.Invoke(sizes, new[] { Enum.Parse(groupMethod.GetParameters()[0].ParameterType, "Standalone") });
            var sizeType = assembly.GetType("UnityEditor.GameViewSize");
            var modeType = assembly.GetType("UnityEditor.GameViewSizeType");
            var size = Activator.CreateInstance(sizeType, new object[] { Enum.Parse(modeType, "FixedResolution"), 1440, 900, "Investigation QA" });
            var labels = (string[])group.GetType().GetMethod("GetDisplayTexts").Invoke(group,null);
            int qaIndex=Array.FindIndex(labels, label=>label.Contains("Investigation QA"));
            if(qaIndex<0){group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });qaIndex=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null)-1;}
            var view = EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
            var selected = view.GetType().GetProperty("selectedSizeIndex", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            SessionState.SetInt("CarX.PreviousGameViewSize", (int)selected.GetValue(view));
            selected.SetValue(view, qaIndex);
            EditorApplication.isPlaying = true;
        }
        static void Check(bool ok, string text)
        {
            if (!ok) throw new Exception(text);
            log += "PASS " + text + "\n";
            Debug.Log("[InvestigationCheck] PASS " + text);
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Armed, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (started == 0) { started = EditorApplication.timeSinceStartup; next = started + 3; stage = SessionState.GetBool("CarX.InvestigationResume", false) ? 10 : 0; }
            if (EditorApplication.timeSinceStartup < next) return;
            try
            {
                if (EditorApplication.timeSinceStartup - started > 150) throw new Exception("Timed out at stage " + stage);
                var desk = InvestigationIntegration.Controller;
                if (!desk || desk.Session == null) return;
                if (stage == 10)
                {
                    Check(ClueJournal.Count == 0, "fresh Unity process starts without static clue state");
                    SaveSystem.PendingLoad = SaveSystem.Load();
                    SceneManager.LoadScene(SaveSystem.PendingLoad.scene);
                    stage = 11; next = EditorApplication.timeSinceStartup + 3;
                }
                else if (stage == 11)
                {
                    if (!Object.FindAnyObjectByType<SimpleUIHUD>().isActiveAndEnabled) return;
                    Check(ClueJournal.Count == 4 && ClueJournal.Has(FatherCase.Bar) && ClueJournal.Has(FatherCase.Port), "fresh process Continue restores real discoveries and conclusions");
                    Check(desk.Session.State.pins.Count == 3 && desk.Session.State.threads.Count == 2, "fresh process restores board layout and both red threads");
                    var keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.J));
                    InputSystem.Update();
                    Check(Object.FindAnyObjectByType<SimpleUIHUD>().HandleJournalInput(Camera.main.transform, true), "J opens diary through existing HUD and Input System");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                    Check(desk.View == DeskView.Journal, "J selects the physical diary view");
                    stage = 12; next = EditorApplication.timeSinceStartup + 3;
                }
                else if (stage == 12)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "Diary-in-game.png"));
                    stage = 13; next = EditorApplication.timeSinceStartup + 3;
                }
                else if (stage == 13)
                {
                    desk.Close();
                    var keyboard = Keyboard.current;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab)); InputSystem.Update();
                    Check(Object.FindAnyObjectByType<SimpleUIHUD>().HandleJournalInput(Camera.main.transform, true), "Tab opens board through Input System");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                    Check(desk.View == DeskView.Board, "Tab selects integrated cork board");
                    desk.Session.State.zoom=1;desk.Session.State.pan=Vector2.zero;
                    stage = 14; next = EditorApplication.timeSinceStartup + 3;
                }
                else if (stage == 14)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "Board-in-game.png"));
                    stage = 15; next = EditorApplication.timeSinceStartup + 3;
                }
                else if (stage == 15)
                {
                    Check(File.Exists(Path.Combine(output, "Diary-in-game.png")) && File.Exists(Path.Combine(output, "Board-in-game.png")), "both real in-game views rendered and captured");
                    File.WriteAllText(Path.Combine(output, "resume-result.txt"), log + "RESTART CHECKS PASSED\n");
                    SessionState.SetBool(Armed, false); SessionState.SetBool("CarX.InvestigationResume", false);
                    EditorApplication.Exit(0);
                }
                else if (stage == 0)
                {
                    Check(ClueJournal.Count == 0 && desk.Session.State.found.Count == 0, "new game has no undiscovered clues or sample content");
                    var clues = Object.FindObjectsByType<ClueObject>(FindObjectsSortMode.None);
                    var hud = Object.FindAnyObjectByType<SimpleUIHUD>();
                    foreach (var id in new[] { FatherCase.Door, FatherCase.Valuables, FatherCase.Matches })
                    {
                        var clue = clues.First(c => c.clueId == id);
                        clue.Inspect(hud);
                        Check(desk.Session.Get(id) != null && desk.caseFile.Find(id).observation == clue.clueDescription, "actual Inspect writes original clue " + id);
                    }
                    clues.First(c => c.clueId == FatherCase.Matches).Inspect(hud);
                    Check(ClueJournal.Count == 3 && desk.Session.State.found.Count == 3, "reinspection does not duplicate pages");
                    Check(!desk.caseFile.Find(FatherCase.Matches).attachments.Any(a => a.image && a.image.name == "clue_cups"), "obsolete photos are not assigned to unrelated story clues");
                    desk.OpenJournal();
                    Check(hud.IsShowingJournal && Cursor.visible && Cursor.lockState == CursorLockMode.None, "diary claims gameplay input and unlocks cursor");
                    stage = 1; next = EditorApplication.timeSinceStartup + 2;
                }
                else if (stage == 1)
                {
                    Check(Object.FindAnyObjectByType<OfficerController>().PlanarVelocity.sqrMagnitude < .0001f, "hero stops while diary is open");
                    desk.Close();
                    var hud = Object.FindAnyObjectByType<SimpleUIHUD>();
                    Check(!hud.IsShowingJournal, "closing diary releases gameplay input");
                    Object.FindAnyObjectByType<ApartmentAnsweringMachine>().Play(hud);
                    Check(!ClueJournal.Has(FatherCase.Messages), "answering-machine clue waits for actual playback");
                    stage = 2;
                }
                else if (stage == 2)
                {
                    if (Object.FindAnyObjectByType<ApartmentAnsweringMachine>().IsPlaying) return;
                    Check(ClueJournal.Has(FatherCase.Messages) && desk.Session.Get(FatherCase.Messages) != null, "real answering-machine coroutine adds recording to diary");
                    foreach (var id in FatherCase.RequiredClues)
                    {
                        Check(desk.Session.Detach(id), "detach " + id);
                        Check(desk.Session.Place(id, new Vector2(80 + 230 * Array.IndexOf(FatherCase.RequiredClues, id), 160)), "pin " + id);
                        Check(!desk.Session.Place(id, Vector2.zero), "prevent duplicate pin " + id);
                    }
                    Check(desk.Session.Tie(FatherCase.Matches, FatherCase.Door) == Match.Unsupported, "incorrect pair reveals no hint");
                    Check(desk.Session.Tie(FatherCase.Matches, FatherCase.Messages) == Match.Discovered, "actual bar deduction unlocks");
                    Check(ClueJournal.Has(FatherCase.Bar) && desk.Session.Get(FatherCase.Bar) != null, "bar conclusion reaches game progression and a diary page");
                    Check(desk.Session.Tie(FatherCase.Messages, FatherCase.Matches) == Match.Existing, "reversed duplicate thread is rejected");
                    desk.Session.Get(FatherCase.Matches).note = "Проверить рассказ Дмитрия";
                    desk.Session.Get(FatherCase.Matches).studied = true;
                    desk.Session.State.zoom = 1.35f;
                    desk.Session.State.pan = new Vector2(-34, 29);
                    desk.Session.Save();
                    var save = SaveSystem.Load();
                    Check(save.clues.Length == 4 && save.conclusions.Contains(FatherCase.Bar) && save.investigation.pins.Count == 4, "unified save contains discoveries, game flags and board layout before scene transition");
                    ClueJournal.SkipPrecinctIntro = true;
                    SceneManager.LoadScene("02_Precinct"); stage = 3; next = EditorApplication.timeSinceStartup + 3;
                }
                else if (stage == 3)
                {
                    var board = Object.FindAnyObjectByType<DeductionBoard>();
                    Check(board && board.DecisionReady, "the previously matched bar evidence unlocks its route");
                    board.Interact(Object.FindAnyObjectByType<SimpleUIHUD>());
                    Check(desk.View == DeskView.Board, "world board interaction opens integrated board");
                    Check(desk.Session.Tie(FatherCase.Valuables, FatherCase.Messages) == Match.Discovered, "actual port deduction unlocks");
                    Check(board.DecisionReady && ClueJournal.Has(FatherCase.Port), "existing route decision unlocks after authored requirements");
                    desk.Session.Untie(FatherCase.Bar);
                    Check(ClueJournal.Has(FatherCase.Bar) && desk.Session.Get(FatherCase.Bar) != null, "removing thread retains discovered hint and story flag");
                    Check(desk.Session.Tie(FatherCase.Matches, FatherCase.Messages) == Match.Linked, "restoring thread does not rediscover hint");
                    desk.Session.Return(FatherCase.Door);
                    Check(ClueJournal.Has(FatherCase.Door) && desk.Session.Get(FatherCase.Door) != null && desk.Session.Pinned(FatherCase.Door) == null, "returning page preserves original clue");
                    board.OpenDecision();
                    Check(!InvestigationIntegration.IsOpen && !Object.FindAnyObjectByType<OfficerController>().enabled, "existing travel screen takes over input safely");
                    // Exercise the existing scene-transition restore path, with serialized JSON as the only source.
                    SaveSystem.PendingLoad = SaveSystem.Load();
                    SceneManager.LoadScene("02_Precinct"); stage = 4; next = EditorApplication.timeSinceStartup + 3;
                }
                else if (stage == 4)
                {
                    Check(ClueJournal.Count == 4 && ClueJournal.Has(FatherCase.Bar) && ClueJournal.Has(FatherCase.Port), "Continue restores true game clues and both progression flags");
                    Check(desk.Session.State.pins.Count == 3 && desk.Session.State.threads.Count == 2 && desk.Session.State.insights.Count == 2, "Continue restores pins, threads and hints");
                    Check(desk.Session.Get(FatherCase.Matches).note == "Проверить рассказ Дмитрия" && desk.Session.Get(FatherCase.Matches).studied, "Continue restores player annotation and studied status");
                    Check(Mathf.Approximately(desk.Session.State.zoom, 1.35f) && desk.Session.State.pan == new Vector2(-34,29), "Continue restores board zoom and pan");
                    Check(desk.Session.Pinned(FatherCase.Matches).position == new Vector2(540,160), "Continue restores exact page position");
                    // Keep the serialized artifact for inspection, then verify reset semantics.
                    File.Copy(SaveSystem.FilePath, Path.Combine(output, "verified-save.json"), true);
                    GameSession.StartNewGame();
                    Check(ClueJournal.Count == 0 && desk.Session.State.found.Count == 0 && desk.Session.State.insights.Count == 0 && !File.Exists(SaveSystem.FilePath), "New Game clears diary, board and old plot flags");
                    File.WriteAllText(Path.Combine(output, "result.txt"), log + "ALL CHECKS PASSED\n");
                    SessionState.SetBool(Armed, false);
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(output, "result.txt"), log + "FAILED " + error);
                Debug.LogException(error); SessionState.SetBool(Armed, false); EditorApplication.Exit(1);
            }
        }
    }
}

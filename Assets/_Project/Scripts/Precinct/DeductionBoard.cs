using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using DetectiveGame.Core;

namespace CarXCase.Precinct
{
    /// The detective's physical evidence board. Its cards are picked directly in the game camera.
    public sealed class DeductionBoard : MonoBehaviour, IInteractable
    {
        [Serializable] public class ClueDef { public string id, title; [TextArea] public string description; }
        [Serializable] public class Deduction { public string id, clueA, clueB, title; [TextArea] public string text; }
        [Serializable] public class Option { public string label, requiresConclusion, targetScene; [TextArea] public string outcome; public string unavailableStatus; }

        [Header("Content")]
        [Tooltip("Used when the scene is started directly (no clues were collected in the apartment).")]
        public ClueDef[] fallbackClues;
        public Deduction[] deductions;
        public Option[] options;
        public int conclusionsForDecision = 3;
        public string[] requiredConclusions;

        [Header("Scene")]
        public Transform viewPoint;
        [Tooltip("Board surface: local X = right, local Y = up, local Z points out of the board.")]
        public Transform surface;
        public Vector2 surfaceSize = new Vector2(1.7f, 1.0f);
        public Material cardMaterial, pinMaterial, yarnMaterial, textMaterial;
        public Material photoMaterial;
        [Tooltip("The texture name must match the clue ID.")]
        public Texture2D[] cluePhotos;
        public float cameraBlendTime = 0.6f;

        enum Mode { Closed, Decision, Outcome }
        Mode mode;
        int chosen = -1;
        string travelError;
        float leaveFade = -1;
        OfficerController officer;
        ThirdPersonCamera follow;
        bool restoreOfficer, restoreFollow;
        CursorLockMode cursor;
        bool cursorVisible;
        GUIStyle h1, body, bodySmall, optionStyle;

        public bool CanInteract => mode == Mode.Closed && !Investigation.InvestigationIntegration.IsOpen;
        public string InteractPrompt => "E  Осмотреть доску улик";
        public bool DecisionReady => options != null && options.Any(Unlocked);
        internal bool HasAvailableDestination => options != null && options.Any(option =>
            Unlocked(option) && string.IsNullOrEmpty(option.unavailableStatus) &&
            !string.IsNullOrEmpty(option.targetScene) && Application.CanStreamedLevelBeLoaded(option.targetScene));
        static bool Unlocked(Option option) => option != null &&
            HasRouteThread(Investigation.InvestigationIntegration.State, option.requiresConclusion);

        static bool HasRouteThread(Casebook.CaseState state, string conclusion) =>
            !string.IsNullOrEmpty(conclusion) && state?.threads.Contains(conclusion) == true;

        void OnEnable()
        {
            Investigation.InvestigationIntegration.RegisterBoard(this);
            if (!TryGetComponent<PrecinctReadingBoard>(out _)) gameObject.AddComponent<PrecinctReadingBoard>();
        }
        public void Interact(SimpleUIHUD hud)
        {
            if (!CanInteract) return;
            hud?.ClearPrompt();
            Investigation.InvestigationIntegration.OpenBoard(this);
        }

        public void OpenDecision()
        {
            if (!DecisionReady || mode != Mode.Closed) return;
            Investigation.InvestigationIntegration.Close();
            officer = FindAnyObjectByType<OfficerController>();
            follow = Camera.main ? Camera.main.GetComponent<ThirdPersonCamera>() : null;
            restoreOfficer = officer && officer.enabled;
            restoreFollow = follow && follow.enabled;
            if (officer) officer.enabled = false;
            if (follow) follow.enabled = false;
            cursor = Cursor.lockState; cursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            mode = Mode.Decision;
        }
        void CloseDecision(bool reopen)
        {
            mode = Mode.Closed;
            if (officer) officer.enabled = restoreOfficer;
            if (follow) follow.enabled = restoreFollow;
            Cursor.lockState = cursor; Cursor.visible = cursorVisible;
            if (reopen) Investigation.InvestigationIntegration.OpenBoard(this);
        }
        void OnDisable()
        {
            if (mode != Mode.Closed) CloseDecision(false);
            Investigation.InvestigationIntegration.UnregisterBoard(this);
        }
        void Choose(int i)
        {
            if (options == null || i < 0 || i >= options.Length || !Unlocked(options[i])) return;
            chosen = i; travelError = null; mode = Mode.Outcome;
        }
        void Go()
        {
            if (chosen < 0 || !Unlocked(options[chosen]) || leaveFade >= 0 || !string.IsNullOrEmpty(options[chosen].unavailableStatus)) return;
            var target = options[chosen].targetScene;
            if (string.IsNullOrEmpty(target) || !Application.CanStreamedLevelBeLoaded(target))
            { travelError = "Сцена " + target + " не добавлена в Build Settings"; return; }
            ClueJournal.Decision = options[chosen].label;
            NoirMenu.GameSession.SaveCurrent();
            leaveFade = 0;
        }
        void Update()
        {
            if (mode == Mode.Closed) return;
            if (leaveFade >= 0)
            {
                leaveFade += Time.unscaledDeltaTime / 1.2f;
                if (leaveFade >= 1 && leaveFade < 50)
                { leaveFade = 99; SceneManager.LoadSceneAsync(options[chosen].targetScene); }
                return;
            }
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame)
            {
                if (mode == Mode.Outcome) { chosen = -1; mode = Mode.Decision; }
                else CloseDecision(true);
                return;
            }
            if (mode == Mode.Outcome && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) Go();
            if (mode == Mode.Decision)
            {
                var visible = options.Where(Unlocked).Take(3).ToArray();
                if (visible.Length > 0 && kb.digit1Key.wasPressedThisFrame) Choose(Array.IndexOf(options, visible[0]));
                if (visible.Length > 1 && kb.digit2Key.wasPressedThisFrame) Choose(Array.IndexOf(options, visible[1]));
                if (visible.Length > 2 && kb.digit3Key.wasPressedThisFrame) Choose(Array.IndexOf(options, visible[2]));
            }
        }

        // Travel selection uses the existing separate screen, after leaving the board close-up.
        void Styles()
        {
            if (h1 != null) return;
            h1 = new GUIStyle(GameTypography.Label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            h1.normal.textColor = new Color(1f, .85f, .4f);
            body = new GUIStyle(GameTypography.Label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            body.normal.textColor = Color.white;
            bodySmall = new GUIStyle(body);
            bodySmall.normal.textColor = new Color(.78f, .78f, .78f);
            optionStyle = new GUIStyle(GameTypography.Label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true, richText = true };
        }

        void OnGUI()
        {
            if (mode == Mode.Closed) return;
            Styles();
            float k = Mathf.Clamp(Screen.height / 720f, .7f, 2f);
            h1.fontSize = Mathf.RoundToInt(22 * k);
            body.fontSize = Mathf.RoundToInt(14 * k);
            bodySmall.fontSize = Mathf.RoundToInt(12 * k);
            optionStyle.fontSize = Mathf.RoundToInt(24 * k);
            float W = Screen.width, H = Screen.height;

            if (mode == Mode.Decision)
            {
                Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, .72f));
                GUI.Label(new Rect(0, H * .18f, W, 40 * k), "ЧТО ДЕЛАТЬ ДАЛЬШЕ?", h1);
                GUI.Label(new Rect(0, H * .18f + 38 * k, W, 24 * k), "Выберите направление. Esc — вернуться к доске.", bodySmall);
                Vector2 center = new Vector2(W / 2, H * .56f);
                var visible = options.Where(Unlocked).Take(3).ToArray();
                for (int slot = 0; slot < visible.Length; slot++)
                {
                    int i = Array.IndexOf(options, visible[slot]);
                    Vector2 position = center;
                    if (visible.Length == 2) position.x += (slot - .5f) * W * .47f;
                    if (visible.Length == 3)
                    {
                        float angle = (-90 + slot * 120) * Mathf.Deg2Rad;
                        position += new Vector2(Mathf.Cos(angle) * W * .22f, Mathf.Sin(angle) * H * .2f);
                    }
                    var rect = new Rect(position.x - 170 * k, position.y - 34 * k, 340 * k, 68 * k);
                    bool hover = rect.Contains(Event.current.mousePosition);
                    optionStyle.normal.textColor = hover ? new Color(1f, .85f, .4f) : Color.white;
                    GUI.Label(rect, (slot + 1) + ".  " + options[i].label, optionStyle);
                    if (Event.current.type == EventType.MouseDown && hover) Choose(i);
                }
            }
            else if (chosen >= 0)
            {
                Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, .8f));
                GUILayout.BeginArea(new Rect(W * .2f, H * .22f, W * .6f, H * .5f));
                GUILayout.Label("РЕШЕНИЕ: " + options[chosen].label.ToUpper(), h1);
                GUILayout.Space(14 * k);
                GUILayout.Label(options[chosen].outcome, body);
                GUILayout.FlexibleSpace();
                var status = options[chosen].unavailableStatus;
                if (!string.IsNullOrEmpty(status)) GUILayout.Label(status, bodySmall);
                else if (GUILayout.Button("Отправиться  [Enter]", GameTypography.Button, GUILayout.Height(36 * k))) Go();
                if (!string.IsNullOrEmpty(travelError)) GUILayout.Label(travelError, bodySmall);
                GUILayout.Label("Esc — выбрать другой путь", bodySmall);
                GUILayout.EndArea();
            }
            if (leaveFade >= 0) Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, Mathf.Clamp01(leaveFade)));
        }

        static void Fill(Rect rect, Color color)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}

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
        int focus;
        float openedAt;
        GUIStyle title, heading, detail, caption;
        static readonly Color Paper = new Color(.95f, .93f, .87f);
        static readonly Color Steel = new Color(.71f, .78f, .77f);
        static readonly Color Amber = new Color(1f, .79f, .45f);

        public bool CanInteract => mode == Mode.Closed && !Investigation.InvestigationIntegration.IsOpen;
        public string InteractPrompt => "E  Осмотреть доску улик";
        public bool DecisionReady => HasAvailableDestination;
        internal bool IsChoosingRoute => mode != Mode.Closed;
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
            openedAt = Time.unscaledTime;
            focus = Array.FindIndex(options, Unlocked);
            chosen = -1;
            travelError = null;
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
            chosen = focus = i; travelError = null; mode = Mode.Outcome;
        }
        void Go()
        {
            if (chosen < 0 || !Unlocked(options[chosen]) || leaveFade >= 0 || !string.IsNullOrEmpty(options[chosen].unavailableStatus)) return;
            var target = options[chosen].targetScene;
            if (string.IsNullOrEmpty(target) || !Application.CanStreamedLevelBeLoaded(target))
            { travelError = "Сцена " + target + " не добавлена в Build Settings"; return; }
            ClueJournal.Decision = options[chosen].label;
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
            if (mode != Mode.Decision) return;
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) Choose(0);
            else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) Choose(1);
            else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) Choose(2);
            else if (kb.leftArrowKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) MoveFocus(-1);
            else if (kb.rightArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) MoveFocus(1);
            else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Choose(focus);
        }

        void MoveFocus(int direction)
        {
            for (int step = 1; step <= options.Length; step++)
            {
                int index = (focus + direction * step + options.Length) % options.Length;
                if (!Unlocked(options[index])) continue;
                focus = index;
                break;
            }
        }

        void Styles()
        {
            if (title != null) return;
            title = new GUIStyle(GameTypography.Label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            heading = new GUIStyle(title) { wordWrap = true };
            detail = new GUIStyle(GameTypography.Label) { wordWrap = true, alignment = TextAnchor.UpperLeft };
            caption = new GUIStyle(GameTypography.Label) { alignment = TextAnchor.MiddleLeft };
        }

        void OnGUI()
        {
            if (mode == Mode.Closed) return;
            Styles();
            float w = Screen.width, h = Screen.height;
            float k = Mathf.Clamp(Mathf.Min(w / 1100f, h / 720f), .52f, 2f);
            float arrival = Mathf.Clamp01((Time.unscaledTime - openedAt) / .28f);
            float margin = Mathf.Min(w * .09f, 120f * k);
            Fill(new Rect(0f, 0f, w, h), new Color(.023f, .036f, .042f, .73f * arrival));
            Fill(new Rect(0f, 0f, w, h * .095f), new Color(.016f, .022f, .027f, .92f));
            Fill(new Rect(0f, h * .9f, w, h * .1f), new Color(.016f, .022f, .027f, .92f));
            Label(new Rect(margin, h * .135f, w - 2f * margin, 24f * k), "60-Й УЧАСТОК   /   01:20", caption, Steel, 17f * k);
            Label(new Rect(margin, h * .178f, w - 2f * margin, 66f * k), "Куда ехать?", title, Paper, 52f * k);

            if (mode == Mode.Decision)
                DrawOptions(w, h, k, margin, arrival);
            else if (chosen >= 0)
                DrawConfirmation(w, h, k, margin);

            if (leaveFade >= 0f) Fill(new Rect(0, 0, w, h), new Color(0, 0, 0, Mathf.Clamp01(leaveFade)));
        }

        void DrawOptions(float w, float h, float k, float margin, float arrival)
        {
            int count = Mathf.Min(3, options.Length);
            bool stacked = w / h < 1.35f;
            float gap = 24f * k;
            float width = stacked ? w - 2f * margin : (w - 2f * margin - (count - 1) * gap) / count;
            float height = stacked ? 100f * k : 145f * k;
            for (int i = 0; i < count; i++)
            {
                var option = options[i];
                if (option == null) continue;
                var rect = new Rect(margin + (stacked ? 0f : i * (width + gap)),
                    h * .42f + (stacked ? i * (height + gap) : i * 30f * k) + (1f - arrival) * 10f * k,
                    width, height);
                bool available = Unlocked(option);
                bool active = available && (i == focus || rect.Contains(Event.current.mousePosition));
                Color edge = active ? Amber : available ? Steel : new Color(.45f, .48f, .48f);
                Fill(new Rect(rect.x + 3f * k, rect.y + 5f * k, rect.width, rect.height), new Color(0f, 0f, 0f, .28f));
                Fill(rect, active ? new Color(.12f, .16f, .16f, .9f) : new Color(.05f, .075f, .082f, .83f));
                Fill(new Rect(rect.x, rect.y, rect.width, Mathf.Max(1f, k)), edge);
                Fill(new Rect(rect.x, rect.y, 3f * k, rect.height), edge);
                Label(new Rect(rect.x + 18f * k, rect.y + 12f * k, 32f * k, 26f * k), available ? $"[{i + 1}]" : "—", caption, edge, 17f * k);
                Label(new Rect(rect.x + 55f * k, rect.y + 10f * k, rect.width - 68f * k, 49f * k),
                    option.label, heading, available ? Paper : Steel, 32f * k);
                Label(new Rect(rect.x + 18f * k, rect.y + 65f * k, rect.width - 36f * k, rect.height - 68f * k),
                    available ? option.outcome : "Свяжите улики на доске", detail, Steel, 17f * k);
                if (available && GUI.Button(rect, GUIContent.none, GUIStyle.none)) Choose(i);
            }
            Label(new Rect(margin, h * .92f, w - 2f * margin, h * .044f), "← / →  Выбрать     Enter  Открыть     Esc  К доске", caption, Steel, 17f * k);
        }

        void DrawConfirmation(float w, float h, float k, float margin)
        {
            var option = options[chosen];
            float width = Mathf.Min(w - 2f * margin, 610f * k);
            var rect = new Rect(margin, h * .39f, width, 190f * k);
            Fill(rect, new Color(.045f, .069f, .076f, .9f));
            Fill(new Rect(rect.x, rect.y, rect.width, 2f * k), Amber);
            Label(new Rect(rect.x + 19f * k, rect.y + 12f * k, width - 38f * k, 50f * k), option.label, heading, Paper, 38f * k);
            Label(new Rect(rect.x + 19f * k, rect.y + 70f * k, width - 38f * k, 60f * k), option.outcome, detail, Steel, 20f * k);
            string status = travelError ?? option.unavailableStatus;
            if (string.IsNullOrEmpty(status))
            {
                var confirm = new Rect(rect.x + 19f * k, rect.yMax - 53f * k, width - 38f * k, 36f * k);
                Fill(confirm, confirm.Contains(Event.current.mousePosition) ? new Color(.22f, .19f, .13f, .94f) : new Color(.11f, .13f, .13f, .92f));
                Label(new Rect(confirm.x + 12f * k, confirm.y, confirm.width - 16f * k, confirm.height), "[Enter]   Поехать", caption, Amber, 21f * k);
                if (GUI.Button(confirm, GUIContent.none, GUIStyle.none)) Go();
            }
            else Label(new Rect(rect.x + 19f * k, rect.yMax - 52f * k, width - 38f * k, 38f * k), status, detail, Amber, 17f * k);
            Label(new Rect(margin, h * .92f, w - 2f * margin, h * .044f), "Esc  Сменить направление", caption, Steel, 17f * k);
        }

        static void Label(Rect rect, string text, GUIStyle style, Color color, float size)
        {
            style.fontSize = Mathf.Max(11, Mathf.RoundToInt(size));
            style.normal.textColor = color;
            GUI.Label(rect, text, style);
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

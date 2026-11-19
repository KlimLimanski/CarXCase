using UnityEngine;
using UnityEngine.InputSystem;

namespace DetectiveGame.Core
{
    public class SimpleUIHUD : MonoBehaviour
    {
        private string _promptText = "";
        private bool _isShowingClue = false;
        private string _clueTitle = "";
        private string _clueDesc = "";
        private string _inspectionLine = "";
        private float _inspectionUntil;
        float journalHintUntil;
        GUIStyle journalHintStyle;
        private GUIStyle _inspectionPromptStyle;
        public bool showInspectionHud;
        public bool enableJournal;
        public bool IsListening { get; set; }
        public bool IsShowingJournal => CarXCase.Investigation.InvestigationIntegration.IsOpen;

        public void ShowInspectionComment(string line)
        {
            ShowInspectionComment(line, Mathf.Clamp(2.7f + line.Length * .033f, 3.2f, 7f));
        }

        public void ShowInspectionComment(string line, float duration)
        {
            _inspectionLine = line;
            _inspectionUntil = Time.unscaledTime + duration;
        }

        public void ShowJournalHint() => journalHintUntil = Time.unscaledTime + 8f;
        /// Drawn by JournalKeyHint: true once the scene asked for the hint, Fresh while it should stay bright.
        public bool JournalHintRequested => journalHintUntil > 0f;
        public bool JournalHintFresh => Time.unscaledTime < journalHintUntil;

        public bool HandleJournalInput(Transform view, bool freeInput)
        {
            if (IsShowingJournal) return true;
            if (!freeInput || IsListening || _isShowingClue) return false;
            return CarXCase.Investigation.InvestigationIntegration.HandleInput(this);
        }

        public void CloseJournal() => CarXCase.Investigation.InvestigationIntegration.Close();
        void OnDisable() => CloseJournal();

        public string CurrentInspectionComment => Time.unscaledTime < _inspectionUntil ? _inspectionLine : "";

        public void SetPrompt(string text)
        {
            _promptText = text;
        }

        public void ClearPrompt()
        {
            _promptText = "";
        }

        public void ShowClue(string title, string desc)
        {
            _isShowingClue = true;
            _clueTitle = title;
            _clueDesc = desc;
        }

        public bool IsShowingClue() => _isShowingClue;

        private void Update()
        {
            if (_isShowingClue && Keyboard.current != null)
            {
                if (Keyboard.current.spaceKey.wasPressedThisFrame ||
                    Keyboard.current.escapeKey.wasPressedThisFrame ||
                    Keyboard.current.eKey.wasPressedThisFrame)
                {
                    _isShowingClue = false;
                }
            }
        }

        [Tooltip("HUD прототипа (подсказки управления, прицел, подсказка взаимодействия). Пока выключен.")]
        public bool showHud = false;

        private void OnGUI()
        {
            if (IsShowingJournal)
            {
                return;
            }
            if (showInspectionHud)
            {
                DrawInspectionHud();
                DrawClueOnly();
                return;
            }
            if (!showHud) { DrawClueOnly(); return; }
            Rect infoRect = new Rect(20, 20, 320, 145);
            GUI.Box(infoRect, "");
            GUILayout.BeginArea(new Rect(30, 25, 300, 135));
            GUI.color = new Color(1f, 0.85f, 0.4f);
            GUILayout.Label("CASE 1999: TEST PROTO");
            GUI.color = Color.white;
            GUILayout.Label("WASD — Перемещение");
            GUILayout.Label("Left Shift — Бег");
            GUILayout.Label("Мышь — Обзор (Esc — курсор)");
            GUILayout.Label("F — Фонарик | E — Осмотр");
            GUI.color = new Color(0.4f, 0.95f, 1f);
            GUILayout.Label("T — Быстрый тест QTE");
            GUILayout.EndArea();

            if (!_isShowingClue && (QteManager.Instance == null || !QteManager.Instance.IsQteActive()))
            {
                float centerX = Screen.width / 2f;
                float centerY = Screen.height / 2f;
                GUI.color = new Color(1f, 1f, 1f, 0.7f);
                GUI.Box(new Rect(centerX - 3, centerY - 3, 6, 6), "");
            }

            if (!string.IsNullOrEmpty(_promptText) && !_isShowingClue && (QteManager.Instance == null || !QteManager.Instance.IsQteActive()))
            {
                float width = 500;
                float height = 44;
                Rect promptRect = new Rect((Screen.width - width) / 2f, Screen.height - 110, width, height);
                GUI.Box(promptRect, "");
                var centeredStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 15,
                    fontStyle = FontStyle.Bold
                };
                centeredStyle.normal.textColor = new Color(0.4f, 0.95f, 1f);
                GUI.Label(promptRect, _promptText, centeredStyle);
            }

            DrawClueOnly();
        }

        void DrawInspectionHud()
        {
            if (_isShowingClue || (QteManager.Instance && QteManager.Instance.IsQteActive())) return;
            var oldColor = GUI.color;
            GUI.color = Color.white;
            float x = Screen.width * .5f;
            float y = Screen.height * .5f;
            float scale = Mathf.Clamp(Screen.height / 1080f, .7f, 1.8f);
            // No crosshair: prompts live in the scene, the frame stays clean.
            GUI.color = Color.white;
            if (!string.IsNullOrEmpty(_promptText))
            {
                if (_inspectionPromptStyle == null)
                    _inspectionPromptStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, richText = false };
                _inspectionPromptStyle.fontSize = Mathf.RoundToInt(19 * scale);
                float width = Mathf.Min(Screen.width * .88f, _inspectionPromptStyle.CalcSize(new GUIContent(_promptText)).x + 22 * scale);
                var area = new Rect(x-width*.5f, y+14*scale, width, 40*scale);
                var shadow = new Rect(area.x + scale, area.y + scale, area.width, area.height);
                GUI.color = new Color(.035f, .030f, .024f, .85f);
                GUI.Label(shadow, _promptText, _inspectionPromptStyle);
                GUI.color = new Color(.99f, .95f, .82f);
                GUI.Label(area, _promptText, _inspectionPromptStyle);
            }
            GUI.color = Color.white;
            CarXCase.UI.Subtitles.Draw(CurrentInspectionComment);
            if (Time.unscaledTime < journalHintUntil && !GetComponent<CarXCase.Investigation.JournalKeyHint>())
            {
                journalHintStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
                journalHintStyle.fontSize = Mathf.RoundToInt(17 * scale);
                var hint = new Rect(Screen.width - 264 * scale, 24 * scale, 240 * scale, 40 * scale);
                GUI.color = new Color(.03f, .025f, .02f, .83f);
                GUI.DrawTexture(hint, Texture2D.whiteTexture);
                GUI.color = new Color(.99f, .95f, .82f);
                GUI.Label(hint, "J  —  Открыть дневник", journalHintStyle);
            }
            GUI.color = oldColor;
        }

        private void DrawClueOnly()
        {
            if (_isShowingClue)
            {
                float dialogWidth = 560;
                float dialogHeight = 240;
                Rect dialogRect = new Rect((Screen.width - dialogWidth) / 2f, (Screen.height - dialogHeight) / 2f, dialogWidth, dialogHeight);
                GUI.color = Color.white;
                GUI.Box(dialogRect, "");

                GUILayout.BeginArea(new Rect(dialogRect.x + 25, dialogRect.y + 20, dialogWidth - 50, dialogHeight - 40));
                
                var titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
                    fontStyle = FontStyle.Bold
                };
                titleStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);
                GUILayout.Label($"[УЛИКА]: {_clueTitle.ToUpper()}", titleStyle);

                GUILayout.Space(12);

                var bodyStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    wordWrap = true
                };
                bodyStyle.normal.textColor = Color.white;
                GUILayout.Label(_clueDesc, bodyStyle);

                GUILayout.FlexibleSpace();

                var footStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Italic
                };
                footStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
                GUILayout.Label("Нажмите [ПРОБЕЛ] или [E], чтобы вернуться", footStyle);

                GUILayout.EndArea();
            }
        }
    }
}

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
        public bool IsShowingJournal { get; private set; }

        int selectedEntry;
        Vector2 journalListScroll, journalDetailScroll;
        ThirdPersonCamera journalFollow;
        CursorLockMode journalCursorLock;
        bool journalCursorVisible;
        GUIStyle journalTitle, journalBody, journalEntry;

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

        public bool HandleJournalInput(Transform view, bool freeInput)
        {
            var keyboard = Keyboard.current;
            if (IsShowingJournal)
            {
                if (!freeInput || (keyboard != null &&
                    (keyboard.jKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)))
                    CloseJournal();
                return true;
            }

            if (!enableJournal || !freeInput || IsListening || _isShowingClue ||
                keyboard == null || !keyboard.jKey.wasPressedThisFrame) return false;

            IsShowingJournal = true;
            journalHintUntil = 0f;
            journalCursorLock = Cursor.lockState;
            journalCursorVisible = Cursor.visible;
            journalFollow = view ? view.GetComponent<ThirdPersonCamera>() : null;
            if (journalFollow) journalFollow.SetCursorLock(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return true;
        }

        public void CloseJournal()
        {
            if (!IsShowingJournal) return;
            IsShowingJournal = false;
            if (journalFollow) journalFollow.SetCursorLock(journalCursorLock == CursorLockMode.Locked);
            Cursor.lockState = journalCursorLock;
            Cursor.visible = journalCursorVisible;
            journalFollow = null;
        }

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
                DrawJournal();
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

        void DrawJournal()
        {
            float scale = Mathf.Clamp(Screen.height / 850f, .65f, 1.7f);
            float width = Mathf.Min(Screen.width * .92f, 880 * scale);
            float height = Mathf.Min(Screen.height * .86f, 560 * scale);
            float pad = 24 * scale;
            var panel = new Rect((Screen.width - width) / 2, (Screen.height - height) / 2, width, height);

            var previous = GUI.color;
            GUI.color = new Color(0, 0, 0, .66f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = new Color(.105f, .095f, .080f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = new Color(.50f, .41f, .27f);
            GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 2 * scale), Texture2D.whiteTexture);
            GUI.color = Color.white;

            journalTitle ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, wordWrap = true };
            journalTitle.normal.textColor = new Color(.98f, .85f, .61f);
            journalTitle.fontSize = Mathf.RoundToInt(24 * scale);
            journalBody ??= new GUIStyle(GUI.skin.label) { wordWrap = true };
            journalBody.normal.textColor = new Color(.94f, .90f, .81f);
            journalBody.fontSize = Mathf.RoundToInt(18 * scale);
            journalEntry ??= new GUIStyle(GUI.skin.button) { wordWrap = true, alignment = TextAnchor.MiddleLeft };
            journalEntry.fontSize = Mathf.RoundToInt(16 * scale);

            GUI.Label(new Rect(panel.x + pad, panel.y + pad * .5f, width - 2 * pad, 38 * scale),
                "Дневник", journalTitle);
            float top = panel.y + 58 * scale;
            float columnsHeight = height - 103 * scale;
            float listWidth = (width - 3 * pad) * .39f;
            var entries = ClueJournal.Entries;

            if (entries.Count == 0)
                GUI.Label(new Rect(panel.x + pad, top, width - 2 * pad, 36 * scale), "Пока нет записей", journalBody);
            else
            {
                selectedEntry = Mathf.Clamp(selectedEntry, 0, entries.Count - 1);
                GUILayout.BeginArea(new Rect(panel.x + pad, top, listWidth, columnsHeight));
                journalListScroll = GUILayout.BeginScrollView(journalListScroll);
                for (int i = 0; i < entries.Count; i++)
                {
                    GUI.backgroundColor = i == selectedEntry ? new Color(.77f, .60f, .37f) : Color.white;
                    if (GUILayout.Button(entries[i].title, journalEntry, GUILayout.MinHeight(42 * scale)))
                    {
                        selectedEntry = i;
                        journalDetailScroll = Vector2.zero;
                    }
                }
                GUI.backgroundColor = Color.white;
                GUILayout.EndScrollView();
                GUILayout.EndArea();

                float detailX = panel.x + 2 * pad + listWidth;
                GUILayout.BeginArea(new Rect(detailX, top, panel.xMax - pad - detailX, columnsHeight));
                journalDetailScroll = GUILayout.BeginScrollView(journalDetailScroll);
                var entry = entries[selectedEntry];
                GUILayout.Label(entry.title, journalTitle);
                GUILayout.Space(10 * scale);
                GUILayout.Label(entry.description, journalBody);
                if (!string.IsNullOrEmpty(entry.thought))
                {
                    GUILayout.Space(12 * scale);
                    GUILayout.Label("Алексей: " + entry.thought, journalBody);
                }
                GUILayout.EndScrollView();
                GUILayout.EndArea();
            }

            GUI.Label(new Rect(panel.x + pad, panel.yMax - 38 * scale, width - 2 * pad, 28 * scale),
                "J / Esc — закрыть", journalBody);
            GUI.color = previous;
        }

        void DrawInspectionHud()
        {
            if (_isShowingClue || (QteManager.Instance && QteManager.Instance.IsQteActive())) return;
            var oldColor = GUI.color;
            GUI.color = Color.white;
            float x = Screen.width * .5f;
            float y = Screen.height * .5f;
            float scale = Mathf.Clamp(Screen.height / 1080f, .7f, 1.8f);
            float dotSize = Mathf.Max(2, 3 * scale);
            GUI.color = new Color(0, 0, 0, .65f);
            GUI.DrawTexture(new Rect(x-dotSize*.5f-1, y-dotSize*.5f-1, dotSize+2, dotSize+2), Texture2D.whiteTexture);
            GUI.color = new Color(.96f, .94f, .85f, .85f);
            GUI.DrawTexture(new Rect(x-dotSize*.5f, y-dotSize*.5f, dotSize, dotSize), Texture2D.whiteTexture);
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
            if (Time.unscaledTime < journalHintUntil)
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

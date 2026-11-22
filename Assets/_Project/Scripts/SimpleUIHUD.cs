using UnityEngine;
using UnityEngine.InputSystem;
using CarXCase.Precinct;

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
            if (!destinationAnnounced && DestinationAvailable())
            {
                destinationAnnounced = true;
                locationNoticeUntil = Time.unscaledTime + 4f;
            }

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

        private OfficerController objectivePlayer;
        private CarXCase.Precinct.SceneExit apartmentExit;
        private static GUIStyle objectiveStyle;
        private string objectiveScene;
        private DeductionBoard precinctBoard;
        private HangarFinale harborFinale;
        private bool destinationAnnounced;
        private float locationNoticeUntil;

        private bool DestinationAvailable()
        {
            switch (objectiveScene)
            {
                case "01_FatherApartment":
                    return apartmentExit && apartmentExit.requiredClueIds is { Length: > 0 } ids &&
                        System.Array.TrueForAll(ids, ClueJournal.Has);
                case "04_Precinct":
                    return precinctBoard && precinctBoard.HasAvailableDestination;
                case "02_Bar":
                    return ClueJournal.Has(BarServiceRoute.CoordinatesId);
                default:
                    return false;
            }
        }

        private void Start()
        {
            objectiveScene = gameObject.scene.name;
            objectivePlayer = FindAnyObjectByType<OfficerController>();
            if (objectiveScene == "01_FatherApartment")
                apartmentExit = FindAnyObjectByType<CarXCase.Precinct.SceneExit>();
            if (objectiveScene == "04_Precinct")
                precinctBoard = FindAnyObjectByType<DeductionBoard>();
            if (objectiveScene == "05_BrightonHarbor")
                harborFinale = FindAnyObjectByType<HangarFinale>();
            destinationAnnounced = DestinationAvailable();
        }

        private string CurrentObjective()
        {
            switch (objectiveScene)
            {
                case "01_FatherApartment":
                    if (DestinationAvailable())
                        return "Отправляйтесь в полицейский участок";
                    return "Найдите улики в квартире отца";
                case "02_Bar":
                    if (ClueJournal.Has(BarServiceRoute.CoordinatesId)) return "Отправляйтесь в порт";
                    return ClueJournal.Has(BarServiceRoute.DepartureFlag) ? "Проверьте записку у кассы" : "Поговорите с Дмитрием";
                case "03_Pawnshop": return "Поговорите с Сёмой";
                case "04_Precinct": return "Изучите доску расследования";
                case "05_BrightonHarbor":
                    return ClueJournal.Has("harbor_gate_open") ? (harborFinale ? harborFinale.Objective : "Осмотрите склад") : "Откройте ворота порта";
                default: return null;
            }
        }

        private void DrawObjective()
        {
            if (_isShowingClue || IsListening || (objectivePlayer && (objectivePlayer.InputLocked || !objectivePlayer.enabled)) ||
                (QteManager.Instance && QteManager.Instance.IsQteActive())) return;

            string text = CurrentObjective();
            if (string.IsNullOrEmpty(text)) return;
            DrawObjectiveCard(text, showHud && !showInspectionHud ? 178 : Screen.height * .085f);
        }

        internal static void DrawObjectiveCard(string text, float top)
        {
            float scale = Mathf.Clamp(Screen.height / 800f, .72f, 2f);
            objectiveStyle ??= new GUIStyle(GameTypography.Label) { wordWrap = true, padding = new RectOffset() };
            objectiveStyle.fontSize = Mathf.RoundToInt(19 * scale);
            objectiveStyle.normal.textColor = new Color(.96f, .94f, .86f);
            var content = new GUIContent(text);
            float inset = 15 * scale;
            float width = Mathf.Min(Screen.width * .70f, objectiveStyle.CalcSize(content).x + 2 * inset);
            float bodyHeight = objectiveStyle.CalcHeight(content, width - 2 * inset);
            var card = new Rect(24 * scale, top,
                width, bodyHeight + 20 * scale);
            Color previous = GUI.color;
            GUI.color = new Color(0, 0, 0, .22f);
            GUI.DrawTexture(new Rect(card.x + 2 * scale, card.y + 3 * scale, width, card.height), Texture2D.whiteTexture);
            GUI.color = new Color(.08f, .12f, .12f, .91f);
            GUI.DrawTexture(card, Texture2D.whiteTexture);
            GUI.color = new Color(.77f, .65f, .43f);
            GUI.DrawTexture(new Rect(card.x, card.y, 42 * scale, Mathf.Max(1, 2 * scale)), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(card.x + inset, card.y + 10 * scale, width - 2 * inset, bodyHeight + scale), content, objectiveStyle);
            GUI.color = previous;
        }

        private void OnGUI()
        {
            if (Time.unscaledTime < locationNoticeUntil)
                DrawObjectiveCard("Открыта новая локация", Screen.height * .19f);
            if (IsShowingJournal)
            {
                return;
            }
            DrawObjective();
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
            GUILayout.Label("CASE 1999: TEST PROTO", GameTypography.Label);
            GUI.color = Color.white;
            GUILayout.Label("WASD — Перемещение", GameTypography.Label);
            GUILayout.Label("Left Shift — Бег", GameTypography.Label);
            GUILayout.Label("Мышь — Обзор (Esc — курсор)", GameTypography.Label);
            GUILayout.Label("F — Фонарик | E — Осмотр", GameTypography.Label);
            GUI.color = new Color(0.4f, 0.95f, 1f);
            GUILayout.Label("T — Быстрый тест QTE", GameTypography.Label);
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
                var centeredStyle = new GUIStyle(GameTypography.Label)
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
                    _inspectionPromptStyle = new GUIStyle(GameTypography.Label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, richText = false };
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
                journalHintStyle ??= new GUIStyle(GameTypography.Label) { alignment = TextAnchor.MiddleCenter };
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
                
                var titleStyle = new GUIStyle(GameTypography.Label)
                {
                    fontSize = 18,
                    fontStyle = FontStyle.Bold
                };
                titleStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);
                GUILayout.Label($"[УЛИКА]: {_clueTitle.ToUpper()}", titleStyle);

                GUILayout.Space(12);

                var bodyStyle = new GUIStyle(GameTypography.Label)
                {
                    fontSize = 14,
                    wordWrap = true
                };
                bodyStyle.normal.textColor = Color.white;
                GUILayout.Label(_clueDesc, bodyStyle);

                GUILayout.FlexibleSpace();

                var footStyle = new GUIStyle(GameTypography.Label)
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

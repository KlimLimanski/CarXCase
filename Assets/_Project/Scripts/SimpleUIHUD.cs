using UnityEngine;
using CarXCase.Precinct;

namespace DetectiveGame.Core
{
    public class SimpleUIHUD : MonoBehaviour
    {
        private string _promptText = "";
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

        public void ShowInspectionComment(string line, float duration, string speaker = "Алексей")
        {
            _inspectionLine = CarXCase.UI.Subtitles.WithSpeaker(speaker, line);
            _inspectionUntil = Time.unscaledTime + duration;
        }

        public void ShowJournalHint() => journalHintUntil = Time.unscaledTime + 8f;
        /// Drawn by JournalKeyHint: true once the scene asked for the hint, Fresh while it should stay bright.
        public bool JournalHintRequested => journalHintUntil > 0f;
        public bool JournalHintFresh => Time.unscaledTime < journalHintUntil;

        public bool HandleJournalInput(Transform view, bool freeInput)
        {
            if (IsShowingJournal) return true;
            if (!freeInput || IsListening) return false;
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

        private OfficerController objectivePlayer;
        private CarXCase.Precinct.SceneExit apartmentExit;
        private string objectiveScene;
        private DeductionBoard precinctBoard;
        private ConversationInteraction barConversation;
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
                    return PortLead.HasAny(PortLead.Bar);
                case "03_Pawnshop":
                    return PortLead.HasAny(PortLead.Pawnshop);
                default:
                    return false;
            }
        }

        private void Start()
        {
            objectiveScene = gameObject.scene.name;
            objectivePlayer = FindAnyObjectByType<OfficerController>();
            if (objectiveScene == "02_Bar")
                barConversation = FindAnyObjectByType<ConversationInteraction>();
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
                    if (PortLead.HasAny(PortLead.Bar)) return "Отправляйтесь в Шипсхед-Бей";
                    if (ClueJournal.Has(BarServiceRoute.DepartureFlag)) return "Осмотрите кассу за стойкой";
                    if (ClueJournal.Has(PortLead.AskWitness)) return "Поговорите с девушкой в конце стойки";
                    if (barConversation && !barConversation.ReadyToTalk)
                        return "Осмотрите зал бара";
                    return "Поговорите с Дмитрием";
                case "03_Pawnshop":
                    if (PortLead.HasAny(PortLead.Pawnshop)) return "Отправляйтесь в Шипсхед-Бей";
                    return ClueJournal.Has(PortLead.CheckSafe) ? "Найдите сейф за стойкой" : "Осмотрите ломбард";
                case "04_Precinct": return "Осмотрите кабинет";
                case "05_BrightonHarbor":
                    return ClueJournal.Has("harbor_gate_open") ? (harborFinale ? harborFinale.Objective : "Осмотрите склад") : "Откройте ворота порта";
                default: return null;
            }
        }

        private void Update()
        {
            if (destinationAnnounced || !DestinationAvailable()) return;
            destinationAnnounced = true;
            locationNoticeUntil = Time.unscaledTime + 4f;
        }

        private void DrawObjective()
        {
            if (IsListening || (objectivePlayer && (objectivePlayer.InputLocked || !objectivePlayer.enabled)) ||
                (QteManager.Instance && QteManager.Instance.IsQteActive())) return;

            string text = CurrentObjective();
            if (string.IsNullOrEmpty(text)) return;
            DrawObjectiveCard(text, Screen.height * .085f);
        }

        private static GUIStyle objectiveStyle;

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
            if (IsShowingJournal || (precinctBoard && precinctBoard.IsChoosingRoute)) return;
            DrawObjective();
            CarXCase.UI.Subtitles.Draw(CurrentInspectionComment);
            if (showInspectionHud) DrawInspectionHud();
        }

        void DrawInspectionHud()
        {
            if (QteManager.Instance && QteManager.Instance.IsQteActive()) return;
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

    }
}

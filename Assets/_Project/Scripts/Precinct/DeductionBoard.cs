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

        const float ThreadDuration = .34f;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        static readonly int EmissionMap = Shader.PropertyToID("_EmissionMap");
        static readonly string[] CardOrder =
        {
            Apartment.FatherCase.Matches, Apartment.FatherCase.Messages, Apartment.FatherCase.Valuables,
            Apartment.FatherCase.Door, Apartment.FatherCase.Record
        };

        enum Mode { Closed, Board, Decision, Outcome }
        Mode mode;
        readonly Dictionary<string, Transform> cards = new Dictionary<string, Transform>();
        readonly List<Deduction> found = new List<Deduction>();
        MaterialPropertyBlock appearance;

        string firstCard, secondCard, hoveredCard;
        bool rejected;
        float clearSelectionAt;
        Transform decisionNote;
        LineRenderer stretchingThread;
        Vector3 threadStart, threadEnd;
        float threadStartedAt;

        int chosen = -1;
        string travelError;
        float leaveFade = -1;
        Camera cam;
        Vector3 savedPos;
        Quaternion savedRot;
        float savedFov, blend;
        OfficerController officer;
        CharacterController capsule;
        ThirdPersonCamera follow;
        Renderer[] hiddenHeroRenderers = Array.Empty<Renderer>();
        bool restoreOfficer, restoreCapsule, restoreFollow;
        SimpleUIHUD gameplayHud;
        bool restoreHud;
        GUIStyle h1, body, bodySmall, optionStyle;

        public bool CanInteract => mode == Mode.Closed;
        public string InteractPrompt => "E  Осмотреть доску улик";

        public void Interact(SimpleUIHUD hud)
        {
            if (mode != Mode.Closed || !surface || !viewPoint || !Camera.main) return;
            BuildCards();

            cam = Camera.main;
            savedPos = cam.transform.position;
            savedRot = cam.transform.rotation;
            savedFov = cam.fieldOfView;
            blend = 0;

            officer = FindAnyObjectByType<OfficerController>();
            follow = cam.GetComponent<ThirdPersonCamera>();
            gameplayHud = hud;
            restoreHud = hud && hud.enabled;
            restoreOfficer = officer && officer.enabled;
            restoreFollow = follow && follow.enabled;
            capsule = officer ? officer.GetComponent<CharacterController>() : null;
            restoreCapsule = capsule && capsule.enabled;
            if (officer) officer.enabled = false;
            if (capsule) capsule.enabled = false;
            if (follow) follow.enabled = false;

            var hero = officer ? officer.transform.Find("Character_Visual") : null;
            if (hero)
            {
                hiddenHeroRenderers = hero.GetComponentsInChildren<Renderer>(true)
                    .Where(renderer => !renderer.forceRenderingOff).ToArray();
                foreach (var renderer in hiddenHeroRenderers) renderer.forceRenderingOff = true;
            }
            if (hud)
            {
                hud.ClearPrompt();
                hud.enabled = false;
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            mode = Mode.Board;
        }

        void Close()
        {
            mode = Mode.Closed;
            FinishThread();
            hoveredCard = null;
            ClearSelection();
            if (cam)
            {
                cam.transform.SetPositionAndRotation(savedPos, savedRot);
                cam.fieldOfView = savedFov;
            }
            if (gameplayHud) gameplayHud.enabled = restoreHud;
            if (capsule) capsule.enabled = restoreCapsule;
            if (officer) officer.enabled = restoreOfficer;
            if (follow)
            {
                follow.enabled = restoreFollow;
                if (restoreFollow) follow.SetCursorLock(true);
            }
            RestoreHeroVisibility();
        }

        void RestoreHeroVisibility()
        {
            foreach (var renderer in hiddenHeroRenderers)
                if (renderer) renderer.forceRenderingOff = false;
            hiddenHeroRenderers = Array.Empty<Renderer>();
        }

        void OnDisable()
        {
            if (mode == Mode.Closed) return;
            if (leaveFade < 0) Close();
            else RestoreHeroVisibility();
        }

        void BuildCards()
        {
            if (cards.Count > 0) return;
            appearance = new MaterialPropertyBlock();
            var clues = ClueJournal.Entries.Select(e =>
                fallbackClues?.FirstOrDefault(card => card.id == e.id) ??
                new ClueDef { id = e.id, title = e.title, description = e.description }).ToList();
            if (clues.Count == 0 && fallbackClues != null) clues.AddRange(fallbackClues);
            clues = clues.OrderBy(clue =>
            {
                int position = Array.IndexOf(CardOrder, clue.id);
                return position >= 0 ? position : CardOrder.Length;
            }).ToList();

            int cols = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(clues.Count * 1.7f)), 1, 4);
            int rows = Mathf.Max(1, Mathf.CeilToInt(clues.Count / (float)cols));
            float slotWidth = surfaceSize.x * .94f / cols;
            float slotHeight = surfaceSize.y * .67f / rows;
            var size = new Vector2(Mathf.Min(.345f, slotWidth * .88f), Mathf.Min(.295f, slotHeight * .88f));
            var rng = new System.Random(1999);
            for (int i = 0; i < clues.Count; i++)
            {
                int col = i % cols;
                int row = i / cols;
                int rowCount = Mathf.Min(cols, clues.Count - row * cols);
                float x = (col - (rowCount - 1) / 2f) * slotWidth + Jitter(rng, .017f);
                float y = surfaceSize.y * .135f + ((rows - 1) / 2f - row) * slotHeight + Jitter(rng, .014f);
                var clue = clues[i];
                cards[clue.id] = MakeCard(clue, new Vector3(x, y, .014f), size, Jitter(rng, 3f));
            }

            foreach (var deduction in deductions)
            {
                if (!ClueJournal.Has(deduction.id) || found.Any(f => f.id == deduction.id)
                    || !cards.ContainsKey(deduction.clueA) || !cards.ContainsKey(deduction.clueB)) continue;
                AddDeduction(deduction, deduction.clueA, deduction.clueB, false);
            }
            UpdateDecisionNote();
            RefreshHighlights();
        }

        static float Jitter(System.Random random, float amount) => ((float)random.NextDouble() - .5f) * 2f * amount;

        Transform MakeCard(ClueDef clue, Vector3 position, Vector2 size, float tilt)
        {
            var root = Paper("Card_" + clue.id, position, size, tilt);
            var photo = cluePhotos?.FirstOrDefault(texture => texture && texture.name == clue.id);
            if (photo && photoMaterial)
            {
                var picture = new GameObject("Evidence_Photo").transform;
                picture.SetParent(root, false);
                picture.localPosition = new Vector3(0, size.y * .061f, .002f);
                picture.localRotation = Quaternion.Euler(0, 180, 0);
                picture.localScale = new Vector3(size.x * .89f, size.y * .69f, 1);
                picture.gameObject.AddComponent<MeshFilter>().sharedMesh = root.Find("Paper").GetComponent<MeshFilter>().sharedMesh;
                var renderer = picture.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = photoMaterial;
                appearance.Clear();
                appearance.SetTexture(BaseMap, photo);
                appearance.SetTexture(EmissionMap, photo);
                renderer.SetPropertyBlock(appearance);
                Print(root, "Title", clue.title, -.386f * size.y, size.x * .89f, size.y * .175f, 20, FontStyle.Bold, .0045f);
            }
            else
            {
                Print(root, "Title", clue.title, size.y * .26f, size.x * .88f, size.y * .22f, 19, FontStyle.Bold, .0055f);
                if (!string.IsNullOrWhiteSpace(clue.description))
                    Print(root, "Description", clue.description, -.12f * size.y, size.x * .87f, size.y * .44f, 32, FontStyle.Normal, .0035f);
            }
            return root;
        }

        Transform Paper(string name, Vector3 position, Vector2 size, float tilt)
        {
            var root = new GameObject(name).transform;
            root.SetParent(surface, false);
            root.localPosition = position;
            root.localRotation = Quaternion.Euler(0, 0, tilt);
            var paper = GameObject.CreatePrimitive(PrimitiveType.Quad);
            paper.name = "Paper";
            paper.transform.SetParent(root, false);
            paper.transform.localScale = new Vector3(size.x, size.y, 1);
            paper.transform.localRotation = Quaternion.Euler(0, 180, 0);
            if (cardMaterial) paper.GetComponent<Renderer>().sharedMaterial = cardMaterial;

            var pin = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pin.name = "Pin";
            pin.transform.SetParent(root, false);
            pin.transform.localPosition = new Vector3(0, size.y / 2f - .016f, .008f);
            pin.transform.localScale = new Vector3(.020f, .020f, .014f);
            pin.GetComponent<Collider>().enabled = false;
            if (pinMaterial) pin.GetComponent<Renderer>().sharedMaterial = pinMaterial;
            return root;
        }

        void Print(Transform root, string name, string text, float y, float width, float height, int columns, FontStyle style, float size)
        {
            var label = new GameObject(name).AddComponent<TextMesh>();
            label.transform.SetParent(root, false);
            label.transform.localPosition = new Vector3(0, y, .004f);
            label.transform.localRotation = Quaternion.Euler(0, 180, 0);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontStyle = style;
            label.fontSize = 64;
            label.characterSize = size;
            label.color = new Color(.025f, .019f, .014f);
            var renderer = label.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = textMaterial ? textMaterial : label.font.material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            label.text = Wrap(text, columns);
            Vector3 bounds = renderer.localBounds.size;
            float fit = Mathf.Min(1f, width / Mathf.Max(.001f, bounds.x), height / Mathf.Max(.001f, bounds.y));
            label.transform.localScale = Vector3.one * fit;
        }

        static string Wrap(string text, int width)
        {
            var lines = new List<string>();
            string line = "";
            foreach (var word in text.Split(' '))
            {
                if (line.Length + word.Length + 1 > width && line.Length > 0)
                {
                    lines.Add(line);
                    line = word;
                }
                else line = line.Length == 0 ? word : line + " " + word;
            }
            if (line.Length > 0) lines.Add(line);
            return string.Join("\n", lines);
        }

        void AddDeduction(Deduction deduction, string from, string to, bool animate)
        {
            found.Add(deduction);
            var line = new GameObject("Yarn_" + from + "_" + to).AddComponent<LineRenderer>();
            line.transform.SetParent(surface, false);
            line.useWorldSpace = false;
            line.positionCount = 9;
            line.numCapVertices = 2;
            line.widthMultiplier = .005f;
            line.shadowCastingMode = ShadowCastingMode.Off;
            if (yarnMaterial) line.sharedMaterial = yarnMaterial;
            Vector3 start = PinPosition(cards[from]);
            Vector3 end = PinPosition(cards[to]);
            ShapeThread(line, start, animate ? start : end);

            if (animate)
            {
                stretchingThread = line;
                threadStart = start;
                threadEnd = end;
                threadStartedAt = Time.unscaledTime;
            }

            var note = NotebookSlip("Conclusion_" + deduction.id, deduction.title, found.Count - 1, new Color(.79f, .68f, .46f));
            note.Find("Paper").GetComponent<Collider>().enabled = false;
        }

        Vector3 PinPosition(Transform card)
        {
            return surface.InverseTransformPoint(card.Find("Pin").position) + Vector3.forward * .009f;
        }

        static void ShapeThread(LineRenderer line, Vector3 start, Vector3 end)
        {
            for (int i = 0; i < line.positionCount; i++)
            {
                float t = i / (float)(line.positionCount - 1);
                Vector3 point = Vector3.Lerp(start, end, t);
                point.y -= Mathf.Sin(Mathf.PI * t) * Vector3.Distance(start, end) * .003f;
                line.SetPosition(i, point);
            }
        }

        void FinishThread()
        {
            if (!stretchingThread) return;
            ShapeThread(stretchingThread, threadStart, threadEnd);
            stretchingThread = null;
        }

        Transform NotebookSlip(string name, string title, int index, Color tint)
        {
            const int cols = 4;
            float slot = surfaceSize.x * .94f / cols;
            float noteWidth = slot * .84f;
            float noteHeight = surfaceSize.y * .086f;
            float x = (index % cols - (cols - 1) / 2f) * slot;
            float y = surfaceSize.y * (-.302f - index / cols * .118f);
            var root = Paper(name, new Vector3(x, y, .012f), new Vector2(noteWidth, noteHeight), index % 2 == 0 ? -1.2f : 1.5f);
            SetPaperTint(root, tint);
            Print(root, "Title", title, -noteHeight * .085f, noteWidth * .86f, noteHeight * .63f, 24, FontStyle.Bold, .0046f);
            return root;
        }

        void SetPaperTint(Transform card, Color color)
        {
            appearance.Clear();
            appearance.SetColor(BaseColor, color);
            card.Find("Paper").GetComponent<Renderer>().SetPropertyBlock(appearance);
        }

        void RefreshHighlights()
        {
            foreach (var entry in cards)
            {
                bool selected = entry.Key == firstCard || entry.Key == secondCard;
                bool used = found.Any(d => d.clueA == entry.Key || d.clueB == entry.Key);
                Color tint = used ? new Color(.96f, .92f, .79f) : new Color(.87f, .84f, .75f);
                if (entry.Key == hoveredCard) tint = new Color(1f, .96f, .83f);
                if (selected) tint = rejected ? new Color(.94f, .57f, .49f) : new Color(1f, .84f, .49f);
                SetPaperTint(entry.Value, tint);
            }
        }

        void ClearSelection()
        {
            firstCard = secondCard = null;
            rejected = false;
            clearSelectionAt = 0;
            RefreshHighlights();
        }

        void Select(string id)
        {
            if (secondCard != null) return;
            if (id == firstCard) { ClearSelection(); return; }
            if (firstCard == null)
            {
                firstCard = id;
                RefreshHighlights();
                return;
            }

            secondCard = id;
            var deduction = deductions.FirstOrDefault(d =>
                d.clueA == firstCard && d.clueB == secondCard || d.clueA == secondCard && d.clueB == firstCard);
            rejected = deduction == null;
            if (deduction != null && !found.Any(d => d.id == deduction.id))
            {
                ClueJournal.AddConclusion(deduction.id);
                AddDeduction(deduction, firstCard, secondCard, true);
                UpdateDecisionNote();
            }
            clearSelectionAt = Time.unscaledTime + ThreadDuration;
            RefreshHighlights();
        }

        int AvailableDeductions => deductions.Count(d => cards.ContainsKey(d.clueA) && cards.ContainsKey(d.clueB));
        int Needed => Mathf.Max(1, Mathf.Min(conclusionsForDecision, AvailableDeductions));
        bool Unlocked(int i) => string.IsNullOrEmpty(options[i].requiresConclusion) || found.Any(f => f.id == options[i].requiresConclusion);

        void UpdateDecisionNote()
        {
            bool ready = requiredConclusions != null && requiredConclusions.Length > 0
                ? requiredConclusions.All(id => found.Any(d => d.id == id))
                : found.Count >= Needed;
            if (decisionNote || options == null || !ready || !Enumerable.Range(0, options.Length).Any(Unlocked))
                return;
            decisionNote = NotebookSlip("Decision_Note", "Что дальше?", 7, new Color(1f, .85f, .49f));
        }

        void Choose(int i)
        {
            if (options == null || i < 0 || i >= options.Length || !Unlocked(i)) return;
            chosen = i;
            travelError = null;
            mode = Mode.Outcome;
        }

        void Go()
        {
            if (chosen < 0 || leaveFade >= 0) return;
            if (!string.IsNullOrEmpty(options[chosen].unavailableStatus)) return;
            var target = options[chosen].targetScene;
            if (string.IsNullOrEmpty(target) || !Application.CanStreamedLevelBeLoaded(target))
            {
                travelError = "Сцена " + target + " не добавлена в Build Settings";
                return;
            }
            ClueJournal.Decision = options[chosen].label;
            leaveFade = 0;
        }

        void Update()
        {
            if (mode == Mode.Closed) return;
            if (blend < 1f)
            {
                blend = Mathf.MoveTowards(blend, 1, Time.unscaledDeltaTime / Mathf.Max(cameraBlendTime, .01f));
                float t = Mathf.SmoothStep(0, 1, blend);
                cam.transform.SetPositionAndRotation(Vector3.Lerp(savedPos, viewPoint.position, t), Quaternion.Slerp(savedRot, viewPoint.rotation, t));
                float height = Mathf.Max(surfaceSize.y + .37f, (surfaceSize.x + .41f) / cam.aspect);
                float fov = 2 * Mathf.Atan(height * .5f / Vector3.Distance(surface.position, viewPoint.position)) * Mathf.Rad2Deg;
                cam.fieldOfView = Mathf.Lerp(savedFov, fov, t);
            }

            if (stretchingThread)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - threadStartedAt) / ThreadDuration);
                ShapeThread(stretchingThread, threadStart, Vector3.Lerp(threadStart, threadEnd, Mathf.SmoothStep(0, 1, t)));
                if (t >= 1f) FinishThread();
            }
            if (secondCard != null && Time.unscaledTime >= clearSelectionAt) ClearSelection();

            if (leaveFade >= 0)
            {
                leaveFade += Time.deltaTime / 1.2f;
                if (leaveFade >= 1 && leaveFade < 50)
                {
                    leaveFade = 99;
                    SceneManager.LoadSceneAsync(options[chosen].targetScene);
                }
                return;
            }

            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (mode == Mode.Decision) mode = Mode.Board;
                else if (mode == Mode.Outcome) { chosen = -1; mode = Mode.Decision; }
                else Close();
                return;
            }
            if (mode == Mode.Board && blend >= 1f) HandleBoardClick();
            if (mode == Mode.Outcome && kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) Go();
            if (mode == Mode.Decision && kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) Choose(0);
                if (kb.digit2Key.wasPressedThisFrame) Choose(1);
                if (kb.digit3Key.wasPressedThisFrame) Choose(2);
            }
        }

        void HandleBoardClick()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            string card = null;
            bool goToDecision = false;
            var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.Raycast(ray, out var hit, 5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                goToDecision = decisionNote && hit.collider.transform.IsChildOf(decisionNote);
                foreach (var entry in cards)
                    if (hit.collider.transform.IsChildOf(entry.Value)) { card = entry.Key; break; }
            }
            if (card != hoveredCard)
            {
                hoveredCard = card;
                RefreshHighlights();
            }
            if (!mouse.leftButton.wasPressedThisFrame) return;
            if (goToDecision)
            {
                hoveredCard = null;
                ClearSelection();
                mode = Mode.Decision;
            }
            else if (card != null) Select(card);
            else if (secondCard == null) ClearSelection();
        }

        // Travel selection uses the existing separate screen, after leaving the board close-up.
        void Styles()
        {
            if (h1 != null) return;
            h1 = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            h1.normal.textColor = new Color(1f, .85f, .4f);
            body = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            body.normal.textColor = Color.white;
            bodySmall = new GUIStyle(body);
            bodySmall.normal.textColor = new Color(.78f, .78f, .78f);
            optionStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true, richText = true };
        }

        void OnGUI()
        {
            if (mode == Mode.Closed || mode == Mode.Board) return;
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
                for (int i = 0; i < options.Length && i < 3; i++)
                {
                    float angle = (-90 + i * 120) * Mathf.Deg2Rad;
                    Vector2 position = center + new Vector2(Mathf.Cos(angle) * W * .22f, Mathf.Sin(angle) * H * .2f);
                    if (options.Length == 2) position = center + new Vector2((i - .5f) * W * .47f, 0);
                    position += new Vector2(Mathf.Sin(Time.time * 1.3f + i * 2) * 6 * k, Mathf.Cos(Time.time * 1.1f + i) * 5 * k);
                    var rect = new Rect(position.x - 170 * k, position.y - 34 * k, 340 * k, 68 * k);
                    bool open = Unlocked(i);
                    bool hover = open && rect.Contains(Event.current.mousePosition);
                    optionStyle.normal.textColor = !open ? new Color(.45f, .45f, .45f) : hover ? new Color(1f, .85f, .4f) : Color.white;
                    GUI.Label(rect, open ? (i + 1) + ".  " + options[i].label : (i + 1) + ".  ???\n<size=" + Mathf.RoundToInt(13 * k) + ">не хватает связи улик</size>", optionStyle);
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
                else if (GUILayout.Button("Отправиться  [Enter]", GUILayout.Height(36 * k))) Go();
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

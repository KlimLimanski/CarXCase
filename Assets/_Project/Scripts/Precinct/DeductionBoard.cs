using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using DetectiveGame.Core;
using CarXCase.Investigation;
using CarXCase.UI;

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

        enum Mode { Closed, SteppingAway, Decision, Leaving }
        Mode mode;
        int chosen = -1;
        float modeStarted;
        int decisionFrame;
        OfficerController officer;
        ThirdPersonCamera follow;
        CursorLockMode cursor;
        bool cursorVisible, loadScheduled;
        readonly DialogueChoiceRows choiceRows = new();
        GUIStyle prompt;

        public bool CanInteract => mode == Mode.Closed && !InvestigationIntegration.IsOpen;
        public string InteractPrompt => "E  Осмотреть доску улик";
        public bool DecisionReady => HasAvailableDestination;
        internal bool IsChoosingRoute => mode != Mode.Closed;
        internal bool HasAvailableDestination => options != null && options.Any(Available);

        internal static bool Unlocked(Option option) => option != null && !string.IsNullOrEmpty(option.requiresConclusion) &&
            InvestigationIntegration.State?.threads.Contains(option.requiresConclusion) == true;

        static bool Available(Option option) => Unlocked(option) && string.IsNullOrEmpty(option.unavailableStatus) &&
            !string.IsNullOrEmpty(option.targetScene) && Application.CanStreamedLevelBeLoaded(option.targetScene);

        void OnEnable()
        {
            InvestigationIntegration.RegisterBoard(this);
            if (!TryGetComponent<PrecinctReadingBoard>(out _)) gameObject.AddComponent<PrecinctReadingBoard>();
        }

        public void Interact(SimpleUIHUD hud)
        {
            if (!CanInteract) return;
            hud?.ClearPrompt();
            InvestigationIntegration.OpenBoard(this);
        }

        public void OpenDecision()
        {
            if (!DecisionReady || mode != Mode.Closed) return;
            InvestigationIntegration.Close();
            officer = FindAnyObjectByType<OfficerController>();
            follow = Camera.main ? Camera.main.GetComponent<ThirdPersonCamera>() : null;
            cursor = Cursor.lockState;
            cursorVisible = Cursor.visible;
            modeStarted = Time.time;

            if (officer)
            {
                officer.LockInput(true);
                Vector3 away = Vector3.ProjectOnPlane(surface ? surface.forward : officer.transform.forward, Vector3.up).normalized;
                Vector3 end = officer.transform.position + away * .62f;
                officer.WalkTo(end, .62f);
                if (follow)
                {
                    Vector3 side = Vector3.Cross(Vector3.up, away);
                    follow.BeginStation(end + away * 1.72f + side * .87f + Vector3.up * 1.65f,
                        end + Vector3.up * 1.43f);
                }
            }
            mode = Mode.SteppingAway;
        }

        void CloseDecision()
        {
            mode = Mode.Closed;
            if (officer)
            {
                officer.StopWalking();
                officer.LockInput(false);
            }
            if (follow) follow.EndShot();
            Cursor.lockState = cursor;
            Cursor.visible = cursorVisible;
        }

        void OnDisable()
        {
            if (mode != Mode.Closed) CloseDecision();
            InvestigationIntegration.UnregisterBoard(this);
        }

        void Select(int index)
        {
            if (mode != Mode.Decision || index < 0 || index >= options.Length || !Available(options[index])) return;
            chosen = index;
            loadScheduled = false;
            ClueJournal.Decision = options[index].label;
            mode = Mode.Leaving;
            modeStarted = Time.time;
        }

        void Update()
        {
            if (NoirMenu.GameSession.Paused) return;
            float elapsed = Time.time - modeStarted;
            if (mode == Mode.SteppingAway && (elapsed >= .92f || (elapsed >= .50f && (!officer || !officer.IsWalking))))
            {
                if (officer) officer.StopWalking();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                decisionFrame = Time.frameCount;
                mode = Mode.Decision;
            }

            if (mode == Mode.Leaving && !loadScheduled && elapsed >= 1.05f)
            {
                loadScheduled = true;
                SceneManager.LoadSceneAsync(options[chosen].targetScene);
                return;
            }
            if (mode != Mode.Decision || Time.frameCount == decisionFrame || Keyboard.current == null) return;
            var keys = Keyboard.current;
            if (keys.escapeKey.wasPressedThisFrame) { CloseDecision(); return; }
            for (int i = 0; i < Mathf.Min(options.Length, 4); i++)
            {
                if (!keys[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame &&
                    !keys[(Key)((int)Key.Numpad1 + i)].wasPressedThisFrame) continue;
                Select(i);
                break;
            }
        }

        void OnGUI()
        {
            if (NoirMenu.GameSession.Paused) return;
            if (mode != Mode.Decision && mode != Mode.Leaving) return;
            float w = Screen.width, h = Screen.height;
            float scale = Mathf.Clamp(Mathf.Min(h / 720f, w / 650f), .60f, 2f);
            DialogueChoiceRows.Fill(new Rect(0, 0, w, h * .06f), Color.black);
            DialogueChoiceRows.Fill(new Rect(0, h * .94f, w, h * .06f), Color.black);
            if (prompt == null)
            {
                prompt = new GUIStyle(GameTypography.Label) { alignment = TextAnchor.MiddleRight };
                prompt.normal.textColor = DialogueChoiceRows.Cream;
            }
            prompt.fontSize = Mathf.RoundToInt(16f * scale);

            float width = Mathf.Min(282f * scale, w * .40f);
            float right = w * .84f;
            float rowHeight = 47f * scale;
            float y = h * .42f;
            GUI.Label(new Rect(right - width, y - 31f * scale, width, 26f * scale), "Куда поехать?", prompt);
            for (int i = 0; i < Mathf.Min(options.Length, 4); i++)
            {
                var option = options[i];
                if (!Available(option)) continue;
                float used = choiceRows.Measure(option.label, scale, width);
                var rect = new Rect(right - used, y, used, rowHeight - 6f * scale);
                bool hover = mode == Mode.Decision && rect.Contains(Event.current.mousePosition);
                choiceRows.Draw(rect, option.label, i + 1, scale, hover || (mode == Mode.Leaving && chosen == i));
                if (hover && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    Select(i);
                    Event.current.Use();
                }
                y += rowHeight;
            }
            if (mode == Mode.Leaving)
            {
                float fade = Mathf.Clamp01((Time.time - modeStarted - .16f) / .88f);
                DialogueChoiceRows.Fill(new Rect(0, 0, w, h), new Color(0, 0, 0, fade));
            }
        }
    }
}

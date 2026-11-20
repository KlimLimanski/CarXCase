using CarXCase.Interaction;
using DetectiveGame.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CarXCase.Harbor
{
    public sealed class HarborGate : MonoBehaviour, IInteractable, IGlyphAnchor
    {
        public const string AccessCode = "7043";
        public const string CodeNote = "clue_dmitry_harbor_code";
        const string OpenFlag = "harbor_gate_open";
        const int PulseCount = 4;
        static readonly Key[] Digits = { Key.Digit0, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4,
            Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };
        static readonly Key[] Numpad = { Key.Numpad0, Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4,
            Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9 };

        public Transform leftLeaf, rightLeaf;
        public GameObject chain;
        public OfficerController player;
        public ThirdPersonCamera follow;
        Quaternion leftClosed, rightClosed;
        bool open, panel, timing, cameraWasEnabled, cursorWasLocked;
        float openAmount, pulseStart;
        int pulses;
        string entry = "", status = "";
        SimpleUIHUD hud;

        public bool CanInteract => isActiveAndEnabled && !open && !panel;
        public string InteractPrompt => "E  Проверить кодовый замок";
        public Vector3 GlyphPosition => transform.position + Vector3.up * .16f;

        void Awake()
        {
            leftClosed = leftLeaf.localRotation;
            rightClosed = rightLeaf.localRotation;
        }

        void Start()
        {
            if (!ClueJournal.Has(OpenFlag)) return;
            open = true;
            openAmount = 1f;
            SetGate();
        }

        public void Interact(SimpleUIHUD sourceHud)
        {
            if (!CanInteract) return;
            hud = sourceHud;
            panel = true;
            timing = false;
            entry = status = "";
            cursorWasLocked = Cursor.lockState == CursorLockMode.Locked;
            cameraWasEnabled = follow && follow.enabled;
            if (player) player.LockInput(true);
            if (follow) follow.enabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Update()
        {
            if (open && openAmount < 1f)
            {
                openAmount = Mathf.MoveTowards(openAmount, 1f, Time.unscaledDeltaTime / 1.35f);
                SetGate();
            }
            var keyboard = Keyboard.current;
            if (!panel || keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (timing) { timing = false; status = ""; }
                else ClosePanel();
                return;
            }
            if (timing)
            {
                if (keyboard.spaceKey.wasPressedThisFrame) MatchPulse();
                return;
            }
            for (int i = 0; i < Digits.Length; i++)
                if (keyboard[Digits[i]].wasPressedThisFrame || keyboard[Numpad[i]].wasPressedThisFrame) AddDigit(i);
            if (keyboard.backspaceKey.wasPressedThisFrame) DeleteDigit();
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) TryCode();
            if (panel && keyboard.fKey.wasPressedThisFrame) BeginTiming();
        }

        void AddDigit(int digit)
        {
            if (entry.Length < AccessCode.Length) entry += digit;
        }

        void DeleteDigit()
        {
            if (entry.Length > 0) entry = entry[..^1];
        }

        void TryCode()
        {
            if (entry.Length != AccessCode.Length) { status = "Введите четыре цифры."; return; }
            if (entry == AccessCode) { Unlock(); return; }
            status = "Неверный код. Попробуйте ещё раз.";
            entry = "";
        }

        void BeginTiming()
        {
            timing = true;
            pulses = 0;
            status = "";
            pulseStart = Time.unscaledTime;
        }

        float PulsePosition => Mathf.PingPong((Time.unscaledTime - pulseStart) * (.60f + pulses * .17f), 1f);
        float TargetPosition => pulses % 2 == 0 ? .64f - pulses * .04f : .31f + pulses * .04f;
        float TargetHalfWidth => .122f - pulses * .011f;

        void MatchPulse()
        {
            if (Mathf.Abs(PulsePosition - TargetPosition) <= TargetHalfWidth)
            {
                pulses++;
                if (pulses == PulseCount) { Unlock(); return; }
                status = "Импульс пойман.";
            }
            else
            {
                pulses = 0;
                status = "Сигнал сбился. Начните последовательность заново.";
            }
            pulseStart = Time.unscaledTime;
        }

        void Unlock()
        {
            if (open) return;
            open = true;
            SetGate();
            ClueJournal.AddConclusion(OpenFlag);
            ClosePanel();
            if (hud) hud.ShowInspectionComment("Проход открыт. Сначала проверю контейнеры.", 4.1f);
        }

        void SetGate()
        {
            float degrees = Mathf.SmoothStep(0f, 95f, openAmount);
            leftLeaf.localRotation = leftClosed * Quaternion.Euler(0f, -degrees, 0f);
            rightLeaf.localRotation = rightClosed * Quaternion.Euler(0f, degrees, 0f);
            if (chain) chain.SetActive(!open);
        }

        void ClosePanel()
        {
            panel = false;
            if (player) player.LockInput(false);
            if (follow)
            {
                follow.enabled = cameraWasEnabled;
                follow.SetCursorLock(cursorWasLocked);
            }
        }

        void OnDisable()
        {
            if (panel) ClosePanel();
        }

        void OnGUI()
        {
            if (!panel) return;
            float width = Mathf.Min(420f, Screen.width * .9f);
            var bounds = new Rect((Screen.width - width) * .5f, Screen.height * .19f, width, 352f);
            Paint(bounds, new Color(.045f, .062f, .074f, .96f));
            GUILayout.BeginArea(new Rect(bounds.x + 22f, bounds.y + 15f, bounds.width - 44f, bounds.height - 30f));
            GUILayout.Label(timing ? "Синхронизация замка" : "Кодовый замок пирса");
            GUILayout.Space(8f);
            if (timing) DrawTiming();
            else DrawKeypad();
            GUILayout.Label(status, GUILayout.Height(34f));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(timing ? "К вводу кода  [Esc]" : "Отойти  [Esc]", GUILayout.Height(25f)))
            {
                if (timing) { timing = false; status = ""; }
                else ClosePanel();
            }
            GUILayout.EndArea();
        }

        void DrawKeypad()
        {
            GUILayout.Label(ClueJournal.Has(CodeNote) ? "Код от Дмитрия: " + AccessCode : "Введите четырёхзначный код.");
            GUILayout.Label(string.Join("  ", entry.PadRight(AccessCode.Length, '·').ToCharArray()), GUILayout.Height(25f));
            for (int row = 0; row < 3; row++)
            {
                GUILayout.BeginHorizontal();
                for (int col = 1; col <= 3; col++)
                {
                    int digit = row * 3 + col;
                    if (GUILayout.Button(digit.ToString(), GUILayout.Height(26f))) AddDigit(digit);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Стереть", GUILayout.Height(26f))) DeleteDigit();
            if (GUILayout.Button("0", GUILayout.Height(26f))) AddDigit(0);
            if (GUILayout.Button("Ввод  [Enter]", GUILayout.Height(26f))) TryCode();
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Попробовать взлом  [F]", GUILayout.Height(27f))) BeginTiming();
        }

        void DrawTiming()
        {
            GUILayout.Label($"Поймайте четыре импульса. Сейчас: {pulses}/{PulseCount}");
            GUILayout.Space(16f);
            var bar = GUILayoutUtility.GetRect(1f, 24f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                Paint(bar, new Color(.14f, .19f, .22f));
                var zone = new Rect(bar.x + (TargetPosition - TargetHalfWidth) * bar.width,
                    bar.y, 2f * TargetHalfWidth * bar.width, bar.height);
                Paint(zone, new Color(.32f, .58f, .51f));
                Paint(new Rect(bar.x + PulsePosition * bar.width - 2f, bar.y - 2f, 4f, bar.height + 4f), Color.white);
            }
            GUILayout.Space(13f);
            if (GUILayout.Button("Поймать импульс  [Пробел]", GUILayout.Height(32f))) MatchPulse();
        }

        static void Paint(Rect area, Color color)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}

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
            openedAt = Time.time;
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
            if (NoirMenu.GameSession.Paused) return;
            if (open && openAmount < 1f)
            {
                openAmount = Mathf.MoveTowards(openAmount, 1f, Time.deltaTime / 1.35f);
                SetGate();
            }
            var keyboard = Keyboard.current;
            if (!panel || keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (timing) { EndTiming(); status = ""; }
                else ClosePanel();
                return;
            }
            if (timing)
            {
                UpdateSequence(keyboard);
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
            if (!panel || entry.Length >= AccessCode.Length) return;
            LocationAudio.Effect("Keypad digit", .57f);
            entry += digit;
            status = "";
            if (entry.Length == AccessCode.Length) TryCode();
        }

        void DeleteDigit()
        {
            if (entry.Length == 0) return;
            entry = entry[..^1];
        }

        void TryCode()
        {
            if (entry.Length != AccessCode.Length) { status = "Введите четыре цифры."; return; }
            if (entry == AccessCode) { Unlock(); return; }
            status = "Неверный код. Попробуйте ещё раз.";
            entry = "";
        }

        public Transform lockDetail;
        static readonly Key[] Sequence = { Key.A, Key.D, Key.W, Key.S, Key.Space, Key.D, Key.S, Key.A, Key.W, Key.Space };
        static string KeyLabel(Key key) => key == Key.Space ? "Пробел" : key.ToString();
        const float ApproachDuration = .55f;
        float approachStart;
        Vector3 previousCameraPosition;
        Quaternion previousCameraRotation;

        void BeginTiming()
        {
            timing = true;
            pulses = 0;
            status = "";
            pulseStart = Time.time;
            if (follow && lockDetail)
            {
                previousCameraPosition = follow.transform.position;
                previousCameraRotation = follow.transform.rotation;
                approachStart = Time.time;
                pulseStart = approachStart + ApproachDuration;
            }
        }

        void LateUpdate()
        {
            if (NoirMenu.GameSession.Paused) return;
            if (!timing || !follow || !lockDetail) return;
            float blend = Mathf.SmoothStep(0, 1, (Time.time - approachStart) / ApproachDuration);
            Vector3 end = lockDetail.position + lockDetail.rotation * new Vector3(.06f, .06f, -.48f);
            Quaternion aim = Quaternion.LookRotation(lockDetail.position + Vector3.up * .04f - end);
            follow.transform.SetPositionAndRotation(Vector3.Lerp(previousCameraPosition, end, blend),
                Quaternion.Slerp(previousCameraRotation, aim, blend));
        }

        void EndTiming()
        {
            if (timing && follow && lockDetail)
                follow.transform.SetPositionAndRotation(previousCameraPosition, previousCameraRotation);
            timing = false;
        }

        float TimeLeft => Mathf.Clamp01(1f - (Time.time - pulseStart) / Mathf.Lerp(1.9f, .85f, (float)pulses / (Sequence.Length - 1)));

        void UpdateSequence(Keyboard keyboard)
        {
            if (Time.time < pulseStart) return;
            if (lockDetail)
                InteractionGlyphs.Show(this, lockDetail.position + Vector3.right * .20f, KeyLabel(Sequence[pulses]), progress: TimeLeft, urgent: true);
            bool wrongKey = System.Array.Exists(Sequence, key => key != Sequence[pulses] && keyboard[key].wasPressedThisFrame);
            if (wrongKey || keyboard[Sequence[pulses]].wasPressedThisFrame) LocationAudio.Effect("Lock key press", .57f);
            if (TimeLeft <= 0 || wrongKey)
            {
                EndTiming();
                status = "Попытка сорвалась. F — повторить.";
                return;
            }
            if (!keyboard[Sequence[pulses]].wasPressedThisFrame) return;
            InteractionGlyphs.Press(this);
            if (++pulses == Sequence.Length) { Unlock(); return; }
            pulseStart = Time.time;
        }
        void Unlock()
        {
            if (open) return;
            LocationAudio.Effect("Gate unlocked", .72f);
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
            EndTiming();
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
            if (NoirMenu.GameSession.Paused) return;
            if (!panel) return;
            if (timing)
            {
                DrawTimingPanel();
                return;
            }
            DrawKeypad();
        }

        float openedAt;
        GUIStyle lettering;
        Texture2D patina;
        static readonly Color Bronze = new Color(.56f,.44f,.33f);
        static readonly Color Ivory = new Color(.89f,.79f,.66f);

        void PreparePanel()
        {
            if (lettering != null) return;
            lettering = new GUIStyle(GameTypography.Label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            patina = new Texture2D(256, 160, TextureFormat.RGBA32, false);
            var pixels = new Color32[256 * 160];
            var grain = new System.Random(17);
            for (int y = 0; y < 160; y++)
                for (int x = 0; x < 256; x++)
                {
                    float edge = Mathf.Clamp01(1f - Mathf.Min(Mathf.Min(x,255-x),Mathf.Min(y,159-y))/20f);
                    int v = 19 + grain.Next(5) + (int)((5 + edge * 15) * Mathf.PerlinNoise(x*.09f,y*.09f));
                    pixels[y*256+x] = new Color32((byte)(v+4),(byte)v,(byte)(v-3),252);
                }
            patina.SetPixels32(pixels);
            patina.Apply(false, true);
        }

        void OnDestroy()
        {
            if (patina) Destroy(patina);
        }

        void Caption(Rect r, string value, int size, Color ink)
        {
            lettering.fontSize = size;
            lettering.normal.textColor = ink; lettering.Steady();
            GUI.Label(r, value, lettering);
        }

        static void Edge(Rect r, Color ink)
        {
            Paint(new Rect(r.x,r.y,r.width,1),ink);
            Paint(new Rect(r.x,r.yMax-1,r.width,1),ink);
            Paint(new Rect(r.x,r.y,1,r.height),ink);
            Paint(new Rect(r.xMax-1,r.y,1,r.height),ink);
        }

        bool PanelButton(Rect r, string title, int size, bool accent = false)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            Paint(r, hover ? new Color(.31f,.22f,.16f,.92f) : new Color(.21f,.17f,.14f,.72f));
            Edge(r, hover || accent ? new Color(.82f,.56f,.41f) : Bronze);
            Caption(r,title,size,Ivory);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        void DrawTimingPanel()
        {
            PreparePanel();
            const float width = 460f;
            const float height = 146f;
            float scale = Mathf.Min(Screen.width / 620f, Screen.height / 620f, 1.25f);
            float remaining = TimeLeft;
            Color accent = Color.Lerp(new Color(.77f,.30f,.23f), new Color(.90f,.70f,.43f),
                Mathf.SmoothStep(0f, 1f, remaining * 2f));

            var previous = GUI.matrix;
            GUI.matrix = previous * Matrix4x4.TRS(
                new Vector3((Screen.width - width * scale) * .5f, Screen.height - (height + 28f) * scale),
                Quaternion.identity, Vector3.one * scale);

            Paint(new Rect(-3,4,width+6,height+4), new Color(0,0,0,.42f));
            GUI.DrawTexture(new Rect(0,0,width,height), patina);
            Edge(new Rect(0,0,width,height), Bronze);
            Edge(new Rect(5,5,width-10,height-10), new Color(.56f,.44f,.33f,.33f));

            var key = new Rect(19,19,103,88);
            Paint(key, new Color(.12f,.085f,.061f));
            Edge(key, accent);
            Caption(key, KeyLabel(Sequence[pulses]), Sequence[pulses] == Key.Space ? 23 : 40, Ivory);

            Caption(new Rect(134,17,210,28), "ВЗЛОМ ЗАМКА", 21, Ivory);
            Caption(new Rect(352,17,88,28), $"{pulses + 1} / {Sequence.Length}", 17, Ivory);

            const float trackWidth = 294f;
            float stride = trackWidth / Sequence.Length;
            for (int i = 0; i < Sequence.Length; i++)
                Paint(new Rect(145+i*stride,53,stride-4,5),
                    i < pulses ? Bronze : i == pulses ? Ivory : new Color(.29f,.24f,.20f));

            Paint(new Rect(145,78,trackWidth,7), new Color(.10f,.075f,.055f));
            Paint(new Rect(145,78,trackWidth*remaining,7), accent);
            Caption(new Rect(145,91,trackWidth,24), "Нажмите указанную клавишу", 16, Ivory);
            Paint(new Rect(19,116,420,1), new Color(.56f,.44f,.33f,.42f));
            Caption(new Rect(19,119,420,19), "Esc · отойти", 14, Ivory);
            GUI.matrix = previous;
        }

        void DrawKeypad()
        {
            PreparePanel();
            float appear = Mathf.SmoothStep(0,1,Mathf.Clamp01((Time.time-openedAt)/.18f));
            Paint(new Rect(0,0,Screen.width,Screen.height),new Color(0,0,0,.48f*appear));
            float scale = Mathf.Min(Screen.width/850f, Screen.height/605f, 1.5f);
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width-800*scale)/2,
                (Screen.height-480*scale)/2 + (1-appear)*16*scale,0),Quaternion.identity,Vector3.one*scale);
            GUI.DrawTexture(new Rect(0,0,800,480),patina);
            Edge(new Rect(8,8,784,464),Bronze);
            Paint(new Rect(95,48,610,1),Bronze);
            Caption(new Rect(75,54,650,44),"ВВЕДИТЕ КОД ДОСТУПА",27,Ivory);
            Paint(new Rect(95,102,610,1),Bronze);
            if (PanelButton(new Rect(744,22,34,34),"×",24)) ClosePanel();
            for (int i=0;i<AccessCode.Length;i++)
            {
                var cell=new Rect(232+86*i,119,74,65);
                Paint(cell,new Color(.04f,.035f,.03f,.89f));
                Edge(cell,i==entry.Length?Ivory:Bronze);
                Caption(cell,i<entry.Length?entry[i].ToString():i==entry.Length?"│":"",30,Ivory);
            }
            Caption(new Rect(60,188,680,34),status.Length>0?status:ClueJournal.Has(CodeNote)?"Код от Дмитрия: "+AccessCode:"",16,Ivory);
            for(int i=0;i<9;i++)
                if(PanelButton(new Rect(89+(i%3)*126,231+(i/3)*54,116,46),(i+1).ToString(),30)) AddDigit(i+1);
            if(PanelButton(new Rect(89,393,368,46),"0",30)) AddDigit(0);
            if(PanelButton(new Rect(495,244,241,75),"ПОПРОБОВАТЬ\nВЗЛОМ",21,true)) BeginTiming();
            if(PanelButton(new Rect(495,343,241,64),"ОТОЙТИ",20)) ClosePanel();
            if(entry.Length>0 && GUI.Button(new Rect(495,415,241,28),"Стереть цифру",GameTypography.Label)) DeleteDigit();
            GUI.matrix=previous;
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

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
            openedAt = Time.unscaledTime;
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
            entry += digit;
            status = "";
            if (entry.Length == AccessCode.Length) TryCode();
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

        public Transform lockDetail;
        static readonly Key[] Sequence = { Key.A, Key.D, Key.W, Key.Space };
        static readonly string[] Symbols = { "A", "D", "W", "Space" };
        const float ApproachDuration = .55f;
        float approachStart;
        Vector3 previousCameraPosition;
        Quaternion previousCameraRotation;

        void BeginTiming()
        {
            timing = true;
            pulses = 0;
            status = "";
            pulseStart = Time.unscaledTime;
            if (follow && lockDetail)
            {
                previousCameraPosition = follow.transform.position;
                previousCameraRotation = follow.transform.rotation;
                approachStart = Time.unscaledTime;
                pulseStart = approachStart + ApproachDuration;
            }
        }

        void LateUpdate()
        {
            if (!timing || !follow || !lockDetail) return;
            float blend = Mathf.SmoothStep(0, 1, (Time.unscaledTime - approachStart) / ApproachDuration);
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

        float TimeLeft => Mathf.Clamp01(1f - (Time.unscaledTime - pulseStart) / (2.1f - pulses * .16f));

        void UpdateSequence(Keyboard keyboard)
        {
            if (Time.unscaledTime < pulseStart) return;
            if (lockDetail)
                InteractionGlyphs.Show(this, lockDetail.position + Vector3.right * .20f, Symbols[pulses], progress: TimeLeft, urgent: true);
            if (TimeLeft <= 0 || System.Array.Exists(Sequence, key => key != Sequence[pulses] && keyboard[key].wasPressedThisFrame))
            {
                EndTiming();
                status = "Попытка сорвалась. F — повторить.";
                return;
            }
            if (!keyboard[Sequence[pulses]].wasPressedThisFrame) return;
            InteractionGlyphs.Press(this);
            if (++pulses == Sequence.Length) { Unlock(); return; }
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
            if (!panel) return;
            if (timing)
            {
                var area = new Rect(Screen.width * .25f, Screen.height * .81f, Screen.width * .5f, 56f);
                Paint(area, new Color(.04f,.06f,.07f,.88f));
                GUI.Label(area, $"   {pulses + 1} / {Sequence.Length}     Нажмите {(pulses == 3 ? "Пробел" : Symbols[pulses])}     ·     Esc — отойти");
                Paint(new Rect(area.x, area.yMax - 4, area.width * TimeLeft, 4), new Color(.83f,.68f,.43f));
                return;
            }
            DrawKeypad();
        }

        float openedAt;
        GUIStyle lettering;
        Font panelFont;
        Texture2D patina;
        static readonly Color Bronze = new Color(.56f,.44f,.33f);
        static readonly Color Ivory = new Color(.89f,.79f,.66f);

        void PreparePanel()
        {
            if (lettering != null) return;
            panelFont = Font.CreateDynamicFontFromOSFont(new[] { "Georgia", "Times New Roman", "Arial" }, 32);
            lettering = new GUIStyle(GUI.skin.label) { font = panelFont, alignment = TextAnchor.MiddleCenter, wordWrap = true };
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
            if (panelFont) Destroy(panelFont);
        }

        void Caption(Rect r, string value, int size, Color ink)
        {
            lettering.fontSize = size;
            lettering.normal.textColor = ink;
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

        void DrawKeypad()
        {
            PreparePanel();
            float appear = Mathf.SmoothStep(0,1,Mathf.Clamp01((Time.unscaledTime-openedAt)/.18f));
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
            if(entry.Length>0 && GUI.Button(new Rect(495,415,241,28),"Стереть цифру",GUI.skin.label)) DeleteDigit();
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

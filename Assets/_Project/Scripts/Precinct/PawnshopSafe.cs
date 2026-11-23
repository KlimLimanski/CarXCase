using System.Collections;
using CarXCase.Interaction;
using DetectiveGame.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CarXCase.Precinct
{
    public sealed class PawnshopSafe : MonoBehaviour, IInteractable, IGlyphAnchor
    {
        public const string OpenFlag = "pawnshop_safe_open";
        const int DialPresses = 3, LatchPresses = 5;
        public ClueObject note;
        public Transform door;
        [SerializeField] Transform lockGlyph, safeShot;

        Quaternion closed;
        bool opened, revealing, restoreInputLock;
        int requiredPresses;
        QteManager qte;
        OfficerController player;
        ThirdPersonCamera view;
        SimpleUIHUD hud;
        GUIStyle counter;

        public bool CanInteract => isActiveAndEnabled && !player && !opened && ClueJournal.Has(PortLead.CheckSafe)
            && note && !ClueJournal.Has(PortLead.Safe);
        public string InteractPrompt => "E  Открыть сейф";
        public Vector3 GlyphPosition => transform.position;

        Quaternion OpenRotation => closed * Quaternion.Euler(0, 94f, 0);

        void Awake()
        {
            if (door) closed = door.localRotation;
        }

        void Start()
        {
            if (note && (ClueJournal.Has(OpenFlag) || ClueJournal.Has(PortLead.Safe))) Reveal();
        }

        public void Interact(SimpleUIHUD sourceHud)
        {
            var manager = QteManager.Instance;
            if (!CanInteract || !manager || manager.IsQteActive()) return;
            player = FindAnyObjectByType<OfficerController>();
            if (!player) return;
            qte = manager;
            hud = sourceHud;
            restoreInputLock = player.InputLocked;
            player.LockInput(true);
            hud?.ClearPrompt();
            if (safeShot && player.cameraTransform && player.cameraTransform.TryGetComponent(out view))
                view.BeginStation(safeShot.position, safeShot.position + safeShot.forward);
            StartCoroutine(ApproachSafe());
        }

        IEnumerator ApproachSafe()
        {
            if (view) yield return new WaitForSeconds(.42f);
            StartStage(Key.Q, DialPresses, 1.7f, StartLatch);
        }

        void StartLatch() => StartStage(Key.E, LatchPresses, 2.15f, BeginOpen);

        void StartStage(Key key, int presses, float duration, System.Action onSuccess)
        {
            requiredPresses = presses;
            qte.Present(lockGlyph ? lockGlyph : transform, null);
            qte.StartRapidMash(key, key.ToString(), presses, duration, onSuccess, Missed);
        }

        void Update()
        {
            if (NoirMenu.GameSession.Paused || !player || revealing) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Cancel();
        }

        void OnGUI()
        {
            if (NoirMenu.GameSession.Paused || !player || revealing || requiredPresses == 0 || !qte || !qte.IsQteActive()) return;
            var cam = Camera.main;
            if (!cam) return;
            var point = cam.WorldToScreenPoint(lockGlyph ? lockGlyph.position : GlyphPosition);
            if (point.z <= 0f) return;
            counter ??= new GUIStyle(GameTypography.Label) { alignment = TextAnchor.MiddleCenter };
            counter.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * .022f), 14, 27);
            GUI.Label(new Rect(point.x - 52f, Screen.height - point.y + Screen.height * .052f, 104f, 26f),
                $"{qte.CurrentMashes}/{requiredPresses}", counter);
        }

        void Missed()
        {
            if (hud) hud.ShowInspectionComment("Замок не поддался. Можно попробовать ещё раз.", 2.5f);
            Release();
        }

        void BeginOpen()
        {
            revealing = true;
            requiredPresses = 0;
            StartCoroutine(Open());
        }

        IEnumerator Open()
        {
            LocationAudio.Effect("Lock key press", .68f);
            if (door)
            {
                for (float elapsed = 0f; elapsed < .55f; elapsed += Time.deltaTime)
                {
                    door.localRotation = Quaternion.Slerp(closed, OpenRotation, Mathf.SmoothStep(0f, 1f, elapsed / .55f));
                    yield return NoirMenu.GameSession.NextStoryFrame;
                }
            }
            Reveal();
            ClueJournal.AddConclusion(OpenFlag);
            NoirMenu.GameSession.SaveCurrent();
            Release();
        }

        void Reveal()
        {
            opened = true;
            if (door) door.localRotation = OpenRotation;
            note.gameObject.SetActive(true);
            note.enabled = true;
        }

        void Cancel()
        {
            StopAllCoroutines();
            if (qte && requiredPresses != 0) qte.Cancel();
            if (!opened && door) door.localRotation = closed;
            Release();
        }

        void Release()
        {
            if (view) view.EndShot();
            if (player) player.LockInput(restoreInputLock);
            player = null;
            view = null;
            qte = null;
            hud = null;
            requiredPresses = 0;
            revealing = false;
        }

        void OnDisable()
        {
            if (qte || player) Cancel();
        }
    }
}

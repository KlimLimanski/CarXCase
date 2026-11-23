using System.Collections;
using CarXCase.Interaction;
using DetectiveGame.Core;
using UnityEngine;

namespace CarXCase.Precinct
{
    public sealed class PawnshopSafe : MonoBehaviour, IInteractable, IGlyphAnchor
    {
        public ClueObject note;
        public Transform door;
        Quaternion closed;
        bool opening;

        public bool CanInteract => isActiveAndEnabled && !opening && ClueJournal.Has(PortLead.CheckSafe) &&
            note && !ClueJournal.Has(PortLead.Safe);
        public string InteractPrompt => "E  Открыть сейф";
        public Vector3 GlyphPosition => transform.position;

        Quaternion OpenRotation => closed * Quaternion.Euler(0, 94f, 0);

        void Awake()
        {
            if (door) closed = door.localRotation;
        }

        void Start()
        {
            if (ClueJournal.Has(PortLead.Safe)) Reveal();
        }

        public void Interact(SimpleUIHUD hud)
        {
            if (!CanInteract) return;
            opening = true;
            hud?.ClearPrompt();
            StartCoroutine(Open());
        }

        IEnumerator Open()
        {
            LocationAudio.Effect("Lock key press", .68f);
            if (door)
            {
                float elapsed = 0f;
                while (elapsed < .55f)
                {
                    elapsed += Time.deltaTime;
                    door.localRotation = Quaternion.Slerp(closed, OpenRotation, Mathf.SmoothStep(0, 1, elapsed / .55f));
                    yield return NoirMenu.GameSession.NextStoryFrame;
                }
            }
            Reveal();
            note.enabled = true;
        }

        void Reveal()
        {
            if (door) door.localRotation = OpenRotation;
            note.gameObject.SetActive(true);
        }
    }
}

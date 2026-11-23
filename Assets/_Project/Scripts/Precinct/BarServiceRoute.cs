using System.Collections;
using CarXCase.Interaction;
using DetectiveGame.Core;
using NoirMenu;
using UnityEngine;

namespace CarXCase.Precinct
{
    public sealed class BarServiceRoute : MonoBehaviour, IInteractable, IGlyphAnchor
    {
        public const string DepartureFlag = "bar_dmitry_departed";
        public const string CoordinatesId = "clue_bar_shipping_manifest";
        [SerializeField] Transform hatch;
        [SerializeField] Collider hatchBlocker;
        [SerializeField] Transform staffDoor;
        [SerializeField] Collider staffDoorBlocker;
        [SerializeField] Transform doorApproach, insideStaffRoom;
        [SerializeField] Transform departureShot;
        [SerializeField] ConversationInteraction introduction;
        [SerializeField] BarDmitryPerformance dmitry;
        [SerializeField] ClueObject coordinates;
        [SerializeField] SceneExit harborExit;
        [SerializeField, TextArea] string departureLine = "Хватит, Лёш. Я ухожу.";
        Quaternion hatchClosed, doorClosed;
        bool unlocked, hatchOpening;

        public string DepartureLine => departureLine;
        public Transform DepartureShot => departureShot;
        public bool CanInteract => isActiveAndEnabled && unlocked && !hatchOpening;
        public string InteractPrompt => "E  Поднять секцию стойки";
        public Vector3 GlyphPosition => transform.position + Vector3.up * .18f;

        void Awake()
        {
            hatchClosed = hatch.localRotation;
            doorClosed = staffDoor.rotation;
        }

        void Start()
        {
            if (ClueJournal.Has(DepartureFlag) || ClueJournal.Has(coordinates.clueId))
                Unlock();
            ClueJournal.Changed += RefreshExit;
            RefreshExit();
        }

        void OnDestroy() => ClueJournal.Changed -= RefreshExit;

        public IEnumerator DmitryLeaves()
        {
            yield return dmitry.WalkTo(doorApproach.position);
            staffDoorBlocker.enabled = false;
            yield return Swing(staffDoor, doorClosed, Quaternion.AngleAxis(94f, Vector3.up) * doorClosed, .65f, true);
            yield return dmitry.WalkTo(insideStaffRoom.position);
            dmitry.gameObject.SetActive(false);
            yield return Swing(staffDoor, staffDoor.rotation, doorClosed, .55f, true);
            staffDoorBlocker.enabled = true;
            Unlock();
            ClueJournal.AddConclusion(DepartureFlag);
            GameSession.SaveCurrent();
        }

        void Unlock()
        {
            unlocked = true;
            dmitry.gameObject.SetActive(false);
            introduction.enabled = false;
            coordinates.gameObject.SetActive(true);
        }

        void RefreshExit()
        {
            // New routes let the exit itself check every valid source of the port lead.
            if (harborExit.alternateClueIds == null || harborExit.alternateClueIds.Length == 0)
                harborExit.enabled = unlocked && ClueJournal.Has(coordinates.clueId);
        }

        public void Interact(SimpleUIHUD hud)
        {
            if (!CanInteract) return;
            hatchOpening = true;
            StartCoroutine(LiftHatch());
        }

        IEnumerator LiftHatch()
        {
            yield return Swing(hatch, hatchClosed, hatchClosed * Quaternion.Euler(0f, 0f, 86f), .65f, false);
            hatchBlocker.enabled = false;
        }

        static IEnumerator Swing(Transform part, Quaternion from, Quaternion to, float duration, bool world)
        {
            float elapsed = 0f;
            do
            {
                elapsed += Time.deltaTime;
                Quaternion rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / duration));
                if (world) part.rotation = rotation;
                else part.localRotation = rotation;
                yield return NoirMenu.GameSession.NextStoryFrame;
            } while (elapsed < duration);
        }
    }
}

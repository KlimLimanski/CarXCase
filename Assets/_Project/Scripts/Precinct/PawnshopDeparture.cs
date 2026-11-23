using System.Collections;
using DetectiveGame.Core;
using UnityEngine;

namespace CarXCase.Precinct
{
    public sealed class PawnshopDeparture : MonoBehaviour
    {
        [SerializeField] Animator boris;
        [SerializeField] Transform ironDoor, approach, backRoom, departureShot;
        [SerializeField] Collider doorBlocker;
        Quaternion closed;

        public Transform Shot => departureShot;
        public string Farewell => "Отец оставил бумаги в сейфе за стойкой. Проверь его.";

        Transform Actor => boris.transform.parent;

        void Awake() => closed = ironDoor.localRotation;

        void Start()
        {
            if (!ClueJournal.Has(PortLead.PawnshopFinished)) return;
            Actor.gameObject.SetActive(false);
            if (!ClueJournal.Has(PortLead.Safe)) ClueJournal.AddConclusion(PortLead.CheckSafe);
        }

        public IEnumerator Leave()
        {
            yield return ConversationActing.WalkTo(Actor, boris, approach.position, 1.3f);
            if (doorBlocker) doorBlocker.enabled = false;
            LocationAudio.Effect("Lock key press", .47f);
            yield return Swing(closed * Quaternion.Euler(0f, -98f, 0f), .56f);
            yield return ConversationActing.WalkTo(Actor, boris, backRoom.position, 1.22f);
            yield return Swing(closed, .66f);
            Actor.gameObject.SetActive(false);
            if (doorBlocker) doorBlocker.enabled = true;
            LocationAudio.Effect("Lock key press", .55f);
        }

        IEnumerator Swing(Quaternion target, float duration)
        {
            Quaternion from = ironDoor.localRotation;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                ironDoor.localRotation = Quaternion.Slerp(from, target, Mathf.SmoothStep(0f, 1f, elapsed / duration));
                yield return NoirMenu.GameSession.NextStoryFrame;
            }
            ironDoor.localRotation = target;
        }
    }
}

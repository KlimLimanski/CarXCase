using System.Collections;
using CarXCase.Precinct;
using DetectiveGame.Core;
using UnityEngine;

namespace CarXCase.Apartment
{
    public sealed class ApartmentThreshold : MonoBehaviour, IInteractable, CarXCase.Interaction.IGlyphAnchor
    {
        [SerializeField] Transform door;
        [SerializeField] Transform exteriorLock;
        [SerializeField] OfficerController officer;
        [SerializeField] SceneExit exit;
        [SerializeField] ApartmentLightSwitch lightSwitch;
        [SerializeField] Transform journalHintPoint;
        [SerializeField] float openAngle = 86f;
        [Tooltip("How far past the threshold Alexey walks in by himself before the door shuts (m).")]
        [SerializeField] float walkInDistance = 1.7f;
        bool entered, doorOpened, doorClosed;
        ApartmentInteractable hinge;

        const string DoorNoteId = "note_door_unlocked";

        // One beat at the door (GDD 4.1): E at the lock → Alexey's line, a diary note and the J hint,
        // then the door opens and he calls his father. Once he is inside, the door shuts behind him.
        public bool CanInteract => isActiveAndEnabled && !entered;
        public string InteractPrompt => "E  Осмотреть замок";
        // The key hangs right at the lock, where Alexey looks.
        public Vector3 GlyphPosition => exteriorLock ? exteriorLock.position : door.position + Vector3.up * 1.1f;

        void Awake()
        {
            if (door) door.TryGetComponent(out hinge);
            // The entry door is driven by this beat only, never by a free E press.
            if (hinge) hinge.playerUsable = false;
        }

        public void Interact(SimpleUIHUD hud)
        {
            if (!CanInteract) return;
            entered = true;
            StartCoroutine(Enter(hud));
        }

        void Update()
        {
            if (!doorOpened || doorClosed || !officer || !officer.isActiveAndEnabled || !journalHintPoint || !hinge) return;
            // Close only once Alexey is well inside, so the leaf never sweeps through him.
            if (Vector3.Dot(officer.transform.position - journalHintPoint.position, journalHintPoint.forward) < .9f) return;
            doorClosed = true;
            hinge.SetOpen(false);
        }

        /// Opens the leaf away from Alexey (into the flat), so he can stay where he stopped.
        float OpenAngle(Quaternion start)
        {
            Quaternion a = Quaternion.AngleAxis(openAngle, Vector3.up) * start;
            Quaternion b = Quaternion.AngleAxis(-openAngle, Vector3.up) * start;
            if (!exteriorLock) return openAngle;
            Vector3 lockLocal = Quaternion.Inverse(start) * (exteriorLock.position - door.position);
            Vector3 officerPos = officer.transform.position;
            float da = Vector3.ProjectOnPlane(door.position + a * lockLocal - officerPos, Vector3.up).sqrMagnitude;
            float db = Vector3.ProjectOnPlane(door.position + b * lockLocal - officerPos, Vector3.up).sqrMagnitude;
            return da >= db ? openAngle : -openAngle;
        }

        IEnumerator Enter(SimpleUIHUD hud)
        {
            // Alexey comes to a natural stop even if the player is still holding W, and stays put.
            officer.LockInput(true);
            ThirdPersonCamera view = null;
            if (officer.cameraTransform) officer.cameraTransform.TryGetComponent(out view);
            Vector3 doorway = journalHintPoint ? journalHintPoint.position : door.position;
            Vector3 into = Vector3.ProjectOnPlane(doorway - officer.transform.position, Vector3.up);
            if (into.sqrMagnitude < .01f) into = -Vector3.ProjectOnPlane(door.forward, Vector3.up);
            Vector3 inside = doorway + into.normalized * 1.5f + Vector3.up * 1.45f;

            // 1) The lock: intact, no marks.
            if (exteriorLock)
            {
                officer.FaceTowards(exteriorLock.position);
                if (view) view.BeginFocus(exteriorLock.position);
            }
            // Line 1 of 2: the lock and the detail together.
            if (hud) hud.ShowInspectionComment("Замок цел, следов взлома нет. И не заперто — либо он ушёл в спешке, либо открыл сам.", 5.2f);

            // 2) The detail goes into the diary, with the J hint.
            ClueJournal.Collect(DoorNoteId, "Дверь не заперта",
                "Замок и косяк целы, следов взлома нет, но дверь в квартиру отца не заперта.",
                "Взламывать не пришлось. Отец ушёл в спешке или сам открыл тому, кого знал.");
            if (hud) hud.ShowJournalHint();
            yield return new WaitForSecondsRealtime(5.2f);

            // 3) He turns to the doorway; the shot follows the door as it swings inwards.
            officer.FaceTowards(inside);
            if (view) view.BeginFocus(inside);
            // Line 2 of 2: he calls his father and steps in to look around.
            if (hud) hud.ShowInspectionComment("Отец? Фёдор Павлович, ты дома?.. Нужно осмотреть квартиру.", 6.5f);

            Quaternion closed = door.localRotation;
            Quaternion start = door.rotation;
            float signedAngle = OpenAngle(start);
            Quaternion target = Quaternion.AngleAxis(signedAngle, Vector3.up) * start;
            float elapsed = 0;
            while (elapsed < 3.4f)
            {
                elapsed += Time.unscaledDeltaTime;
                door.rotation = Quaternion.Slerp(start, target, Mathf.SmoothStep(0, 1, elapsed / 1.1f));
                yield return null;
            }

            if (hinge)
            {
                hinge.EnableOpenedHinge(closed, Quaternion.Inverse(start) * Vector3.up, signedAngle);
                hinge.playerUsable = false; // shuts by itself once Alexey is inside; the exit uses SceneExit
            }
            doorOpened = true;

            // 4) He steps in on his own; the shot keeps looking into the flat while he walks past it.
            Vector3 flat = into.normalized;
            Vector3 stop = doorway + flat * walkInDistance;
            stop.y = officer.transform.position.y;
            officer.ClearFaceTarget();
            if (view) view.BeginFocus(doorway + flat * (walkInDistance + 2.2f) + Vector3.up * 1.4f);
            officer.WalkTo(stop);
            float walkStarted = Time.unscaledTime;
            while (officer.IsWalking && Time.unscaledTime - walkStarted < 6f) yield return null;
            officer.StopWalking();

            // 5) The door shuts behind him (Update closes it once he is far enough in), a beat, then control.
            float waitClose = Time.unscaledTime;
            while (!doorClosed && Time.unscaledTime - waitClose < 1.5f) yield return null;
            if (!doorClosed && hinge) { doorClosed = true; hinge.SetOpen(false); }
            yield return new WaitForSecondsRealtime(.6f);
            exit.enabled = true;
            lightSwitch.enabled = true;
            // Control returns facing deeper into the flat.
            if (view) view.EndFocus(stop + flat * 2f + Vector3.up * 1.4f);
            officer.LockInput(false);
        }
    }
}

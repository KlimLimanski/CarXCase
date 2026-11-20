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
        public Vector3 GlyphPosition
        {
            get
            {
                if (!exteriorLock) return door.position + Vector3.up * 1.1f;
                // Leave the key cylinder visible and keep the glyph over the leaf, away from the jamb.
                Vector3 inboard = Vector3.ProjectOnPlane(door.position - exteriorLock.position, exteriorLock.forward);
                inboard = Vector3.ProjectOnPlane(inboard, Vector3.up).normalized;
                return exteriorLock.position + inboard * .07f + Vector3.up * .22f + exteriorLock.forward * .07f;
            }
        }

        void Awake()
        {
            if (door) door.TryGetComponent(out hinge);
            // The entry door is driven by this beat only, never by a free E press.
            if (hinge) hinge.playerUsable = false;
        }

        void Start()
        {
            if (!journalHintPoint || !ClueJournal.Has(DoorNoteId)) return;

            // The checkpoint already contains the lock observation. Resume inside the flat.
            entered = doorOpened = doorClosed = true;
            Vector3 direction = IntoApartment(journalHintPoint.position);
            Vector3 position = journalHintPoint.position + direction * walkInDistance + Vector3.up * .02f;
            Vector3 shift = position - officer.transform.position;
            var controller = officer.GetComponent<CharacterController>();
            bool controllerEnabled = controller.enabled;
            controller.enabled = false;
            officer.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
            controller.enabled = controllerEnabled;
            if (officer.cameraTransform) officer.cameraTransform.position += shift;
            EnableInterior();
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
            EnableInterior();
        }

        void EnableInterior()
        {
            if (exit) exit.enabled = true;
            if (lightSwitch) lightSwitch.enabled = true;
        }

        Vector3 IntoApartment(Vector3 doorway)
        {
            Vector3 forward = journalHintPoint ? journalHintPoint.forward : doorway - officer.transform.position;
            Vector3 flat = Vector3.ProjectOnPlane(forward, Vector3.up);
            return flat.sqrMagnitude >= .01f ? flat.normalized : -Vector3.ProjectOnPlane(door.forward, Vector3.up).normalized;
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
            Vector3 into = IntoApartment(doorway);
            Vector3 inside = doorway + into * 1.5f + Vector3.up * 1.45f;

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
            const float swingTime = 1.1f;
            float elapsed = 0;
            while (elapsed < swingTime)
            {
                elapsed += Time.unscaledDeltaTime;
                door.rotation = Quaternion.Slerp(start, target, Mathf.SmoothStep(0, 1, elapsed / swingTime));
                yield return null;
            }
            door.rotation = target;

            if (hinge)
            {
                hinge.EnableOpenedHinge(closed, Quaternion.Inverse(start) * Vector3.up, signedAngle);
                hinge.playerUsable = false; // shuts by itself once Alexey is inside; the exit uses SceneExit
            }
            doorOpened = true;

            // 4) He steps in on his own; the shot keeps looking into the flat while he walks past it.
            Vector3 stop = doorway + into * walkInDistance;
            officer.ClearFaceTarget();
            if (view) view.BeginFocus(doorway + into * (walkInDistance + 2.2f) + Vector3.up * 1.4f);
            if (journalHintPoint)
            {
                float lateral = Vector3.Dot(officer.transform.position - doorway, Vector3.Cross(into, Vector3.up));
                if (Mathf.Abs(lateral) > .22f)
                    yield return WalkTo(doorway - into * .95f, 3f);
            }
            yield return WalkTo(stop, 6f);

            // 5) The door shuts once he clears the opening, then control returns.
            if (!journalHintPoint || !hinge) EnableInterior();
            yield return new WaitForSecondsRealtime(.6f);
            if (view) view.EndFocus(stop + into * 2f + Vector3.up * 1.4f);
            officer.LockInput(false);
        }

        IEnumerator WalkTo(Vector3 point, float timeout)
        {
            point.y = officer.transform.position.y;
            officer.WalkTo(point);
            float walkStarted = Time.unscaledTime;
            while (officer.IsWalking && Time.unscaledTime - walkStarted < timeout) yield return null;
            officer.StopWalking();
        }
    }
}

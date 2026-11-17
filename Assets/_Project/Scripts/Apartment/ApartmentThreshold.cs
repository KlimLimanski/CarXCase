using System.Collections;
using CarXCase.Precinct;
using DetectiveGame.Core;
using UnityEngine;

namespace CarXCase.Apartment
{
    public sealed class ApartmentThreshold : MonoBehaviour, IInteractable
    {
        [SerializeField] Transform door;
        [SerializeField] Transform exteriorLock;
        [SerializeField] OfficerController officer;
        [SerializeField] SceneExit exit;
        [SerializeField] ApartmentLightSwitch lightSwitch;
        [SerializeField] Transform journalHintPoint;
        [SerializeField] float openAngle = 86f;
        bool entered, journalHintShown;

        public bool CanInteract => isActiveAndEnabled && !entered;
        public string InteractPrompt => "E  Проверить замок";

        public void Interact(SimpleUIHUD hud)
        {
            if (!CanInteract) return;
            entered = true;
            StartCoroutine(Enter(hud));
        }

        void Update()
        {
            if (journalHintShown || !entered || !officer.isActiveAndEnabled || !journalHintPoint) return;
            if (Vector3.Dot(officer.transform.position - journalHintPoint.position, journalHintPoint.forward) <= 0) return;
            journalHintShown = true;
            if (officer.hud) officer.hud.ShowJournalHint();
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
            if (exteriorLock)
            {
                officer.FaceTowards(exteriorLock.position);
                if (view) view.BeginFocus(exteriorLock.position);
            }
            const float inspectionTime = 1.9f;
            if (hud) hud.ShowInspectionComment("Замок цел. Следов взлома нет.", inspectionTime);
            yield return new WaitForSecondsRealtime(inspectionTime);

            // He turns to the doorway; the shot follows the door as it swings inwards.
            officer.FaceTowards(inside);
            if (view) view.BeginFocus(inside);
            if (hud) hud.ShowInspectionComment("Алексей (тихо): Отец? Фёдор Павлович, ты дома?", 3.4f);

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

            if (door.TryGetComponent<ApartmentInteractable>(out var hinge))
                hinge.EnableOpenedHinge(closed, Quaternion.Inverse(start) * Vector3.up, signedAngle);
            if (hud) hud.ShowInspectionComment("Нужно осмотреть квартиру. Может, здесь есть что-то, что объяснит, что случилось.");
            exit.enabled = true;
            lightSwitch.enabled = true;
            // Control returns facing into the flat, so holding W simply walks in.
            if (view) view.EndFocus(inside);
            officer.ClearFaceTarget();
            officer.LockInput(false);
        }
    }
}

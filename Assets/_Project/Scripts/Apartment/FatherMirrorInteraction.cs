using System.Collections;
using CarXCase.Interaction;
using DetectiveGame.Core;
using UnityEngine;

namespace CarXCase.Apartment
{
    [RequireComponent(typeof(MeshFilter))]
    public sealed class FatherMirrorInteraction : MonoBehaviour, IInteractable, IGlyphAnchor
    {
        [SerializeField] OfficerController officer;
        [SerializeField] Vector3 facing = Vector3.forward;

        Camera lens;
        ThirdPersonCamera follow;
        Vector3 originalPosition;
        Quaternion originalRotation;
        float originalFov;
        bool active;

        public string InteractPrompt => "E  Посмотреть в зеркало";
        public Vector3 GlyphPosition => transform.TransformPoint(GetComponent<MeshFilter>().sharedMesh.bounds.center);
        public bool CanInteract => isActiveAndEnabled && !active && officer && !officer.InputLocked
            && Vector3.Dot(officer.transform.position - GlyphPosition, facing) > .15f;

        public void Interact(SimpleUIHUD hud)
        {
            if (!CanInteract || !officer.cameraTransform) return;
            lens = officer.cameraTransform.GetComponent<Camera>();
            follow = officer.cameraTransform.GetComponent<ThirdPersonCamera>();
            if (!lens || !follow || !follow.enabled) return;
            StartCoroutine(LookInMirror(hud));
        }

        IEnumerator LookInMirror(SimpleUIHUD hud)
        {
            active = true;
            originalPosition = lens.transform.position;
            originalRotation = lens.transform.rotation;
            originalFov = lens.fieldOfView;
            follow.enabled = false;
            officer.LockInput(true);
            officer.FaceTowards(GlyphPosition);

            Vector3 normal = facing.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, normal);
            var body = officer.GetComponent<CharacterController>().bounds;
            Vector3 head = new Vector3(body.center.x, body.max.y - .12f, body.center.z);
            Vector3 reflection = head - 2f * Vector3.Dot(head - GlyphPosition, normal) * normal;
            Vector3 aim = GlyphPosition + Vector3.up * .04f;
            float side = Vector3.Dot(originalPosition - aim, right) >= 0 ? 1f : -1f;
            aim += right * (.046f * side);
            Vector3 eye = aim + (aim - reflection) * (.52f / Mathf.Max(.2f, Vector3.Dot(aim - reflection, normal)));
            Quaternion rotation = Quaternion.LookRotation(aim - eye);
            yield return MoveCamera(eye, rotation, 43f, .85f);
            if (hud) hud.ShowInspectionComment("Выгляжу уставшим…", 2.9f);
            yield return new WaitForSeconds(2.9f);
            yield return MoveCamera(originalPosition, originalRotation, originalFov, .68f);
            Restore();
        }

        IEnumerator MoveCamera(Vector3 position, Quaternion rotation, float fov, float duration)
        {
            var startPosition = lens.transform.position;
            var startRotation = lens.transform.rotation;
            float startFov = lens.fieldOfView;
            float started = Time.time;
            while (Time.time < started + duration)
            {
                float blend = Mathf.SmoothStep(0, 1, (Time.time - started) / duration);
                lens.transform.SetPositionAndRotation(Vector3.Lerp(startPosition, position, blend),
                    Quaternion.Slerp(startRotation, rotation, blend));
                lens.fieldOfView = Mathf.Lerp(startFov, fov, blend);
                yield return NoirMenu.GameSession.NextStoryFrame;
            }
            lens.transform.SetPositionAndRotation(position, rotation);
            lens.fieldOfView = fov;
        }

        void OnDisable()
        {
            StopAllCoroutines();
            Restore();
        }

        void Restore()
        {
            if (!active) return;
            active = false;
            if (lens)
            {
                lens.transform.SetPositionAndRotation(originalPosition, originalRotation);
                lens.fieldOfView = originalFov;
            }
            if (follow) follow.enabled = true;
            if (officer)
            {
                officer.ClearFaceTarget();
                officer.LockInput(false);
            }
        }
    }
}

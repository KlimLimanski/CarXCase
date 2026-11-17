using UnityEngine;
using UnityEngine.InputSystem;

namespace DetectiveGame.Core
{
    [RequireComponent(typeof(CharacterController))]
    public class OfficerController : MonoBehaviour
    {
        public float walkSpeed = 3.2f;
        public float runSpeed = 5.8f;
        public float turnSmoothTime = 0.08f;
        public float gravity = -15f;
        [Tooltip("Time (s) to build up speed when starting to move or speeding up.")]
        [Min(0f)] public float accelerationTime = 0.14f;
        [Tooltip("Time (s) to shed speed when stopping or slowing down.")]
        [Min(0f)] public float decelerationTime = 0.11f;

        public Transform cameraTransform;
        public Light flashlight;
        public SimpleUIHUD hud;

        public float interactionRadius = 2.4f;
        public bool aimToInspectClues;
        [Min(.1f)] public float inspectionReach = 2f;

        private CharacterController _controller;
        private ApartmentExplorer _apartmentExplorer;
        private float _verticalVelocity = 0f;
        private float _turnSmoothVelocity;
        private Vector3 _planarVelocity;
        private Vector3 _planarAcceleration;
        private bool _inputLocked;
        private bool _hasFaceTarget;
        private Vector3 _faceTarget;
        private ClueObject _nearestClue;
        private QteTrigger _nearestQte;
        private IInteractable _nearestInteractable;
        public bool CanUseLegacyInteraction => isActiveAndEnabled && !_inputLocked && _nearestClue == null &&
            _nearestInteractable == null && _nearestQte == null &&
            (!hud || (!hud.IsShowingJournal && !hud.IsListening && !hud.IsShowingClue())) &&
            (!QteManager.Instance || !QteManager.Instance.IsQteActive());

        /// Horizontal velocity the controller is currently moving with (m/s).
        public Vector3 PlanarVelocity => _planarVelocity;
        public bool InputLocked => _inputLocked;

        readonly RaycastHit[] _inspectionHits = new RaycastHit[32];

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _apartmentExplorer = GetComponent<ApartmentExplorer>();
            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
        }

        /// Ignores player input while a scripted beat plays; the hero eases to a stop instead of freezing.
        public void LockInput(bool locked)
        {
            _inputLocked = locked;
            if (locked) ClearInteraction();
        }

        /// Turns the hero smoothly towards a point (used while input is locked).
        public void FaceTowards(Vector3 point)
        {
            _faceTarget = point;
            _hasFaceTarget = true;
        }

        public void ClearFaceTarget() => _hasFaceTarget = false;

        private void Update()
        {
            if (_inputLocked)
            {
                HandleMovement();
                return;
            }

            bool qte = QteManager.Instance && QteManager.Instance.IsQteActive();
            if (hud && hud.isActiveAndEnabled && hud.HandleJournalInput(cameraTransform, !qte))
            {
                StopInPlace();
                ClearInteraction();
                return;
            }

            HandleMovement();
            if (hud && hud.IsListening)
            {
                ClearInteraction();
                return;
            }
            HandleFlashlight();
            HandleInteraction();
        }

        void StopInPlace()
        {
            _planarVelocity = _planarAcceleration = Vector3.zero;
            _controller.Move(Vector3.up * gravity * Time.deltaTime);
        }

        private void HandleMovement()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            if (QteManager.Instance != null && QteManager.Instance.IsQteActive())
            {
                StopInPlace();
                return;
            }

            if (hud != null && hud.IsShowingClue())
            {
                StopInPlace();
                return;
            }

            float horizontal = 0f;
            float vertical = 0f;

            if (!_inputLocked)
            {
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) vertical += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) vertical -= 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) horizontal -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) horizontal += 1f;
            }

            Vector3 inputDirection = new Vector3(horizontal, 0f, vertical).normalized;

            bool isRunning = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
            float speed = isRunning ? runSpeed : walkSpeed;

            Vector3 moveDirection = Vector3.zero;

            if (inputDirection.magnitude >= 0.1f)
            {
                float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg;
                if (cameraTransform != null)
                {
                    targetAngle += cameraTransform.eulerAngles.y;
                }

                float smoothedAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref _turnSmoothVelocity, turnSmoothTime);
                transform.rotation = Quaternion.Euler(0f, smoothedAngle, 0f);

                moveDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            }
            else if (_hasFaceTarget)
            {
                Vector3 toward = Vector3.ProjectOnPlane(_faceTarget - transform.position, Vector3.up);
                if (toward.sqrMagnitude > .0001f)
                {
                    float targetAngle = Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg;
                    float smoothedAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle,
                        ref _turnSmoothVelocity, Mathf.Max(turnSmoothTime, .1f) * 2.5f);
                    transform.rotation = Quaternion.Euler(0f, smoothedAngle, 0f);
                }
            }

            // Ease in and out of motion: starts, stops, reversals and Shift changes all blend.
            Vector3 desired = moveDirection * speed;
            float smoothTime = desired.sqrMagnitude >= _planarVelocity.sqrMagnitude ? accelerationTime : decelerationTime;
            _planarVelocity = smoothTime > 0f
                ? Vector3.SmoothDamp(_planarVelocity, desired, ref _planarAcceleration, smoothTime, Mathf.Infinity, Time.deltaTime)
                : desired;
            if (desired == Vector3.zero && _planarVelocity.sqrMagnitude < .0004f)
                _planarVelocity = _planarAcceleration = Vector3.zero;

            if (_controller.isGrounded)
            {
                _verticalVelocity = -1f;
            }
            else
            {
                _verticalVelocity += gravity * Time.deltaTime;
            }

            Vector3 motion = _planarVelocity + Vector3.up * _verticalVelocity;
            var flags = _controller.Move(motion * Time.deltaTime);
            if ((flags & CollisionFlags.Sides) != 0 && Time.deltaTime > 0f)
            {
                // Don't keep momentum against a wall or a closed door.
                Vector3 actual = Vector3.ProjectOnPlane(_controller.velocity, Vector3.up);
                if (actual.sqrMagnitude < _planarVelocity.sqrMagnitude) _planarVelocity = actual;
            }
        }

        private void HandleFlashlight()
        {
            if (flashlight == null || Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.fKey.wasPressedThisFrame)
            {
                flashlight.enabled = !flashlight.enabled;
            }
        }

        private void HandleInteraction()
        {
            if (QteManager.Instance != null && QteManager.Instance.IsQteActive())
            {
                return;
            }

            Collider[] colliders = Physics.OverlapSphere(transform.position, interactionRadius);
            ClueObject foundClue = aimToInspectClues ? FindAimedClue() : null;
            QteTrigger foundQte = null;
            IInteractable foundInteractable = null;
            float closestDistance = float.MaxValue;
            float closestInteractable = float.MaxValue;

            foreach (var col in colliders)
            {
                var qte = col.GetComponent<QteTrigger>();
                if (qte != null && !qte.IsResolved())
                {
                    float distance = Vector3.Distance(transform.position, col.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        foundQte = qte;
                        foundClue = null;
                    }
                }

                var interactable = col.GetComponentInParent<IInteractable>();
                if (interactable != null && interactable.CanInteract)
                {
                    float d = Vector3.Distance(transform.position, col.bounds.ClosestPoint(transform.position));
                    if (d < closestInteractable) { closestInteractable = d; foundInteractable = interactable; }
                }

                var clue = aimToInspectClues ? null : col.GetComponent<ClueObject>();
                if (clue != null && clue.isActiveAndEnabled && foundQte == null)
                {
                    float distance = Vector3.Distance(transform.position, col.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        foundClue = clue;
                    }
                }
            }

            _nearestQte = foundQte;
            _nearestInteractable = (foundQte == null && foundClue == null) ? foundInteractable : null;
            if (_nearestInteractable != null && _apartmentExplorer && _apartmentExplorer.HasPriorityInteraction())
                _nearestInteractable = null;

            if (_nearestClue != foundClue)
            {
                if (_nearestClue != null)
                {
                    _nearestClue.SetHighlight(false);
                }
                _nearestClue = foundClue;
                if (_nearestClue != null)
                {
                    _nearestClue.SetHighlight(true);
                }
            }

            if (hud != null)
            {
                if (_nearestQte != null)
                {
                    hud.SetPrompt($"Нажмите [E]: {_nearestQte.actionName} (QTE)");
                }
                else if (_nearestClue != null)
                {
                    hud.SetPrompt(aimToInspectClues ? $"E  {_nearestClue.InspectionAction} · {_nearestClue.clueTitle}" : $"Нажмите [E], чтобы осмотреть: {_nearestClue.clueTitle}");
                }
                else if (_nearestInteractable != null)
                {
                    hud.SetPrompt(_nearestInteractable.InteractPrompt);
                }
                else
                {
                    hud.ClearPrompt();
                }
            }

            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                if (_nearestQte != null)
                {
                    _nearestQte.TriggerQte(hud);
                }
                else if (_nearestClue != null)
                {
                    _nearestClue.Inspect(hud);
                }
                else if (_nearestInteractable != null)
                {
                    _nearestInteractable.Interact(hud);
                }
            }
        }

        ClueObject FindAimedClue()
        {
            if (!cameraTransform) return null;
            Vector3 playerCenter = _controller.bounds.center;
            float limit = inspectionReach + Vector3.Distance(playerCenter, cameraTransform.position);
            var ray = new Ray(cameraTransform.position, cameraTransform.forward);
            int count = Physics.RaycastNonAlloc(ray, _inspectionHits, limit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            if (count == _inspectionHits.Length)
            {
                var allHits = Physics.RaycastAll(ray, limit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
                return NearestVisibleClue(allHits, allHits.Length, playerCenter);
            }
            return NearestVisibleClue(_inspectionHits, count, playerCenter);
        }

        ClueObject NearestVisibleClue(RaycastHit[] hits, int count, Vector3 playerCenter)
        {
            ClueObject selected = null;
            float closest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.distance >= closest || hit.collider.transform.IsChildOf(transform)) continue;
                var aim = hit.collider.GetComponent<ClueAimTarget>();
                var clue = aim ? aim.clue : hit.collider.GetComponentInParent<ClueObject>();
                if ((!clue || !clue.isActiveAndEnabled) && hit.collider.isTrigger) continue;
                closest = hit.distance;
                selected = clue && clue.isActiveAndEnabled && (hit.point - playerCenter).sqrMagnitude <= inspectionReach * inspectionReach ? clue : null;
            }
            return selected;
        }

        void OnDisable()
        {
            _planarVelocity = _planarAcceleration = Vector3.zero;
            ClearInteraction();
            if (hud) hud.CloseJournal();
        }

        void ClearInteraction()
        {
            if (_nearestClue) _nearestClue.SetHighlight(false);
            _nearestClue = null;
            _nearestQte = null;
            _nearestInteractable = null;
            if (hud) hud.ClearPrompt();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, interactionRadius);
        }
    }
}

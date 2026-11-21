using UnityEngine;
using UnityEngine.InputSystem;
using CarXCase.Interaction;

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
            _nearestInteractable == null && _nearestQte == null && _nearestDoor == null &&
            (!hud || (!hud.IsShowingJournal && !hud.IsListening)) &&
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

        Vector3? _walkTarget;
        float _walkSpeedScale = .7f;
        /// Scripted walk (works while input is locked): the hero strolls to the point with the normal
        /// controller, collisions and locomotion animation, then stops there.
        public void WalkTo(Vector3 point, float speedScale = .7f)
        {
            _walkTarget = point;
            _walkSpeedScale = speedScale;
        }
        public void StopWalking() => _walkTarget = null;
        public bool IsWalking => _walkTarget.HasValue;

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
            bool scripted = false;
            if (_walkTarget.HasValue)
            {
                Vector3 to = Vector3.ProjectOnPlane(_walkTarget.Value - transform.position, Vector3.up);
                if (to.magnitude < .12f) _walkTarget = null;
                else { inputDirection = to.normalized; scripted = true; }
            }

            bool isRunning = !scripted && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
            float speed = scripted ? walkSpeed * _walkSpeedScale : isRunning ? runSpeed : walkSpeed;

            Vector3 moveDirection = Vector3.zero;

            if (inputDirection.magnitude >= 0.1f)
            {
                float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg;
                if (cameraTransform != null && !scripted)
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

        // ---------------------------------------------------------------- Interaction
        // One target at a time. A candidate must be within reach, in front of the view and in plain
        // sight of both the lens and Alexey's eyes; walls and closed doors hide it. The current target
        // is kept with looser limits (hysteresis), so the key never flickers on a boundary.

        [Header("Interaction targets")]
        [Tooltip("Max horizontal angle (deg) between the view and an object for its key to appear.")]
        [Range(5f, 60f)] public float lookAngle = 30f;
        [Tooltip("Extra angle and reach the current target keeps before it is dropped (anti-flicker).")]
        public float keepAngle = 10f, keepReach = .3f;

        enum TargetKind { None, Qte, Clue, Interactable, Door }

        struct Target
        {
            public TargetKind kind;
            public Component component;
            public Vector3 point;
            public float score;
            public bool Valid => kind != TargetKind.None && component;
        }

        Target _target;
        ApartmentInteractable _nearestDoor;
        /// What E would act on right now (null if nothing is in reach and in sight).
        public Component InteractionTarget => _target.Valid ? _target.component : null;
        ClueObject[] _allClues;
        float _cluesListedAt = -10f;
        readonly Collider[] _nearby = new Collider[48];
        readonly RaycastHit[] _sightHits = new RaycastHit[24];
        readonly System.Collections.Generic.HashSet<Component> _seen = new System.Collections.Generic.HashSet<Component>();
        bool _inBeat;

        private void HandleInteraction()
        {
            if (QteManager.Instance != null && QteManager.Instance.IsQteActive())
            {
                return;
            }

            var cam = cameraTransform ? cameraTransform.GetComponent<Camera>() : Camera.main;
            Target best = default;
            if (cam)
            {
                Vector3 center = _controller.bounds.center;
                Target previous = _target;
                _seen.Clear();
                // 1) A clue right under the crosshair always wins.
                var aimed = FindAimedClue();
                if (aimed) best = new Target { kind = TargetKind.Clue, component = aimed, point = InteractionGlyphs.PointOf(aimed), score = -1f };
                else
                {
                    // 2) Everything else competes on distance and on how directly Alexey faces it.
                    if (Time.unscaledTime - _cluesListedAt > 1f)
                    {
                        _allClues = FindObjectsByType<ClueObject>(FindObjectsInactive.Exclude);
                        _cluesListedAt = Time.unscaledTime;
                    }
                    if (_allClues != null)
                        foreach (var clue in _allClues)
                            if (clue && clue.isActiveAndEnabled && !clue.IsDone)
                                Consider(ref best, previous, cam, center, TargetKind.Clue, clue, InteractionGlyphs.PointOf(clue), inspectionReach, null);

                    int count = Physics.OverlapSphereNonAlloc(center, interactionRadius + .6f, _nearby, ~0, QueryTriggerInteraction.Collide);
                    for (int i = 0; i < count; i++)
                    {
                        var col = _nearby[i];
                        if (!col || col.transform.IsChildOf(transform)) continue;
                        var qte = col.GetComponent<QteTrigger>();
                        if (qte && !qte.IsResolved() && _seen.Add(qte))
                            Consider(ref best, previous, cam, center, TargetKind.Qte, qte, InteractionGlyphs.PointOf(qte), interactionRadius, col);

                        var interactable = col.GetComponentInParent<IInteractable>();
                        if (interactable is Component ic && interactable.CanInteract && _seen.Add(ic))
                            Consider(ref best, previous, cam, center, TargetKind.Interactable, ic, InteractionGlyphs.PointOf(ic), interactionRadius, col);

                        var door = col.GetComponentInParent<ApartmentInteractable>();
                        if (door && door.isActiveAndEnabled && door.playerUsable && door.kind != "locked" && _seen.Add(door))
                            Consider(ref best, previous, cam, center, TargetKind.Door, door, InteractionGlyphs.PointOf(door), interactionRadius, col);
                    }
                }
            }
            SetTarget(best);

            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame && _target.Valid)
            {
                InteractionGlyphs.Press(_target.component);
                switch (_target.kind)
                {
                    case TargetKind.Qte: _nearestQte.TriggerQte(hud); break;
                    case TargetKind.Clue: StartCoroutine(ClueBeat(_nearestClue)); break;
                    case TargetKind.Interactable: _nearestInteractable.Interact(hud); break;
                    case TargetKind.Door: _nearestDoor.Use(cameraTransform ? cameraTransform : transform); break;
                }
            }
        }

        void Consider(ref Target best, Target previous, Camera cam, Vector3 center, TargetKind kind, Component c, Vector3 point, float reach, Collider shape)
        {
            bool current = previous.Valid && previous.component == c;
            float distance = shape ? Vector3.Distance(center, shape.ClosestPoint(center)) : Vector3.Distance(center, point);
            float score = Score(cam, center, c, point, distance, reach + (current ? keepReach : 0f), lookAngle + (current ? keepAngle : 0f));
            if (score == float.MaxValue) return;
            if (current) score -= .35f; // the current target is only replaced by a clearly better one
            if (c is CarXCase.Precinct.SceneExit) score -= .45f; // the way on wins over a neighbouring door or switch
            if (!best.Valid || score < best.score)
                best = new Target { kind = kind, component = c, point = point, score = score };
        }

        /// MaxValue = not available: too far, out of view, or something solid in the way.
        float Score(Camera cam, Vector3 center, Component c, Vector3 point, float distance, float reach, float maxAngle)
        {
            if (distance > reach) return float.MaxValue;
            Vector3 view = cam.WorldToViewportPoint(point);
            if (view.z <= 0f || view.x < .08f || view.x > .92f || view.y < .05f || view.y > .95f) return float.MaxValue;
            Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            Vector3 to = Vector3.ProjectOnPlane(point - cam.transform.position, Vector3.up);
            float angle = forward.sqrMagnitude > .001f && to.sqrMagnitude > .001f ? Vector3.Angle(forward, to) : 0f;
            if (angle > maxAngle) return float.MaxValue;
            Vector3 eye = transform.position + Vector3.up * 1.6f;
            if (Blocked(cam.transform.position, point, c.transform) || Blocked(eye, point, c.transform)) return float.MaxValue;
            return distance + angle / 35f;
        }

        /// True if any solid collider that is not the target itself (or the hero) sits on the line before the point.
        bool Blocked(Vector3 from, Vector3 to, Transform target)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < .05f) return false;
            int count = Physics.RaycastNonAlloc(from, delta / length, _sightHits, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = _sightHits[i];
                var t = hit.collider.transform;
                if (t.IsChildOf(transform) || t.IsChildOf(target)) continue;
                var aim = hit.collider.GetComponent<ClueAimTarget>();
                if (aim && aim.clue && aim.clue.transform == target) continue;
                // The last few centimetres belong to whatever the object rests on or is mounted in.
                if (hit.distance >= length - .12f) continue;
                // Furniture whose collider encloses the object (a nightstand box around a matchbox) is not a wall.
                var bounds = hit.collider.bounds;
                if (bounds.Contains(to) && bounds.size.magnitude < 2.5f) continue;
                return true;
            }
            return false;
        }

        void SetTarget(Target next)
        {
            var nextClue = next.kind == TargetKind.Clue ? (ClueObject)next.component : null;
            if (_nearestClue != nextClue)
            {
                if (_nearestClue) _nearestClue.SetHighlight(false);
                if (nextClue) nextClue.SetHighlight(true);
            }
            _target = next;
            _nearestClue = nextClue;
            _nearestQte = next.kind == TargetKind.Qte ? (QteTrigger)next.component : null;
            _nearestInteractable = next.kind == TargetKind.Interactable ? (IInteractable)next.component : null;
            _nearestDoor = next.kind == TargetKind.Door ? (ApartmentInteractable)next.component : null;
            if (hud) hud.ClearPrompt();
            if (!next.Valid) return;
            // The key hangs in the scene next to the object; it carries no text.
            if (next.kind == TargetKind.Interactable) InteractionGlyphs.ShowPrompt(next.component, next.point, _nearestInteractable.InteractPrompt);
            else InteractionGlyphs.Show(next.component, next.point, "E");
        }

        /// Detroit-style examination: Alexey stops and turns to the clue, the camera moves to a side
        /// angle that frames both, he says his line, then the shot eases back to the gameplay view.
        System.Collections.IEnumerator ClueBeat(ClueObject clue)
        {
            if (!clue || _inBeat) yield break;
            _inBeat = true;
            Vector3 point = InteractionGlyphs.PointOf(clue);
            LockInput(true);
            FaceTowards(point);
            var view = cameraTransform ? cameraTransform.GetComponent<ThirdPersonCamera>() : null;
            if (view) view.BeginShot(point, clue.cameraShot, clue.clueId);
            yield return new WaitForSecondsRealtime(.45f);
            clue.Inspect(hud);
            float started = Time.unscaledTime;
            yield return null;
            while (Time.unscaledTime - started < 1.6f ||
                   (hud && (hud.IsListening || !string.IsNullOrEmpty(hud.CurrentInspectionComment))))
                yield return null;
            if (view) view.EndShot();
            ClearFaceTarget();
            LockInput(false);
            _inBeat = false;
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

        /// The first solid thing the crosshair meets; a clue only if it is that thing, still unexamined and within reach.
        ClueObject NearestVisibleClue(RaycastHit[] hits, int count, Vector3 playerCenter)
        {
            ClueObject selected = null;
            float closest = float.MaxValue;
            Vector3 eye = transform.position + Vector3.up * 1.6f;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.distance >= closest || hit.collider.transform.IsChildOf(transform)) continue;
                var aim = hit.collider.GetComponent<ClueAimTarget>();
                var clue = aim ? aim.clue : hit.collider.GetComponentInParent<ClueObject>();
                if ((!clue || !clue.isActiveAndEnabled) && hit.collider.isTrigger) continue;
                closest = hit.distance;
                selected = clue && clue.isActiveAndEnabled && !clue.IsDone
                    && (hit.point - playerCenter).sqrMagnitude <= inspectionReach * inspectionReach
                    && !Blocked(eye, hit.point, clue.transform) ? clue : null;
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
            _nearestDoor = null;
            _target = default;
            if (hud) hud.ClearPrompt();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, interactionRadius);
        }
    }
}

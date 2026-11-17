using CarXCase.Hero;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DetectiveGame.Core
{
    /// Over-the-shoulder exploration camera: mouse orbit, a follow that trails the hero slightly,
    /// a footstep rhythm tied to the locomotion cycle, soft wall avoidance and a staged look at
    /// inspected objects.
    public class ThirdPersonCamera : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0f, 1.6f, 0f);
        public float distance = 3.6f;
        public float minDistance = 1.2f;
        public float maxDistance = 5.5f;
        [Min(0)] public float shoulderOffset;

        public float mouseSensitivity = 2.2f;
        public float pitchMin = -20f;
        public float pitchMax = 65f;
        [Min(0f)] public float orbitSmoothTime;
        [Min(0f)] public float wallSmoothTime;

        [Header("Follow inertia")]
        [Tooltip("Lag (s) of the horizontal follow: the frame trails the hero on starts, stops and turns.")]
        [Min(0f)] public float followSmoothTime;
        [Tooltip("Lag (s) of the vertical follow: smooths stair steps.")]
        [Min(0f)] public float verticalSmoothTime;
        [Tooltip("The follow never trails the hero by more than this (m).")]
        [Min(0f)] public float maxFollowLag = .4f;

        [Header("Footstep rhythm")]
        [Tooltip("Vertical bob (m) at walking speed.")]
        [Min(0f)] public float walkBob;
        [Tooltip("Vertical bob (m) at running speed.")]
        [Min(0f)] public float runBob;
        [Tooltip("Side sway (m) at running speed; walking uses half.")]
        [Min(0f)] public float runSway;
        [Tooltip("Roll (degrees) at running speed; walking uses half.")]
        [Min(0f)] public float runRoll;
        public float walkReferenceSpeed = 1.8f;
        public float runReferenceSpeed = 3f;
        [Tooltip("How quickly the rhythm fades in and out with the hero's actual speed (s).")]
        [Min(0.01f)] public float bobFadeTime = .3f;

        [Header("Inspection framing")]
        [Min(0.01f)] public float focusBlendTime = .55f;
        [Range(.5f, 1f)] public float focusDistanceScale = .85f;
        [Tooltip("Aims a little above the inspected point, towards the hero's eye line.")]
        [Range(0f, 1f)] public float focusHeroBias = .2f;
        [Tooltip("Steepest downward look of the staged shot (degrees).")]
        public float focusMaxPitch = 18f;

        private float _yaw = 0f;
        private float _pitch = 12f;
        private float _renderYaw;
        private float _renderPitch;
        private float _yawVelocity;
        private float _pitchVelocity;
        private float _shoulderDistance;
        private float _cameraDistance;
        private float _shoulderVelocity;
        private float _cameraVelocity;
        private bool _offsetsInitialized;
        private bool _cursorLocked = true;
        private int _cursorLockFrame = -1;
        private Renderer[] _characterRenderers;
        private SimpleUIHUD _hud;

        private HeroLocomotion _locomotion;
        private bool _followInitialized;
        private Vector3 _follow;
        private Vector2 _followVelocity;
        private float _followVerticalVelocity;
        private Vector3 _lastTargetPosition;
        private float _measuredSpeed;
        private float _bobWeight, _bobWeightVelocity, _bobRunWeight;
        private float _fallbackPhase;
        private bool _focusing;
        private Vector3 _focusPoint;
        private float _focusAmount, _focusAmountVelocity;
        private float _releaseUntil;
        private bool _radialInitialized;
        private float _radialShare = 1f, _radialVelocity;
        private readonly RaycastHit[] _hits = new RaycastHit[16];

        public bool IsFocusing => _focusing;

        private void Start()
        {
            SetCursorLock(true);
            if (target != null)
            {
                _hud = target.GetComponent<OfficerController>()?.hud;
                var visual = target.Find("Character_Visual");
                if (visual != null) _characterRenderers = visual.GetComponentsInChildren<Renderer>();
                _locomotion = target.GetComponentInChildren<HeroLocomotion>();
                Vector3 angles = transform.eulerAngles;
                _yaw = angles.y;
                _pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, angles.x), pitchMin, pitchMax);
            }
            _renderYaw = _yaw;
            _renderPitch = _pitch;
        }

        private void LateUpdate()
        {
            if (target == null || (_hud && _hud.IsShowingJournal))
            {
                return;
            }

            if (Time.frameCount != _cursorLockFrame && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                SetCursorLock(!_cursorLocked);
            }

            if (!_cursorLocked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                SetCursorLock(true);
            }

            bool isQteRunning = QteManager.Instance != null && QteManager.Instance.IsQteActive();
            float dt = Time.deltaTime;

            if (_focusing)
            {
                // Staged look: the frame settles on the inspected point and ignores the mouse.
                Vector3 eye = target.position + offset;
                Vector3 aim = Vector3.Lerp(_focusPoint, eye, focusHeroBias);
                Vector3 direction = aim - transform.position;
                if (direction.sqrMagnitude > .0001f)
                {
                    Vector3 angles = Quaternion.LookRotation(direction).eulerAngles;
                    _yaw = _renderYaw + Mathf.DeltaAngle(_renderYaw, angles.y);
                    _pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, angles.x), pitchMin, Mathf.Min(pitchMax, focusMaxPitch));
                }
            }
            else if (_cursorLocked && Mouse.current != null && !isQteRunning)
            {
                Vector2 delta = Mouse.current.delta.ReadValue() * (mouseSensitivity * 0.1f);
                _yaw += delta.x;
                _pitch -= delta.y;
                _pitch = Mathf.Clamp(_pitch, pitchMin, pitchMax);
            }

            // The staged shot and the hand-back both ease at the slower focus pace.
            float orbitTime = _focusing || Time.time < _releaseUntil ? focusBlendTime : orbitSmoothTime;
            if (orbitTime > 0f)
            {
                _renderYaw = Mathf.SmoothDampAngle(_renderYaw, _yaw, ref _yawVelocity, orbitTime);
                _renderPitch = Mathf.SmoothDamp(_renderPitch, _pitch, ref _pitchVelocity, orbitTime);
            }
            else
            {
                _renderYaw = _yaw;
                _renderPitch = _pitch;
            }
            _focusAmount = Mathf.SmoothDamp(_focusAmount, _focusing ? 1f : 0f, ref _focusAmountVelocity, focusBlendTime);

            Quaternion rotation = Quaternion.Euler(_renderPitch, _renderYaw, 0f);
            Vector3 focus = Follow(target.position + offset, dt);
            Vector3 shoulder = rotation * Vector3.right;
            _shoulderDistance = ResolveOffset(focus, shoulder, shoulderOffset, 0f,
                _shoulderDistance, ref _shoulderVelocity);
            Vector3 pivot = focus + shoulder * _shoulderDistance;

            Vector3 backward = rotation * Vector3.back;
            float requested = distance * Mathf.Lerp(1f, focusDistanceScale, _focusAmount);
            _cameraDistance = ResolveOffset(pivot, backward, requested, wallSmoothTime > 0f ? 0f : .08f,
                _cameraDistance, ref _cameraVelocity);
            _offsetsInitialized = true;

            Vector3 position = pivot + backward * _cameraDistance;
            if (wallSmoothTime > 0f && _shoulderDistance > 0f)
            {
                // A jamb can cross the diagonal between the player and the camera
                // even when the two individual offset sweeps are clear.
                Vector3 radial = position - focus;
                Vector3 direction = radial.normalized;
                float wanted = Mathf.Max(radial.magnitude, .01f);
                float hard = ProbeOffset(focus, direction, wanted, 0f, .12f, .06f, .06f);
                // Ease in early with a fatter probe so jambs rarely force the hard snap.
                float soft = Mathf.Min(hard, ProbeOffset(focus, direction, wanted, 0f, .2f, .14f, .16f));
                float share = soft / wanted;
                _radialShare = _radialInitialized
                    ? Mathf.SmoothDamp(_radialShare, share, ref _radialVelocity, wallSmoothTime)
                    : share;
                _radialInitialized = true;
                if (_radialShare * wanted > hard)
                {
                    _radialShare = hard / wanted;
                    _radialVelocity = Mathf.Min(0f, _radialVelocity);
                }
                position = focus + direction * (_radialShare * wanted);
            }

            float roll = FootstepRhythm(dt, out Vector3 bob);
            if (bob != Vector3.zero)
            {
                Vector3 bobbed = position + rotation * bob;
                // Never let the rhythm push the lens into a wall.
                Vector3 step = bobbed - position;
                if (Cast(position, .05f, step.normalized, step.magnitude + .04f, out _) == false)
                    position = bobbed;
            }

            transform.SetPositionAndRotation(position, Quaternion.Euler(_renderPitch, _renderYaw, roll));
            if (_characterRenderers != null)
            {
                bool visible = Vector3.Dot(position - focus, backward) >= 0.85f;
                foreach (var bodyRenderer in _characterRenderers)
                    if (bodyRenderer != null) bodyRenderer.enabled = visible;
            }
        }

        /// The point the camera orbits trails the hero a little, so starts, stops and stairs feel weighted.
        private Vector3 Follow(Vector3 desired, float dt)
        {
            if (!_followInitialized || (desired - _follow).sqrMagnitude > 9f)
            {
                _follow = desired;
                _followVelocity = Vector2.zero;
                _followVerticalVelocity = 0f;
                _lastTargetPosition = target.position;
                _followInitialized = true;
            }

            if (dt > 0f)
            {
                Vector3 moved = target.position - _lastTargetPosition;
                moved.y = 0f;
                _measuredSpeed = Mathf.Lerp(_measuredSpeed, moved.magnitude / dt, 1f - Mathf.Exp(-dt / .1f));
            }
            _lastTargetPosition = target.position;

            Vector2 current = new Vector2(_follow.x, _follow.z);
            Vector2 goal = new Vector2(desired.x, desired.z);
            current = followSmoothTime > 0f && dt > 0f
                ? Vector2.SmoothDamp(current, goal, ref _followVelocity, followSmoothTime, Mathf.Infinity, dt)
                : goal;
            Vector2 lag = current - goal;
            if (lag.magnitude > maxFollowLag) current = goal + lag.normalized * maxFollowLag;

            float y = verticalSmoothTime > 0f && dt > 0f
                ? Mathf.SmoothDamp(_follow.y, desired.y, ref _followVerticalVelocity, verticalSmoothTime, Mathf.Infinity, dt)
                : desired.y;
            y = Mathf.Clamp(y, desired.y - .3f, desired.y + .3f);

            _follow = new Vector3(current.x, y, current.y);
            return _follow;
        }

        /// Returns the roll and a local-space offset following the hero's footfalls.
        private float FootstepRhythm(float dt, out Vector3 bob)
        {
            bob = Vector3.zero;
            if (walkBob <= 0f && runBob <= 0f && runSway <= 0f && runRoll <= 0f) return 0f;

            float speed = _locomotion ? _locomotion.GroundSpeed : _measuredSpeed;
            float walkWeight = Mathf.SmoothStep(0f, 1f, speed / Mathf.Max(walkReferenceSpeed, .01f));
            float runWeight = Mathf.Clamp01((speed - walkReferenceSpeed) / Mathf.Max(runReferenceSpeed - walkReferenceSpeed, .01f));
            if (_focusing) walkWeight = 0f;
            _bobWeight = Mathf.SmoothDamp(_bobWeight, walkWeight, ref _bobWeightVelocity, bobFadeTime, Mathf.Infinity, dt);
            _bobRunWeight = Mathf.Lerp(_bobRunWeight, runWeight, 1f - Mathf.Exp(-dt / bobFadeTime));
            if (_bobWeight < .001f) return 0f;

            float phase;
            if (_locomotion) phase = _locomotion.CyclePhase;
            else
            {
                // About 1.1 m per locomotion cycle when no animator drives the rhythm.
                _fallbackPhase = Mathf.Repeat(_fallbackPhase + speed * dt / 2.2f, 1f);
                phase = _fallbackPhase;
            }
            float angle = phase * 2f * Mathf.PI;
            float height = Mathf.Lerp(walkBob, runBob, _bobRunWeight) * _bobWeight;
            float sideShare = Mathf.Lerp(.5f, 1f, _bobRunWeight) * _bobWeight;
            // Two dips per cycle (one per footfall), one side-to-side sway per cycle.
            bob = new Vector3(runSway * sideShare * Mathf.Sin(angle), height * Mathf.Cos(2f * angle), 0f);
            return runRoll * sideShare * Mathf.Sin(angle + .4f);
        }

        private float ResolveOffset(Vector3 origin, Vector3 direction, float requested,
            float minimum, float current, ref float velocity)
        {
            if (requested <= 0f)
                return 0f;

            float limit = ProbeOffset(origin, direction, requested, minimum, .12f, .06f,
                wallSmoothTime > 0f ? .06f : 0f);
            if (wallSmoothTime <= 0f)
                return limit;

            // Begin pulling in before reaching the collision limit, then ease
            // back out once the wall clears.
            float preferred = Mathf.Min(limit,
                ProbeOffset(origin, direction, requested, minimum, .18f, .13f, .13f));
            if (!_offsetsInitialized)
                return preferred;

            float smoothed = Mathf.SmoothDamp(current, preferred, ref velocity, wallSmoothTime);
            if (smoothed <= limit)
                return smoothed;

            velocity = Mathf.Min(0f, velocity);
            return limit;
        }

        private float ProbeOffset(Vector3 origin, Vector3 direction, float requested,
            float minimum, float radius, float gap, float lookAhead = 0f)
        {
            if (!Cast(origin, radius, direction, requested + lookAhead, out float hitDistance))
                return requested;

            return Mathf.Clamp(hitDistance - gap, Mathf.Min(minimum, requested), requested);
        }

        /// Sphere cast that ignores the hero's own colliders: with the trailing follow the hero
        /// can stand between the orbit point and the lens (e.g. walking towards the camera).
        private bool Cast(Vector3 origin, float radius, Vector3 direction, float length, out float distance)
        {
            distance = length;
            int count = Physics.SphereCastNonAlloc(origin, radius, direction, _hits, length, ~0, QueryTriggerInteraction.Ignore);
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (target && hit.collider.transform.IsChildOf(target)) continue;
                // Initial overlaps report zero distance; like Physics.SphereCast, ignore them.
                if (hit.distance <= 0f && hit.point == Vector3.zero) continue;
                if (hit.distance < distance) { distance = hit.distance; found = true; }
            }
            return found;
        }

        private void OnDisable()
        {
            _offsetsInitialized = false;
            _followInitialized = false;
            _radialInitialized = false;
            _yawVelocity = _pitchVelocity = _shoulderVelocity = _cameraVelocity = 0f;
            _bobWeight = _bobWeightVelocity = 0f;
            if (_characterRenderers != null)
                foreach (var bodyRenderer in _characterRenderers)
                    if (bodyRenderer != null) bodyRenderer.enabled = true;
            SetCursorLock(false);
        }

        public void LookTowards(Vector3 point)
        {
            Vector3 direction = point - transform.position;
            if (direction.sqrMagnitude < .001f) return;
            Vector3 angles = Quaternion.LookRotation(direction).eulerAngles;
            _yaw = angles.y;
            _pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, angles.x), pitchMin, pitchMax);
        }

        /// Holds a staged view of the point (e.g. a lock) until EndFocus; the mouse is ignored meanwhile.
        public void BeginFocus(Vector3 point)
        {
            _focusPoint = point;
            _focusing = true;
        }

        /// Hands the view back to the player from wherever the staged shot left it.
        public void EndFocus() => EndFocus(null);

        /// Hands the view back to the player, optionally turning it towards where the hero goes next.
        public void EndFocus(Vector3? lookAt)
        {
            if (!_focusing) return;
            _focusing = false;
            _yaw = _renderYaw;
            _pitch = _renderPitch;
            _releaseUntil = Time.time + focusBlendTime * 2f;
            if (lookAt.HasValue)
            {
                Vector3 direction = lookAt.Value - transform.position;
                if (direction.sqrMagnitude > .001f)
                {
                    Vector3 angles = Quaternion.LookRotation(direction).eulerAngles;
                    _yaw = _renderYaw + Mathf.DeltaAngle(_renderYaw, angles.y);
                    _pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, angles.x), pitchMin, pitchMax);
                }
            }
        }

        public void SetCursorLock(bool locked)
        {
            _cursorLocked = locked;
            _cursorLockFrame = Time.frameCount;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}

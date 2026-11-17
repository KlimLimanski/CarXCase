using UnityEngine;

namespace CarXCase.Hero
{
    /// Feeds the hero Animator with the horizontal movement of the controlled character
    /// (Idle → Walk → Run blend tree, parameter "Speed" in m/s). The value is measured from the
    /// actual displacement, so collisions, stairs and scripted moves drive the cycle as well.
    [DefaultExecutionOrder(20)]
    public sealed class HeroLocomotion : MonoBehaviour
    {
        public Animator animator;
        public CharacterController controller;
        [Tooltip("Speed (m/s) at which the Walk clip matches the ground without foot sliding.")]
        public float walkClipSpeed = 1.8f;
        [Tooltip("Smoothing (s) of the measured speed while speeding up.")]
        public float damping = 0.12f;
        [Tooltip("Smoothing (s) of the measured speed while slowing down.")]
        public float stopDamping = 0.09f;
        [Tooltip("Displacements faster than this are treated as teleports and ignored (m/s).")]
        public float maxSpeed = 8f;

        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int DirectionId = Animator.StringToHash("Direction");
        Vector3 lastPosition;
        float speed;
        float direction = 1f;
        bool hasDirection;
        Transform movementRoot;

        /// Smoothed horizontal speed (m/s) that drives the blend tree.
        public float GroundSpeed => speed;
        /// Position inside the locomotion cycle, 0..1; each cycle holds two footfalls.
        public float CyclePhase { get; private set; }

        void Awake()
        {
            if (!animator) animator = GetComponentInChildren<Animator>();
            if (!controller) controller = GetComponentInParent<CharacterController>();
            movementRoot = controller ? controller.transform : transform;
            if (animator)
                foreach (var parameter in animator.parameters)
                    hasDirection |= parameter.nameHash == DirectionId;
        }

        void OnEnable()
        {
            lastPosition = movementRoot.position;
            speed = 0f;
            direction = 1f;
        }

        void Update()
        {
            Vector3 position = movementRoot.position;
            Vector3 delta = position - lastPosition;
            lastPosition = position;
            if (!animator) return;

            // CharacterController.velocity can retain the last Move when input is suspended.
            delta.y = 0f;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            float target = delta.magnitude / dt;
            if (target > maxSpeed) target = speed;
            float tau = target < speed ? stopDamping : damping;
            speed = Mathf.Lerp(speed, target, 1f - Mathf.Exp(-dt / Mathf.Max(tau, 1e-3f)));
            if (target < .02f && speed < .04f) speed = 0f;

            // Scripted back-steps play the cycle in reverse instead of moonwalking forwards.
            float facing = target > .1f ? Vector3.Dot(delta.normalized, animator.transform.forward) : 1f;
            direction = Mathf.MoveTowards(direction, facing < -.35f ? -1f : 1f, dt * 5f);

            animator.SetFloat(SpeedId, speed);
            if (hasDirection) animator.SetFloat(DirectionId, direction);
            if (animator.isActiveAndEnabled && animator.runtimeAnimatorController)
                CyclePhase = Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1f);
        }
    }
}

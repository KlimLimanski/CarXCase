using UnityEngine;

namespace CarXCase.Precinct
{
    /// Desynchronises background NPCs that share a controller: each starts at its own point
    /// of the cycle and plays at a slightly different pace.
    [RequireComponent(typeof(Animator))]
    public sealed class NpcAnimationVariety : MonoBehaviour
    {
        [Range(0f, .2f)] public float speedJitter = .06f;

        void Start()
        {
            var animator = GetComponent<Animator>();
            if (!animator || !animator.runtimeAnimatorController) return;
            var random = new System.Random(name.GetHashCode() ^ transform.parent.name.GetHashCode());
            var state = animator.GetCurrentAnimatorStateInfo(0);
            animator.Play(state.fullPathHash, 0, (float)random.NextDouble());
            animator.speed = 1f + ((float)random.NextDouble() * 2f - 1f) * speedJitter;
        }
    }
}

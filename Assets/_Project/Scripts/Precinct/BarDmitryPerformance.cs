using System.Collections;
using CarXCase.Hero;
using UnityEngine;

namespace CarXCase.Precinct
{
    public sealed class BarDmitryPerformance : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] HeroFace face;
        static readonly int Standing = Animator.StringToHash("Base Layer.Standing");
        static readonly int Walking = Animator.StringToHash("Base Layer.Walking");
        const float ReleaseCounter = .25f;
        const float TurnDuration = .85f;

        public IEnumerator TurnTo(Transform listener)
        {
            Quaternion start = transform.rotation;
            Vector3 direction = Vector3.ProjectOnPlane(listener.position - transform.position, Vector3.up);
            Quaternion end = direction.sqrMagnitude > .001f ? Quaternion.LookRotation(direction) : start;
            animator.CrossFadeInFixedTime(Standing, .5f);
            yield return new WaitForSeconds(ReleaseCounter);
            float elapsed = 0f;
            while (elapsed < TurnDuration)
            {
                elapsed += Time.deltaTime;
                transform.rotation = Quaternion.Slerp(start, end, Mathf.SmoothStep(0f, 1f, elapsed / TurnDuration));
                yield return NoirMenu.GameSession.NextStoryFrame;
            }
            transform.rotation = end;
        }

        public void SetTalking(bool talking)
        {
            if (face) face.SetSpeech(talking);
        }

        public IEnumerator WalkTo(Vector3 destination)
        {
            const float Speed = 1.65f;
            destination.y = transform.position.y;
            Vector3 delta = destination - transform.position;
            if (delta.sqrMagnitude < .001f) yield break;
            Quaternion facing = Quaternion.LookRotation(delta);
            while (Quaternion.Angle(transform.rotation, facing) > 1f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, 280f * Time.deltaTime);
                yield return NoirMenu.GameSession.NextStoryFrame;
            }
            animator.CrossFadeInFixedTime(Walking, .2f);
            while (transform.position != destination)
            {
                transform.position = Vector3.MoveTowards(transform.position, destination, Speed * Time.deltaTime);
                yield return NoirMenu.GameSession.NextStoryFrame;
            }
            animator.CrossFadeInFixedTime(Standing, .2f);
        }
    }
}

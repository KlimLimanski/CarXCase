using System.Collections;
using CarXCase.Hero;
using UnityEngine;

namespace CarXCase.Precinct
{
    public sealed class BarDmitryPerformance : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] HeroFace face;
        [Tooltip("Conversation acting on the humanoid (gaze, weight shifts, gestures).")]
        [SerializeField] ConversationActing acting;
        static readonly int Standing = Animator.StringToHash("Base Layer.Standing");
        const float ReleaseCounter = .25f;
        const float TurnDuration = .85f;

        void Awake()
        {
            if (!acting && animator) acting = animator.GetComponent<ConversationActing>();
        }

        /// The conversation starts: Dmitry turns his attention to the listener.
        public void Engage(Transform listener)
        {
            if (acting) acting.Engage(listener);
        }

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

        /// Dmitry says a line: lips follow the subtitle, body acting follows its meaning.
        public void Speak(string line, float shownFor, Transform glance = null, float glanceStart = 0f)
        {
            if (acting) acting.Speak(line, shownFor, glance, glanceStart);
            else if (face) face.Speak(line, shownFor);
        }

        /// Alexey speaks: Dmitry listens and reacts.
        public void Listen(string heroLine)
        {
            if (acting) acting.Listen(heroLine);
            else if (face) face.StopSpeaking();
        }

        /// Nobody speaks (a choice is on screen).
        public void Wait()
        {
            if (acting) acting.Wait();
            else if (face) face.StopSpeaking();
        }

        public void EndConversation()
        {
            if (acting) acting.Disengage();
            else if (face) face.StopSpeaking();
        }

        /// Kept for older callers: talking without a line of text.
        public void SetTalking(bool talking)
        {
            if (face) face.SetSpeech(talking);
            if (!talking) Wait();
        }

        public IEnumerator WalkTo(Vector3 destination)
        {
            if (acting) acting.Disengage(true);
            if (face) face.StopSpeaking();
            return ConversationActing.WalkTo(transform, animator, destination, 1.65f);
        }
    }
}

using System.Collections;
using UnityEngine;

namespace NoirMenu
{
    /// <summary>Full-screen black overlay used for fade-in on start and fade-to-black on scene change.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ScreenFader : MonoBehaviour
    {
        public float fadeInOnStart = 1.6f;
        CanvasGroup group;

        void Awake()
        {
            group = GetComponent<CanvasGroup>();
            group.alpha = GameSession.Paused ? 0f : 1f;
            group.blocksRaycasts = !GameSession.Paused;
            group.interactable = false;
        }

        IEnumerator Start()
        {
            if (GameSession.Paused) yield break;
            if (fadeInOnStart > 0f) yield return FadeTo(0f, fadeInOnStart);
            else { group.alpha = 0f; group.blocksRaycasts = false; }
        }

        public IEnumerator FadeTo(float alpha, float duration)
        {
            group.blocksRaycasts = true;
            float from = group.alpha;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(from, alpha, Mathf.SmoothStep(0f, 1f, t / duration));
                yield return null;
            }
            group.alpha = alpha;
            group.blocksRaycasts = alpha > 0.01f;
        }
    }
}

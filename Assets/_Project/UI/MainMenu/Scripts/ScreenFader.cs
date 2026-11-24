using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace NoirMenu
{
    /// <summary>Full-screen black overlay used for fade-in on start and fade-to-black on scene change.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ScreenFader : MonoBehaviour
    {
        public float fadeInOnStart = 1.6f;
        CanvasGroup group;
        AsyncOperation sceneLoad;

        public static void RevealAfterLoad(AsyncOperation load, float seconds)
        {
            if (load == null) return;
            var overlay = new GameObject("Scene transition", typeof(Canvas), typeof(CanvasGroup));
            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            DontDestroyOnLoad(overlay);

            var black = new GameObject("Black", typeof(Image)).GetComponent<Image>();
            black.color = Color.black;
            black.raycastTarget = false;
            black.rectTransform.SetParent(overlay.transform, false);
            black.rectTransform.anchorMin = Vector2.zero;
            black.rectTransform.anchorMax = Vector2.one;
            black.rectTransform.offsetMin = black.rectTransform.offsetMax = Vector2.zero;

            var fader = overlay.AddComponent<ScreenFader>();
            fader.sceneLoad = load;
            fader.fadeInOnStart = seconds;
        }

        void Awake()
        {
            group = GetComponent<CanvasGroup>();
            group.alpha = GameSession.Paused ? 0f : 1f;
            group.blocksRaycasts = !GameSession.Paused;
            group.interactable = false;
        }

        IEnumerator Start()
        {
            if (sceneLoad != null)
            {
                yield return sceneLoad;
                yield return null;
                yield return FadeTo(0f, fadeInOnStart);
                Destroy(gameObject);
                yield break;
            }
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

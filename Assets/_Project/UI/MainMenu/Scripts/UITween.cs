using System.Collections;
using UnityEngine;

namespace NoirMenu
{
    /// <summary>Small coroutine helpers for unhurried UI fades (unscaled time).</summary>
    public static class UITween
    {
        /// <summary>
        /// Fades a CanvasGroup to <paramref name="to"/>. If <paramref name="slide"/> is given, it is
        /// moved horizontally from basePos+fromX to basePos+toX at the same time.
        /// </summary>
        public static IEnumerator Fade(CanvasGroup group, float to, float duration,
            RectTransform slide = null, Vector2 basePos = default, float fromX = 0f, float toX = 0f)
        {
            if (group == null) yield break;
            float from = group.alpha;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / duration);
                group.alpha = Mathf.Lerp(from, to, k);
                if (slide) slide.anchoredPosition = basePos + new Vector2(Mathf.Lerp(fromX, toX, k), 0f);
                yield return null;
            }
            group.alpha = to;
            if (slide) slide.anchoredPosition = basePos + new Vector2(toX, 0f);
        }

        public static void SetVisible(CanvasGroup group, bool visible)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}

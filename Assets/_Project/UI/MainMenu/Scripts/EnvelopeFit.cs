using UnityEngine;

namespace NoirMenu
{
    /// <summary>
    /// Keeps a fixed-size "video space" rect (the video's native 1672×940) covering its parent
    /// without distortion (envelope / cover). Everything parented under it is laid out in the
    /// video's own pixel coordinates, so UI lines up exactly with the frame lines drawn in the video.
    /// If the screen is narrower than the video, the left column is nudged right so it never gets clipped.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class EnvelopeFit : MonoBehaviour
    {
        public Vector2 designSize = new Vector2(1672f, 940f);
        public RectTransform leftColumn;
        [Tooltip("Left-most x (video pixels) of the content in the left column.")]
        public float leftColumnContentX = 100f;
        public float leftColumnMargin = 32f;

        RectTransform rect;

        void OnEnable()
        {
            rect = (RectTransform)transform;
            Apply();
        }

        void LateUpdate() => Apply();

        void Apply()
        {
            if (rect == null) rect = (RectTransform)transform;
            var parent = rect.parent as RectTransform;
            if (parent == null) return;

            Set(rect, new Vector2(0.5f, 0.5f), designSize, Vector2.zero);

            Vector2 ps = parent.rect.size;
            if (ps.x <= 0f || ps.y <= 0f) return;
            float s = Mathf.Max(ps.x / designSize.x, ps.y / designSize.y);
            var scale = new Vector3(s, s, 1f);
            if (rect.localScale != scale) rect.localScale = scale;

            if (leftColumn)
            {
                float hiddenLeft = (designSize.x - ps.x / s) * 0.5f; // video pixels cut off at the left edge
                float shift = Mathf.Max(0f, hiddenLeft + leftColumnMargin - leftColumnContentX);
                var pos = new Vector2(Mathf.Round(shift), 0f);
                if (leftColumn.anchoredPosition != pos) leftColumn.anchoredPosition = pos;
            }
        }
    
        static void Set(RectTransform r, Vector2 anchor, Vector2 size, Vector2 pos)
        {
            if (r.anchorMin != anchor) r.anchorMin = anchor;
            if (r.anchorMax != anchor) r.anchorMax = anchor;
            if (r.pivot != anchor) r.pivot = anchor;
            if (r.sizeDelta != size) r.sizeDelta = size;
            if (r.anchoredPosition != pos) r.anchoredPosition = pos;
        }
    }
}

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NoirMenu
{
    /// <summary>
    /// Restrained highlight for a menu item / settings row, styled after the video:
    /// a faint warm bar fading to the right, a hairline on its left edge, a small arrow,
    /// and text that brightens. Hovering with the mouse selects the item, so mouse,
    /// keyboard and gamepad share a single highlight.
    /// </summary>
    public class SelectionVisual : MonoBehaviour, IPointerEnterHandler
    {
        public Selectable target;
        public Graphic bar;
        public Graphic border;
        public Graphic arrow;
        public Text[] labels;
        public RectTransform slideLabel;

        [Header("Colours at full highlight")]
        public Color barColor = new Color32(150, 120, 100, 66);
        public Color borderColor = new Color32(118, 102, 88, 150);
        public Color arrowColor = new Color32(125, 108, 94, 255);
        public Color normalText = new Color32(76, 65, 56, 255);
        public Color selectedText = new Color32(221, 211, 202, 255);
        public Color disabledText = new Color32(44, 37, 32, 255);
        public float barAlpha = 1f;
        public float labelSlide = 4f;
        public float speed = 10f;

        float k;               // 0 = idle, 1 = selected
        bool wasSelected;
        float baseLabelX;

        void Awake()
        {
            if (target == null) target = GetComponent<Selectable>();
            if (slideLabel) baseLabelX = slideLabel.anchoredPosition.x;
            Apply(true);
        }

        void OnEnable() => Apply(true);

        bool IsSelected()
        {
            var es = EventSystem.current;
            return es != null && target != null && es.currentSelectedGameObject == target.gameObject;
        }

        bool IsInteractable() => target == null || target.IsInteractable();

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (target == null || !IsInteractable() || EventSystem.current == null) return;
            if (EventSystem.current.currentSelectedGameObject != target.gameObject)
                EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        void Update() => Apply(false);

        void Apply(bool instant)
        {
            bool interactable = target == null || target.interactable;   // own state (not the window's fade state)
            bool selected = interactable && IsInteractable() && IsSelected();
            if (selected && !wasSelected && !instant) UiSfx.PlayHover();
            wasSelected = selected;
            float goal = selected ? 1f : 0f;
            k = instant ? goal : Mathf.MoveTowards(k, goal, Time.unscaledDeltaTime * speed);
            float e = Mathf.SmoothStep(0f, 1f, k);

            Color text = interactable ? Color.Lerp(normalText, selectedText, e) : disabledText;
            if (labels != null)
                foreach (var l in labels) if (l) l.color = text;
            if (bar) bar.color = WithAlpha(barColor, barColor.a * barAlpha * e);
            if (border) border.color = WithAlpha(borderColor, borderColor.a * e);
            if (arrow) arrow.color = WithAlpha(arrowColor, arrowColor.a * e);
            if (slideLabel)
                slideLabel.anchoredPosition = new Vector2(baseLabelX + labelSlide * e, slideLabel.anchoredPosition.y);
        }

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }
    }
}

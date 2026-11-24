using UnityEngine;

namespace CarXCase.UI
{
    /// The answer button from the bar dialogue: a flat dark-teal plate that warms to brown under the cursor,
    /// with plain left-aligned text. Shared so every choice / action button in the game looks the same.
    public static class ChoiceButton
    {
        public static readonly Color Idle = new Color(.045f, .072f, .079f, .88f);
        public static readonly Color Hover = new Color(.32f, .27f, .19f, .92f);

        static GUIStyle style;

        /// Draws the button and returns true when it was clicked.
        /// `selected` keeps the hover colour on, e.g. for an already picked answer.
        public static bool Draw(Rect rect, string text, int fontSize = 20,
            TextAnchor alignment = TextAnchor.MiddleLeft, bool selected = false)
        {
            style ??= new GUIStyle(GameTypography.Label) { wordWrap = true };
            style.fontSize = fontSize;
            style.alignment = alignment;

            bool hover = selected || rect.Contains(Event.current.mousePosition);
            var previous = GUI.color;
            GUI.color = hover ? Hover : Idle;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;

            float pad = alignment == TextAnchor.MiddleLeft ? 12f * fontSize / 20f : 6f;
            GUI.Label(new Rect(rect.x + pad, rect.y, rect.width - pad - 6f, rect.height), text, style);
            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }
    }
}

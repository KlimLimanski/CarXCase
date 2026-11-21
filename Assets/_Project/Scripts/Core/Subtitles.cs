using UnityEngine;

namespace CarXCase.UI
{
    /// One subtitle style for the whole game: plain white text, no panel, no speaker name.
    /// A faint 1-2 px shadow keeps it readable on bright frames without looking like a background.
    public static class Subtitles
    {
        static GUIStyle text, shadow;

        public static void Draw(string line, float top = 0.79f)
        {
            if (string.IsNullOrEmpty(line)) return;
            if (text == null)
            {
                text = new GUIStyle(GameTypography.Label) { alignment = TextAnchor.UpperCenter, wordWrap = true, fontStyle = FontStyle.Normal, richText = false };
                text.normal.textColor = new Color(0.95f, 0.95f, 0.93f, 1f);
                shadow = new GUIStyle(text);
                shadow.normal.textColor = new Color(0f, 0f, 0f, 0.55f);
            }
            float W = Screen.width, H = Screen.height;
            int size = Mathf.RoundToInt(Mathf.Clamp(H * 0.031f, 15f, 64f));
            text.fontSize = size; shadow.fontSize = size;
            var r = new Rect(W * 0.14f, H * top, W * 0.72f, H * 0.13f);
            float o = Mathf.Max(1f, size / 18f);
            GUI.Label(new Rect(r.x + o, r.y + o, r.width, r.height), line, shadow);
            GUI.Label(r, line, text);
        }
    }
}

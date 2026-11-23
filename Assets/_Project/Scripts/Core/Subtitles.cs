using UnityEngine;

namespace CarXCase.UI
{
    /// One subtitle style for the whole game: plain white text with optional speaker attribution.
    /// A faint 1-2 px shadow keeps it readable on bright frames without looking like a background.
    public static class Subtitles
    {
        static GUIStyle text, shadow;

        public static string WithSpeaker(string speaker, string line) =>
            string.IsNullOrWhiteSpace(speaker) || string.IsNullOrWhiteSpace(line) ? line : $"{speaker}: {line}";

        /// Subtitles are not controls: no hover, press or focus tint, whatever the mouse does.
        static void Flat(GUIStyle style)
        {
            foreach (var state in new[] { style.hover, style.active, style.focused, style.onNormal, style.onHover, style.onActive, style.onFocused })
            {
                state.textColor = style.normal.textColor;
                state.background = style.normal.background;
                state.scaledBackgrounds = style.normal.scaledBackgrounds;
            }
        }

        public static void Draw(string line, float top = 0.79f)
        {
            if (string.IsNullOrEmpty(line)) return;
            VoiceOver.OnSubtitle(line);
            if (text == null)
            {
                text = new GUIStyle(GUI.skin.label) { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), alignment = TextAnchor.UpperCenter, wordWrap = true, fontStyle = FontStyle.Normal, richText = false };
                text.normal.textColor = new Color(0.95f, 0.95f, 0.93f, 1f);
                Flat(text);
                shadow = new GUIStyle(text);
                shadow.normal.textColor = new Color(0f, 0f, 0f, 0.55f);
                Flat(shadow);
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

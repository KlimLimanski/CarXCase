using UnityEngine;

namespace CarXCase.UI
{
    internal sealed class DialogueChoiceRows
    {
        internal static readonly Color Gold = new Color(.88f, .73f, .41f);
        internal static readonly Color Cream = new Color(.99f, .95f, .82f);

        GUIStyle label, numeral;

        void SetScale(float scale)
        {
            if (label == null)
            {
                label = new GUIStyle(GameTypography.Label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    wordWrap = true,
                    padding = new RectOffset()
                };
                var font = GameTypography.Face;
                if (font) label.font = font;
                numeral = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
            }
            label.fontSize = Mathf.RoundToInt(19f * scale);
            numeral.fontSize = Mathf.RoundToInt(20f * scale);
        }

        internal float Measure(string text, float scale, float maximum)
        {
            SetScale(scale);
            return Mathf.Min(maximum, Mathf.Max(110f * scale, label.CalcSize(new GUIContent(text)).x + 59f * scale));
        }

        internal void Draw(Rect row, string text, int number, float scale, bool highlighted)
        {
            SetScale(scale);
            float keyWidth = 33f * scale;
            var key = new Rect(row.xMax - keyWidth, row.y, keyWidth, row.height);
            Fill(row, highlighted ? new Color(.19f, .15f, .09f, .94f) : new Color(.036f, .029f, .020f, .87f));
            Fill(new Rect(row.x, row.y, row.width, Mathf.Max(1f, scale)), new Color(Gold.r, Gold.g, Gold.b, highlighted ? .9f : .62f));
            Fill(new Rect(row.x, row.yMax - 2f * scale, 13f * scale, 2f * scale), Gold);
            Fill(key, highlighted ? Gold : new Color(.32f, .25f, .13f, .90f));
            numeral.normal.textColor = highlighted ? new Color(.11f, .08f, .04f) : Cream;
            GUI.Label(key, number.ToString(), numeral);
            label.normal.textColor = Cream;
            GUI.Label(new Rect(row.x + 11f * scale, row.y, key.x - row.x - 20f * scale, row.height), text, label);
        }

        internal static void Fill(Rect rect, Color ink)
        {
            var previous = GUI.color;
            GUI.color = ink;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}

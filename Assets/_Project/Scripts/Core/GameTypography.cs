using UnityEngine;

public static class GameTypography
{
    static Font face;
    static GUIStyle label, button;

    public static Font Face
    {
        get
        {
            if (!face) face = (Resources.Load<Font>("EvidenceLettering/LiberationSerif-Regular") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            return face;
        }
    }

    public static GUIStyle Label => label ??= new GUIStyle(GUI.skin.label) { font = Face }.Steady();
    public static GUIStyle Button => button ??= new GUIStyle(GUI.skin.button) { font = Face };

    /// IMGUI switches a label to its hover state when the mouse is over it, which changes the text colour.
    /// Copies the normal look into the hover / active / focused states so text never reacts to the cursor.
    /// Call again after changing normal.textColor.
    public static GUIStyle Steady(this GUIStyle style)
    {
        Color ink = style.normal.textColor;
        Texture2D back = style.normal.background;
        foreach (var state in new[] { style.hover, style.active, style.focused, style.onHover, style.onActive, style.onFocused })
        {
            state.textColor = ink;
            state.background = back;
        }
        return style;
    }
}

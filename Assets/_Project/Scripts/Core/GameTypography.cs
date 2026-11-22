using UnityEngine;

public static class GameTypography
{
    static Font face;
    static GUIStyle label, button;

    public static Font Face
    {
        get
        {
            if (!face) face = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return face;
        }
    }

    public static GUIStyle Label => label ??= new GUIStyle(GUI.skin.label) { font = Face };
    public static GUIStyle Button => button ??= new GUIStyle(GUI.skin.button) { font = Face };
}

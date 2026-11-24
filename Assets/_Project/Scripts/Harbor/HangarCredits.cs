using System.Collections;
using NoirMenu;
using UnityEngine;
using UnityEngine.SceneManagement;

/// End credits after the case is closed: the screen goes to black, the names roll up from the bottom
/// and dissolve near the edges, under "Goodbye America" from 0:40 to 1:05 with a soft swell and fade.
public sealed partial class HangarFinale
{
    const float CreditsLength = 25f;     // 0:40 → 1:05
    const float SongStart = 40f;

    static readonly (string role, string[] names)[] CreditBlocks =
    {
        ("Разработка", new[] { "Лиманский Клим", "Острикова Мария", "Ремизова Анастасия" }),
        ("Визуал", new[] { "Лиманский Клим", "Ремизова Анастасия", "Острикова Мария" }),
        ("Сценарий", new[] { "Хайновский Максим" }),
        ("Саунд Дизайн", new[] { "Помнющая Аглая" }),
    };

    bool creditsRolling;
    AudioSource creditsSong;
    float creditsTime, creditsBlack;
    GUIStyle creditRole, creditName;

    IEnumerator RollCredits()
    {
        creditsRolling = true;
        float startingBlack = endingFade;
        creditsBlack = startingBlack;
        stage = Stage.Aftermath;            // hides the case report panel
        caption = null;
        var song = creditsSong = gameObject.AddComponent<AudioSource>();
        song.playOnAwake = false;
        song.spatialBlend = 0f;
        song.volume = 0f;
        song.clip = Resources.Load<AudioClip>("Credits/Goodbye America");
        if (song.clip)
        {
            song.clip.LoadAudioData();
            song.time = Mathf.Min(SongStart, song.clip.length - 1f);
            song.Play();
        }
        for (creditsTime = 0f; creditsTime < CreditsLength; )
        {
            if (GameSession.Paused) { if (song.isPlaying) song.Pause(); yield return null; continue; }
            if (song.clip && !song.isPlaying) song.UnPause();
            creditsTime += Time.unscaledDeltaTime;
            float swell = Smooth(0f, 3f, creditsTime), fade = 1f - Smooth(CreditsLength - 5f, CreditsLength, creditsTime);
            song.volume = .95f * GameSettings.MusicGain * swell * fade;
            SceneScore.Duck = 1f - Smooth(0f, 2.2f, creditsTime);
            creditsBlack = Mathf.Lerp(startingBlack, 1f, Smooth(0f, 1.8f, creditsTime));
            yield return null;
        }
        song.Stop();
        Leave();
        endingFade = 1f;
        SceneManager.LoadSceneAsync("00_MainMenu");
    }

    void StopCredits()
    {
        creditsRolling = false;
        if (creditsSong)
        {
            creditsSong.Stop();
            Destroy(creditsSong);
        }
        creditsSong = null;
        SceneScore.Duck = 1f;
    }

    static float Smooth(float a, float b, float v) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, v));

    /// Draws the credits; returns true while they own the screen.
    bool DrawCredits()
    {
        if (!creditsRolling) return false;
        Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, creditsBlack));
        float s = Screen.height / 1080f;
        if (creditRole == null)
        {
            creditRole = new GUIStyle(GameTypography.Label) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
            creditName = new GUIStyle(creditRole);
        }
        creditRole.fontSize = Mathf.RoundToInt(19f * s);
        creditName.fontSize = Mathf.RoundToInt(34f * s);

        float roleLine = 34f * s, nameLine = 50f * s, gap = 92f * s;
        float content = 0f;
        foreach (var block in CreditBlocks) content += roleLine + 14f * s + block.names.Length * nameLine + gap;
        content -= gap;

        // Rolls from just below the frame to just above it between 1.5 s and 3.5 s before the end.
        float travel = Mathf.InverseLerp(1.5f, CreditsLength - 3.5f, creditsTime);
        float y = Mathf.Lerp(Screen.height + 20f * s, -content - 20f * s, travel);
        float w = Screen.width;
        foreach (var block in CreditBlocks)
        {
            Line(new Rect(0, y, w, roleLine), Spaced(block.role.ToUpperInvariant()), creditRole, new Color(.78f, .64f, .42f));
            Fill(new Rect(w * .5f - 22f * s, y + roleLine + 4f * s, 44f * s, Mathf.Max(1f, s)), new Color(.78f, .64f, .42f, .55f * EdgeFade(y)));
            y += roleLine + 14f * s;
            foreach (var name in block.names)
            {
                Line(new Rect(0, y, w, nameLine), name, creditName, new Color(.95f, .93f, .88f));
                y += nameLine;
            }
            y += gap;
        }
        return true;
    }

    void Line(Rect r, string text, GUIStyle style, Color ink)
    {
        float a = EdgeFade(r.center.y);
        if (a <= 0f) return;
        style.normal.textColor = new Color(ink.r, ink.g, ink.b, a);
        style.Steady();
        GUI.Label(r, text, style);
    }

    /// Text dissolves in over the bottom fifth of the screen and out over the top fifth.
    float EdgeFade(float y)
    {
        float band = Screen.height * .2f;
        return Mathf.Clamp01(Mathf.Min(y / band, (Screen.height - y) / band)) * creditsBlack;
    }

    static string Spaced(string word) => string.Join(" ", word.ToCharArray()).Replace("   ", "     ");
}

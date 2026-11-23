using System.Collections.Generic;
using UnityEngine;

/// Cutscene sounds the project has no recordings for, synthesised once on first use.
/// A real file with the same name in Resources/LocationAudio/Effects always wins (see LocationAudio.Effect).
public static class ProceduralSfx
{
    const int Rate = 44100;
    static readonly Dictionary<string, AudioClip> cache = new();

    public static AudioClip Get(string name)
    {
        if (cache.TryGetValue(name, out var clip)) return clip;
        float[] data = name switch
        {
            "Police siren" => Siren(),
            "Body fall" => BodyFall(),
            "Handcuffs" => Handcuffs(),
            "Rope" => Rope(),
            _ => null
        };
        if (data == null) return null;
        clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        cache[name] = clip;
        return clip;
    }

    /// A distant NYPD wail: slow up-and-down sweep, muffled by distance, swelling in and dying away.
    static float[] Siren()
    {
        const float length = 7.5f;
        var s = new float[(int)(length * Rate)];
        double phase = 0;
        float low = 0, low2 = 0;
        var echo = new float[(int)(.11f * Rate)];
        for (int i = 0; i < s.Length; i++)
        {
            float t = (float)i / Rate;
            float f = 680f + 640f * (.5f - .5f * Mathf.Cos(2f * Mathf.PI * t / 3.1f));
            phase += 2 * System.Math.PI * f / Rate;
            float p = (float)phase;
            float v = .62f * Mathf.Sin(p) + .24f * Mathf.Sin(2 * p) + .11f * Mathf.Sin(3 * p);
            low += (v - low) * .22f;            // distance: walls and rain eat the highs
            low2 += (low - low2) * .3f;
            int e = i % echo.Length;
            float wet = low2 + echo[e] * .35f;   // a slap back off the warehouses
            echo[e] = wet;
            float env = Mathf.SmoothStep(0, 1, t / 2.6f) * Mathf.SmoothStep(0, 1, (length - t) / 1.8f);
            s[i] = wet * env * .55f;
        }
        return s;
    }

    /// A body hitting a concrete floor: low thump, a short scuff, a second softer knock.
    static float[] BodyFall()
    {
        var s = new float[(int)(1.1f * Rate)];
        var rnd = new System.Random(7);
        float noise = 0;
        double phase = 0;
        for (int i = 0; i < s.Length; i++)
        {
            float t = (float)i / Rate;
            float f = Mathf.Lerp(78f, 42f, Mathf.Clamp01(t / .25f));
            phase += 2 * System.Math.PI * f / Rate;
            float thump = Mathf.Sin((float)phase) * Mathf.Exp(-t * 11f);
            noise += ((float)rnd.NextDouble() * 2 - 1 - noise) * .08f;
            float scuff = noise * 3.2f * Mathf.Exp(-t * 22f);
            float t2 = t - .16f;
            float knock = t2 > 0 ? Mathf.Sin(2 * Mathf.PI * 95f * t2) * Mathf.Exp(-t2 * 26f) * .45f : 0;
            float slide = t > .2f ? noise * 1.4f * Mathf.Exp(-(t - .2f) * 7f) : 0;
            s[i] = Mathf.Clamp((thump + scuff + knock + slide) * .8f, -1, 1);
        }
        return s;
    }

    /// Handcuffs closing: a quick ratchet of metal clicks, a final lock and a faint chain jingle.
    static float[] Handcuffs()
    {
        var s = new float[(int)(.95f * Rate)];
        var rnd = new System.Random(11);
        float[] clicks = { .02f, .058f, .094f, .128f, .16f, .19f, .218f, .43f };
        for (int c = 0; c < clicks.Length; c++)
        {
            bool final = c == clicks.Length - 1;
            float amp = final ? .9f : .45f + (float)rnd.NextDouble() * .15f;
            float tone = final ? 2600f : 3300f + (float)rnd.NextDouble() * 500f;
            int start = (int)(clicks[c] * Rate);
            for (int i = 0; i < (int)(.06f * Rate) && start + i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float ring = Mathf.Sin(2 * Mathf.PI * tone * t) + .6f * Mathf.Sin(2 * Mathf.PI * tone * 1.47f * t) + .35f * Mathf.Sin(2 * Mathf.PI * tone * 2.13f * t);
                float tick = ((float)rnd.NextDouble() * 2 - 1) * Mathf.Exp(-t * 900f);
                s[start + i] += amp * (ring * .3f * Mathf.Exp(-t * (final ? 55f : 110f)) + tick * .8f);
            }
        }
        // Chain jingle after the lock.
        for (int j = 0; j < 5; j++)
        {
            int start = (int)((.5f + j * .055f + (float)rnd.NextDouble() * .02f) * Rate);
            float tone = 4200f + (float)rnd.NextDouble() * 1800f;
            for (int i = 0; i < (int)(.12f * Rate) && start + i < s.Length; i++)
            {
                float t = (float)i / Rate;
                s[start + i] += .12f * Mathf.Sin(2 * Mathf.PI * tone * t) * Mathf.Exp(-t * 40f);
            }
        }
        for (int i = 0; i < s.Length; i++) s[i] = Mathf.Clamp(s[i] * .7f, -1, 1);
        return s;
    }

    /// Rope being pulled loose: rough fibre rasps in a few tugs.
    static float[] Rope()
    {
        var s = new float[(int)(1.3f * Rate)];
        var rnd = new System.Random(23);
        float a = 0, b = 0;
        float[] tugs = { .0f, .32f, .62f, .9f };
        for (int i = 0; i < s.Length; i++)
        {
            float t = (float)i / Rate;
            float n = (float)rnd.NextDouble() * 2 - 1;
            a += (n - a) * .35f;                 // band-pass: fibre rasp around 1-3 kHz
            b += (a - b) * .06f;
            float band = a - b;
            float env = 0;
            foreach (float start in tugs)
            {
                float u = t - start;
                if (u > 0 && u < .34f) env = Mathf.Max(env, Mathf.Sin(Mathf.PI * u / .34f) * (1f - start * .35f));
            }
            float grain = .65f + .35f * Mathf.PerlinNoise(t * 140f, 3f);
            s[i] = Mathf.Clamp(band * env * grain * 1.7f, -1, 1);
        }
        return s;
    }
}

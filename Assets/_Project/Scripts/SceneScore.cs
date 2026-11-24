using NoirMenu;
using UnityEngine;

// The score survives its owner briefly so a scene change can release the last note.
public sealed class SceneScore : MonoBehaviour
{
    const double Crossfade = 2.4;
    readonly AudioSource[] voices = new AudioSource[2];
    readonly double[] starts = new double[2], ends = new double[2];
    string[] tracks;
    LocationAudio owner;
    int next, lead;
    double release = -1;
    float level;

    /// 0..1 multiplier the end credits use to fade the level score out under their song.
    public static float Duck = 1f;

    public static void StartFor(LocationAudio owner, string[] tracks)
    {
        Duck = 1f;
        var go = new GameObject("Scene soundtrack");
        DontDestroyOnLoad(go);
        var score = go.AddComponent<SceneScore>();
        score.owner = owner;
        score.tracks = tracks;
        for (int i = 0; i < 2; i++)
        {
            var voice = go.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.volume = 0;
            score.voices[i] = voice;
        }
        score.Schedule(0, AudioSettings.dspTime + .15);
    }

    void Schedule(int slot, double start)
    {
        var voice = voices[slot];
        voice.clip = Resources.Load<AudioClip>("LocationAudio/Music/" + tracks[next]);
        next = (next + 1) % tracks.Length;
        starts[slot] = start;
        ends[slot] = start + (double)voice.clip.samples / voice.clip.frequency;
        voice.volume = 0;
        voice.PlayScheduled(start);
    }

    void Update()
    {
        double now = AudioSettings.dspTime;
        if (owner) level = owner.ScoreLevel;
        else if (release < 0) release = now;
        float tail = release < 0 ? 1 : Mathf.Clamp01(1f - (float)(now - release) / 1.5f);
        if (tail <= 0) { Destroy(gameObject); return; }

        if (owner && now >= ends[lead] - Crossfade - 1.0)
        {
            double start = System.Math.Max(now + .05, ends[lead] - Crossfade);
            lead = 1 - lead;
            Schedule(lead, start);
        }
        for (int i = 0; i < voices.Length; i++)
        {
            float edge = Mathf.Clamp01((float)(System.Math.Min(now - starts[i], ends[i] - now) / Crossfade));
            voices[i].volume = Mathf.Sin(edge * Mathf.PI * .5f) * tail * GameSettings.MusicGain * level * Duck;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace CarXCase.Hero
{
    /// Mouth timeline built from a subtitle line: one syllable per vowel, lips closing on
    /// labials and between words, longer rests at punctuation. The whole timeline is stretched or
    /// compressed to the time the subtitle stays on screen, so articulation and pauses follow it.
    public sealed class SpeechTrack
    {
        public struct Mouth
        {
            public float open, round, wide, accent;
            public bool voiced;
        }

        struct Syllable
        {
            public float start, end, open, round, wide;
            public bool stressed;
            public int charIndex;
        }

        const float Lead = .07f;
        const float Tail = .35f;
        readonly List<Syllable> syllables = new List<Syllable>();

        public string Text { get; private set; }
        /// How long the line is shown (seconds).
        public float Duration { get; private set; }
        /// When the mouth stops moving (seconds from the start of the line).
        public float SpeechEnd { get; private set; }
        public int SyllableCount => syllables.Count;

        public static SpeechTrack FromText(string text, float shownFor, float syllablesPerSecond = 5.4f)
        {
            var track = new SpeechTrack { Text = text ?? "" };
            track.Build(Mathf.Max(2f, syllablesPerSecond), shownFor);
            return track;
        }

        void Build(float rate, float shownFor)
        {
            string text = Text;
            float syl = 1f / rate;
            float time = 0f, pendingGap = 0f;
            bool labial = false;
            int wordStart = -1, consonantsAfterVowel = 0;

            for (int i = 0; i <= text.Length; i++)
            {
                char c = i < text.Length ? char.ToLowerInvariant(text[i]) : ' ';
                if (char.IsLetter(c) || char.IsDigit(c))
                {
                    if (pendingGap > 0f && syllables.Count > 0) time += pendingGap;
                    pendingGap = 0f;
                    if (wordStart < 0) wordStart = syllables.Count;
                    if (char.IsDigit(c))
                    {   // numbers are read out: roughly two open syllables per digit
                        Add(ref time, syl, 'а', i);
                        Add(ref time, syl, 'о', i);
                        consonantsAfterVowel = 0;
                        continue;
                    }
                    if (IsVowel(c))
                    {
                        if (labial) time += syl * .3f; // lips meet for м/б/п/в/ф before the vowel
                        labial = false;
                        Add(ref time, syl, c, i);
                        consonantsAfterVowel = 0;
                    }
                    else
                    {
                        labial = IsLabial(c);
                        consonantsAfterVowel++;
                    }
                    continue;
                }

                // separator: finish the word, then remember the longest rest until the next word
                if (wordStart >= 0)
                {
                    time += syl * .18f * Mathf.Min(consonantsAfterVowel, 2); // closing consonants
                    Stress(wordStart);
                    wordStart = -1;
                }
                consonantsAfterVowel = 0;
                labial = false;
                pendingGap = Mathf.Max(pendingGap, GapAfter(c, i, syl));
            }

            float natural = time;
            if (syllables.Count == 0 || natural <= 0f)
            {
                SpeechEnd = 0f;
                Duration = Mathf.Max(shownFor, .1f);
                return;
            }

            float available = shownFor > 0f ? Mathf.Max(.3f, shownFor - Tail - Lead) : natural;
            float scale = Mathf.Clamp(available / natural, .55f, 1.3f);
            for (int i = 0; i < syllables.Count; i++)
            {
                var s = syllables[i];
                s.start = Lead + s.start * scale;
                s.end = Lead + s.end * scale;
                syllables[i] = s;
            }
            SpeechEnd = Lead + natural * scale;
            Duration = shownFor > 0f ? shownFor : SpeechEnd + Tail;
        }

        void Add(ref float time, float length, char vowel, int index)
        {
            var s = Shape(vowel);
            s.start = time;
            s.end = time + length;
            s.charIndex = index;
            syllables.Add(s);
            time += length;
        }

        /// Marks one syllable of the word as stressed: louder, wider open, small head/brow accent.
        void Stress(int first)
        {
            int count = syllables.Count - first;
            if (count <= 0) return;
            int pick = count == 1 ? first : first + Mathf.Clamp(count / 2, 0, count - 1);
            var s = syllables[pick];
            if (count == 1 && s.open < .5f) return; // short function words stay unstressed
            s.stressed = true;
            s.open = Mathf.Min(1f, s.open * 1.15f);
            syllables[pick] = s;
        }

        float GapAfter(char c, int index, float syl)
        {
            switch (c)
            {
                case '.':
                case '!':
                case '?':
                case '…':
                case '\n':
                    return syl * 2.6f;
                case ',':
                case ';':
                case ':':
                case '—':
                case '–':
                case '(':
                case ')':
                    return syl * 1.6f;
                case '-':
                    // a hyphen inside a word (Кони-Айленда) is not a pause
                    bool inside = index > 0 && index + 1 < Text.Length && char.IsLetter(Text[index - 1]) && char.IsLetter(Text[index + 1]);
                    return inside ? syl * .2f : syl * 1.6f;
                default:
                    return syl * .35f;
            }
        }

        static bool IsVowel(char c) => "аеёиоуыэюяaeiouy".IndexOf(c) >= 0;
        static bool IsLabial(char c) => "мбпвфmbpvfw".IndexOf(c) >= 0;

        static Syllable Shape(char vowel)
        {
            switch (vowel)
            {
                case 'а': case 'я': case 'a': return new Syllable { open = 1f, round = 0f, wide = .15f };
                case 'о': case 'ё': case 'o': return new Syllable { open = .78f, round = .75f, wide = 0f };
                case 'у': case 'ю': case 'u': return new Syllable { open = .38f, round = 1f, wide = 0f };
                case 'э': case 'е': case 'e': return new Syllable { open = .7f, round = 0f, wide = .45f };
                case 'и': case 'i': return new Syllable { open = .42f, round = 0f, wide = .85f };
                case 'ы': case 'y': return new Syllable { open = .5f, round = 0f, wide = .5f };
                default: return new Syllable { open = .6f };
            }
        }

        /// Mouth state at a time since the start of the line. Returns closed outside syllables.
        public Mouth Evaluate(float t)
        {
            var m = new Mouth();
            if (syllables.Count == 0 || t < 0f || t > SpeechEnd) return m;
            int lo = 0, hi = syllables.Count - 1;
            while (lo < hi)
            {   // last syllable that starts at or before t
                int mid = (lo + hi + 1) >> 1;
                if (syllables[mid].start <= t) lo = mid;
                else hi = mid - 1;
            }
            var s = syllables[lo];
            if (t < s.start || t > s.end) return m;
            float u = (t - s.start) / Mathf.Max(s.end - s.start, 1e-4f);
            float envelope = Mathf.Sqrt(Mathf.Sin(u * Mathf.PI));
            float hold = Mathf.Clamp01(envelope * 1.6f);
            m.open = s.open * envelope;
            m.round = s.round * hold;
            m.wide = s.wide * hold;
            m.accent = s.stressed ? envelope : 0f;
            m.voiced = true;
            return m;
        }

        /// Time (seconds from the start of the line) at which the given character is spoken.
        public float TimeOfChar(int index)
        {
            foreach (var s in syllables)
                if (s.charIndex >= index) return s.start;
            return SpeechEnd;
        }

        /// Time at which a word or phrase (case-insensitive) is spoken, or -1 when it is not in the line.
        public float TimeOf(string fragment)
        {
            if (string.IsNullOrEmpty(fragment)) return -1f;
            int index = Text.IndexOf(fragment, System.StringComparison.OrdinalIgnoreCase);
            return index < 0 ? -1f : TimeOfChar(index);
        }
    }

    /// Plays a SpeechTrack (optionally locked to a voice clip) and smooths it into mouth controls.
    public sealed class SpeechMouth
    {
        SpeechTrack track;
        float startTime;
        AudioSource voice;
        readonly float[] samples = new float[256];
        float envelope, openVel, roundVel, wideVel;

        public float Open { get; private set; }
        public float Round { get; private set; }
        public float Wide { get; private set; }
        public float Accent { get; private set; }
        public bool Active { get; private set; }
        public SpeechTrack Track => track;
        public float Elapsed => Active ? Time.time - startTime : 0f;
        /// True while syllables are being spoken (false in pauses and after the words end).
        public bool Voiced { get; private set; }

        public void Begin(string text, float shownFor, AudioSource voiceSource = null)
        {
            track = SpeechTrack.FromText(text, shownFor);
            startTime = Time.time;
            voice = voiceSource;
            envelope = 0f;
            Active = true;
        }

        public void Stop()
        {
            Active = false;
            Voiced = false;
            track = null;
            voice = null;
        }

        public void Tick(float dt)
        {
            float open = 0f, round = 0f, wide = 0f, accent = 0f;
            Voiced = false;
            if (Active && track != null)
            {
                float t = Time.time - startTime;
                var m = track.Evaluate(t);
                open = m.open;
                round = m.round;
                wide = m.wide;
                accent = m.accent;
                Voiced = m.voiced;
                bool audible = voice && voice.isPlaying;
                if (audible)
                {   // a recorded voice drives the opening; the text still shapes the lips
                    voice.GetOutputData(samples, 0);
                    float sum = 0f;
                    for (int i = 0; i < samples.Length; i++) sum += samples[i] * samples[i];
                    float rms = Mathf.Sqrt(sum / samples.Length);
                    envelope = Mathf.Lerp(envelope, Mathf.Clamp01((rms - .01f) * 7f), 1f - Mathf.Exp(-dt * 30f));
                    open = envelope;
                    Voiced = envelope > .08f;
                }
                if (t >= track.Duration && !audible) Stop();
            }

            Open = Mathf.SmoothDamp(Open, open, ref openVel, open > Open ? .035f : .055f, Mathf.Infinity, dt);
            Round = Mathf.SmoothDamp(Round, round, ref roundVel, .07f, Mathf.Infinity, dt);
            Wide = Mathf.SmoothDamp(Wide, wide, ref wideVel, .07f, Mathf.Infinity, dt);
            Accent = Mathf.MoveTowards(Accent, accent, dt * 6f);
            if (!Active)
            {   // settle exactly on the neutral face
                if (Open < .002f) { Open = 0f; openVel = 0f; }
                if (Round < .002f) { Round = 0f; roundVel = 0f; }
                if (Wide < .002f) { Wide = 0f; wideVel = 0f; }
            }
        }

        public void ResetFace()
        {
            Stop();
            Open = Round = Wide = Accent = 0f;
            openVel = roundVel = wideVel = 0f;
        }
    }
}

using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CarXCase.UI
{
    /// Recorded dialogue (Resources/Voice). A clip is found by the start of the subtitle it voices,
    /// or by a clue's title for Alexey's inspection comments. Subtitles.Draw starts the clip the first
    /// frame a voiced line appears; Alexey's own comments are voiced through HeroFace instead.
    public static class VoiceOver
    {
        const string Bar = "Voice/Бар Стерлядь/", Dmitry = Bar + "разговор с Дмитрием/", Witness = Bar + "разговор со свидетельницей/";
        const string Flat = "Voice/квартира отца/", Pawn = "Voice/ломбард/", Precinct = "Voice/полицейский участок/";

        /// Clip → the beginning of the line it voices (as it is written in the game).
        static readonly (string clip, string line)[] Lines =
        {
            // Bar «Стерлядь»: Dmitry
            (Dmitry + "начало", "Хватит юлить, Дим"),
            (Dmitry + "раунд1/И что с того_", "И что с того?"),
            (Dmitry + "раунд1/Это же наш отец", "Это же наш отец."),
            (Dmitry + "раунд1/Чёрт с тобой, Лёш", "Чёрт с тобой, Лёш"),
            (Dmitry + "раунд1/Если с отцом беда", "Если с отцом беда"),
            (Dmitry + "раунд1/Тише ты", "Тише ты."),
            (Dmitry + "раунд1/Ты сын ему или кто_", "Ты сын ему или кто?"),
            (Dmitry + "раунд2/Он правда заходил", "Он правда заходил."),
            (Dmitry + "раунд2/А если его сейчас где-то прессуют", "А если его сейчас где-то прессуют"),
            (Dmitry + "раунд2/Думаешь, мне плевать", "Думаешь, мне плевать"),
            (Dmitry + "раунд2/У него погром", "У него погром"),
            (Dmitry + "раунд2/Значит, всё-таки пришли", "Значит, всё-таки пришли"),
            (Dmitry + "раунд2/Вчера видели твою машину", "Вчера видели твою машину"),
            (Dmitry + "раунд2/Моя тачка весь вечер", "Моя тачка весь вечер"),
            (Dmitry + "раунд3/На Брайтоне теперь другие правила", "На Брайтоне теперь другие правила"),
            (Dmitry + "раунд3/В девяносто пятом", "В девяносто пятом"),
            (Dmitry + "раунд3/Никто не узнает", "Никто не узнает"),
            (Dmitry + "раунд3/Либо говоришь, либо оформляю", "Либо говоришь, либо оформляю"),
            (Dmitry + "исходы разговора/успех", "У отца дома меня не было"),
            // Bar: the girl at the counter
            // Father's flat
            (Flat + "вход в квартиру", "Отец? Фёдор Павлович, ты дома"),
            (Flat + "осмотр замка", "Замок цел, следов взлома нет"),
            (Flat + "зеркало", "Выгляжу уставшим"),
            (Flat + "попытка выйти", "Ещё рано уходить"),
            (Flat + "автоответчик1", "21:40 · Фёдор Павлович, передай Дмитрию"),
            (Flat + "автоответчик2", "Контейнер на двенадцатом пирсе"),
            (Flat + "автоответчик3", "Если до утра пятницы"),
            // Pawnshop: Boris
            (Pawn + "раунд1/Лёша_ Господи", "Лёша? Господи"),
            (Pawn + "раунд1/Напомнить о прошлом", "Вы на отца двадцать лет работаете"),
            (Pawn + "раунд1/Поговорить спокойно", "Я без шума, дядь Борь"),
            (Pawn + "раунд1/Пригрозить проверкой", "Через лавку левый товар прошёл"),
            (Pawn + "раунд2/Налоги и аренда задушили", "Налоги и аренда задушили"),
            (Pawn + "раунд2/Молчать опаснее", "Они уже были здесь и от вас не отстанут"),
            (Pawn + "раунд2/Предъявить улики", "У него дома всё перевёрнуто"),
            (Pawn + "раунд2/Обвинить Бориса", "Отец влип, а вы тут сидите целый"),
            (Pawn + "исходы разговора/успех", "Позавчера они ворвались перед моей сменой"),
            // Precinct
            (Precinct + "выводы у доски улик/Бар Дмитрия", "Спички из «Стерляди», и в записи назвали Дмитрия"),
            (Precinct + "выводы у доски улик/Ломбард отца", "Часы и телевизор оставили"),
            (Precinct + "выводы у доски улик/Отец мог знать гостя", "Замок цел. Возможно, отец впустил"),
        };

        /// One recording voicing several moments: (clip, line start, from, to) as fractions of the clip.
        static readonly (string clip, string line, float from, float to)[] Parts =
        {
            (Dmitry + "исходы разговора/провал", "Пошёл ты к чёрту, Лёша", 0f, 8.21f / 14.446f),
            (Dmitry + "исходы разговора/провал", "Она слышала скандал", 8.21f / 14.446f, 1f),
            (Dmitry + "исходы разговора/частичный результат", "Я не был у него, клянусь", 0f, 7.63f / 21.394f),
            (Dmitry + "исходы разговора/частичный результат", "Вчера сюда приходили двое", 7.63f / 21.394f, 16.95f / 21.394f),
            (Dmitry + "исходы разговора/частичный результат", "Девушка в конце стойки видела", 16.95f / 21.394f, 1f),
            (Witness + "начало разговора", "Добрый вечер. Дмитрий говорит", 0f, 8.4f / 12.513f),
            (Witness + "начало разговора", "Выпьешь со мной", 8.4f / 12.513f, 1f),
            (Witness + "выпить с девушкой", "Одну. Но сначала", 0f, 3.34f / 21.081f),
            (Witness + "выпить с девушкой", "Месяц назад они", 3.34f / 21.081f, 11.05f / 21.081f),
            (Witness + "выпить с девушкой", "Найдёшь их в Шипсхед-Бей", 11.05f / 21.081f, 15.45f / 21.081f),
            (Witness + "выпить с девушкой", "Шипсхед-Бей, четвёртый ангар. Спасибо", 15.45f / 21.081f, 1f),
            (Witness + "отказаться", "Я на службе", 0f, 3.66f / 15.491f),
            (Witness + "отказаться", "Как хочешь. Слышала", 3.66f / 15.491f, 12.19f / 15.491f),
            (Witness + "отказаться", "Четвёртый ангар. Этого хватит", 12.19f / 15.491f, 1f),
            (Precinct + "разговор с Маркусом", "Лёш? Ты же недавно смену сдал", 0f, .33f),
        };

        /// Clue title → Alexey's comment when he looks at it.
        static readonly (string clip, string title)[] Titles =
        {
            (Bar + "комментарии алексея при осмотре/Записи на стойке", "Записи на стойке"),
            (Bar + "комментарии алексея при осмотре/Записка Дмитрия у кассы", "Записка Дмитрия у кассы"),
            (Bar + "комментарии алексея при осмотре/Меню «Стерляди»", "Меню «Стерляди»"),
            (Bar + "комментарии алексея при осмотре/Музыкальный автомат", "Музыкальный автомат"),
            (Bar + "комментарии алексея при осмотре/Свидетельница из «Стерляди»", "Свидетельница из «Стерляди»"),
            (Flat + "Дверь открыли изнутри", "Дверь открыли изнутри"),
            (Flat + "Ценности не тронуты", "Ценности не тронуты"),
            (Flat + "Пластинка отца", "Пластинка отца"),
            (Flat + "Коробок из бара «Стерлядь»", "Коробок из бара «Стерлядь»"),
            (Flat + "Запись с автоответчика", "Запись с автоответчика"),
            (Precinct + "Комментарии Алексея при осмотре/Карта Брайтона", "Карта Брайтона"),
            (Precinct + "Комментарии Алексея при осмотре/Рапорты в лотке", "Рапорты в лотке"),
        };

        static readonly Dictionary<string, AudioClip> loaded = new();
        static readonly HashSet<string> handled = new();
        static string lastSubtitle;
        static AudioSource source;

        public static AudioClip ForComment(string line, out float duration)
        {
            var clip = Find(line, out float from, out float to);
            duration = clip ? clip.length * (to - from) : 0f;
            // HeroFace plays whole clips. Subtitles start and stop excerpts at their recorded boundaries.
            return from == 0f && to == 1f ? clip : null;
        }

        /// The recording for a line and the part of it (fractions) that belongs to this line.
        static AudioClip Find(string line, out float from, out float to)
        {
            from = 0f; to = 1f;
            string text = Normalize(StripSpeaker(line));
            if (text.Length == 0) return null;
            string best = null; int bestLength = 0;
            foreach (var (clip, start) in Lines)
            {
                string key = Normalize(start);
                if (key.Length > bestLength && text.StartsWith(key)) { best = clip; bestLength = key.Length; from = 0f; to = 1f; }
            }
            foreach (var (clip, start, a, b) in Parts)
            {
                string key = Normalize(start);
                if (key.Length > bestLength && text.StartsWith(key)) { best = clip; bestLength = key.Length; from = a; to = b; }
            }
            return Load(best);
        }

        public static AudioClip ForTitle(string title)
        {
            string key = Normalize(title);
            foreach (var (clip, name) in Titles)
                if (Normalize(name) == key) return Load(clip);
            return null;
        }

        /// Length of the recording for this line, 0 when it has none.
        public static float Length(string line)
        {
            var clip = Find(line, out float from, out float to);
            return clip ? clip.length * (to - from) : 0f;
        }

        /// How long to hold each of a run of subtitles so they follow the recordings: a recording that starts
        /// on one line also covers the following lines that have none of their own, shared out by text length.
        /// -1 means "no recording, keep your usual timing".
        public static float[] Spread(IList<string> lines)
        {
            var hold = new float[lines.Count];
            for (int i = 0; i < hold.Length; i++) hold[i] = -1f;
            for (int i = 0; i < lines.Count; i++)
            {
                float voice = Length(lines[i]);
                if (voice <= 0f) continue;
                int end = i + 1;
                while (end < lines.Count && Length(lines[end]) <= 0f) end++;
                float chars = 0f;
                for (int j = i; j < end; j++) chars += Mathf.Max(1, (lines[j] ?? "").Length);
                for (int j = i; j < end; j++) hold[j] = voice * Mathf.Max(1, (lines[j] ?? "").Length) / chars + .15f;
                i = end - 1;
            }
            return hold;
        }

        /// The subtitle is voiced elsewhere (HeroFace plays Alexey's comment clip).
        public static void MarkHandled(string shownSubtitle)
        {
            if (!string.IsNullOrEmpty(shownSubtitle)) handled.Add(shownSubtitle);
            lastSubtitle = shownSubtitle;
        }

        /// Called by Subtitles.Draw every frame: starts the recording when a voiced line first appears.
        public static void OnSubtitle(string line)
        {
            if (string.IsNullOrEmpty(line) || line == lastSubtitle) return;
            lastSubtitle = line;
            if (handled.Remove(line)) return;
            var clip = Find(line, out float from, out float to);
            if (!clip) return;
            if (!source)
            {
                var go = new GameObject("Dialogue voice");
                Object.DontDestroyOnLoad(go);
                source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
            }
            source.Stop();
            source.clip = clip;
            source.volume = NoirMenu.GameSettings.SfxGain;
            source.time = clip.length * from;
            source.Play();
            source.SetScheduledEndTime(AudioSettings.dspTime + clip.length * (to - from));
        }

        static AudioClip Load(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (!loaded.TryGetValue(path, out var clip))
            {
                clip = Resources.Load<AudioClip>(path);
                // Unity can index a decomposed Cyrillic folder name on macOS.
                if (!clip && path.StartsWith(Witness, System.StringComparison.Ordinal))
                {
                    var directory = Witness.Normalize(NormalizationForm.FormD);
                    var name = path.Substring(Witness.Length);
                    clip = Resources.Load<AudioClip>(directory + name);
                    if (!clip) clip = Resources.Load<AudioClip>(directory + name.Normalize(NormalizationForm.FormD));
                }
                if (!clip) Debug.LogWarning("Voice clip missing: " + path);
                loaded[path] = clip;
            }
            return clip;
        }

        static string StripSpeaker(string line)
        {
            if (line == null) return "";
            int colon = line.IndexOf(": ", System.StringComparison.Ordinal);
            return colon > 0 && colon < 40 ? line.Substring(colon + 2) : line;
        }

        /// Lower case, ё→е, letters and digits only, single spaces: tolerant to punctuation and quotes.
        static string Normalize(string s)
        {
            var sb = new StringBuilder(s.Length);
            bool space = false;
            foreach (char raw in s.ToLowerInvariant())
            {
                char c = raw == 'ё' ? 'е' : raw;
                if (char.IsLetterOrDigit(c)) { sb.Append(c); space = false; }
                else if (!space && sb.Length > 0) { sb.Append(' '); space = true; }
            }
            return sb.ToString().TrimEnd();
        }
    }
}

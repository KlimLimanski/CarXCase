using System.Collections;
using DetectiveGame.Core;
using UnityEngine;

public sealed partial class HangarFinale
{
    const string Mistake = "hangar_last_mistake", Sins = "hangar_fathers_sins", Silence = "hangar_ten_years";
    string SpecialTitle => ClueJournal.Has(Sins) ? "Грехи отцов" : ClueJournal.Has(Silence) ? "Десять лет молчания" :
        ClueJournal.Has(Mistake) ? "Последняя ошибка" : null;
    string SpecialText => ClueJournal.Has(Sins) ? "Фёдор погиб от выстрела Кости." :
        ClueJournal.Has(Silence) ? "Иногда прощение не отменяет наказания. Но даёт возможность начать заново." :
        ClueJournal.Has(Mistake) ? "Он хотел вернуть семью. Но стал причиной её окончательного распада." : null;

    static readonly string[,] Replies = {
        {"Я хочу тебя выслушать.", "Расскажи про долг.", "Ты ответишь за похищение.", "Посмотри, что ты натворил!"},
        {"Ты всё ещё мой брат.", "Давай по порядку.", "За это придётся ответить.", "Хватит прятаться за отца!"}
    };
    float endingFade;
    Transform fallenVisual;
    Quaternion standingRotation;
    int round, answer, trust, pressure;
    void Pick(int value)
    {
        if (stage != Stage.DialogueChoice) return;
        answer = value;
        stage = Stage.Conversation;
        if (value == 0) { trust += 20; pressure -= 10; }
        if (value >= 2) { trust -= 20; pressure += value == 2 ? 15 : 30; }
    }
    static string SelectOutcome(bool lateArrival,int trust,int pressure) =>
        !lateArrival && pressure >= 70 ? Sins : trust >= 85 && pressure <= 20 ? Silence :
        trust <= 15 && pressure >= 45 ? Mistake : null;

    IEnumerator Line(string who,string words) => Say(who,words,Mathf.Clamp(.65f+words.Length/16f,1.1f,9f));
    IEnumerator Negotiate()
    {
        trust = 50; pressure = 20;
        for (round=0; round<2; round++)
        {
            answer = -1;
            stage = Stage.DialogueChoice;
            while (answer < 0) yield return null;
            yield return Line("Алексей", Replies[round,answer]);
            if (round == 0) yield return Line("Костя", answer == 0 ? "И что ты хочешь услышать, Лёш?" :
                answer == 1 ? "Ты всё ещё про деньги?" : "Приехал читать мне приговор?");
        }
        string outcome = SelectOutcome(delayed,trust,pressure);
        if (outcome == null)
        {
            yield return Line("Костя", "Ты ведь уже всё решил, да? Виновен. Арестовать. Дело закрыто.");
            yield return Line("Алексей", "Я приехал за отцом.");
            if (!delayed) yield return Line("Костя", "А уедешь с двумя. Одного в больницу, другого в тюрьму.");
            if (delayed) yield return Line("Костя", "Опоздал ты за ним, Лёш. Теперь за мной приехал?");
            yield return Line("Алексей", "Ты сам к этому привёл.");
            yield return Line("Костя", "Может быть. Только не надо делать вид, что ты здесь самый чистый.");
            yield return Line("Алексей", "Что ты хочешь от меня услышать?");
            yield return Line("Костя", "Не знаю. Наверное, уже ничего.");
            yield return Line("Костя", "Лёш, ты хоть понимаешь, что будет, если меня заберут? Я слишком много знаю. Меня либо убьют до суда, либо посадят до конца жизни.");
            yield return Line("Алексей", "Это не повод продолжать.");
            yield return Line("Костя", "Для тебя всё просто. Ты всегда умел выбирать правильную сторону.");
            yield return Line("Алексей", "А ты всегда мог выбрать другую.");
            yield return Line("Костя", "Вот и выбирай теперь сам.");
            BeginDecision();
            yield break;
        }
        if (outcome == Sins) yield return FathersSins();
        else if (outcome == Silence) yield return Surrender();
        else yield return LastMistake();
        pulse.Stop();
        yield return Animate(.7f, t => endingFade = t);
        ClueJournal.AddConclusion(outcome);
        ShowReport();
    }

    void RestoreOutcome()
    {
        if (SpecialTitle == null) { EndPose(ClueJournal.Has(Shot)); return; }
        if (ClueJournal.Has(Sins)) { rescue.SetActive(false); late.SetActive(true); }
        if (ClueJournal.Has(Silence)) { staging.OfferWrists(1); staging.SetCuff(true); if (!delayed) { staging.Bindings(false); staging.DropRope(1); } }
    }
    IEnumerator LastMistake()
    {
        yield return Line("Костя", "Знаешь, что самое смешное? Я ведь думал, ты хотя бы попытаешься понять.");
        yield return Line("Алексей", "Я пытался. Но ты похитил собственного отца. Ты преступник, Кость.");
        yield return Line("Костя", "Вот теперь всё встало на свои места. Не брат приехал. Мент.");
        yield return Line("Алексей", "Руку от кармана, Кость.");
        yield return Line("Костя", "А ты меня застрелишь, если не положу?");
        yield return Line("Алексей", "Если придётся.");
        yield return Line("Костя", "Значит, придётся.");
        if (!delayed) yield return Line("Фёдор Павлович", "Костя, сынок, не надо...");
        if (!delayed) yield return Line("Костя", "Замолчи!");
        yield return Line("Костя", "Я десять лет жил с мыслью, что у меня нет семьи. И, знаешь, было легче.");
        yield return Line("Алексей", "Костя, последняя возможность. Руки вверх.");
        yield return Line("Костя", "Прощай, братишка.");
        stage = Stage.Aftermath;
        yield return Animate(.24f, staging.Reach);
        LocationAudio.Effect("Hangar shot", .62f);
        yield return new WaitForSeconds(.13f);
        LocationAudio.Effect("Hangar shot", .5f);
        var visual = player.transform.Find("Character_Visual");
        if (visual)
        {
            fallenVisual = visual;
            standingRotation = visual.localRotation;
            var fallen = Quaternion.AngleAxis(-82f, player.transform.right) * visual.rotation;
            var start = visual.rotation;
            yield return Animate(.85f, t => visual.rotation = Quaternion.Slerp(start, fallen, t*t));
            LocationAudio.Effect("Body fall", .75f);
        }
        LocationAudio.Effect("Police siren", .2f);
        Frame(new Vector3(-.8f,.7f,5.2f),new Vector3(1.1f,1.4f,7.1f));
        yield return Line("Алексей", "Ты... всё-таки...");
        yield return Line("Костя", "Лёш? Эй. Слышишь меня?");
        yield return Line("Алексей", "Я хотел... тебя вернуть...");
        if (!delayed) yield return Line("Фёдор Павлович", "Что ты наделал, Костя?..");
        yield return Line("Костя", "Я... я не хотел...");
    }

    IEnumerator FathersSins()
    {
        yield return Line("Алексей", "Ты всё это сделал из-за старой обиды? Десять лет прошло, Кость. Ты хоть понимаешь, насколько это жалко?");
        yield return Line("Костя", "Повтори.");
        yield return Line("Алексей", "Ты услышал.");
        yield return Line("Костя", "Ты не знаешь, как я жил.");
        yield return Line("Алексей", "Зато я вижу, кем ты стал.");
        yield return Line("Костя", "Он меня вышвырнул! Я три ночи спал на вокзале! Мне жрать было нечего!");
        yield return Line("Алексей", "И теперь ты решил отыграться на старике?");
        yield return Line("Костя", "Я хотел, чтобы он признал, что был неправ!");
        yield return Line("Костя", "Скажи ему. Скажи, что ты мне тогда сказал.");
        yield return Line("Фёдор Павлович", "Костя, сейчас не время...");
        yield return Line("Костя", "А когда будет время?! Ещё через десять лет?!");
        yield return Line("Фёдор Павлович", "Я хотел, чтобы ты человеком вырос...");
        yield return Line("Костя", "ЧЕЛОВЕКОМ?!");
        stage = Stage.Aftermath;
        yield return Animate(.3f, staging.Reach);
        kostya.LookAt(new Vector3(rescue.transform.position.x,kostya.position.y,rescue.transform.position.z));
        Frame(new Vector3(-1.16f,1.14f,5.84f),new Vector3(-.08f,.90f,7.07f));
        yield return Line("Алексей", "Костя! Убери оружие!");
        yield return Line("Костя", "Он даже сейчас не может сказать, что был неправ!");
        yield return Line("Фёдор Павлович", "Сынок, я...");
        yield return Line("Костя", "Не называй меня так!");
        yield return Line("Алексей", "Посмотри на меня. На меня, Кость!");
        yield return Line("Костя", "Ты хоть раз пожалел, что выгнал меня?!");
        yield return Line("Фёдор Павлович", "Я... я думал, так будет лучше...");
        yield return Line("Костя", "Для кого лучше?!");
        LocationAudio.Effect("Hangar shot",.6f);
        pulse.Stop();
        rescue.SetActive(false);
        late.SetActive(true);
        yield return Line("Алексей", "НЕТ!");
        yield return Line("Костя", "Я...");
        yield return Line("Алексей", "Ты убил его.");
        yield return Line("Костя", "Нет...");
        yield return Line("Алексей", "Ты убил нашего отца, Костя.");
        yield return Line("Костя", "Я всю жизнь хотел, чтобы он пожалел о том, что сделал.");
        yield return Line("Костя", "А теперь он уже никогда ничего не скажет.");
    }

    IEnumerator Surrender()
    {
        yield return Line("Костя", "Знаешь, Лёш... Я ведь много раз представлял, как вернусь домой.");
        yield return Line("Алексей", "И что было дальше?");
        yield return Line("Костя", "По-разному. Иногда отец просил прощения. Иногда я сам ему всё высказывал. А иногда просто заходил, садился за стол, и мы делали вид, что ничего не случилось.");
        yield return Line("Алексей", "Почему не вернулся?");
        yield return Line("Костя", "Гордость. Страшная штука. Сначала думаешь, что докажешь всем, чего стоишь. А потом проходит десять лет, и ты уже не помнишь, что вообще пытался доказать.");
        yield return Line("Алексей", "Ещё не поздно всё изменить.");
        yield return Line("Костя", "Лёш, ты посмотри вокруг. Я похитил собственного отца. Подставил его на пятнадцать тысяч. У меня люди в порту работают, которым человеческая жизнь ничего не стоит. Что тут менять?");
        yield return Line("Алексей", "То, что будет дальше.");
        yield return Line("Костя", "А дальше что? Суд? Тюрьма?");
        yield return Line("Алексей", "Да.");
        yield return Line("Костя", "Хоть не врёшь.");
        yield return Line("Алексей", "Я не обещаю, что всё будет хорошо. Но могу обещать, что ты не останешься один.");
        yield return Line("Костя", "Ты правда будешь приходить?");
        yield return Line("Алексей", "Буду.");
        yield return Line("Костя", "Даже после всего?");
        yield return Line("Алексей", "Ты мой брат, Кость. Это никуда не делось.");
        yield return Line("Костя", "Я ведь тогда не деньги хотел получить. И не ломбард отобрать.");
        yield return Line("Алексей", "А что?");
        yield return Line("Костя", "Чтобы он меня увидел. Чтобы посмотрел и понял, что я не пропал. Что я чего-то стою.");
        if (!delayed)
        {
            yield return Line("Фёдор Павлович", "Костя...");
            yield return Line("Фёдор Павлович", "Я знал, что ты жив. Дмитрий говорил, что тебя видели на Брайтоне. Я... хотел найти тебя.");
            yield return Line("Костя", "Почему не нашёл?");
            yield return Line("Фёдор Павлович", "Боялся, что ты дверь не откроешь.");
            yield return Line("Костя", "А я боялся, что ты меня опять выгонишь.");
        }
        yield return Line("Алексей", "Кость. Посмотри на меня. Всё. Хватит. Руки так, чтобы я их видел.");
        yield return Line("Костя", "И ты меня заберёшь?");
        yield return Line("Алексей", "Да.");
        yield return Line("Костя", "Наручники наденешь?");
        yield return Line("Алексей", "Придётся.");
        stage = Stage.Aftermath;
        yield return Animate(.85f, staging.OfferWrists);
        staging.SetCuff(true);
        LocationAudio.Effect("Handcuffs",.6f);
        LocationAudio.Effect("Lock key press",.4f);
        yield return Line("Костя", "Тогда давай, детектив. Делай свою работу.");
        yield return Line("Костя", "Лёш...");
        yield return Line("Алексей", "Что?");
        yield return Line("Костя", "Ты только... Димке скажи, что я не хотел, чтобы так вышло.");
        yield return Line("Алексей", "Сам скажешь.");
        yield return Line("Костя", "Думаешь, захочет меня видеть?");
        yield return Line("Алексей", "Не знаю. Но у тебя будет время спросить.");
        LocationAudio.Effect("Police siren", .32f);   // swells in as the patrol car pulls up
        if (!delayed)
        {
            staging.Bindings(false);
            LocationAudio.Effect("Rope", .55f);
            yield return Animate(.65f,staging.DropRope);
            fatherReleased = true;
            yield return Line("Фёдор Павлович", "Костя... Прости меня, сынок.");
            yield return Line("Костя", "Я столько лет хотел это услышать...");
        }
        var lights = new GameObject("Patrol lights at loading door");
        lights.transform.SetParent(transform, false);
        lights.transform.localPosition = new Vector3(1.2f, 1.5f, .7f);
        var spill = lights.AddComponent<Light>();
        spill.range = 11f;
        LocationAudio.Effect("Approaching traffic", .25f);
        try
        {
            yield return Animate(2.2f, t =>
            {
                float phase = t * 3.5f;
                spill.color = Mathf.Repeat(phase, 1f) < .5f ? new Color(.18f,.38f,1f) : new Color(1f,.12f,.07f);
                spill.intensity = 8f * Mathf.Pow(Mathf.Sin(phase * Mathf.PI * 2f), 2);
            });
        }
        finally { Destroy(lights); }
    }

    IEnumerator StandardLastWords(bool shot)
    {
        if (shot)
        {
            yield return Line("Костя", "Всё-таки... хороший стрелок...");
            yield return Line("Алексей", "Держись, Кость. Слышишь?");
            yield return Line("Костя", "Поздно, Лёш. Для всего поздно.");
        }
        else
        {
            yield return Line("Костя", "Ну что, доволен?");
            yield return Line("Алексей", "Нет.");
            yield return Line("Костя", "Тогда почему руки трясутся?");
            yield return Line("Алексей", "Потому что я только что арестовал собственного брата.");
            yield return Line("Костя", "А я думал, ты уже забыл, что мы братья.");
        }
    }
}

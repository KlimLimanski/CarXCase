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

    IEnumerator Line(string who,string words,string clip = null) => Say(who,words,Mathf.Clamp(.65f+words.Length/16f,1.1f,9f),clip);
    IEnumerator Negotiate()
    {
        trust = 50; pressure = 20;
        for (round=0; round<2; round++)
        {
            answer = -1;
            stage = Stage.DialogueChoice;
            while (answer < 0) yield return null;
            yield return Line("Алексей", Replies[round,answer]);
            if (round == 0)
            {
                string reply = answer == 0 ? "И что ты хочешь услышать, Лёш?" :
                    answer == 1 ? "Ты всё ещё про деньги?" : "Приехал читать мне приговор?";
                yield return Say("Костя", reply, Mathf.Clamp(.65f + reply.Length / 16f, 1.1f, 9f),
                    "round1_" + (answer + 1) + "_2");
            }
        }
        string outcome = SelectOutcome(delayed,trust,pressure);
        if (outcome == null)
        {
            yield return Line("Костя", "Ты ведь уже всё решил, да? Виновен. Арестовать. Дело закрыто.", delayed ? "before_late_1" : "before_early_1");
            yield return Line("Алексей", "Я приехал за отцом.", delayed ? "before_late_2" : "before_early_2");
            if (!delayed) yield return Line("Костя", "А уедешь с двумя. Одного в больницу, другого в тюрьму.", "before_early_3");
            if (delayed) yield return Line("Костя", "Опоздал ты за ним, Лёш. Теперь за мной приехал?", "before_late_3");
            yield return Line("Алексей", "Ты сам к этому привёл.", delayed ? "before_late_4" : "before_early_4");
            yield return Line("Костя", "Может быть. Только не надо делать вид, что ты здесь самый чистый.", delayed ? "before_late_5" : "before_early_5");
            yield return Line("Алексей", "Что ты хочешь от меня услышать?", delayed ? "before_late_6" : "before_early_6");
            yield return Line("Костя", "Не знаю. Наверное, уже ничего.", delayed ? "before_late_7" : "before_early_7");
            yield return Line("Костя", "Лёш, ты хоть понимаешь, что будет, если меня заберут? Я слишком много знаю. Меня либо убьют до суда, либо посадят до конца жизни.", delayed ? "before_late_8" : "before_early_8");
            yield return Line("Алексей", "Это не повод продолжать.", delayed ? "before_late_9" : "before_early_9");
            yield return Line("Костя", "Для тебя всё просто. Ты всегда умел выбирать правильную сторону.", delayed ? "before_late_10" : "before_early_10");
            yield return Line("Алексей", "А ты всегда мог выбрать другую.", delayed ? "before_late_11" : "before_early_11");
            yield return Line("Костя", "Вот и выбирай теперь сам.", delayed ? "before_late_12" : "before_early_12");
            BeginDecision();
            yield break;
        }
        if (outcome == Sins) yield return FathersSins();
        else if (outcome == Silence) yield return Surrender();
        else yield return LastMistake();
        pulse.Stop();
        ClueJournal.AddConclusion(outcome);
        yield return FadeOutEnding();
    }

    void RestoreOutcome()
    {
        if (SpecialTitle == null) { EndPose(ClueJournal.Has(Shot)); return; }
        if (ClueJournal.Has(Sins)) { rescue.SetActive(false); late.SetActive(true); }
        if (ClueJournal.Has(Silence)) { staging.OfferWrists(1); staging.SetCuff(true); if (!delayed) { staging.Bindings(false); staging.DropRope(1); } }
    }
    IEnumerator LastMistake()
    {
        yield return Line("Костя", "Знаешь, что самое смешное? Я ведь думал, ты хотя бы попытаешься понять.", "mistake_1");
        yield return Line("Алексей", "Я пытался. Но ты похитил собственного отца. Ты преступник, Кость.", "mistake_2");
        yield return Line("Костя", "Вот теперь всё встало на свои места. Не брат приехал. Мент.", "mistake_3");
        props.Aim(1f, kostya);
        yield return Line("Алексей", "Руку от кармана, Кость.", "mistake_4");
        yield return Line("Костя", "А ты меня застрелишь, если не положу?", "mistake_5");
        yield return Line("Алексей", "Если придётся.", "mistake_6");
        yield return Line("Костя", "Значит, придётся.", "mistake_7");
        if (!delayed) yield return Line("Фёдор Павлович", "Костя, сынок, не надо...", "mistake_8");
        if (!delayed) yield return Line("Костя", "Замолчи!", "mistake_9");
        yield return Line("Костя", "Я десять лет жил с мыслью, что у меня нет семьи. И, знаешь, было легче.", "mistake_10");
        yield return Line("Алексей", "Костя, последняя возможность. Руки вверх.", "mistake_11");
        yield return Line("Костя", "Прощай, братишка.", "mistake_12");
        stage = Stage.Aftermath;
        yield return Animate(staging.DrawDuration * .9f, t =>
        {
            staging.Draw(t, player.transform);
            if (t >= HangarStaging.RevealMoment) props.ArmKostya(true, player.transform);
        });
        endingFade = 1f;
        LocationAudio.Effect("Hangar shot", .62f);
        yield return new WaitForSeconds(.13f);
        LocationAudio.Effect("Hangar shot", .5f);
        props.Collapse(1f);
        props.Apply();
        props.ShowBlood(true);
        Frame(new Vector3(-.8f,.7f,5.2f),new Vector3(1.1f,1.4f,7.1f));
        yield return RevealAfterDeath(.62f, .75f);
        LocationAudio.Effect("Police siren", .2f);
        yield return Line("Алексей", "Ты... всё-таки...", "mistake_13");
        yield return Line("Костя", "Лёш? Эй. Слышишь меня?", "mistake_14");
        yield return Line("Алексей", "Я хотел... тебя вернуть...", "mistake_15");
        if (!delayed) yield return Line("Фёдор Павлович", "Что ты наделал, Костя?..", "mistake_16");
        yield return Line("Костя", "Я... я не хотел...", "mistake_17");
    }

    IEnumerator FathersSins()
    {
        yield return Line("Алексей", "Ты всё это сделал из-за старой обиды? Десять лет прошло, Кость. Ты хоть понимаешь, насколько это жалко?", "sins_1");
        yield return Line("Костя", "Повтори.", "sins_2");
        yield return Line("Алексей", "Ты услышал.", "sins_3");
        yield return Line("Костя", "Ты не знаешь, как я жил.", "sins_4");
        yield return Line("Алексей", "Зато я вижу, кем ты стал.", "sins_5");
        yield return Line("Костя", "Он меня вышвырнул! Я три ночи спал на вокзале! Мне жрать было нечего!", "sins_6");
        yield return Line("Алексей", "И теперь ты решил отыграться на старике?", "sins_7");
        yield return Line("Костя", "Я хотел, чтобы он признал, что был неправ!", "sins_8");
        yield return Line("Костя", "Скажи ему. Скажи, что ты мне тогда сказал.", "sins_9");
        yield return Line("Фёдор Павлович", "Костя, сейчас не время...", "sins_10");
        yield return Line("Костя", "А когда будет время?! Ещё через десять лет?!", "sins_11");
        yield return Line("Фёдор Павлович", "Я хотел, чтобы ты человеком вырос...", "sins_12");
        yield return Line("Костя", "ЧЕЛОВЕКОМ?!", "sins_13");
        stage = Stage.Aftermath;
        kostya.LookAt(new Vector3(rescue.transform.position.x,kostya.position.y,rescue.transform.position.z));
        yield return Animate(staging.DrawDuration * .9f, t =>
        {
            staging.Draw(t, rescue.transform, .85f);
            if (t >= HangarStaging.RevealMoment) props.ArmKostya(true, rescue.transform, .85f);
        });
        Frame(new Vector3(-1.16f,1.14f,5.84f),new Vector3(-.08f,.90f,7.07f));
        props.Aim(1f, kostya);
        yield return Line("Алексей", "Костя! Убери оружие!", "sins_14");
        yield return Line("Костя", "Он даже сейчас не может сказать, что был неправ!", "sins_15");
        yield return Line("Фёдор Павлович", "Сынок, я...", "sins_16");
        yield return Line("Костя", "Не называй меня так!", "sins_17");
        yield return Line("Алексей", "Посмотри на меня. На меня, Кость!", "sins_18");
        yield return Line("Костя", "Ты хоть раз пожалел, что выгнал меня?!", "sins_19");
        yield return Line("Фёдор Павлович", "Я... я думал, так будет лучше...", "sins_20");
        yield return Line("Костя", "Для кого лучше?!", "sins_21");
        LocationAudio.Effect("Hangar shot",.6f);
        pulse.Stop();
        // The shot cuts to black. When the picture comes back the father is already hit: his head, still
        // raised towards his son, slowly goes down.
        endingFade = 1f;
        caption = null;
        rescue.SetActive(false);
        late.SetActive(true);
        staging.FatherDeath(1f);
        if (directing) Cut(Cover.CloseFather, 2);
        else Frame(new Vector3(-1.16f,1.14f,5.84f),new Vector3(-.08f,.90f,7.07f));
        yield return RevealAfterDeath(.75f);
        yield return Line("Алексей", "НЕТ!", "sins_22");
        yield return Line("Костя", "Я...", "sins_23");
        yield return Line("Алексей", "Ты убил его.", "sins_24");
        yield return Line("Костя", "Нет...", "sins_25");
        yield return Line("Алексей", "Ты убил нашего отца, Костя.", "sins_26");
        yield return Line("Костя", "Я всю жизнь хотел, чтобы он пожалел о том, что сделал.", "sins_27");
        yield return Line("Костя", "А теперь он уже никогда ничего не скажет.", "sins_28");
    }

    IEnumerator Surrender()
    {
        yield return Line("Костя", "Знаешь, Лёш... Я ведь много раз представлял, как вернусь домой.", delayed ? "sil_late_1" : "sil_early_1");
        yield return Line("Алексей", "И что было дальше?", delayed ? "sil_late_2" : "sil_early_2");
        yield return Line("Костя", "По-разному. Иногда отец просил прощения. Иногда я сам ему всё высказывал. А иногда просто заходил, садился за стол, и мы делали вид, что ничего не случилось.", delayed ? "sil_late_3" : "sil_early_3");
        yield return Line("Алексей", "Почему не вернулся?", delayed ? "sil_late_4" : "sil_early_4");
        yield return Line("Костя", "Гордость. Страшная штука. Сначала думаешь, что докажешь всем, чего стоишь. А потом проходит десять лет, и ты уже не помнишь, что вообще пытался доказать.", delayed ? "sil_late_5" : "sil_early_5");
        yield return Line("Алексей", "Ещё не поздно всё изменить.", delayed ? "sil_late_6" : "sil_early_6");
        yield return Line("Костя", "Лёш, ты посмотри вокруг. Я похитил собственного отца. Подставил его на пятнадцать тысяч. У меня люди в порту работают, которым человеческая жизнь ничего не стоит. Что тут менять?", delayed ? "sil_late_7" : "sil_early_7");
        yield return Line("Алексей", "То, что будет дальше.", delayed ? "sil_late_8" : "sil_early_8");
        yield return Line("Костя", "А дальше что? Суд? Тюрьма?", delayed ? "sil_late_9" : "sil_early_9");
        yield return Line("Алексей", "Да.", delayed ? "sil_late_22" : "sil_early_27");
        yield return Line("Костя", "Хоть не врёшь.", delayed ? "sil_late_11" : "sil_early_11");
        yield return Line("Алексей", "Я не обещаю, что всё будет хорошо. Но могу обещать, что ты не останешься один.", delayed ? "sil_late_12" : "sil_early_12");
        yield return Line("Костя", "Ты правда будешь приходить?", delayed ? "sil_late_13" : "sil_early_13");
        yield return Line("Алексей", "Буду.", delayed ? "sil_late_14" : "sil_early_14");
        yield return Line("Костя", "Даже после всего?", delayed ? "sil_late_15" : "sil_early_15");
        yield return Line("Алексей", "Ты мой брат, Кость. Это никуда не делось.", delayed ? "sil_late_16" : "sil_early_16");
        yield return Line("Костя", "Я ведь тогда не деньги хотел получить. И не ломбард отобрать.", delayed ? "sil_late_17" : "sil_early_17");
        yield return Line("Алексей", "А что?", delayed ? "sil_late_18" : "sil_early_18");
        yield return Line("Костя", "Чтобы он меня увидел. Чтобы посмотрел и понял, что я не пропал. Что я чего-то стою.", delayed ? "sil_late_19" : "sil_early_19");
        if (!delayed)
        {
            yield return Line("Фёдор Павлович", "Костя...", "sil_early_20");
            yield return Line("Фёдор Павлович", "Я знал, что ты жив. Дмитрий говорил, что тебя видели на Брайтоне. Я... хотел найти тебя.", "sil_early_21");
            yield return Line("Костя", "Почему не нашёл?", "sil_early_22");
            yield return Line("Фёдор Павлович", "Боялся, что ты дверь не откроешь.", "sil_early_23");
            yield return Line("Костя", "А я боялся, что ты меня опять выгонишь.", "sil_early_24");
        }
        yield return Line("Алексей", "Кость. Посмотри на меня. Всё. Хватит. Руки так, чтобы я их видел.", delayed ? "sil_late_20" : "sil_early_25");
        yield return Line("Костя", "И ты меня заберёшь?", delayed ? "sil_late_21" : "sil_early_26");
        yield return Line("Алексей", "Да.", delayed ? "sil_late_22" : "sil_early_27");
        yield return Line("Костя", "Наручники наденешь?", delayed ? "sil_late_23" : "sil_early_28");
        yield return Line("Алексей", "Придётся.", delayed ? "sil_late_24" : "sil_early_29");
        stage = Stage.Aftermath;
        props.Aim(0f, kostya);
        yield return Animate(.85f, staging.OfferWrists);
        staging.SetCuff(true);
        LocationAudio.Effect("Handcuffs",.6f);
        LocationAudio.Effect("Lock key press",.4f);
        yield return Line("Костя", "Тогда давай, детектив. Делай свою работу.", delayed ? "sil_late_25" : "sil_early_30");
        yield return Line("Костя", "Лёш...", delayed ? "sil_late_26" : "sil_early_31");
        yield return Line("Алексей", "Что?", delayed ? "sil_late_27" : "sil_early_32");
        yield return Line("Костя", "Ты только... Димке скажи, что я не хотел, чтобы так вышло.", delayed ? "sil_late_28" : "sil_early_33");
        yield return Line("Алексей", "Сам скажешь.", delayed ? "sil_late_29" : "sil_early_34");
        yield return Line("Костя", "Думаешь, захочет меня видеть?", delayed ? "sil_late_30" : "sil_early_35");
        yield return Line("Алексей", "Не знаю. Но у тебя будет время спросить.", delayed ? "sil_late_31" : "sil_early_36");
        LocationAudio.Effect("Police siren", .32f);   // swells in as the patrol car pulls up
        if (!delayed)
        {
            staging.Bindings(false);
            LocationAudio.Effect("Rope", .55f);
            yield return Animate(.65f,staging.DropRope);
            fatherReleased = true;
            yield return Line("Фёдор Павлович", "Костя... Прости меня, сынок.", "sil_early_37");
            yield return Line("Костя", "Я столько лет хотел это услышать...", "sil_early_38");
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
            yield return Line("Костя", "Всё-таки... хороший стрелок...", "shot_1");
            yield return Line("Алексей", "Держись, Кость. Слышишь?", "shot_2");
            yield return Line("Костя", "Поздно, Лёш. Для всего поздно.", "shot_3");
        }
        else
        {
            yield return Line("Костя", "Ну что, доволен?", "arrest_1");
            yield return Line("Алексей", "Нет.", "arrest_2");
            yield return Line("Костя", "Тогда почему руки трясутся?", "arrest_3");
            yield return Line("Алексей", "Потому что я только что арестовал собственного брата.", "arrest_4");
            yield return Line("Костя", "А я думал, ты уже забыл, что мы братья.", "arrest_5");
        }
    }
}

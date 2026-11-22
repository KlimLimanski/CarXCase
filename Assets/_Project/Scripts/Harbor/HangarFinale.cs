using System.Collections;
using CarXCase.UI;
using DetectiveGame.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public sealed class HangarFinale : MonoBehaviour
{
    public GameObject rescue, late;
    public Transform chest, head, kostya;
    public Transform reachingArm;
    public Light pendant, backlight;
    public AudioSource pulse;
    public ParticleSystemRenderer yardRain;
    public bool UnderRoof => stage != Stage.Outside && stage != Stage.Listening;
    public const string LateRoute = "hangar_bar_then_pawnshop";
    const string Shot = "hangar_shot", Arrest = "hangar_arrest";
    const string Overheard = "hangar_guards_departed";
    const float DecisionSeconds = 4f;

    enum Stage { Outside, Listening, Conversation, Approach, Decision, Missed, Aftermath, Report }
    Stage stage;
    OfficerController player;
    ThirdPersonCamera view;
    bool delayed, raised, ownsPlayer, previousCursorVisible;
    CursorLockMode previousLock;
    float pitch = 20, deadline;
    string caption;
    GUIStyle title, body, action;
    readonly Color ink = new Color(.065f,.086f,.086f,.96f);
    readonly Color brass = new Color(.77f,.65f,.43f);
    public bool Finished => ClueJournal.Has(Shot) || ClueJournal.Has(Arrest);
    public bool NeedsListening => !Finished && !ClueJournal.Has(Overheard);
    public string Objective => stage == Stage.Approach ? "Подойдите к Косте" :
        stage != Stage.Outside || Finished ? null : NeedsListening ? "Подслушайте разговор у двери ангара" : "Войдите в ангар";

    void Start()
    {
        SetBranch(ClueJournal.Has(LateRoute));
        if (Finished) EndPose(ClueJournal.Has(Shot));
    }

    public void SetBranch(bool arrivedLate)
    {
        delayed = arrivedLate;
        rescue.SetActive(!delayed);
        late.SetActive(delayed);
        pendant.intensity = delayed ? 4.2f : 7f;
        pendant.color = delayed ? new Color(.94f,.72f,.49f) : new Color(1f,.79f,.52f);
        backlight.intensity = delayed ? 2.4f : 1.15f;
    }

    void TakeControl(OfficerController officer)
    {
        player = officer;
        view = officer.cameraTransform ? officer.cameraTransform.GetComponent<ThirdPersonCamera>() : null;
        previousLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        ownsPlayer = true;
        player.LockInput(true);
    }

    void Release()
    {
        if (!ownsPlayer) return;
        if (view) { view.EndShot(); view.SetCursorLock(previousLock == CursorLockMode.Locked); }
        Cursor.lockState = previousLock;
        Cursor.visible = previousCursorVisible;
        if (player) { player.ClearFaceTarget(); player.LockInput(false); }
        ownsPlayer = false;
    }

    void Frame(Vector3 localEye, Vector3 localLook)
    {
        if (view) view.BeginStation(transform.TransformPoint(localEye), transform.TransformPoint(localLook));
    }

    public void Listen(OfficerController officer)
    {
        if (stage != Stage.Outside) return;
        TakeControl(officer);
        stage = Stage.Listening;
        Frame(new Vector3(-.9f,1.58f,-2.55f),new Vector3(.6f,1.33f,-.7f));
        StartCoroutine(AtDoor());
    }

    IEnumerator AtDoor()
    {
        yield return Say("Да кончать его пора, босс! Толку с него, если ломбард пустой?", 4.1f);
        yield return Say("Я сказал — не трогать его без моей команды.", 3.3f);
        yield return Say("Груз на пирсе сам себя не примет. Оба через чёрный ход. Я здесь сам разберусь.", 4.7f);
        yield return Say("Поняли, босс. Через час вернёмся.", 2.7f);
        LocationAudio.Effect("Door movement close", .36f);
        yield return new WaitForSeconds(.55f);
        LocationAudio.Effect("Passing car", .21f);
        yield return new WaitForSeconds(1.1f);
        ClueJournal.AddConclusion(Overheard);
        stage = Stage.Outside;
        Release();
    }

    public void Enter(OfficerController officer)
    {
        TakeControl(officer);
        if (yardRain) yardRain.enabled = false;
        if (Finished) { ShowReport(); return; }
        stage = Stage.Conversation;
        if (!delayed) pulse.Play();
        StartCoroutine(Encounter());
    }

    public void Leave()
    {
        if (yardRain) yardRain.enabled = true;
        StopAllCoroutines();
        Release();
        if (pulse) pulse.Stop();
        caption = null;
        stage = Stage.Outside;
        raised = false;
    }

    void OnDisable() => Leave();

    IEnumerator Say(string words, float seconds)
    {
        caption = words;
        yield return new WaitForSeconds(seconds);
        caption = null;
    }

    IEnumerator Encounter()
    {
        Frame(new Vector3(-1.5f,1.57f,3.65f),new Vector3(.42f,1.03f,7.15f));
        kostya.localRotation = Quaternion.identity;
        yield return Say("На пол! Руки за голову!", 2.1f);
        Quaternion facing = Quaternion.Euler(0,-155,0);
        for (float t = 0; t < 1; t += Time.deltaTime)
        {
            kostya.localRotation = Quaternion.Slerp(Quaternion.identity, facing, Mathf.SmoothStep(0,1,t));
            yield return null;
        }
        kostya.localRotation = facing;
        Frame(new Vector3(.14f,1.5f,5.45f),new Vector3(1.08f,1.48f,7.1f));
        yield return Say("Костя?.. Это ты всё устроил?!", 2.8f);
        yield return Say("Привет, братишка. Вижу, дослужился до значка.", 3.5f);
        yield return Say("Ты родного отца на счётчик поставил! Из-за той ссоры десять лет назад?!", 4.1f);
        yield return Say("Вы вышвырнули меня на улицу без гроша. Я сам себя сделал на этих причалах!", 4.3f);
        yield return Say("Я хотел, чтобы старик почувствовал, каково это, когда у тебя забирают всё.", 4.2f);
        Frame(new Vector3(-.95f,1.22f,5.75f),new Vector3(0,1.04f,7.12f));
        raised = !delayed;
        yield return Say(delayed ? "Отец… Нет. Ты его загнал до смерти!" : "Лёша… Не стреляй… Это же Костя… Не бери грех на душу.", 4);
        yield return Say(delayed ? "Я не хотел… Он сам отключился полчаса назад…" : "Костя, не дёргайся. Руки так, чтобы я их видел.", 3.6f);
        Release();
        stage = Stage.Approach;
    }

    void Update()
    {
        if (stage == Stage.Outside || stage == Stage.Listening || Time.timeScale <= 0) return;
        if (!delayed && !Finished)
        {
            float breath = Mathf.Sin(Time.time * 2.3f);
            chest.localScale = new Vector3(1,1 + breath*.012f,1 + breath*.022f);
            pitch = Mathf.MoveTowards(pitch,raised ? -5 : 20,15*Time.deltaTime);
            head.localRotation = Quaternion.Euler(pitch,0,0);
        }
        if (stage == Stage.Approach && !player.InputLocked && !(player.hud && player.hud.IsShowingJournal) &&
            Vector3.Distance(player.transform.position,kostya.position) < 2)
        {
            TakeControl(player);
            player.FaceTowards(kostya.position);
            Frame(new Vector3(-.05f,1.42f,5.38f),new Vector3(.95f,1.2f,7.1f));
            UnlockCursor();
            BeginDecision();
            return;
        }
        var keys = Keyboard.current;
        if (stage == Stage.Decision)
        {
            if (reachingArm) reachingArm.localRotation = Quaternion.Slerp(Quaternion.identity,
                Quaternion.Euler(-52, 15, -24), Mathf.Clamp01((DecisionSeconds - deadline + Time.time) * 3f));
            if (keys != null && (keys.digit1Key.wasPressedThisFrame || keys.numpad1Key.wasPressedThisFrame)) Resolve(true);
            else if (keys != null && (keys.digit2Key.wasPressedThisFrame || keys.numpad2Key.wasPressedThisFrame)) Resolve(false);
            else if (Time.time >= deadline) stage = Stage.Missed;
        }
        else if (keys != null && keys.enterKey.wasPressedThisFrame)
        {
            if (stage == Stage.Missed) BeginDecision();
            else if (stage == Stage.Report) FinishCase();
        }
    }

    void UnlockCursor()
    {
        if (view) view.SetCursorLock(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void BeginDecision()
    {
        stage = Stage.Decision;
        deadline = Time.time + DecisionSeconds;
    }

    void Resolve(bool shot)
    {
        if (stage != Stage.Decision) return;
        stage = Stage.Aftermath;
        pulse.Stop();
        ClueJournal.AddConclusion(shot ? Shot : Arrest);
        StartCoroutine(Aftermath(shot));
    }

    IEnumerator Aftermath(bool shot)
    {
        LocationAudio.Effect(shot ? "Hangar shot" : "Door movement close", shot ? .55f : .25f);
        var start = kostya.localRotation;
        var target = Quaternion.Euler(shot ? -88 : 86,24,0);
        var origin = kostya.localPosition;
        for (float t = 0; t < 1; t += Time.deltaTime / .65f)
        {
            float u = Mathf.SmoothStep(0,1,t);
            kostya.localRotation = Quaternion.Slerp(start,target,u);
            kostya.localPosition = Vector3.Lerp(origin,new Vector3(1.2f,.32f,7.15f),u);
            yield return null;
        }
        EndPose(shot);
        Frame(new Vector3(-1.45f,1.34f,5.2f),new Vector3(.21f,.87f,7.15f));
        if (!shot) LocationAudio.Effect("Lock key press",.35f);
        if (!delayed)
            yield return Say(shot ? "Спасибо тебе, сынок… Ты спас мне жизнь." :
                "Спасибо тебе, сынок… И за то, что меня вытащил, и за то, что душу не замарал.",5);
        else yield return new WaitForSeconds(2);
        ShowReport();
    }

    void EndPose(bool shot)
    {
        kostya.localRotation = Quaternion.Euler(shot ? -88 : 86,24,0);
        kostya.localPosition = new Vector3(1.2f,.32f,7.15f);
        if (reachingArm) reachingArm.localRotation = Quaternion.Euler(shot ? -18 : 40, 0, shot ? 22 : -12);
        if (delayed) return;
        head.localRotation = Quaternion.Euler(-5,0,0);
        foreach (Transform part in rescue.transform)
            if (part.name == "Rope") part.gameObject.SetActive(false);
    }

    void ShowReport()
    {
        stage = Stage.Report;
        Frame(new Vector3(-1.8f,1.35f,4.7f),new Vector3(.24f,.86f,7.15f));
        UnlockCursor();
    }

    void FinishCase()
    {
        if (stage != Stage.Report) return;
        Leave();
        SceneManager.LoadSceneAsync("00_MainMenu");
    }

    static void Fill(Rect rect, Color tint)
    {
        Color old = GUI.color;
        GUI.color = tint;
        GUI.DrawTexture(rect,Texture2D.whiteTexture);
        GUI.color = old;
    }

    bool ActionButton(Rect rect, string text)
    {
        bool hover = rect.Contains(Event.current.mousePosition);
        Fill(rect,hover ? new Color(.22f,.23f,.2f,.98f) : ink);
        Fill(new Rect(rect.x,rect.y,3,rect.height),brass);
        GUI.Label(rect,text,action);
        return GUI.Button(rect,GUIContent.none,GUIStyle.none);
    }

    void OnGUI()
    {
        Subtitles.Draw(caption);
        if (stage != Stage.Decision && stage != Stage.Missed && stage != Stage.Report) return;
        float k = Mathf.Min(Screen.width / 800f,Screen.height / 800f);
        title ??= new GUIStyle(GameTypography.Label) { alignment = TextAnchor.MiddleLeft, wordWrap = true };
        body ??= new GUIStyle(title);
        action ??= new GUIStyle(title) {alignment = TextAnchor.MiddleCenter};
        title.fontSize = Mathf.RoundToInt(29*k);
        body.fontSize = Mathf.RoundToInt(19*k);
        action.fontSize = Mathf.RoundToInt(20*k);
        title.normal.textColor = action.normal.textColor = new Color(.97f,.94f,.85f);
        body.normal.textColor = brass;
        float width = 570*k;
        var r = new Rect((Screen.width-width)*.5f, Screen.height-226*k,width,198*k);
        Fill(r,ink);
        Fill(new Rect(r.x,r.y,68*k,2*k),brass);
        float x = r.x+22*k, w = width-44*k;
        bool report = stage == Stage.Report, shot = ClueJournal.Has(Shot);
        string heading = report ? (delayed ? (shot ? "Кровь на причале" : "Холодный протокол") :
            (shot ? "Цена спасения" : "Возвращение домой")) :
            stage == Stage.Missed ? "Момент упущен" : "Костя тянется к карману";
        GUI.Label(new Rect(x,r.y+11*k,w,26*k), delayed ? "Шипсхед-Бей   /   02:35" : "Шипсхед-Бей   /   01:55",body);
        GUI.Label(new Rect(x,r.y+39*k,w,43*k),heading,title);
        if (stage == Stage.Decision)
        {
            Fill(new Rect(x,r.y+98*k,w,3*k),new Color(.28f,.28f,.25f));
            Fill(new Rect(x,r.y+98*k,w*Mathf.Clamp01((deadline-Time.time)/DecisionSeconds),3*k),brass);
            if (ActionButton(new Rect(x,r.y+120*k,(w-12*k)/2,50*k),"1   Выстрелить")) Resolve(true);
            else if (ActionButton(new Rect(x+(w+12*k)/2,r.y+120*k,(w-12*k)/2,50*k),"2   Обезвредить")) Resolve(false);
        }
        else
        {
            if (report) GUI.Label(new Rect(x,r.y+80*k,w,48*k),
                (delayed ? "Фёдор погиб. " : "Фёдор спасён. ") + (shot ? "Костя застрелен." : "Костя арестован."),body);
            if (ActionButton(new Rect(x,r.y+129*k,w,45*k),report ? "Завершить дело   [Enter]" : "Повторить выбор   [Enter]"))
            {
                if (report) FinishCase(); else BeginDecision();
            }
        }
    }
}


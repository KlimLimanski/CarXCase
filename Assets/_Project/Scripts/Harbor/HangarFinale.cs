using System.Collections;
using CarXCase.UI;
using DetectiveGame.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public sealed partial class HangarFinale : MonoBehaviour
{
    public GameObject rescue, late;
    public Transform kostya;
    HangarStaging staging;
    HangarProps props;
    AudioSource recordAudio;
    AudioLowPassFilter recordFilter;
    OfficerController recordListener;
    float recordLevel;
    bool fadingToCredits;
    public float RecordScoreDuck { get; private set; }
    public float EndingAmbience => fadingToCredits ? 1f - endingFade : 1f;
    public Light pendant, backlight;
    public AudioSource pulse;
    public ParticleSystemRenderer yardRain;
    public bool UnderRoof => stage != Stage.Outside && stage != Stage.Listening;
    public const string LateRoute = "hangar_bar_then_pawnshop";
    const string Shot = "hangar_shot", Arrest = "hangar_arrest";
    const string Overheard = "hangar_guards_departed";
    const float DecisionSeconds = 4f;
    /// How far the draw gets while the player is deciding (the gun is just clearing the coat).
    const float HoldDraw = .56f;

    enum Stage { Outside, Listening, Conversation, Approach, DialogueChoice, Decision, Missed, Aftermath, Report }
    Stage stage;
    OfficerController player;
    ThirdPersonCamera view;
    bool delayed, turning, ownsPlayer, previousCursorVisible;
    CursorLockMode previousLock;
    Quaternion confrontationFacing;
    Vector3 confrontationMark;
    float deadline, effort, effortGoal, gesture, concern, reach;
    bool kostyaSpeaking, fatherReleased;
    string caption;
    GUIStyle title, body, action;
    readonly Color ink = new Color(.065f,.086f,.086f,.96f);
    readonly Color brass = new Color(.77f,.65f,.43f);
    public bool Finished => ClueJournal.Has(Shot) || ClueJournal.Has(Arrest) || SpecialTitle != null;
    public bool NeedsListening => !Finished && !ClueJournal.Has(Overheard);
    public string Objective => stage == Stage.Approach ? "Подойдите к Косте" :
        stage != Stage.Outside || Finished ? null : NeedsListening ? "Подслушайте разговор у двери ангара" : HangarKey.Found ? "Войдите в ангар" : "Найдите ключ на причале";

    SkinnedMeshRenderer kostyaFace, fatherFace;
    static SkinnedMeshRenderer SpeechFace(Transform root, string model)
    {
        var face = root.Find("Hangar " + model).GetComponentInChildren<SkinnedMeshRenderer>(true);
        var smooth = Resources.Load<Mesh>("HangarSmooth" + model);
        face.sharedMesh = smooth ? smooth : Resources.Load<Mesh>("HangarSpeech" + model);
        face.sharedMaterials = new[] { face.sharedMaterial, Resources.Load<Material>(model == "Kostya" ? "KostyaMouthInterior" : "HangarMouthInterior") };
        return face;
    }

    void Start()
    {
        staging = GetComponent<HangarStaging>();
        kostyaFace = SpeechFace(kostya, "Kostya");
        fatherFace = SpeechFace(rescue.transform, "Rescue");
        var lateMesh = Resources.Load<Mesh>("HangarSmoothLate");
        var lateModel = late.transform.Find("Hangar Late");
        var lateFace = lateModel ? lateModel.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
        if (lateFace && lateMesh) lateFace.sharedMesh = lateMesh;
        staging.Initialize(rescue.transform, kostya);
        staging.InitializeLate(late.transform);
        props = gameObject.AddComponent<HangarProps>();
        props.Setup(staging, kostya, rescue.transform);
        SetupRecordAudio();
        if (pulse) pulse.spatialBlend = 0f;   // The heartbeat is Alexey's, independent of shot size.
        confrontationFacing = Quaternion.Euler(0, -155f, 0);
        confrontationMark = staging.KostyaMark;
        SetBranch(ClueJournal.Has(LateRoute));
        if (Finished) RestoreOutcome();
    }

    void SetupRecordAudio()
    {
        var deck = GetComponentInChildren<CarXCase.Apartment.RecordSpin>();
        var record = Resources.Load<AudioClip>("LocationAudio/Music/Hangar record");
        if (!deck || !record) return;

        recordAudio = deck.gameObject.AddComponent<AudioSource>();
        recordAudio.playOnAwake = false;
        recordAudio.clip = record;
        recordAudio.loop = true;
        // Mix from Alexey's position so dialogue coverage never moves the listener.
        recordAudio.spatialBlend = 0f;
        recordAudio.volume = 0f;
        recordFilter = deck.gameObject.AddComponent<AudioLowPassFilter>();
        recordFilter.cutoffFrequency = 1550f;
        recordFilter.lowpassResonanceQ = 1f;
        recordListener = FindAnyObjectByType<OfficerController>();
    }

    void MixRecordAudio()
    {
        if (!recordAudio || !recordListener || !UnderRoof || !recordAudio.isPlaying) return;

        Transform listener = recordListener.transform;
        Vector3 toDeck = Vector3.ProjectOnPlane(recordAudio.transform.position - listener.position, Vector3.up);
        float distance = toDeck.magnitude;
        float roomDistance = Smooth(2f, 22f, distance);
        float blend = 1f - Mathf.Exp(-Time.deltaTime / .45f);

        float presence = Mathf.Lerp(1f, .62f, roomDistance);
        float speech = string.IsNullOrEmpty(caption) ? .86f : .36f;
        float target = stage == Stage.Report || creditsRolling ? 0f : speech * presence * NoirMenu.GameSettings.MusicGain;
        recordLevel = Mathf.Lerp(recordLevel, target, blend);
        recordAudio.volume = recordLevel * EndingAmbience;

        float pan = .24f * Vector3.Dot(listener.right, toDeck / Mathf.Max(2f, distance));
        recordAudio.panStereo = Mathf.Lerp(recordAudio.panStereo, pan, blend);
        float cutoff = Mathf.Lerp(5600f, 2900f, roomDistance);
        recordFilter.cutoffFrequency = Mathf.Lerp(recordFilter.cutoffFrequency, cutoff, blend);
        RecordScoreDuck = Mathf.Lerp(RecordScoreDuck, 1f, blend);
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
        recordListener = officer;
        props.SetHero(officer);
        view = officer.cameraTransform ? officer.cameraTransform.GetComponent<ThirdPersonCamera>() : null;
        previousLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        ownsPlayer = true;
        player.LockInput(true);
    }

    void Release()
    {
        StopDirecting();
        if (!ownsPlayer) return;
        if (view) { view.EndShot(); view.SetCursorLock(previousLock == CursorLockMode.Locked); }
        Cursor.lockState = previousLock;
        Cursor.visible = previousCursorVisible;
        if (player)
        {
            player.StopWalking();
            player.ClearFaceTarget();
            player.LockInput(false);
        }
        ownsPlayer = false;
    }

    void Frame(Vector3 localEye, Vector3 localLook)
    {
        if (directing)
        {
            // Inside a directed dialogue the placed angle becomes one more cut, held for the next line.
            authoredEye = transform.TransformPoint(localEye);
            authoredLook = transform.TransformPoint(localLook);
            Cut(Cover.Authored, 1);
            return;
        }
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
        yield return Say("Незнакомый голос 1", "Да кончать его пора, босс! Толку с него, если ломбард пустой?", 4.1f);
        yield return Say("Главарь", "Я сказал — не трогать его без моей команды.", 3.3f);
        yield return Say("Главарь", "Груз на пирсе сам себя не примет. Оба через чёрный ход. Я здесь сам разберусь.", 4.7f);
        yield return Say("Незнакомый голос 2", "Поняли, босс. Через час вернёмся.", 2.7f);
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
        if (Finished)
        {
            RestoreOutcome();
            ShowReport();
            return;
        }
        stage = Stage.Conversation;
        if (recordAudio) recordAudio.Play();
        if (!delayed) pulse.Play();
        StartCoroutine(Encounter());
    }

    public void Leave()
    {
        if (recordAudio) { recordAudio.Stop(); recordAudio.volume = 0f; }
        RecordScoreDuck = recordLevel = 0f;
        if (yardRain) yardRain.enabled = true;
        if (!string.IsNullOrEmpty(caption)) VoiceOver.EndHangarLine();
        StopAllCoroutines();
        StopCredits();
        fadingToCredits = false;
        endingFade = 0;
        props.ResetAll();
        if (kostyaFace) kostyaFace.SetBlendShapeWeight(0, 0);
        if (fatherFace) fatherFace.SetBlendShapeWeight(0, 0);
        CarXCase.Hero.HeroFace.SetTalking(false);
        Release();
        if (pulse) pulse.Stop();
        caption = null;
        stage = Stage.Outside;
        turning = false;
        kostyaSpeaking = false;
        fatherReleased = false;
        effort = effortGoal = gesture = concern = reach = 0f;
    }

    void LateUpdate()
    {
        if (NoirMenu.GameSession.Paused) return;
        if (stage == Stage.Aftermath && fatherReleased)
            staging.Father(1f, 1f);
        DirectCamera();
    }

    void OnDisable() => Leave();

    static float LetterOpening(string words, int index)
    {
        if (index < 0 || index >= words.Length) return 0f;
        char c = char.ToLowerInvariant(words[index]);
        if ("ая".IndexOf(c) >= 0) return 98f;
        if ("оёэе".IndexOf(c) >= 0) return 78f;
        if ("иыую".IndexOf(c) >= 0) return 54f;
        if ("мбп".IndexOf(c) >= 0 || !char.IsLetter(c)) return 0f;
        return 19f;
    }

    static float SpeechOpening(string words, float elapsed, float duration)
    {
        // Blend neighbouring sounds so the jaw starts moving before each vowel.
        float cursor = elapsed * words.Length / duration - .5f;
        int i = Mathf.FloorToInt(cursor);
        float t = cursor - i, u = 1f - t;
        float opening = (u * u * u * LetterOpening(words, i - 1)
            + (4f - 6f * t * t + 3f * t * t * t) * LetterOpening(words, i)
            + (4f - 6f * u * u + 3f * u * u * u) * LetterOpening(words, i + 1)
            + t * t * t * LetterOpening(words, i + 2)) / 6f;
        float fade = Mathf.Clamp01(Mathf.Min(elapsed / .12f, (duration - elapsed) / .18f));
        return opening * Mathf.SmoothStep(0f, 1f, fade);
    }

    IEnumerator Say(string speaker, string words, float seconds, string replyClip = null)
    {
        kostyaSpeaking = speaker == "Костя";
        CarXCase.Hero.HeroFace.SetTalking(speaker == "Алексей");
        OnLine(speaker, words);
        caption = Subtitles.WithSpeaker(speaker, words);
        seconds = Mathf.Max(seconds, VoiceOver.StartHangarLine(caption, replyClip) + .12f);
        var face = speaker == "Костя" ? kostyaFace : speaker == "Фёдор Павлович" ? fatherFace : null;
        for (float elapsed = 0; elapsed < seconds; elapsed += Time.deltaTime)
        {
            if (face && words.Length > 0)
            {
                face.SetBlendShapeWeight(0, SpeechOpening(words, elapsed, seconds));
            }
            yield return NoirMenu.GameSession.NextStoryFrame;
        }
        if (face) face.SetBlendShapeWeight(0, 0);
        CarXCase.Hero.HeroFace.SetTalking(false);
        caption = null;
        kostyaSpeaking = false;
    }

    IEnumerator Encounter()
    {
        staging.ResetActors();
        player.FaceTowards(kostya.position);
        StartDirecting(Cover.Walk, 1);
        props.Aim(1f, kostya);              // storms in with his service pistol up
        player.WalkTo(transform.TransformPoint(new Vector3(-.28f, .10f, 3.15f)), .57f);
        yield return Say("Алексей", "На пол! Руки за голову!", 2.1f);
        player.StopWalking();
        Cut(Cover.CloseKostya, 1);   // he turns round into his close-up; Alexey's recognition plays over it
        LocationAudio.Effect("stinger", .3f);
        turning = true;
        yield return Animate(staging.TurnDuration, staging.Turn);
        turning = false;
        confrontationMark = kostya.localPosition;
        yield return Say("Алексей", "Костя?.. Это ты всё устроил?!", 2.8f);
        yield return Say("Костя", "Привет, братишка. Вижу, дослужился до значка.", 3.5f);
        yield return Say("Алексей", "Ты родного отца на счётчик поставил! Из-за той ссоры десять лет назад?!", 4.1f);
        yield return Say("Костя", "Вы вышвырнули меня на улицу без гроша. Я сам себя сделал на этих причалах!", 4.3f);
        yield return Say("Костя", "Я хотел, чтобы старик почувствовал, каково это, когда у тебя забирают всё.", 4.2f);
        Cut(Cover.CloseFather, 1);
        effortGoal = delayed ? 0f : 1f;
        if (!delayed) LocationAudio.Effect("horror-creak-61", .28f);   // the chair under the tied father
        if (!delayed) yield return new WaitForSeconds(.50f);
        yield return Say(delayed ? "Алексей" : "Фёдор Павлович", delayed ? "Отец… Нет. Ты его загнал до смерти!" : "Лёша… Не стреляй… Это же Костя… Не бери грех на душу.", 4);
        if (delayed) concern = 1f;
        yield return Say(delayed ? "Костя" : "Алексей", delayed ? "Я не хотел… Он сам отключился полчаса назад…" : "Костя, не дёргайся. Руки так, чтобы я их видел.", 3.6f);
        concern = .28f;
        props.Aim(0f, kostya);
        Release();
        stage = Stage.Approach;
    }

    void Update()
    {
        if (NoirMenu.GameSession.Paused) return;
#if UNITY_EDITOR
        DebugKeys();
#endif
        MixRecordAudio();
        if (stage == Stage.Outside || stage == Stage.Listening || Time.timeScale <= 0) return;
        if (!delayed && !Finished)
        {
            effort = Mathf.MoveTowards(effort, effortGoal, Time.deltaTime * .90f);
            staging.Father(Mathf.SmoothStep(0f, 1f, effort), 0f);
        }
        if (stage == Stage.Approach && !player.InputLocked && !(player.hud && player.hud.IsShowingJournal) &&
            Vector3.Distance(player.transform.position,kostya.position) < 2)
        {
            TakeControl(player);
            player.FaceTowards(kostya.position);
            StartDirecting(Cover.Choice);
            props.Aim(.5f, kostya);         // low ready while they talk
            UnlockCursor();
            StartCoroutine(Negotiate());
            return;
        }
        if (!turning && (stage == Stage.Conversation || stage == Stage.Approach))
        {
            float gestureTarget = kostyaSpeaking ? 1f : 0f;
            gesture = Mathf.MoveTowards(gesture, gestureTarget, Time.deltaTime * .85f);
            staging.Conversation(gesture, concern);
        }
        var keys = Keyboard.current;
        if (stage == Stage.DialogueChoice && keys != null)
        {
            if (keys.digit1Key.wasPressedThisFrame || keys.numpad1Key.wasPressedThisFrame) Pick(0);
            else if (keys.digit2Key.wasPressedThisFrame || keys.numpad2Key.wasPressedThisFrame) Pick(1);
            else if (keys.digit3Key.wasPressedThisFrame || keys.numpad3Key.wasPressedThisFrame) Pick(2);
            else if (keys.digit4Key.wasPressedThisFrame || keys.numpad4Key.wasPressedThisFrame) Pick(3);
        }
        if (stage == Stage.Decision)
        {
            // Under the timer he goes for it: hand under the lapel, grip, and the gun starting to clear the coat.
            reach = Mathf.Min(HoldDraw, (DecisionSeconds - deadline + Time.time) / staging.DrawDuration);
            staging.Draw(reach, player.transform);
            if (reach >= HangarStaging.RevealMoment && !props.KostyaArmed) props.ArmKostya(true);
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
        reach = 0f;
        staging.Draw(0f, player.transform);
        props.ArmKostya(false);
        props.Aim(1f, kostya);
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

    static IEnumerator Animate(float duration, System.Action<float> pose)
    {
        for (float elapsed = 0; elapsed < duration; elapsed += Time.deltaTime)
        {
            pose(elapsed / duration);
            yield return NoirMenu.GameSession.NextStoryFrame;
        }
        pose(1f);
    }

    IEnumerator Aftermath(bool shot)
    {
        Frame(new Vector3(2.85f, 1.47f, 5.32f), new Vector3(1.03f, .92f, 7.20f));
        if (shot)
        {
            endingFade = 1f;
            LocationAudio.Effect("Hangar shot", .55f);
            props.DropKostyaGun();
            staging.Subside(true, 1f, confrontationMark, confrontationFacing);
            props.ShowBlood(false);
            yield return RevealAfterDeath(.65f, .75f);
        }
        else
        {
            props.DropKostyaGun();
            yield return Animate(staging.FallDuration(false),
                t => staging.Subside(false, t, confrontationMark, confrontationFacing));
            LocationAudio.Effect("Body fall", .4f);
        }
        LocationAudio.Effect("Door movement close", .17f);
        yield return new WaitForSeconds(.20f);
        if (!shot)
        {
            LocationAudio.Effect("Handcuffs", .6f);
            LocationAudio.Effect("Lock key press", .35f);
            staging.SetCuff(true);
            yield return new WaitForSeconds(.44f);
        }
        LocationAudio.Effect("Police siren", .22f);   // backup on its way across the docks
        yield return StandardLastWords(shot);
        props.Aim(0f, kostya);             // holsters before going to his father
        if (!delayed)
        {
            Frame(new Vector3(-1.16f, 1.14f, 5.84f), new Vector3(-.08f, .90f, 7.07f));
            var mark = transform.TransformPoint(new Vector3(-.82f, .10f, 6.68f));
            if (Vector3.Distance(player.transform.position, mark) < 2.9f)
            {
                player.WalkTo(mark, .55f);
                for (float elapsed = 0; elapsed < 1.55f && player.IsWalking; elapsed += Time.deltaTime)
                    yield return NoirMenu.GameSession.NextStoryFrame;
                player.StopWalking();
                player.FaceTowards(rescue.transform.position);
            }
            staging.Bindings(false);
            LocationAudio.Effect("Rope", .55f);
            yield return Animate(.65f, t =>
            {
                staging.DropRope(t);
                staging.Father(1f, Mathf.SmoothStep(0, 1, t));
            });
            fatherReleased = true;
            yield return Say("Фёдор Павлович", shot ? "Спасибо тебе, сынок... за всё. Ты спас мне жизнь." :
                "Спасибо тебе, сынок... за всё. И за то, что меня вытащил, и за то, что душу не замарал.", 5f, shot ? "shot_4" : "arrest_6");
        }
        else yield return new WaitForSeconds(1.5f);
        EndPose(shot);
        yield return FadeOutEnding();
    }

    void EndPose(bool shot)
    {
        staging.Subside(shot, 1f, confrontationMark, confrontationFacing);
        staging.SetCuff(!shot);
        if (delayed) return;
        staging.Father(1f, 1f);
        staging.Bindings(false);
        staging.DropRope(1f);
    }

    IEnumerator RevealAfterDeath(float hold, float fallGain = 0f)
    {
        caption = null;
        yield return new WaitForSeconds(hold);
        if (fallGain > 0f) LocationAudio.Effect("Body fall", fallGain);
        yield return Animate(.65f, t => endingFade = 1f - Mathf.SmoothStep(0, 1, t));
    }

    IEnumerator FadeOutEnding()
    {
        caption = null;
        BeginEndingShot();
        yield return new WaitForSeconds(EndingShotSeconds);
        fadingToCredits = true;
        yield return Animate(1.25f, t => endingFade = Mathf.SmoothStep(0, 1, t));
        yield return new WaitForSeconds(2f);
        yield return RollCredits();
    }

    void ShowReport()
    {
        stage = Stage.Report;
        StopDirecting();
        Frame(new Vector3(-1.8f,1.35f,4.7f),new Vector3(.24f,.86f,7.15f));
        UnlockCursor();
    }

    void FinishCase()
    {
        if (stage != Stage.Report || creditsRolling) return;
        StartCoroutine(RollCredits());   // credits, then back to the main menu
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
        // Same look as the answer buttons in the bar dialogue.
        return ChoiceButton.Draw(rect,text,action.fontSize);
    }

    readonly DialogueChoiceRows hangarRows = new();
    void DrawHangarChoices()
    {
        bool timed = stage == Stage.Decision;
        int count = timed ? 2 : Replies.GetLength(1);
        float w = Screen.width, h = Screen.height, k = Mathf.Clamp(h / 720f, .7f, 2f);
        float width = Mathf.Min(282f*k, w*.43f), margin = Mathf.Min(29f*k, w*.04f);
        float row = Mathf.Min(47f*k, h*.092f);
        var cam = player && player.cameraTransform ? player.cameraTransform.GetComponent<Camera>() : Camera.main;
        Vector3 face = cam ? cam.WorldToViewportPoint(kostya.position + Vector3.up*1.55f) : new Vector3(.5f,.5f,1);
        bool right = face.x <= .56f;
        float x = Mathf.Clamp(right ? Mathf.Max(w*.55f,face.x*w+margin) : Mathf.Min(w*.45f-width,face.x*w-margin-width),margin,w-width-margin);
        float top = Mathf.Clamp((1-face.y)*h-row*.45f,h*.29f,h*.76f-row*count);
        for (int i=0;i<count;i++)
        {
            string text = timed ? (i==0 ? "Выстрелить" : "Обезвредить") : Replies[round,i];
            float size = hangarRows.Measure(text,k,width);
            var rect = new Rect(right ? x+width-size : x,top+i*row,size,row-6*k);
            bool hover = rect.Contains(Event.current.mousePosition);
            hangarRows.Draw(rect,text,i+1,k,hover);
            if (hover && Event.current.type==EventType.MouseDown && Event.current.button==0)
            {
                Event.current.Use();
                if (timed) Resolve(i==0); else Pick(i);
                return;
            }
        }
        if (timed)
        {
            float size = Mathf.Min(width,108*k);
            var timer = new Rect(right ? x+width-size : x,top+count*row+2*k,size,2*k);
            Fill(timer,new Color(.07f,.06f,.04f,.88f));
            timer.width *= Mathf.Clamp01((deadline-Time.time)/DecisionSeconds);
            Fill(timer,DialogueChoiceRows.Gold);
        }
    }

    void OnGUI()
    {
        if (endingFade > 0f || creditsRolling)
        {
            int depth = GUI.depth;
            GUI.depth = -100;
            if (endingFade > 0f)
                Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, endingFade));
            bool drewCredits = DrawCredits();
            GUI.depth = depth;
            if (drewCredits) return;
        }
#if UNITY_EDITOR
        if (DebugMenu()) return;
#endif
        if (endingFade >= 1f) return;
        if (NoirMenu.GameSession.Paused) return;
        Subtitles.Draw(caption);
        if (stage != Stage.DialogueChoice && stage != Stage.Decision && stage != Stage.Missed && stage != Stage.Report) return;
        if (stage == Stage.DialogueChoice || stage == Stage.Decision)
        {
            DrawHangarChoices();
            return;
        }
        float k = Mathf.Min(Screen.width / 800f,Screen.height / 800f);
        title ??= new GUIStyle(GameTypography.Label) { alignment = TextAnchor.MiddleLeft, wordWrap = true };
        body ??= new GUIStyle(title);
        action ??= new GUIStyle(title) {alignment = TextAnchor.MiddleCenter};
        title.fontSize = Mathf.RoundToInt(29*k);
        body.fontSize = Mathf.RoundToInt(19*k);
        action.fontSize = Mathf.RoundToInt(20*k);
        title.normal.textColor = action.normal.textColor = new Color(.97f,.94f,.85f); title.Steady(); action.Steady();
        body.normal.textColor = brass; body.Steady();
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
        GUI.Label(new Rect(x,r.y+39*k,w,43*k),report ? SpecialTitle ?? heading : heading,title);
        {
            if (report) GUI.Label(new Rect(x,r.y+80*k,w,48*k),
                SpecialText ?? (delayed ? "Фёдор погиб. " : "Фёдор спасён. ") + (shot ? "Костя застрелен." : "Костя арестован."),body);
            if (ActionButton(new Rect(x,r.y+129*k,w,45*k),report ? "[Enter]  Завершить дело" : "[Enter]  Повторить выбор"))
            {
                if (report) FinishCase(); else BeginDecision();
            }
        }
    }
}

using System.Collections;
using CarXCase.Hero;
using CarXCase.Interaction;
using CarXCase.UI;
using DetectiveGame.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CarXCase.Precinct
{
    public sealed class BarWitness : MonoBehaviour, IInteractable, IGlyphAnchor
    {
        const string Girl = "Девушка";
        const string Hero = "Алексей";

        public Transform witness;
        public ClueObject lead;
        [Tooltip("Seated acting of the witness (gaze, speech, reactions).")]
        public BarWitnessActor actor;

        OfficerController player;
        ThirdPersonCamera view;
        CursorLockMode previousLock;
        bool previousCursor, active, choosing, skip;
        int answer;
        string line;
        Camera lens;
        readonly DialogueChoiceRows choiceRows = new();
        static readonly string[] Answers = { "Выпить с девушкой", "Отказаться" };

        public bool CanInteract => isActiveAndEnabled && !active && ClueJournal.Has(PortLead.AskWitness) &&
            lead && !ClueJournal.Has(PortLead.Witness);
        public string InteractPrompt => "E  Поговорить с девушкой";
        public Vector3 GlyphPosition => transform.position;

        void Awake()
        {
            if (!actor && witness) actor = witness.GetComponent<BarWitnessActor>();
        }

        public void Interact(SimpleUIHUD hud)
        {
            if (!CanInteract) return;
            player = FindAnyObjectByType<OfficerController>();
            if (!player) return;
            hud?.ClearPrompt();
            previousLock = Cursor.lockState;
            previousCursor = Cursor.visible;
            player.LockInput(true);
            if (witness && player.cameraTransform && player.cameraTransform.TryGetComponent(out view))
            {
                lens = view.GetComponent<Camera>();
                player.FaceTowards(GirlHead);
                FrameConversation();
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            active = true;
            if (actor) actor.BeginConversation(player.transform);
            StartCoroutine(Talk());
        }

        IEnumerator Talk()
        {
            // she notices him first and turns before anyone speaks
            yield return new WaitForSeconds(.55f);
            yield return Say(Hero, "Добрый вечер. Дмитрий говорит, вчера сюда приходили двое, искали моего отца. Вы их видели?");
            yield return Say(Girl, "Выпьешь со мной? Тогда скажу, куда делись те двое.");
            answer = -1;
            choosing = true;
            if (actor) actor.Wait();
            yield return new WaitUntil(() => answer >= 0);
            choosing = false;
            if (answer == 0)
            {
                yield return Say(Hero, "Одну. Но сначала — про них.");
                LocationAudio.Effect("Place Open Box Bottles Ff", .45f);
                yield return Say(Girl, "Месяц назад они так же подставили ювелира с Кони-Айленда. На причалах ими заправляет местный, с Брайтона.");
                yield return Say(Girl, "Найдёшь их в Шипсхед-Бей. Они держат старый ангар, четвёртый.");
                yield return Say(Hero, "Шипсхед-Бей, четвёртый ангар. Спасибо — за мной должок.");
            }
            else
            {
                yield return Say(Hero, "Я на службе. Просто скажи, что слышала.");
                yield return Say(Girl, "Как хочешь. Слышала, как один велел другому ехать на причал Шипсхед-Бей, в четвёртый ангар. Больше ничего.");
                yield return Say(Hero, "Четвёртый ангар. Этого хватит, спасибо.");
            }
            lead.AddEntry();
            Release();
        }

        Vector3 GirlHead => actor && actor.head ? actor.head.position + Vector3.up * .06f : witness.position + Vector3.up * 1.47f;
        Vector3 HeroHead => player.transform.position + Vector3.up * 1.62f;

        // Hold a two-shot from the aisle for the conversation and the response choices.
        void FrameConversation()
        {
            Vector3 girl = GirlHead, hero = HeroHead;
            Vector3 look = (girl + hero) * .5f - Vector3.up * .23f;
            Vector3 alongCounter = Vector3.Cross(witness.forward, Vector3.up).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, girl - hero).normalized;
            if (Vector3.Dot(side, alongCounter) < 0f) side = -side;
            Vector3 direction = (alongCounter * .8f + side * .2f).normalized;
            Vector3 atLensHeight = look + Vector3.up * .39f;
            float distance = 2.4f;
            foreach (var hit in Physics.SphereCastAll(atLensHeight, .16f, direction, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance <= 0f || hit.transform.IsChildOf(witness) || hit.transform.IsChildOf(player.transform)) continue;
                distance = Mathf.Min(distance, Mathf.Max(.05f, hit.distance - .1f));
            }
            view.BeginConversationShot(atLensHeight + direction * distance, look);
        }

        IEnumerator Say(string speaker, string text)
        {
            bool hero = speaker == Hero;
            line = Subtitles.WithSpeaker(speaker, text);
            skip = false;
            float earliest = Time.time + .3f;
            float shown = Mathf.Clamp(text.Length * .05f + (hero ? .2f : 0f), 2.3f, 7f);
            float end = Time.time + shown;
            if (hero)
            {
                if (actor) actor.Listen();
                HeroFace.SpeakAsHero(text, shown);
            }
            else
            {
                HeroFace.StopHero();
                if (actor) actor.Speak(text, shown);
            }
            while (Time.time < end && !(skip && Time.time > earliest))
                yield return NoirMenu.GameSession.NextStoryFrame;
            // line over or skipped: both faces settle at once
            HeroFace.StopHero();
            if (actor) actor.Wait();
            line = null;
        }

        void Update()
        {
            if (NoirMenu.GameSession.Paused) return;
            var keys = Keyboard.current;
            if (!active || keys == null) return;
            if (choosing)
            {
                if (keys.digit1Key.wasPressedThisFrame) answer = 0;
                else if (keys.digit2Key.wasPressedThisFrame) answer = 1;
            }
            else if (keys.spaceKey.wasPressedThisFrame || keys.enterKey.wasPressedThisFrame) skip = true;
        }

        void OnGUI()
        {
            if (NoirMenu.GameSession.Paused) return;
            if (!active) return;
            if (!choosing) { Subtitles.Draw(line); return; }
            // Keep the choices below both faces in the wide shot.
            float k = Mathf.Clamp(Screen.height / 720f, .7f, 2f), W = Screen.width, H = Screen.height;
            float width = Mathf.Min(282f * k, W * .43f);
            float margin = Mathf.Min(29f * k, W * .04f);
            float row = Mathf.Min(47f * k, H * .092f);
            Vector3 face = lens ? lens.WorldToViewportPoint(GirlHead) : new Vector3(.5f, .5f, 1f);
            if (face.z <= 0f) face = new Vector3(.5f, .5f, 1f);
            bool onRight = face.x <= .56f;
            float x = onRight ? Mathf.Max(W * .55f, face.x * W + margin) : Mathf.Min(W * .45f - width, face.x * W - margin - width);
            x = Mathf.Clamp(x, margin, W - width - margin);
            float top = Mathf.Clamp((1f - face.y) * H - row * .45f, H * .53f, H * .79f - row * Answers.Length);
            for (int i = 0; i < Answers.Length; i++)
            {
                float rowWidth = choiceRows.Measure(Answers[i], k, width);
                var r = new Rect(onRight ? x + width - rowWidth : x, top + i * row, rowWidth, row - 6f * k);
                bool hover = answer < 0 && r.Contains(Event.current.mousePosition);
                choiceRows.Draw(r, Answers[i], i + 1, k, answer == i || hover);
                if (hover && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    answer = i;
                    Event.current.Use();
                }
            }
        }

        void OnDisable()
        {
            if (!active) return;
            StopAllCoroutines();
            Release();
        }

        void Release()
        {
            HeroFace.StopHero();
            if (actor) actor.EndConversation();
            if (view) view.EndShot();
            if (player) { player.ClearFaceTarget(); player.LockInput(false); }
            Cursor.lockState = previousLock;
            Cursor.visible = previousCursor;
            active = choosing = false;
            line = null;
        }
    }
}

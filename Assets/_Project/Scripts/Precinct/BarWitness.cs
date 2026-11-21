using System.Collections;
using CarXCase.Interaction;
using CarXCase.UI;
using DetectiveGame.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CarXCase.Precinct
{
    public sealed class BarWitness : MonoBehaviour, IInteractable, IGlyphAnchor
    {
        public Transform witness;
        public ClueObject lead;

        OfficerController player;
        ThirdPersonCamera view;
        CursorLockMode previousLock;
        bool previousCursor, active, choosing, skip;
        int answer;
        string line;
        GUIStyle choiceStyle;

        public bool CanInteract => isActiveAndEnabled && !active && ClueJournal.Has(PortLead.AskWitness) &&
            lead && !ClueJournal.Has(PortLead.Witness);
        public string InteractPrompt => "E  Поговорить с девушкой";
        public Vector3 GlyphPosition => transform.position;

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
                var head = witness.position + Vector3.up * 1.33f;
                // The seated mesh faces local -Z. Frame her from the open end of the counter.
                Vector3 front = -witness.forward;
                Vector3 eye = head + front * 1.12f + Vector3.Cross(Vector3.up, front) * .63f;
                player.FaceTowards(head);
                view.BeginStation(eye + Vector3.up * .12f, head);
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            active = true;
            StartCoroutine(Talk());
        }

        IEnumerator Talk()
        {
            yield return Say("Девушка: Выпьешь со мной? Тогда скажу, куда делись те двое.");
            answer = -1;
            choosing = true;
            yield return new WaitUntil(() => answer >= 0);
            choosing = false;
            if (answer == 0)
            {
                LocationAudio.Effect("Place Open Box Bottles Ff", .45f);
                yield return Say("Девушка: Месяц назад они так же подставили ювелира с Кони-Айленда. На причалах ими заправляет местный, с Брайтона.");
                yield return Say("Девушка: Найдёшь их в Шипсхед-Бей. Они держат старый ангар, четвёртый.");
            }
            else
                yield return Say("Девушка: Как хочешь. Слышала, как один велел другому ехать на причал Шипсхед-Бей, в четвёртый ангар. Больше ничего.");
            lead.AddEntry();
            Release();
        }

        IEnumerator Say(string text)
        {
            line = text;
            skip = false;
            float earliest = Time.unscaledTime + .3f;
            float end = Time.unscaledTime + Mathf.Clamp(text.Length * .05f, 2.3f, 7f);
            while (Time.unscaledTime < end && !(skip && Time.unscaledTime > earliest))
                yield return null;
            line = null;
        }

        void Update()
        {
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
            if (!active) return;
            if (!choosing) { Subtitles.Draw(line); return; }
            choiceStyle ??= new GUIStyle(GameTypography.Label) { alignment = TextAnchor.MiddleLeft, fontSize = 20 };
            float width = Mathf.Min(420f, Screen.width * .45f);
            float left = Screen.width - width - Screen.width * .08f;
            DrawAnswer(new Rect(left, Screen.height * .61f, width, 42f), "[1]  Выпить с девушкой", 0);
            DrawAnswer(new Rect(left, Screen.height * .68f, width, 42f), "[2]  Отказаться", 1);
        }

        void DrawAnswer(Rect rect, string text, int index)
        {
            var previous = GUI.color;
            bool hover = rect.Contains(Event.current.mousePosition);
            GUI.color = hover ? new Color(.32f, .27f, .19f, .92f) : new Color(.045f, .072f, .079f, .88f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(new Rect(rect.x + 12f, rect.y, rect.width - 18f, rect.height), text, choiceStyle);
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) answer = index;
        }

        void OnDisable()
        {
            if (!active) return;
            StopAllCoroutines();
            Release();
        }

        void Release()
        {
            if (view) view.EndShot();
            if (player) { player.ClearFaceTarget(); player.LockInput(false); }
            Cursor.lockState = previousLock;
            Cursor.visible = previousCursor;
            active = choosing = false;
        }
    }
}

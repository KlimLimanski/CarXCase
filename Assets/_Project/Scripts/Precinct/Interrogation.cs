using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using DetectiveGame.Core;
using CarXCase.Hero;

namespace CarXCase.Precinct
{
    /// Detroit: Become Human–style interrogation: a success probability, the suspect's stress,
    /// four timed answers (W/A/D/S or arrows), answers unlocked by clues, and an outcome screen.
    public sealed class Interrogation : MonoBehaviour
    {
        [Serializable]
        public class Choice
        {
            [Tooltip("0 = up (W), 1 = left (A), 2 = right (D), 3 = down (S)")] public int slot;
            public string label;
            [TextArea] public string heroLine;
            public int probability;
            public int stress;
            [Tooltip("Next node id, or END")] public string next = "END";
            [Tooltip("Clue or conclusion id needed to see this answer (empty = always).")] public string requires;
            [TextArea] public string reply;
        }
        [Serializable] public class Node { public string id; [TextArea] public string[] lines; public Choice[] choices; }

        [Header("Cast")]
        public string suspectName = "ПОДОЗРЕВАЕМЫЙ";
        public string heroName = "АЛЕКСЕЙ";
        public NpcActor suspect;
        public Camera cam;
        public Transform camOnSuspect, camOnHero, camWide;

        [Header("Rules")]
        [Range(0, 100)] public int probability = 40;
        [Range(0, 100)] public float stress = 40;
        public int successThreshold = 60;
        public float choiceTime = 12;
        public Node[] nodes;

        [Header("Outcome")]
        public string successTitle, failTitle, shutdownTitle = "ДОПРОС СОРВАН";
        [TextArea(3, 8)] public string successText, failText, shutdownText;
        public string endingTag;
        public string grantId, grantTitle;
        [TextArea] public string grantText;
        public string returnScene = "02_Precinct";

        enum Phase { Idle, Talking, Choosing, Outcome }
        Phase phase = Phase.Idle;
        string speaker, line; float lineLeft; bool skip;
        Choice[] current; float choiceLeft; Choice picked;
        int lastDelta; float deltaTime = -10;
        bool success, shutdown; float fade, leave = -1;
        GUIStyle sub, spk, label, big, small, keyStyle;
        readonly CamSmoother smoother = new CamSmoother { blendTime = .6f, follow = .25f };
        Transform camTarget; float camSince;
        bool liveConversation;
        static readonly string[] ChoiceKeys = { "W", "A", "D", "S" };

        public void Begin()
        {
            if (phase != Phase.Idle || liveConversation) return;
            liveConversation = true;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            if (cam) smoother.Apply(cam, cam.transform.position, cam.transform.rotation, cam.fieldOfView, 0);
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            var node = nodes.FirstOrDefault();
            SetCam(camWide ? camWide : camOnSuspect);
            yield return new WaitForSeconds(0.6f);
            while (node != null)
            {
                foreach (var l in node.lines) yield return Say(suspectName, l, true);
                current = node.choices.Where(c => string.IsNullOrEmpty(c.requires) || ClueJournal.Has(c.requires)).ToArray();
                if (current.Length == 0) break;
                picked = null; choiceLeft = choiceTime; phase = Phase.Choosing;
                SetCam(camOnSuspect);
                while (picked == null)
                {
                    choiceLeft -= Time.deltaTime;
                    if (choiceLeft <= 0) picked = current.OrderBy(c => c.probability).First(); // hesitation costs
                    yield return null;
                }
                bool timedOut = choiceLeft <= 0;
                if (!timedOut) yield return new WaitForSeconds(.24f);
                HeroFace.SetEmotion(picked.stress >= 15 ? -0.9f : picked.stress <= -10 ? 0.6f : picked.probability < 0 ? -0.4f : 0.15f);
                Apply(timedOut ? picked.probability - 5 : picked.probability, picked.stress + (timedOut ? 5 : 0));
                yield return Say(heroName, timedOut ? "…" : picked.heroLine, false);
                if (!string.IsNullOrEmpty(picked.reply)) yield return Say(suspectName, picked.reply, true);
                if (stress >= 100) { shutdown = true; break; }
                node = picked.next == "END" ? null : nodes.FirstOrDefault(n => n.id == picked.next);
            }
            success = !shutdown && probability >= successThreshold;
            if (success && !string.IsNullOrEmpty(grantId)) { ClueJournal.Collect(grantId, grantTitle, grantText); ClueJournal.AddConclusion(grantId); }
            phase = Phase.Outcome;
            SetCam(camWide ? camWide : camOnSuspect);
            if (suspect) suspect.talking = false;
        }

        void Apply(int dProb, int dStress)
        {
            probability = Mathf.Clamp(probability + dProb, 0, 99);
            stress = Mathf.Clamp(stress + dStress, 0, 100);
            if (suspect) suspect.stress = stress;
            lastDelta = dProb; deltaTime = Time.time;
        }

        IEnumerator Say(string who, string text, bool isSuspect)
        {
            phase = Phase.Talking; speaker = who; line = text; skip = false;
            lineLeft = Mathf.Clamp(text.Length * 0.055f, 2.2f, 7.5f);
            SetCam(isSuspect ? camOnSuspect : camOnHero);
            if (suspect) suspect.talking = isSuspect;
            HeroFace.SetTalking(!isSuspect && text != "…");
            float minShow = 0.3f;
            while (lineLeft > 0 && !(skip && minShow <= 0)) { lineLeft -= Time.deltaTime; minShow -= Time.deltaTime; yield return null; }
            if (suspect) suspect.talking = false;
            HeroFace.SetTalking(false);
            line = null;
        }

        void SetCam(Transform a)
        {
            if (!a || a == camTarget) return;
            camTarget = a; camSince = Time.time;
        }

        void Update()
        {
            if (phase == Phase.Idle) return;
            if (cam && camTarget)
            {   // slow push-in on the current shot + gentle handheld drift, blended between shots
                float push = Mathf.Min(Time.time - camSince, 8f) * 0.012f;
                Vector3 p = camTarget.position + camTarget.forward * push + new Vector3(Mathf.Sin(Time.time * .6f), Mathf.Sin(Time.time * .8f + 1), 0) * .006f;
                smoother.Apply(cam, p, camTarget.rotation, 40, Time.deltaTime);
            }
            var kb = Keyboard.current; var mouse = Mouse.current;
            if (phase == Phase.Talking)
            {
                if ((kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) || (mouse != null && mouse.leftButton.wasPressedThisFrame)) skip = true;
            }
            else if (phase == Phase.Choosing && picked == null && kb != null)
            {
                int slot = -1;
                if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) slot = 0;
                if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) slot = 1;
                if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) slot = 2;
                if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) slot = 3;
                if (slot >= 0) picked = current.FirstOrDefault(c => c.slot == slot) ?? picked;
            }
            else if (phase == Phase.Outcome)
            {
                if (leave >= 0)
                {
                    leave += Time.deltaTime / 1.2f;
                    if (leave >= 1 && leave < 50) { leave = 99; ClueJournal.SkipPrecinctIntro = true; SceneManager.LoadSceneAsync(returnScene); }
                }
                else if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) leave = 0;
            }
        }

        // ------------------------------------------------------------------ GUI
        void OnGUI()
        {
            if (phase == Phase.Idle) return;
            if (sub == null)
            {
                sub = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, wordWrap = true }; sub.normal.textColor = Color.white;
                spk = new GUIStyle(sub) { fontStyle = FontStyle.Bold }; spk.normal.textColor = new Color(.55f, .85f, 1f);
                label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, wordWrap = true, fontStyle = FontStyle.Bold }; label.normal.textColor = Color.white;
                big = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true };
                small = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true }; small.normal.textColor = new Color(.8f, .8f, .8f);
                keyStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            }
            float k = Mathf.Clamp(Screen.height / 720f, .7f, 2f), W = Screen.width, H = Screen.height;
            sub.fontSize = Mathf.RoundToInt(20 * k); spk.fontSize = Mathf.RoundToInt(15 * k); label.fontSize = Mathf.RoundToInt(16 * k);
            big.fontSize = Mathf.RoundToInt(30 * k); small.fontSize = Mathf.RoundToInt(14 * k); keyStyle.fontSize = Mathf.RoundToInt(15 * k);

            // letterbox
            Fill(new Rect(0, 0, W, H * .06f), Color.black); Fill(new Rect(0, H * .94f, W, H * .06f), Color.black);

            if (phase == Phase.Choosing || (phase == Phase.Talking && Time.time - deltaTime < 2.5f))
            {
                // probability + stress (top-left, Detroit style)
                var panel = new Rect(18 * k, H * .07f, 250 * k, 104 * k);
                Fill(panel, new Color(0, 0, 0, .55f));
                small.alignment = TextAnchor.MiddleLeft; small.normal.textColor = new Color(.55f, .85f, 1f);
                GUI.Label(new Rect(panel.x + 12 * k, panel.y + 6 * k, panel.width, 20 * k), "ВЕРОЯТНОСТЬ УСПЕХА", small);
                big.alignment = TextAnchor.MiddleLeft; big.normal.textColor = Color.white;
                GUI.Label(new Rect(panel.x + 12 * k, panel.y + 24 * k, 120 * k, 36 * k), probability + "%", big);
                if (Time.time - deltaTime < 2.5f && lastDelta != 0)
                {
                    var arrow = new GUIStyle(big); arrow.normal.textColor = lastDelta > 0 ? new Color(.4f, 1f, .5f) : new Color(1f, .35f, .3f);
                    arrow.fontSize = Mathf.RoundToInt(22 * k);
                    GUI.Label(new Rect(panel.x + 110 * k, panel.y + 26 * k, 120 * k, 32 * k), (lastDelta > 0 ? "▲ +" : "▼ ") + lastDelta, arrow);
                }
                GUI.Label(new Rect(panel.x + 12 * k, panel.y + 62 * k, panel.width, 18 * k), "СТРЕСС · " + suspectName, small);
                var bar = new Rect(panel.x + 12 * k, panel.y + 84 * k, panel.width - 24 * k, 8 * k);
                Fill(bar, new Color(1, 1, 1, .15f));
                Fill(new Rect(bar.x, bar.y, bar.width * stress / 100f, bar.height), Color.Lerp(new Color(.4f, .8f, 1f), new Color(1f, .25f, .2f), stress / 100f));
                small.alignment = TextAnchor.MiddleCenter; small.normal.textColor = new Color(.8f, .8f, .8f);
                big.alignment = TextAnchor.MiddleCenter;
            }

            if (phase == Phase.Talking && !string.IsNullOrEmpty(line))
            {
                CarXCase.UI.Subtitles.Draw(line);
            }

            if (phase == Phase.Choosing)
            {
                float width = Mathf.Min(265f * k, W * .43f);
                float margin = Mathf.Min(24f * k, W * .04f);
                float row = Mathf.Min(44f * k, H * .092f);
                Vector3 face = cam && suspect
                    ? cam.WorldToViewportPoint(suspect.transform.position + Vector3.up * 1.55f)
                    : new Vector3(.5f, .5f, 1f);
                // Place the prompts beside the interviewee, with room for every available line.
                bool onRight = face.x <= .56f;
                float x = onRight ? Mathf.Max(W * .55f, face.x * W + margin) :
                    Mathf.Min(W * .45f - width, face.x * W - margin - width);
                x = Mathf.Clamp(x, margin, W - width - margin);
                float top = Mathf.Clamp((1f - face.y) * H - row * .45f,
                    H * .33f, H * .90f - row * current.Length - 9f * k);
                float progress = Mathf.Clamp01(choiceLeft / Mathf.Max(choiceTime, .01f));
                Color accent = progress < .25f ? new Color(1f, .62f, .37f) : new Color(.56f, .83f, .92f);
                for (int i = 0; i < current.Length; i++)
                {
                    Choice c = current[i];
                    var r = new Rect(x, top + i * row, width, row - 3f * k);
                    bool hover = picked == null && r.Contains(Event.current.mousePosition);
                    bool selected = picked == c;
                    Color ink = !string.IsNullOrEmpty(c.requires) ? new Color(.95f,.80f,.52f) : accent;
                    Fill(r, selected ? ink : hover ? new Color(.12f,.21f,.24f,.92f) : new Color(.035f,.055f,.065f,.82f));
                    Fill(new Rect(x, r.y, 2f*k, r.height), ink);
                    var keyRect = new Rect(x + 7*k, r.y, 30*k, r.height);
                    keyStyle.normal.textColor = selected ? Color.black : ink;
                    GUI.Label(keyRect, ChoiceKeys[Mathf.Clamp(c.slot, 0, 3)], keyStyle);
                    label.normal.textColor = selected ? Color.black : Color.white;
                    GUI.Label(new Rect(x + 44*k, r.y, width - 48*k, r.height), c.label, label);
                    if (hover && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                    {
                        picked = c;
                        Event.current.Use();
                    }
                }
                var timer = new Rect(x, top + current.Length * row, width, 3f*k);
                Fill(timer, new Color(.08f,.10f,.11f,.8f));
                Fill(new Rect(timer.x, timer.y, width * progress, timer.height), accent);
            }

            if (phase == Phase.Outcome)
            {
                Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, .78f));
                var box = new Rect(W * .18f, H * .16f, W * .64f, H * .68f);
                GUILayout.BeginArea(box);
                var t = new GUIStyle(big); t.normal.textColor = success ? new Color(.45f, 1f, .6f) : new Color(1f, .4f, .35f);
                GUILayout.Label(shutdown ? shutdownTitle : success ? successTitle : failTitle, t);
                GUILayout.Label($"Итоговая вероятность успеха: {probability}%  ·  стресс: {Mathf.RoundToInt(stress)}", small);
                GUILayout.Space(16 * k);
                var body = new GUIStyle(sub) { alignment = TextAnchor.UpperCenter };
                GUILayout.Label(shutdown ? shutdownText : success ? successText : failText, body);
                if (success && !string.IsNullOrEmpty(grantTitle))
                {
                    GUILayout.Space(8 * k);
                    var g = new GUIStyle(small); g.normal.textColor = new Color(1f, .85f, .4f);
                    GUILayout.Label("Новая улика: " + grantTitle, g);
                }
                if (success && !string.IsNullOrEmpty(endingTag))
                {
                    GUILayout.Space(10 * k);
                    var e = new GUIStyle(big); e.normal.textColor = new Color(1f, .85f, .4f); e.fontSize = Mathf.RoundToInt(24 * k);
                    GUILayout.Label(endingTag, e);
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Вернуться в участок к доске улик  [Enter]", GUILayout.Height(36 * k))) leave = 0;
                GUILayout.EndArea();
                if (leave >= 0) Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, Mathf.Clamp01(leave)));
            }
        }

        static void Fill(Rect r, Color c) { var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = o; }
    }
}

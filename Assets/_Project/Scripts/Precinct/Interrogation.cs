using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using CarXCase.Harbor;
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
            [Tooltip("Order in the list of available answers, starting at zero.")] public int slot;
            public string label;
            [TextArea] public string heroLine;
            public int probability;
            public int stress;
            [Tooltip("Next node id, or END")] public string next = "END";
            [Tooltip("Clue or conclusion id needed to see this answer (empty = always).")] public string requires;
            [TextArea] public string reply;
        }
        [Serializable]
        public class Node
        {
            public string id;
            [TextArea] public string[] lines;
            public Choice[] choices;
            [Tooltip("Optional camera glance during the first line of this node.")]
            public Transform glanceTarget;
            [Min(0)] public float glanceStart;
        }

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
        [Header("Subtitle pacing")]
        [Min(1f)] public float subtitleDurationMultiplier = 1f;
        [Min(.3f)] public float minimumSubtitleHold = .3f;
        [Header("Trust and follow-up")]
        public bool useTrust;
        public int failureThreshold = 25;
        public bool interruptAtFailureThreshold;
        [TextArea] public string openingLine, partialReply, followUpThought, failureThought;
        public string followUpFlag, completedFlag;

        [Header("Outcome")]
        public string successTitle, failTitle, shutdownTitle = "ДОПРОС СОРВАН";
        [TextArea(3, 8)] public string successText, failText, shutdownText;
        public string endingTag;
        public string grantId, grantTitle;
        [TextArea] public string grantText;
        public string returnScene = "02_Precinct";
        public string travelLabel = "Вернуться в участок к доске улик";
        [Min(0f)] public float autoTravelDelay;
        public string recoveryAction, recoveryClueId, recoveryClueTitle;
        public bool revealHarborCode;
        [TextArea] public string recoveryClueText;
        [SerializeField] BarServiceRoute barServiceRoute;
        [SerializeField] PawnshopDeparture pawnshopDeparture;

        enum Phase { Idle, Arrival, Talking, Choosing, Outcome }
        Phase phase = Phase.Idle;
        string speaker, line; float lineLeft; bool skip;
        Choice[] current; float choiceLeft; Choice picked;
        int lastDelta; float deltaTime = -10;
        bool success, shutdown; float fade, leave = -1;
        float travelAt;
        bool recoveredLead;
        bool NeedsLead => !success && !recoveredLead && !string.IsNullOrEmpty(recoveryClueId);
        GUIStyle big, small;
        readonly CarXCase.UI.DialogueChoiceRows choiceRows = new();
        GUIStyle trustLabel, trustValue;
        Font displayFont;
        float displayedTrust;
        static readonly Color DialogueGold = new Color(.88f, .73f, .41f);
        static readonly Color DialogueCream = new Color(.99f, .95f, .82f);
        readonly CamSmoother smoother = new CamSmoother { blendTime = .6f, follow = .25f };
        Transform camTarget; float camSince;
        Transform glanceTarget;
        float glanceWeight;
        bool liveConversation;
        BarDmitryPerformance barPerformance;
        ConversationActing suspectActing;
        Vector3 explorationCameraPosition;
        Quaternion explorationCameraRotation;
        float explorationFov;

        public void Begin() => Begin(null);

        public void Begin(Transform listener)
        {
            if (phase != Phase.Idle || liveConversation) return;
            liveConversation = true;
            displayedTrust = probability;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            if (cam)
            {
                explorationCameraPosition = cam.transform.position;
                explorationCameraRotation = cam.transform.rotation;
                explorationFov = cam.fieldOfView;
                smoother.Apply(cam, explorationCameraPosition, explorationCameraRotation, explorationFov, 0);
            }
            barPerformance = suspect ? suspect.GetComponent<BarDmitryPerformance>() : null;
            suspectActing = suspect && !barPerformance ? suspect.GetComponentInChildren<ConversationActing>() : null;
            if (suspectActing) suspectActing.Engage(listener);
            StartCoroutine(Run(listener));
        }

        IEnumerator Run(Transform listener)
        {
            var node = nodes.FirstOrDefault();
            SetCam(camWide ? camWide : camOnSuspect);
            if (barPerformance && listener)
            {
                phase = Phase.Arrival;
                SetCam(camOnSuspect);
                barPerformance.Engage(listener);
                yield return barPerformance.TurnTo(listener);
            }
            else yield return new WaitForSeconds(0.6f);
            if (!string.IsNullOrEmpty(openingLine)) yield return SayLines(heroName, openingLine, false);
            while (node != null)
            {
                var lineHold = CarXCase.UI.VoiceOver.Spread(node.lines);
                for (int i = 0; i < node.lines.Length; i++)
                    yield return Say(suspectName, node.lines[i], true, i == 0 ? node.glanceTarget : null, node.glanceStart, lineHold[i]);
                current = node.choices.Where(c => string.IsNullOrEmpty(c.requires) || ClueJournal.Has(c.requires))
                    .OrderBy(c => c.slot).ToArray();
                if (current.Length == 0) break;
                picked = null; choiceLeft = choiceTime; phase = Phase.Choosing;
                SetCam(camOnSuspect);
                SetSuspectTalking(false);
                while (picked == null)
                {
                    choiceLeft -= Time.deltaTime;
                    if (choiceLeft <= 0) picked = current.OrderBy(c => c.probability).First(); // hesitation costs
                    yield return NoirMenu.GameSession.NextStoryFrame;
                }
                bool timedOut = choiceLeft <= 0;
                if (!timedOut) yield return new WaitForSeconds(.24f);
                HeroFace.SetEmotion(picked.stress >= 15 ? -0.9f : picked.stress <= -10 ? 0.6f : picked.probability < 0 ? -0.4f : 0.15f);
                Apply(timedOut && !useTrust ? picked.probability - 5 : picked.probability, picked.stress + (timedOut ? 5 : 0));
                // A recorded answer may carry the reply too: keep both subtitles in step with it.
                var exchange = CarXCase.UI.VoiceOver.Spread(new[] { timedOut ? "…" : picked.heroLine, picked.reply ?? "" });
                yield return SayLines(heroName, timedOut ? "…" : picked.heroLine, false, exchange[0]);
                if (!string.IsNullOrEmpty(picked.reply)) yield return SayLines(suspectName, picked.reply, true, exchange[1]);
                if (useTrust ? interruptAtFailureThreshold && probability <= failureThreshold : stress >= 100)
                {
                    shutdown = true;
                    break;
                }
                node = picked.next == "END" ? null : nodes.FirstOrDefault(n => n.id == picked.next);
            }
            success = !shutdown && probability >= successThreshold;
            if (pawnshopDeparture && listener && listener.TryGetComponent<OfficerController>(out var shopper))
            {
                bool failed = !success && probability <= failureThreshold;
                yield return SayLines(suspectName, success ? successText : failed ? shutdownText : partialReply, true);
                if (success)
                {
                    GrantClue();
                    yield return Say(suspectName, pawnshopDeparture.Farewell, true);
                }
                phase = Phase.Arrival;
                SetCam(pawnshopDeparture.Shot);
                EndPerformance();
                yield return pawnshopDeparture.Leave();
                ClueJournal.AddConclusion(followUpFlag);
                ClueJournal.AddConclusion(completedFlag);
                NoirMenu.GameSession.SaveCurrent();
                ResumeExploration(shopper, failed ? failureThought : followUpThought);
                yield break;
            }
            if (useTrust)
            {
                if (success) yield return SayLines(suspectName, successText, true);
                else if (listener && listener.TryGetComponent<OfficerController>(out var detective))
                {
                    bool failed = probability <= failureThreshold;
                    yield return SayLines(suspectName, failed ? shutdownText : partialReply, true);
                    if (failed && barServiceRoute)
                    {
                        phase = Phase.Arrival;
                        SetCam(barServiceRoute.DepartureShot);
                        yield return barServiceRoute.DmitryLeaves();
                    }
                    else ClueJournal.AddConclusion(followUpFlag);
                    ClueJournal.AddConclusion(completedFlag);
                    ResumeExploration(detective, failed ? failureThought : followUpThought);
                    yield break;
                }
            }
            if (success) GrantClue();
            if (useTrust && success) ClueJournal.AddConclusion(completedFlag);
            if (success && revealHarborCode)
                ClueJournal.Collect(HarborGate.CodeNote, "Код ворот пирса", "Дмитрий назвал код ворот: " + HarborGate.AccessCode + ".");
            if (!success && barServiceRoute && listener && listener.TryGetComponent<OfficerController>(out var player))
            {
                yield return Say(suspectName, barServiceRoute.DepartureLine, true);
                phase = Phase.Arrival;
                SetCam(barServiceRoute.DepartureShot);
                yield return barServiceRoute.DmitryLeaves();
                ResumeExploration(player, "Надо проверить, что он оставил у кассы.");
                yield break;
            }
            phase = Phase.Outcome;
            travelAt = Time.time + autoTravelDelay;
            SetCam(camWide ? camWide : camOnSuspect);
            EndPerformance();
            if (useTrust && success) BeginTravel();
        }

        void GrantClue()
        {
            if (string.IsNullOrEmpty(grantId)) return;
            ClueJournal.Collect(grantId, grantTitle, grantText);
            ClueJournal.AddConclusion(grantId);
        }

        void ResumeExploration(OfficerController player, string thought)
        {
            phase = Phase.Idle;
            EndPerformance();
            if (cam)
            {
                cam.transform.SetPositionAndRotation(explorationCameraPosition, explorationCameraRotation);
                cam.fieldOfView = explorationFov;
                if (cam.TryGetComponent<ThirdPersonCamera>(out var follow))
                {
                    follow.enabled = true;
                    follow.SetCursorLock(true);
                }
            }
            if (player.hud)
            {
                player.hud.enabled = true;
                if (!string.IsNullOrEmpty(thought)) player.hud.ShowInspectionComment(thought);
            }
            player.enabled = true;
            player.LockInput(false);
        }

        void Apply(int dProb, int dStress)
        {
            probability = Mathf.Clamp(probability + dProb, 0, useTrust ? 100 : 99);
            stress = Mathf.Clamp(stress + dStress, 0, 100);
            if (suspect) suspect.stress = stress;
            lastDelta = dProb; deltaTime = Time.time;
        }

        IEnumerator SayLines(string who, string text, bool isSuspect, float voiced = -1f)
        {
            var parts = (text ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
            // One recording can cover several subtitle lines: share its length out by line length.
            if (voiced < 0f && parts.Length > 1 && System.Array.TrueForAll(parts, p => CarXCase.UI.VoiceOver.Length(p.Trim()) > 0f))
            {
                foreach (var part in parts) yield return Say(who, part.Trim(), isSuspect);
                yield break;
            }
            float voice = voiced > 0f ? voiced : parts.Length > 0 ? CarXCase.UI.VoiceOver.Length(parts[0]) : 0f, total = 0f;
            foreach (var part in parts) total += part.Trim().Length;
            foreach (var part in parts)
                yield return Say(who, part.Trim(), isSuspect, null, 0f,
                    voice > 0f ? voice * part.Trim().Length / Mathf.Max(1f, total) + .15f : -1f);
        }

        IEnumerator Say(string who, string text, bool isSuspect, Transform glance = null, float glanceStart = 0f, float voiced = -1f)
        {
            phase = Phase.Talking; speaker = who; line = text; skip = false;
            lineLeft = Mathf.Clamp(text.Length * .055f, 2.2f, 7.5f) * Mathf.Max(1f, subtitleDurationMultiplier);
            // Hold the line for its recording (voiced < 0: look it up; 0: part of an earlier recording).
            if (voiced < 0f) voiced = CarXCase.UI.VoiceOver.Length(text) + .25f;
            if (voiced > 0f) lineLeft = Mathf.Max(lineLeft, voiced);
            SetCam(isSuspect ? camOnSuspect : camOnHero);
            // The speaker articulates the subtitle over the time it is shown; the other one listens.
            if (isSuspect)
            {
                HeroFace.StopHero();
                if (suspect) suspect.talking = true;
                if (barPerformance) barPerformance.Speak(text, lineLeft, glance, glanceStart);
                else if (suspectActing) suspectActing.Speak(text, lineLeft, glance, glanceStart);
            }
            else
            {
                if (suspect) suspect.talking = false;
                if (barPerformance) barPerformance.Listen(text);
                else if (suspectActing) suspectActing.Listen(text);
                if (text != "…") HeroFace.SpeakAsHero(text, lineLeft);
                else HeroFace.StopHero();
            }
            float minShow = Mathf.Max(.3f, minimumSubtitleHold);
            float duration = lineLeft;
            glanceTarget = glance;
            while (lineLeft > 0 && !(skip && minShow <= 0))
            {
                if (minimumSubtitleHold > .3f) skip = false;
                if (glanceTarget)
                {
                    float t = duration - lineLeft - glanceStart;
                    float turn = Mathf.SmoothStep(0f, 1f, t / .65f);
                    float back = Mathf.SmoothStep(0f, 1f, (t - 1.4f) / .7f);
                    glanceWeight = turn * (1f - back);
                }
                lineLeft -= Time.deltaTime;
                minShow -= Time.deltaTime;
                yield return NoirMenu.GameSession.NextStoryFrame;
            }
            glanceTarget = null;
            glanceWeight = 0f;
            // Line over or skipped: mouths close at once, the brother goes back to waiting.
            SetSuspectTalking(false);
            HeroFace.StopHero();
            line = null;
        }

        void SetSuspectTalking(bool talking)
        {
            if (suspect) suspect.talking = talking;
            if (talking) return;
            if (barPerformance) barPerformance.Wait();
            else if (suspectActing) suspectActing.Wait();
        }

        void EndPerformance()
        {
            HeroFace.StopHero();
            HeroFace.SetEmotion(0f);
            if (suspect) suspect.talking = false;
            if (barPerformance) barPerformance.EndConversation();
            else if (suspectActing) suspectActing.Disengage();
        }

        void OnDisable()
        {
            if (phase == Phase.Idle && !liveConversation) return;
            EndPerformance();
        }

        void SetCam(Transform a)
        {
            if (!a || a == camTarget) return;
            camTarget = a; camSince = Time.time;
        }

        void Update()
        {
            if (NoirMenu.GameSession.Paused) return;
            if (phase == Phase.Idle) return;
            displayedTrust = Mathf.MoveTowards(displayedTrust, probability, Time.deltaTime * 85f);
            if (cam && camTarget)
            {   // slow push-in on the current shot + gentle handheld drift, blended between shots
                float push = Mathf.Min(Time.time - camSince, 8f) * 0.012f;
                Vector3 p = camTarget.position + camTarget.forward * push + new Vector3(Mathf.Sin(Time.time * .6f), Mathf.Sin(Time.time * .8f + 1), 0) * .006f;
                Quaternion rotation = camTarget.rotation;
                if (glanceTarget && glanceWeight > 0f)
                {
                    Vector3 aim = glanceTarget.position + Vector3.up * .09f - p;
                    rotation = Quaternion.Slerp(rotation, Quaternion.LookRotation(aim), .9f * glanceWeight);
                }
                smoother.Apply(cam, p, rotation, phase == Phase.Arrival ? 52f : 40f, Time.deltaTime);
            }
            var kb = Keyboard.current; var mouse = Mouse.current;
            if (phase == Phase.Talking)
            {
                if ((kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) || (mouse != null && mouse.leftButton.wasPressedThisFrame)) skip = true;
            }
            else if (phase == Phase.Choosing && picked == null && kb != null)
            {
                for (int i = 0; i < Mathf.Min(current.Length, 4); i++)
                {
                    if (!kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame &&
                        !kb[(Key)((int)Key.Numpad1 + i)].wasPressedThisFrame) continue;
                    picked = current[i];
                    break;
                }
            }
            else if (phase == Phase.Outcome)
            {
                if (leave >= 0)
                {
                    leave += Time.deltaTime / 1.2f;
                    if (leave >= 1 && leave < 50)
                    {
                        leave = 99;
                        if (returnScene == "02_Precinct" || returnScene == "04_Precinct")
                            ClueJournal.SkipPrecinctIntro = true;
                        SceneManager.LoadSceneAsync(returnScene);
                    }
                }
                else if (NeedsLead)
                {
                    if (kb != null && kb.eKey.wasPressedThisFrame) RecoverLead();
                }
                else if ((autoTravelDelay > 0f && Time.time >= travelAt) ||
                         (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)))
                    BeginTravel();
            }
        }

        void RecoverLead()
        {
            if (!NeedsLead) return;
            ClueJournal.Collect(recoveryClueId, recoveryClueTitle, recoveryClueText);
            recoveredLead = true;
            travelAt = Time.time + autoTravelDelay;
        }

        void BeginTravel()
        {
            if (leave >= 0f || NeedsLead) return;
            if (!Application.CanStreamedLevelBeLoaded(returnScene))
            {
                Debug.LogError($"[Interrogation] Scene '{returnScene}' is missing from Build Profiles.");
                travelAt = float.PositiveInfinity;
                return;
            }
            leave = 0f;
        }

        // ------------------------------------------------------------------ GUI
        void OnGUI()
        {
            if (NoirMenu.GameSession.Paused) return;
            if (phase == Phase.Idle) return;
            if (useTrust && success && phase == Phase.Outcome && leave >= 0f)
            {
                Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, Mathf.Clamp01(leave)));
                return;
            }
            if (big == null)
            {
                displayFont = GameTypography.Face;
                big = new GUIStyle(GameTypography.Label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true };
                small = new GUIStyle(GameTypography.Label) { alignment = TextAnchor.MiddleCenter, wordWrap = true }; small.normal.textColor = new Color(.8f, .8f, .8f); small.Steady();
            }
            float k = Mathf.Clamp(Screen.height / 720f, .7f, 2f), W = Screen.width, H = Screen.height;
            big.fontSize = Mathf.RoundToInt(30 * k); small.fontSize = Mathf.RoundToInt(14 * k);

            // letterbox
            Fill(new Rect(0, 0, W, H * .06f), Color.black); Fill(new Rect(0, H * .94f, W, H * .06f), Color.black);

            bool recentChange = Time.time - deltaTime < 2.5f;
            bool showDelta = recentChange && lastDelta != 0;
            if (useTrust && (phase == Phase.Choosing || (phase == Phase.Talking && showDelta)))
            {
                DrawTrust(k, H);
            }
            else if (!useTrust && (phase == Phase.Choosing || (phase == Phase.Talking && recentChange)))
            {
                // probability + stress (top-left, Detroit style)
                var panel = new Rect(18 * k, H * .07f, 250 * k, 104 * k);
                Fill(panel, new Color(0, 0, 0, .55f));
                small.alignment = TextAnchor.MiddleLeft; small.normal.textColor = new Color(.55f, .85f, 1f); small.Steady();
                GUI.Label(new Rect(panel.x + 12 * k, panel.y + 6 * k, panel.width, 20 * k), "ВЕРОЯТНОСТЬ УСПЕХА", small);
                big.alignment = TextAnchor.MiddleLeft; big.normal.textColor = Color.white; big.Steady();
                GUI.Label(new Rect(panel.x + 12 * k, panel.y + 24 * k, 120 * k, 36 * k), probability + "%", big);
                if (showDelta)
                {
                    var arrow = new GUIStyle(big); arrow.normal.textColor = lastDelta > 0 ? new Color(.4f, 1f, .5f) : new Color(1f, .35f, .3f); arrow.Steady();
                    arrow.fontSize = Mathf.RoundToInt(22 * k);
                    GUI.Label(new Rect(panel.x + 110 * k, panel.y + 26 * k, 120 * k, 32 * k), (lastDelta > 0 ? "▲ +" : "▼ ") + lastDelta, arrow);
                }
                GUI.Label(new Rect(panel.x + 12 * k, panel.y + 62 * k, panel.width, 18 * k), "СТРЕСС · " + suspectName, small);
                var bar = new Rect(panel.x + 12 * k, panel.y + 84 * k, panel.width - 24 * k, 8 * k);
                Fill(bar, new Color(1, 1, 1, .15f));
                Fill(new Rect(bar.x, bar.y, bar.width * stress / 100f, bar.height), Color.Lerp(new Color(.4f, .8f, 1f), new Color(1f, .25f, .2f), stress / 100f));
                small.alignment = TextAnchor.MiddleCenter; small.normal.textColor = new Color(.8f, .8f, .8f); small.Steady();
                big.alignment = TextAnchor.MiddleCenter;
            }

            if (phase == Phase.Talking && !string.IsNullOrEmpty(line))
            {
                CarXCase.UI.Subtitles.Draw(CarXCase.UI.Subtitles.WithSpeaker(speaker, line));
            }

            if (phase == Phase.Choosing)
            {
                float width = Mathf.Min(282f * k, W * .43f);
                float margin = Mathf.Min(29f * k, W * .04f);
                float row = Mathf.Min(47f * k, H * .092f);
                Vector3 face = cam && suspect
                    ? cam.WorldToViewportPoint(suspect.transform.position + Vector3.up * 1.55f)
                    : new Vector3(.5f, .5f, 1f);
                // Place the prompts beside the interviewee, with room for every available line.
                bool onRight = face.x <= .56f;
                float x = onRight ? Mathf.Max(W * .55f, face.x * W + margin) :
                    Mathf.Min(W * .45f - width, face.x * W - margin - width);
                x = Mathf.Clamp(x, margin, W - width - margin);
                float top = Mathf.Clamp((1f - face.y) * H - row * .45f,
                    H * .29f, H * .76f - row * current.Length);
                float progress = Mathf.Clamp01(choiceLeft / Mathf.Max(choiceTime, .01f));
                for (int i = 0; i < current.Length; i++)
                {
                    Choice c = current[i];
                    float rowWidth = choiceRows.Measure(c.label, k, width);
                    var r = new Rect(onRight ? x + width - rowWidth : x, top + i * row, rowWidth, row - 6f * k);
                    bool hover = picked == null && r.Contains(Event.current.mousePosition);
                    choiceRows.Draw(r, c.label, i + 1, k, picked == c || hover);
                    if (hover && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                    {
                        picked = c;
                        Event.current.Use();
                    }
                }
                float timerWidth = Mathf.Min(width, 108f * k);
                var timer = new Rect(onRight ? x + width - timerWidth : x, top + current.Length * row + 2f * k, timerWidth, 2f * k);
                Fill(timer, new Color(.07f, .06f, .04f, .88f));
                Fill(new Rect(timer.x, timer.y, timer.width * progress, timer.height), DialogueGold);
            }

            if (phase == Phase.Outcome)
            {
                Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, .78f));
                var box = new Rect(W * .18f, H * .16f, W * .64f, H * .68f);
                GUILayout.BeginArea(box);
                var t = new GUIStyle(big); t.normal.textColor = success ? new Color(.45f, 1f, .6f) : new Color(1f, .4f, .35f); t.Steady();
                GUILayout.Label(shutdown ? shutdownTitle : success ? successTitle : failTitle, t);
                GUILayout.Label($"Итоговая вероятность успеха: {probability}%  ·  стресс: {Mathf.RoundToInt(stress)}", small);
                GUILayout.Space(16 * k);
                var body = new GUIStyle(GameTypography.Label) { wordWrap = true, alignment = TextAnchor.UpperCenter };
                GUILayout.Label(shutdown ? shutdownText : success ? successText : failText, body);
                if (success && revealHarborCode)
                    GUILayout.Label("Дмитрий: код ворот — " + HarborGate.AccessCode + ".", body);
                if (success && !string.IsNullOrEmpty(grantTitle))
                {
                    GUILayout.Space(8 * k);
                    var g = new GUIStyle(small); g.normal.textColor = new Color(1f, .85f, .4f); g.Steady();
                    GUILayout.Label("Новая улика: " + grantTitle, g);
                }
                if (success && !string.IsNullOrEmpty(endingTag))
                {
                    GUILayout.Space(10 * k);
                    var e = new GUIStyle(big); e.normal.textColor = new Color(1f, .85f, .4f); e.Steady(); e.fontSize = Mathf.RoundToInt(24 * k);
                    GUILayout.Label(endingTag, e);
                }
                if (recoveredLead)
                {
                    GUILayout.Space(12 * k);
                    GUILayout.Label(recoveryClueText, body);
                }
                GUILayout.FlexibleSpace();
                if (NeedsLead)
                {
                    if (GUILayout.Button(recoveryAction + "  [E]", GameTypography.Button, GUILayout.Height(36 * k))) RecoverLead();
                }
                else if (GUILayout.Button(travelLabel + "  [Enter]", GameTypography.Button, GUILayout.Height(36 * k))) BeginTravel();
                GUILayout.EndArea();
                if (leave >= 0) Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, Mathf.Clamp01(leave)));
            }
        }

        void DrawTrust(float scale, float screenHeight)
        {
            if (trustLabel == null)
            {
                trustLabel = new GUIStyle(GameTypography.Label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(),
                    wordWrap = false,
                    normal = { textColor = DialogueCream }
                };
                if (displayFont) trustLabel.font = displayFont;
                trustValue = new GUIStyle(trustLabel)
                {
                    normal = { textColor = DialogueGold }
                };
                trustLabel.Steady(); trustValue.Steady();
            }

            trustLabel.fontSize = Mathf.RoundToInt(18f * scale);
            trustValue.fontSize = Mathf.RoundToInt(42f * scale);
            float left = 26f * scale;
            float top = screenHeight * .09f;
            var valueRect = new Rect(left, top, 110f * scale, 50f * scale);
            string value = Mathf.RoundToInt(displayedTrust).ToString();
            float numberWidth = trustValue.CalcSize(new GUIContent(value)).x;
            var percentRect = new Rect(left + numberWidth + 3f * scale, top + 10f * scale, 20f * scale, 30f * scale);
            const string caption = "УРОВЕНЬ ДОВЕРИЯ";
            float captionWidth = trustLabel.CalcSize(new GUIContent(caption)).x + 2f * scale;
            var labelRect = new Rect(valueRect.xMax + 2f * scale, top + 10f * scale, captionWidth, 26f * scale);
            DrawWithShadow(valueRect, value, trustValue);
            DrawWithShadow(percentRect, "%", trustLabel);
            DrawWithShadow(labelRect, caption, trustLabel);

            var track = new Rect(left, valueRect.yMax + 3f * scale, 132f * scale, Mathf.Max(2f, 2f * scale));
            Fill(track, new Color(.04f, .03f, .02f, .82f));
            Fill(new Rect(track.x, track.y, track.width * Mathf.Clamp01(displayedTrust / 100f), track.height), DialogueGold);
        }

        static void DrawWithShadow(Rect rect, string text, GUIStyle style)
        {
            var ink = style.normal.textColor;
            style.normal.textColor = new Color(.06f, .04f, .025f, .95f); style.Steady();
            GUI.Label(new Rect(rect.x + 1f, rect.y + 2f, rect.width, rect.height), text, style);
            style.normal.textColor = ink; style.Steady();
            GUI.Label(rect, text, style);
        }

        static void Fill(Rect r, Color c) { var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = o; }
    }
}

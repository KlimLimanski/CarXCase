using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using CarXCase.Hero;

namespace CarXCase.Precinct
{
    /// Small data-driven cutscene: the hero walks a path, the camera cuts between shots,
    /// subtitles play, then onFinished fires (used to start an interrogation).
    public sealed class ScriptedCutscene : MonoBehaviour
    {
        [Serializable]
        public class Shot
        {
            public float start;
            [Tooltip("Fixed camera transform. Leave empty to follow the hero with 'followOffset'.")]
            public Transform anchor;
            public Vector3 followOffset = new Vector3(0, 1.7f, -1.6f);
            [Tooltip("What to look at. Empty = hero's head.")]
            public Transform lookAt;
            public float fov = 50;
        }
        [Serializable] public class Line { public float start, end; public string speaker; [TextArea] public string text; }

        public Transform hero;
        public Camera shotCamera;
        public Transform[] path;
        public float walkSpeed = 1.5f, walkStart = 1.5f, tail = 1.5f;
        public Transform door; public float doorAngle = -95; public float doorOpenAt = 0.3f;
        public Shot[] shots;
        public Line[] dialogue;
        public string title, subtitle;
        public string heroName = "АЛЕКСЕЙ";
        public NpcActor npc;
        [Tooltip("Seconds to accelerate / decelerate the walk.")] public float ease = 0.8f;
        public float cameraBlend = 1.1f;
        public UnityEvent onFinished = new UnityEvent();

        public float WalkTime => PathLength / Mathf.Max(walkSpeed, .01f) + ease;
        public float Duration => walkStart + WalkTime + tail;
        public float PathLength
        {
            get { float l = 0; for (int i = 1; i < path.Length; i++) l += Flat(path[i].position - path[i - 1].position).magnitude; return l; }
        }
        public bool Finished { get; private set; }

        float elapsed, fade = 1;
        Quaternion doorClosed; bool doorInit;
        GUIStyle sub, spk, big, small;
        readonly CamSmoother smoother = new CamSmoother();
        bool live;

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);

        void Start() { FitDialogueToVoice(); Evaluate(0); }

        /// A recorded line holds until its recording ends; later lines move back and the cutscene grows,
        /// so voices never overlap or get cut off.
        void FitDialogueToVoice()
        {
            if (dialogue == null || dialogue.Length == 0) return;
            var ordered = (Line[])dialogue.Clone();
            System.Array.Sort(ordered, (a, b) => a.start.CompareTo(b.start));
            var hold = CarXCase.UI.VoiceOver.Spread(System.Array.ConvertAll(ordered, l => l.text));
            float shift = 0f;
            for (int i = 0; i < ordered.Length; i++)
            {
                ordered[i].start += shift;
                ordered[i].end += shift;
                if (hold[i] <= 0f) continue;
                float missing = ordered[i].start + hold[i] - ordered[i].end;
                if (missing <= 0f) continue;
                ordered[i].end += missing;
                shift += missing;
            }
            tail += shift;
        }

        void Update()
        {
            if (NoirMenu.GameSession.Paused) return;
            if (Finished) return;
            fade = Mathf.MoveTowards(fade, 0, Time.deltaTime / 1.2f);
            var kb = Keyboard.current;
            if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)) elapsed = Duration;
            elapsed += Time.deltaTime;
            live = true; Evaluate(elapsed); live = false;
            Line cur = null;
            if (dialogue != null) foreach (var l in dialogue) if (elapsed >= l.start && elapsed < l.end) { cur = l; break; }
            HeroFace.SetTalking(cur != null && cur.speaker == heroName);
            if (npc) npc.talking = cur != null && cur.speaker != heroName;
            if (elapsed >= Duration) { Finished = true; HeroFace.SetTalking(false); if (npc) npc.talking = false; onFinished.Invoke(); }
        }

        /// Walk distance with an accelerate / cruise / decelerate profile.
        public float Distance(float tau)
        {
            float L = PathLength, v = Mathf.Max(walkSpeed, .01f), a = Mathf.Max(ease, .01f), T = WalkTime;
            if (tau <= 0) return 0;
            if (tau >= T) return L;
            if (tau < a) return .5f * v / a * tau * tau;
            if (tau > T - a) return L - .5f * v / a * (T - tau) * (T - tau);
            return .5f * v * a + v * (tau - a);
        }

        public Vector3 PointAt(float s, out Vector3 dir)
        {
            dir = path[path.Length - 1].forward;
            for (int i = 1; i < path.Length; i++)
            {
                float seg = Flat(path[i].position - path[i - 1].position).magnitude;
                if (s <= seg || i == path.Length - 1)
                {
                    float k = seg < 1e-4f ? 1 : Mathf.Clamp01(s / seg);
                    if (k < 1) dir = Flat(path[i].position - path[i - 1].position).normalized;
                    return Vector3.Lerp(path[i - 1].position, path[i].position, k);
                }
                s -= seg;
            }
            return path[path.Length - 1].position;
        }

        public void Evaluate(float t)
        {
            if (!hero || path == null || path.Length < 2) return;
            if (door && !doorInit) { doorClosed = door.localRotation; doorInit = true; }
            float s = Distance(t - walkStart);
            hero.position = PointAt(s, out var dir);
            // smooth the heading a little around corners
            PointAt(Mathf.Min(s + 0.5f, PathLength), out var dir2);
            var face = (dir + dir2); face.y = 0;
            if (s >= PathLength - 0.01f) face = path[path.Length - 1].forward;
            if (face.sqrMagnitude > 1e-4f)
            {
                var want = Quaternion.LookRotation(face.normalized);
                hero.rotation = live ? Quaternion.Slerp(hero.rotation, want, 1 - Mathf.Exp(-Time.deltaTime / 0.22f)) : want;
            }
            if (door) door.localRotation = doorClosed * Quaternion.Euler(0, doorAngle * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(doorOpenAt, doorOpenAt + 1.2f, t)), 0);

            if (!shotCamera || shots == null || shots.Length == 0) return;
            Shot shot = shots[0];
            foreach (var sh in shots) if (t >= sh.start) shot = sh;
            Vector3 head = hero.position + Vector3.up * 1.55f;
            Vector3 pos = shot.anchor ? shot.anchor.position : hero.position + hero.rotation * shot.followOffset;
            Vector3 look = shot.lookAt ? shot.lookAt.position : head;
            if (!shot.anchor && !shot.lookAt) look = head + hero.forward * 1.2f;
            pos += new Vector3(Mathf.Sin(t * 0.9f), Mathf.Sin(t * 1.3f + 1), 0) * 0.008f;
            var r = Quaternion.LookRotation(look - pos);
            if (live) { smoother.blendTime = cameraBlend; smoother.Apply(shotCamera, pos, r, shot.fov, Time.deltaTime); }
            else { shotCamera.transform.SetPositionAndRotation(pos, r); shotCamera.fieldOfView = shot.fov; }
        }

        void OnGUI()
        {
            if (NoirMenu.GameSession.Paused) return;
            if (Finished) return;
            if (sub == null)
            {
                sub = new GUIStyle(GameTypography.Label) { alignment = TextAnchor.MiddleCenter, wordWrap = true }; sub.normal.textColor = Color.white; sub.Steady();
                spk = new GUIStyle(sub); spk.normal.textColor = new Color(1, .82f, .51f); spk.Steady();
                big = new GUIStyle(sub) { font = GameTypography.Face, fontStyle = FontStyle.Bold };
                small = new GUIStyle(sub) { font = GameTypography.Face, alignment = TextAnchor.MiddleRight };
            }
            float k = Mathf.Clamp(Screen.height / 720f, .65f, 2);
            sub.fontSize = Mathf.RoundToInt(21 * k); spk.fontSize = Mathf.RoundToInt(16 * k);
            big.fontSize = Mathf.RoundToInt(28 * k); small.fontSize = Mathf.RoundToInt(14 * k);
            Fill(new Rect(0, 0, Screen.width, Screen.height * .08f), Color.black);
            Fill(new Rect(0, Screen.height * .92f, Screen.width, Screen.height * .08f), Color.black);
            if (elapsed < 4.5f && !string.IsNullOrEmpty(title))
            {
                GUI.Label(new Rect(0, Screen.height * .13f, Screen.width, 44 * k), title, big);
                GUI.Label(new Rect(0, Screen.height * .13f + 44 * k, Screen.width, 30 * k), subtitle, new GUIStyle(GameTypography.Label) { fontSize = sub.fontSize, alignment = TextAnchor.MiddleCenter, wordWrap = true });
            }
            if (dialogue != null)
                foreach (var l in dialogue)
                    if (elapsed >= l.start && elapsed < l.end)
                    {
                        CarXCase.UI.Subtitles.Draw(CarXCase.UI.Subtitles.WithSpeaker(l.speaker, l.text), .76f);
                        break;
                    }
            if (fade > 0) Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, fade));
        }

        static void Fill(Rect r, Color c) { var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = o; }
    }
}

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using DetectiveGame.Core;
using CarXCase.Hero;

namespace CarXCase.Precinct
{
    /// Entry cutscene: the front doors open, Alexey walks through the lobby and the bullpen
    /// into his office, then control is handed to the player.
    public sealed class PrecinctIntroCutscene : MonoBehaviour
    {
        [Serializable]
        public class Line { public float start, end; public string speaker; [TextArea] public string text; }

        public Transform player;
        public Camera shotCamera;
        public Transform[] path;
        public Transform frontDoorLeft, frontDoorRight, officeDoor;
        [Tooltip("Fixed camera inside the lobby for the opening shot.")]
        public Transform establishingShot;
        public float walkSpeed = 1.7f;
        public float walkStart = 2.2f;
        public Line[] dialogue;
        public string title = "13-Й УЧАСТОК · БРУКЛИН";
        public string subtitle = "Тот же вечер";

        [Header("Emerging from darkness")]
        [Tooltip("Seconds over which the frame opens up from the night outside (after the black fade).")]
        public float darknessTime = 4.2f;
        [Tooltip("Exposure (EV) at the very start: the doorway reads as black, Alexey as a silhouette.")]
        public float startExposure = -2.6f;
        [Range(0f, 1f)] public float startVignette = .5f;
        Volume reveal;
        VolumeProfile revealProfile;

        public bool Finished { get; private set; }
        public float ease = 0.8f;
        readonly CamSmoother smoother = new CamSmoother();
        bool live;
        int lastShot = -1;
        float elapsed, fadeIn = 1, fadeOut;
        float[] cumulative;
        Quaternion leftClosed, rightClosed, officeClosed;
        GUIStyle sub, speaker, small, big;

        public float TotalLength
        {
            get { Prepare(); return cumulative[cumulative.Length - 1]; }
        }
        public float Duration => walkStart + TotalLength / walkSpeed + ease + 1.2f;

        void Prepare()
        {
            if (cumulative != null && cumulative.Length == path.Length) return;
            cumulative = new float[path.Length];
            for (int i = 1; i < path.Length; i++)
                cumulative[i] = cumulative[i - 1] + Vector3.Distance(Flat(path[i - 1].position), Flat(path[i].position));
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);

        public Vector3 PointAt(float s)
        {
            Prepare();
            s = Mathf.Clamp(s, 0, cumulative[cumulative.Length - 1]);
            for (int i = 1; i < path.Length; i++)
                if (s <= cumulative[i] || i == path.Length - 1)
                {
                    float seg = Mathf.Max(cumulative[i] - cumulative[i - 1], 1e-4f);
                    return Vector3.Lerp(path[i - 1].position, path[i].position, (s - cumulative[i - 1]) / seg);
                }
            return path[path.Length - 1].position;
        }

        void Awake()
        {
            if (frontDoorLeft) leftClosed = frontDoorLeft.localRotation;
            if (frontDoorRight) rightClosed = frontDoorRight.localRotation;
            if (officeDoor) officeClosed = officeDoor.localRotation;
        }

        void Start()
        {
            SetupReveal();
            SetGameplay(false);
            Evaluate(0);
            if (ClueJournal.SkipPrecinctIntro)
            {   // coming back from a location: straight to the office
                ClueJournal.SkipPrecinctIntro = false;
                elapsed = Duration; Finish();
            }
        }

        void SetGameplay(bool on)
        {
            if (!player) return;
            var cc = player.GetComponent<CharacterController>(); if (cc) cc.enabled = on;
            var officer = player.GetComponent<OfficerController>(); if (officer) officer.enabled = on;
            var follow = shotCamera ? shotCamera.GetComponent<ThirdPersonCamera>() : null;
            if (follow)
            {
                follow.enabled = on;
                if (on) follow.SetCursorLock(true);
            }
            foreach (var hud in FindObjectsByType<SimpleUIHUD>(FindObjectsSortMode.None)) hud.enabled = on;
        }

        void Update()
        {
            if (Finished)
            {
                fadeIn = Mathf.MoveTowards(fadeIn, 0, Time.deltaTime / 0.8f);
                if (fadeIn <= 0) enabled = false;
                return;
            }
            fadeIn = Mathf.MoveTowards(fadeIn, 0, Time.deltaTime / 1.5f);
            if (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame))
                elapsed = Mathf.Max(elapsed, Duration - 1.0f);
            elapsed += Time.deltaTime;
            live = true; Evaluate(elapsed); live = false;
            UpdateReveal(elapsed);
            Line cur = null;
            if (dialogue != null) foreach (var l in dialogue) if (elapsed >= l.start && elapsed < l.end) { cur = l; break; }
            HeroFace.SetTalking(cur != null && cur.speaker == "АЛЕКСЕЙ");
            if (elapsed > Duration - 1.0f) fadeOut = Mathf.InverseLerp(Duration - 1.0f, Duration, elapsed);
            if (elapsed >= Duration) Finish();
        }

        /// A temporary global volume: low exposure and a heavy vignette that lift as he steps in from the dark.
        void SetupReveal()
        {
            if (reveal) return;
            revealProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            var exposure = revealProfile.Add<ColorAdjustments>(true);
            exposure.postExposure.value = startExposure;
            var vignette = revealProfile.Add<Vignette>(true);
            vignette.intensity.value = startVignette;
            vignette.smoothness.value = .6f;
            vignette.color.value = Color.black;
            var go = new GameObject("Intro_Darkness_Reveal");
            go.transform.SetParent(transform, false);
            reveal = go.AddComponent<Volume>();
            reveal.isGlobal = true;
            reveal.priority = 100;
            reveal.sharedProfile = revealProfile;
            reveal.weight = 1f;
        }

        void UpdateReveal(float t)
        {
            if (!reveal) return;
            // Starts lifting as the doors part and he steps over the threshold.
            float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(walkStart - .8f, walkStart + darknessTime, t));
            reveal.weight = 1f - k;
        }

        void EndReveal()
        {
            if (reveal) Destroy(reveal.gameObject);
            if (revealProfile) Destroy(revealProfile);
            reveal = null;
        }

        void OnDestroy() => EndReveal();

        void Finish()
        {
            EndReveal();
            Evaluate(Duration);
            HeroFace.SetTalking(false);
            Finished = true;
            fadeOut = 0; fadeIn = 1;
            // put the gameplay camera behind the hero
            if (shotCamera && player)
            {
                var follow = shotCamera.GetComponent<ThirdPersonCamera>();
                float dist = follow ? follow.distance : 1.6f;
                Vector3 off = follow ? follow.offset : new Vector3(0, 1.45f, 0);
                var rot = Quaternion.Euler(12, player.eulerAngles.y, 0);
                shotCamera.transform.SetPositionAndRotation(player.position + off - rot * Vector3.forward * dist, rot);
            }
            SetGameplay(true);
        }

        /// Poses hero, doors and camera for time t (seconds). Also used by the editor for previews.
        public void Evaluate(float t)
        {
            if (!player || path == null || path.Length < 2) return;
            Prepare();
            float total = cumulative[cumulative.Length - 1];
            float tau = t - walkStart, v = walkSpeed, a = Mathf.Max(ease, .01f), T = total / v + a;
            float s = tau <= 0 ? 0 : tau >= T ? total : tau < a ? .5f * v / a * tau * tau : tau > T - a ? total - .5f * v / a * (T - tau) * (T - tau) : .5f * v * a + v * (tau - a);
            Vector3 pos = PointAt(s);
            Vector3 ahead = PointAt(s + 0.7f) - PointAt(Mathf.Max(0, s - 0.1f));
            if (s >= total - 0.01f) ahead = path[path.Length - 1].forward;
            ahead.y = 0;
            player.position = pos;
            if (ahead.sqrMagnitude > 1e-4f)
            {
                var want = Quaternion.LookRotation(ahead.normalized, Vector3.up);
                player.rotation = live ? Quaternion.Slerp(player.rotation, want, 1 - Mathf.Exp(-Time.deltaTime / 0.22f)) : want;
            }

            float front = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(walkStart - 1.2f, walkStart + 0.4f, t));
            if (frontDoorLeft) frontDoorLeft.localRotation = leftClosed * Quaternion.Euler(0, -80 * front, 0);
            if (frontDoorRight) frontDoorRight.localRotation = rightClosed * Quaternion.Euler(0, 80 * front, 0);
            float officeOpen = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(total - 4.6f, total - 3.2f, s));
            // Swing shut during the pause after Alexey reaches the last waypoint.
            float officeClose = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((tau - T) / 0.85f));
            if (officeDoor) officeDoor.localRotation = officeClosed * Quaternion.Euler(0, -95f * officeOpen * (1f - officeClose), 0);

            if (!shotCamera) return;
            Vector3 head = pos + Vector3.up * 1.55f, chest = pos + Vector3.up * 1.25f;
            Vector3 fwd = player.forward, right = player.right;
            Vector3 camPos, look; float fov; int shot;
            float doorS = Vector3.Distance(Flat(path[0].position), Vector3.zero);
            float turnS = cumulative[Mathf.Max(1, path.Length - 3)];
            float sA = doorS + 3.0f, sB = sA + 4.2f, sC = sB + 4.0f, sD = turnS - 0.6f, sE = turnS + 2.4f;
            if (s < sA && establishingShot)
            {   // A: lobby, slow push-in as the doors open onto daylight and he steps inside
                shot = 0;
                float k = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, walkStart + 3.5f, t));
                Vector3 toDoor = Flat(Vector3.zero - establishingShot.position).normalized;
                camPos = establishingShot.position + toDoor * (0.55f * k) + Vector3.down * (0.12f * k);
                look = Vector3.Lerp(new Vector3(0, 1.5f, 0), chest, Mathf.Clamp01(s / 3f)); fov = Mathf.Lerp(46, 40, k);
            }
            else if (s < sB)
            {   // B: low reverse dolly ahead of him through the lobby, looking slightly up
                shot = 1;
                float k = Mathf.InverseLerp(sA, sB, s);
                camPos = pos + fwd * Mathf.Lerp(2.1f, 2.5f, k) + right * 0.55f + Vector3.up * 0.8f;
                look = head + Vector3.up * 0.05f; fov = 38;
            }
            else if (s < sC)
            {   // C: tight profile track along the aisle, desks sliding through the foreground
                shot = 2;
                float k = Mathf.InverseLerp(sB, sC, s);
                camPos = new Vector3(pos.x - 1.25f, 1.15f + 0.1f * k, pos.z - 0.55f + 0.35f * k);
                look = chest + fwd * 0.8f; fov = 50;
            }
            else if (s < sD)
            {   // D: high wide from the corner of the bullpen, drifting as he passes the talkers
                shot = 3;
                float k = Mathf.InverseLerp(sC, sD, s);
                camPos = new Vector3(5.6f - 1.1f * k, 2.8f - 0.15f * k, 17.35f);
                look = Vector3.Lerp(chest, head, k); fov = Mathf.Lerp(46, 40, k);
            }
            else if (s < sE)
            {   // E: close three-quarter on his face as he turns towards the office
                shot = 4;
                camPos = pos + fwd * 1.45f - right * 0.45f + Vector3.up * 1.52f;
                look = head + Vector3.down * 0.04f; fov = 34;
            }
            else
            {   // F: over the shoulder, pushing into the office
                shot = 5;
                float k = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(sE, total, s));
                camPos = pos - fwd * Mathf.Lerp(1.5f, 1.2f, k) + right * 0.36f + Vector3.up * Mathf.Lerp(1.74f, 1.62f, k);
                look = pos + fwd * 3f + Vector3.up * 1.32f; fov = Mathf.Lerp(54, 48, k);
            }
            // handheld breathing: slow drift plus a faint roll
            float n1 = Mathf.PerlinNoise(t * 0.55f, 1.3f) - .5f, n2 = Mathf.PerlinNoise(t * 0.6f, 7.1f) - .5f, n3 = Mathf.PerlinNoise(t * 0.4f, 3.7f) - .5f;
            var rot = Quaternion.LookRotation(look - camPos, Vector3.up) * Quaternion.Euler(n2 * 0.8f, n1 * 0.8f, n3 * 1.1f);
            camPos += rot * new Vector3(n1, n2, 0) * 0.03f;
            if (live)
            {
                // hard cut between set-ups, smooth follow within one
                if (shot != lastShot) smoother.Reset();
                lastShot = shot;
                smoother.Apply(shotCamera, camPos, rot, fov, Time.deltaTime);
            }
            else { shotCamera.transform.SetPositionAndRotation(camPos, rot); shotCamera.fieldOfView = fov; }
        }

        void OnGUI()
        {
            if (Finished)
            {
                if (fadeIn > 0) Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, fadeIn));
                return;
            }
            if (sub == null)
            {
                sub = new GUIStyle(GameTypography.Label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
                sub.normal.textColor = Color.white;
                speaker = new GUIStyle(sub); speaker.normal.textColor = new Color(1, .82f, .51f);
                small = new GUIStyle(sub) { font = GameTypography.Face, alignment = TextAnchor.MiddleRight };
                big = new GUIStyle(sub) { font = GameTypography.Face, fontStyle = FontStyle.Bold };
            }
            float k = Mathf.Clamp(Screen.height / 720f, .65f, 2);
            sub.fontSize = Mathf.RoundToInt(22 * k); speaker.fontSize = Mathf.RoundToInt(16 * k);
            small.fontSize = Mathf.RoundToInt(14 * k); big.fontSize = Mathf.RoundToInt(28 * k);
            Fill(new Rect(0, 0, Screen.width, Screen.height * .08f), Color.black);
            Fill(new Rect(0, Screen.height * .92f, Screen.width, Screen.height * .08f), Color.black);
            if (elapsed < 5)
            {
                GUI.Label(new Rect(0, Screen.height * .13f, Screen.width, 44 * k), title, big);
                GUI.Label(new Rect(0, Screen.height * .13f + 44 * k, Screen.width, 30 * k), subtitle, new GUIStyle(GameTypography.Label) { fontSize = sub.fontSize, alignment = TextAnchor.MiddleCenter, wordWrap = true });
            }
            if (dialogue != null)
                foreach (var l in dialogue)
                    if (elapsed >= l.start && elapsed < l.end)
                    {
                        CarXCase.UI.Subtitles.Draw(l.text, .76f);
                        break;
                    }
            float f = Mathf.Max(fadeIn, fadeOut);
            if (f > 0) Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, f));
        }

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color; GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = old;
        }
    }
}

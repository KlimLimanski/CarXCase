using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using CarXCase.Hero;
using NoirMenu;

namespace CarXCase.Intro
{
    public sealed class BrightonDriveCutscene : MonoBehaviour
    {
        [Serializable]
        public class DialogueLine
        {
            public float start, end;
            public string speaker;
            [TextArea(2, 4)] public string text;
        }

        [Serializable]
        public class OncomingVehicle
        {
            public Transform root;
            public float spawnTime;
            public float speed;
            public float startZ;
            public float endZ;
            [NonSerialized] public AudioSource approachAudio;
            [NonSerialized] public AudioSource passAudio;
        }

        [Header("Timing and destination")]
        [Min(10)] public float duration = 150;
        public string destinationScene = "FatherApartment";
        public Transform car, cameraRig;
        public Transform[] wheels;
        public float roadDistance = 600;

        [Header("Oncoming Traffic")]
        public List<OncomingVehicle> oncomingVehicles = new List<OncomingVehicle>();

        [Header("Hero car audio")]
        [SerializeField, Range(0, 1)] float engineVolume = .65f;

        [Header("Recorded dialogue")]
        [SerializeField] AudioClip dialogueClip;
        [SerializeField, Min(0)] float dialogueDelay = 4f;
        [Tooltip("Subtitle times are in seconds from the start of the recording.")]
        public DialogueLine[] dialogue;

        [Header("Interior props")]
        [SerializeField] Transform hangingCross;

        const float PassPeakSeconds = 1.7f;
        const float BlackHold = 2.5f, TitleHold = 2f;
        const float PreludeDuration = BlackHold + 2 * TitleHold;
        const float ExitFadeDuration = 2.5f;
        AudioClip approachClip, passClip;
        AudioSource music, engine, dialogueSource;
        AudioLowPassFilter musicFilter;
        float preludeElapsed, elapsed, skipFade, previousZ;
        float musicLevel = .18f, engineLevel;
        bool loading, skipping, dialogueStarted;
        DialogueLine currentLine;
        Quaternion crossRest;
        string loadError;
        Vector3 cameraStart;
        Quaternion cameraRotation;
        Camera shotCamera;
        float initialFov;

        [Header("Cinematic camera")]
        public bool dynamicCamera = true;
        [Range(0.1f, 1f)] public float cameraMotionAmount = 1f;
        GUIStyle subtitleStyle, speakerStyle, smallStyle, titleStyle;

        void Start()
        {
            if (cameraRig != null)
            {
                cameraStart = cameraRig.localPosition;
                cameraRotation = cameraRig.localRotation;
                shotCamera = cameraRig.GetComponent<Camera>();
                initialFov = shotCamera ? shotCamera.fieldOfView : 64f;
            }
            ApplyPose(0);
            if (hangingCross) crossRest = hangingCross.localRotation;
            previousZ = car != null ? car.position.z : 0;
            StartEngineAudio();
            if (dialogueClip)
            {
                dialogueSource = gameObject.AddComponent<AudioSource>();
                dialogueSource.playOnAwake = false;
                dialogueSource.clip = dialogueClip;
            }
            approachClip = Resources.Load<AudioClip>("LocationAudio/Effects/Approaching traffic");
            passClip = Resources.Load<AudioClip>("LocationAudio/Effects/Passing car");
            var track = Resources.Load<AudioClip>("LocationAudio/Music/Zver1Music");
            if (track)
            {
                var soundtrack = new GameObject("Brighton intro music");
                soundtrack.transform.SetParent(transform, false);
                music = soundtrack.AddComponent<AudioSource>();
                music.playOnAwake = false;
                music.clip = track;
                music.volume = 0;
                musicFilter = soundtrack.AddComponent<AudioLowPassFilter>();
                musicFilter.cutoffFrequency = 3500f;
                music.Play();
            }
        }

        void Update()
        {
            if (NoirMenu.GameSession.Paused) return;
            if (loading || loadError != null) return;

            if (Application.isEditor && Keyboard.current != null && (Keyboard.current.escapeKey.wasPressedThisFrame ||
                Keyboard.current.spaceKey.wasPressedThisFrame)) skipping = true;

            if (skipping)
            {
                skipFade = Mathf.MoveTowards(skipFade, 1, Time.deltaTime / 1.2f);
                UpdateMusic(currentLine != null, Mathf.Max(ExitFade, skipFade));
                if (engine) engine.volume = engineLevel * (1 - Mathf.SmoothStep(0, 1, Mathf.Max(ExitFade, skipFade)));
                if (dialogueSource) dialogueSource.volume = 1 - Mathf.SmoothStep(0, 1, skipFade);
                HeroFace.StopHero();
                foreach (var vehicle in oncomingVehicles)
                {
                    if (vehicle.approachAudio) vehicle.approachAudio.volume = 0;
                    if (vehicle.passAudio) vehicle.passAudio.volume *= 1 - skipFade;
                }
                if (skipFade >= 1) LoadApartment();
                return;
            }

            if (preludeElapsed < PreludeDuration)
            {
                preludeElapsed = Mathf.Min(PreludeDuration, preludeElapsed + Time.deltaTime);
                UpdateMusic(false, 0);
                return;
            }

            elapsed = Mathf.Min(duration, elapsed + Time.deltaTime);
            float t = Mathf.Clamp01(elapsed / duration);
            UpdateDialogue();
            UpdateMusic(currentLine != null, ExitFade);
            ApplyPose(t);
            UpdateOncomingTraffic();

            if (car != null)
            {
                float moved = car.position.z - previousZ;
                UpdateEngineAudio(moved / Mathf.Max(Time.deltaTime, .001f), currentLine != null);
                if (wheels != null)
                {
                    foreach (var wheel in wheels)
                    {
                        if (wheel) wheel.Rotate(Vector3.up, moved / .32f * Mathf.Rad2Deg, Space.Self);
                    }
                }
                previousZ = car.position.z;
            }

            if (hangingCross)
            {
                float swing = Mathf.Lerp(1f, .25f, Mathf.InverseLerp(.90f, .97f, t));
                hangingCross.localRotation = crossRest * Quaternion.Euler(
                    .75f * swing * Mathf.Sin(elapsed * 3.3f), 0, 1.8f * swing * Mathf.Sin(elapsed * 3.9f));
            }

            if (elapsed >= duration) LoadApartment();
        }

        float ExitFade => Mathf.InverseLerp(duration - ExitFadeDuration, duration, elapsed);

        void UpdateDialogue()
        {
            if (!dialogueSource) return;
            if (!dialogueStarted && elapsed >= dialogueDelay)
            {
                dialogueStarted = true;
                dialogueSource.Play();
            }

            float time = dialogueSource.isPlaying ? (float)dialogueSource.timeSamples / dialogueClip.frequency : -1f;
            DialogueLine next = ActiveLine(time);
            if (next == currentLine) return;

            currentLine = next;
            if (currentLine?.speaker == "АЛЕКСЕЙ")
                HeroFace.SpeakAsHero(currentLine.text, currentLine.end - time);
            else
                HeroFace.StopHero();
        }

        void StartEngineAudio()
        {
            if (!car) return;
            var clip = Resources.Load<AudioClip>("LocationAudio/Effects/Hero car engine");
            if (!clip) return;

            var motor = new GameObject("Hero car engine");
            motor.transform.SetParent(car, false);
            engine = motor.AddComponent<AudioSource>();
            engine.playOnAwake = false;
            engine.clip = clip;
            engine.loop = true;
            engine.volume = 0;
        }

        void UpdateEngineAudio(float speed, bool dialogueActive)
        {
            if (!engine) return;
            if (!engine.isPlaying) engine.Play();

            float throttle = Mathf.InverseLerp(0, roadDistance / (.905f * duration), speed);
            engine.pitch = Mathf.MoveTowards(engine.pitch, Mathf.Lerp(.92f, 1.26f, throttle), Time.deltaTime);
            float target = engineVolume * GameSettings.SfxGain * Mathf.Lerp(.55f, 1, throttle);
            if (dialogueActive) target *= .62f;
            engineLevel = Mathf.MoveTowards(engineLevel, target, Time.deltaTime * (dialogueActive ? 1.4f : .6f));
            engine.volume = engineLevel * (1 - Mathf.SmoothStep(0, 1, ExitFade));
        }

        DialogueLine ActiveLine(float storyTime)
        {
            if (dialogue != null)
                foreach (var line in dialogue)
                    if (storyTime >= line.start && storyTime < line.end) return line;
            return null;
        }

        void UpdateMusic(bool dialogueActive, float fade)
        {
            if (!music) return;

            float cabin = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, 2f, elapsed));
            float target = dialogueActive ? .16f : Mathf.Lerp(.9f, .58f, cabin);
            musicLevel = Mathf.MoveTowards(musicLevel, target, Time.deltaTime * (dialogueActive ? 3f : .35f));
            musicFilter.cutoffFrequency = Mathf.Lerp(3500f, 1600f, cabin);
            float entrance = Mathf.SmoothStep(0, 1, Mathf.Clamp01(preludeElapsed / 1.2f));
            music.volume = musicLevel * GameSettings.MusicGain * entrance * (1 - Mathf.SmoothStep(0, 1, fade));
        }

        void UpdateOncomingTraffic()
        {
            if (oncomingVehicles == null) return;

            foreach (var v in oncomingVehicles)
            {
                if (v.root == null) continue;

                if (elapsed < v.spawnTime)
                {
                    v.root.gameObject.SetActive(false);
                    continue;
                }

                float timeActive = elapsed - v.spawnTime;
                float currentZ = v.startZ - (timeActive * v.speed);

                if (currentZ < v.endZ)
                {
                    v.root.gameObject.SetActive(false);
                }
                else
                {
                    v.root.gameObject.SetActive(true);
                    Vector3 pos = v.root.position;
                    pos.z = currentZ;
                    v.root.position = pos;
                    if (car && shotCamera) UpdateTrafficAudio(v);
                }
            }
        }

        void UpdateTrafficAudio(OncomingVehicle vehicle)
        {
            float distance = vehicle.root.position.z - car.position.z;
            float heroSpeed = (car.position.z - previousZ) / Mathf.Max(Time.deltaTime, .001f);
            float timeToPass = distance / Mathf.Max(.1f, vehicle.speed + heroSpeed);
            if (!vehicle.approachAudio)
            {
                Vector3 screen = shotCamera.WorldToViewportPoint(vehicle.root.position + Vector3.up * .65f);
                if (screen.z < shotCamera.nearClipPlane || screen.z > shotCamera.farClipPlane ||
                    screen.x < -.025f || screen.x > 1.025f || screen.y < 0 || screen.y > 1) return;
                if (approachClip && timeToPass > 0)
                    vehicle.approachAudio = PlayTraffic(vehicle, approachClip, true);
            }

            if (vehicle.approachAudio && timeToPass <= PassPeakSeconds && !vehicle.passAudio && passClip)
            {
                vehicle.passAudio = PlayTraffic(vehicle, passClip, false);
                vehicle.passAudio.time = Mathf.Clamp(PassPeakSeconds - timeToPass, 0, passClip.length - .02f);
            }
            if (vehicle.approachAudio)
            {
                float proximity = Mathf.Clamp01(1 - distance / 130f);
                float handoff = vehicle.passAudio ? 1 - Mathf.Clamp01(vehicle.passAudio.time / .8f) : 1;
                if (vehicle.passAudio && !vehicle.passAudio.isPlaying) handoff = 0;
                float target = GameSettings.SfxGain * Mathf.Lerp(.055f, .21f, proximity) * handoff;
                vehicle.approachAudio.volume = Mathf.MoveTowards(vehicle.approachAudio.volume, target, Time.deltaTime);
                if (vehicle.passAudio && vehicle.approachAudio.volume <= .001f) vehicle.approachAudio.Stop();
            }
            if (vehicle.passAudio) vehicle.passAudio.volume = GameSettings.SfxGain * .32f;
        }

        AudioSource PlayTraffic(OncomingVehicle vehicle, AudioClip clip, bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.clip = clip;
            source.volume = 0;
            source.panStereo = vehicle.root.position.x < car.position.x ? -.34f : .34f;
            source.Play();
            return source;
        }

        void ApplyPose(float t)
        {
            if (car == null) return;

            float distance;
            if (t < .06f) distance = t * t / .12f;
            else if (t < .9f) distance = t - .03f;
            else if (t < .97f)
            {
                float u = t - .9f;
                distance = .87f + u - u * u / .14f;
            }
            else distance = .905f;

            float pullOver = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.86f, .97f, t));
            car.position = new Vector3(Mathf.Lerp(2.2f, 4.9f, pullOver), 0, roadDistance * distance / .905f);

            if (cameraRig == null) return;

            float motion = t < .97f ? Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / .97f)) : 0;
            Vector3 shotOffset = Vector3.zero;
            Vector3 shotAngles = Vector3.zero;
            float shotFov = initialFov;

            if (dynamicCamera)
            {
                float storyTime = t * 150f;
                float driverShot = BlendWindow(storyTime, 28, 36, 65, 73);
                float roadShot = BlendWindow(storyTime, 72, 81, 105, 114);
                float finalDriverShot = BlendWindow(storyTime, 110, 118, 130, 138);
                float driverWeight = Mathf.Max(driverShot, finalDriverShot);
                float amount = cameraMotionAmount;

                shotOffset = (new Vector3(.18f, .03f, -.18f) * driverWeight +
                              new Vector3(-.1f, .03f, .1f) * roadShot) * amount;
                shotAngles = new Vector3(-1f * roadShot, -12f * driverWeight + 5f * roadShot, 0) * amount;
                shotFov -= (7f * driverWeight + 3f * roadShot) * amount;
            }

            cameraRig.localPosition = cameraStart + shotOffset +
                Vector3.up * (Mathf.Sin(elapsed * 2.3f) * .004f * motion);

            float houseLook = Mathf.SmoothStep(0, 18, Mathf.InverseLerp(.91f, .98f, t));
            cameraRig.localRotation = cameraRotation * Quaternion.Euler(shotAngles.x,
                shotAngles.y + houseLook, Mathf.Sin(elapsed * 1.3f) * .12f * motion);

            if (shotCamera) shotCamera.fieldOfView = shotFov;
        }

        static float BlendWindow(float time, float enterStart, float enterEnd, float exitStart, float exitEnd)
        {
            return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(enterStart, enterEnd, time)) *
                (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(exitStart, exitEnd, time)));
        }

        void LoadApartment()
        {
            if (!Application.CanStreamedLevelBeLoaded(destinationScene))
            {
                loadError = "Не найдена сцена " + destinationScene + ". Добавьте её в Build Settings.";
                Debug.LogError(loadError);
                return;
            }
            loading = true;
            var load = SceneManager.LoadSceneAsync(destinationScene, LoadSceneMode.Single);
            ScreenFader.RevealAfterLoad(load, 1.25f);
        }

        void OnGUI()
        {
            if (NoirMenu.GameSession.Paused) return;
            if (subtitleStyle == null)
            {
                subtitleStyle = new GUIStyle(GameTypography.Label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
                subtitleStyle.normal.textColor = Color.white; subtitleStyle.Steady();
                speakerStyle = new GUIStyle(subtitleStyle);
                speakerStyle.normal.textColor = new Color(1, .82f, .51f); speakerStyle.Steady();
                smallStyle = new GUIStyle(subtitleStyle) { font = GameTypography.Face, alignment = TextAnchor.MiddleRight };
                titleStyle = new GUIStyle(subtitleStyle) { font = GameTypography.Face, fontStyle = FontStyle.Bold };
            }

            float s = Mathf.Clamp(Screen.height / 720f, .65f, 2);
            subtitleStyle.fontSize = Mathf.RoundToInt(23 * s);
            speakerStyle.fontSize = Mathf.RoundToInt(17 * s);
            smallStyle.fontSize = Mathf.RoundToInt(14 * s);
            titleStyle.fontSize = Mathf.RoundToInt(29 * s);

            Fill(new Rect(0, 0, Screen.width, Screen.height * .065f), Color.black);
            Fill(new Rect(0, Screen.height * .935f, Screen.width, Screen.height * .065f), Color.black);

            if (currentLine != null && !skipping)
                CarXCase.UI.Subtitles.Draw(CarXCase.UI.Subtitles.WithSpeaker(currentLine.speaker, currentLine.text));

            float fade = Mathf.Max(1 - Mathf.Clamp01(elapsed / 2), ExitFade);
            fade = Mathf.Max(fade, skipFade);
            if (loading) fade = 1;
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, fade));

            if (!skipping && preludeElapsed >= BlackHold && preludeElapsed < PreludeDuration)
            {
                bool showYear = preludeElapsed >= BlackHold + TitleHold;
                float titleTime = preludeElapsed - BlackHold - (showYear ? TitleHold : 0);
                float alpha = BlendWindow(titleTime, 0, .3f, TitleHold - .3f, TitleHold);
                titleStyle.normal.textColor = new Color(1, 1, 1, alpha);
                GUI.Label(new Rect(0, (Screen.height - 45 * s) / 2, Screen.width, 45 * s),
                    showYear ? "1999" : "БРАЙТОН-БИЧ", titleStyle);
            }

            if (loadError != null)
            {
                GUI.Label(new Rect(40, Screen.height / 2 - 60, Screen.width - 80, 120), loadError, new GUIStyle(GameTypography.Label) { fontSize = subtitleStyle.fontSize, alignment = TextAnchor.MiddleCenter, wordWrap = true });
            }
        }

        static void Fill(Rect area, Color color)
        {
            Color before = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = before;
        }
    }
}


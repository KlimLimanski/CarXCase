using System;
using System.Collections.Generic;
using UnityEngine;

namespace NoirMenu
{
    /// <summary>
    /// Player settings (volume, resolution, fullscreen, graphics quality).
    /// Stored in PlayerPrefs, so they survive a restart of the game.
    /// </summary>
    public static class GameSettings
    {
        const string KeyMaster = "settings.masterVolume";
        const string KeyMusic = "settings.musicVolume";
        const string KeySfx = "settings.sfxVolume";
        const string KeyResW = "settings.resolutionWidth";
        const string KeyResH = "settings.resolutionHeight";
        const string KeyFullscreen = "settings.fullscreen";
        const string KeyQuality = "settings.quality";

        public static float MasterVolume { get; private set; } = 1f;
        public static float MusicVolume { get; private set; } = 0.8f;
        public static float SfxVolume { get; private set; } = 0.8f;
        public static bool Fullscreen { get; private set; } = true;
        public static int QualityLevel { get; private set; }
        public static Vector2Int Resolution { get; private set; }

        /// <summary>Raised whenever any setting changes.</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            Changed = null;
            Load();
            ApplyAll();
            Application.quitting -= Save;
            Application.quitting += Save;
        }

        public static void Load()
        {
            MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMaster, 1f));
            MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMusic, 0.8f));
            SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeySfx, 0.8f));
            Fullscreen = PlayerPrefs.GetInt(KeyFullscreen, Screen.fullScreen ? 1 : 0) == 1;

            int qualityCount = Mathf.Max(1, QualitySettings.names.Length);
            QualityLevel = Mathf.Clamp(PlayerPrefs.GetInt(KeyQuality, QualitySettings.GetQualityLevel()), 0, qualityCount - 1);

            int w = PlayerPrefs.GetInt(KeyResW, 0);
            int h = PlayerPrefs.GetInt(KeyResH, 0);
            if (w <= 0 || h <= 0)
            {
                var current = Screen.currentResolution;
                w = Screen.fullScreen ? current.width : Screen.width;
                h = Screen.fullScreen ? current.height : Screen.height;
            }
            Resolution = new Vector2Int(w, h);
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat(KeyMaster, MasterVolume);
            PlayerPrefs.SetFloat(KeyMusic, MusicVolume);
            PlayerPrefs.SetFloat(KeySfx, SfxVolume);
            PlayerPrefs.SetInt(KeyFullscreen, Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(KeyQuality, QualityLevel);
            PlayerPrefs.SetInt(KeyResW, Resolution.x);
            PlayerPrefs.SetInt(KeyResH, Resolution.y);
            PlayerPrefs.Save();
        }

        public static void ApplyAll()
        {
            AudioListener.volume = Gain(MasterVolume);
            if (QualitySettings.GetQualityLevel() != QualityLevel)
                QualitySettings.SetQualityLevel(QualityLevel, true);
            ApplyScreen();
            Changed?.Invoke();
        }

        /// <summary>Perceptual curve for a 0..1 slider value.</summary>
        public static float Gain(float sliderValue) => sliderValue * sliderValue;

        public static float MusicGain => Gain(MusicVolume);
        public static float SfxGain => Gain(SfxVolume);

        public static void SetMasterVolume(float v)
        {
            MasterVolume = Mathf.Clamp01(v);
            AudioListener.volume = Gain(MasterVolume);
            PlayerPrefs.SetFloat(KeyMaster, MasterVolume);
            Changed?.Invoke();
        }

        public static void SetMusicVolume(float v)
        {
            MusicVolume = Mathf.Clamp01(v);
            PlayerPrefs.SetFloat(KeyMusic, MusicVolume);
            Changed?.Invoke();
        }

        public static void SetSfxVolume(float v)
        {
            SfxVolume = Mathf.Clamp01(v);
            PlayerPrefs.SetFloat(KeySfx, SfxVolume);
            Changed?.Invoke();
        }

        public static void SetFullscreen(bool on)
        {
            Fullscreen = on;
            PlayerPrefs.SetInt(KeyFullscreen, on ? 1 : 0);
            ApplyScreen();
            Changed?.Invoke();
        }

        public static void SetResolution(Vector2Int r)
        {
            Resolution = r;
            PlayerPrefs.SetInt(KeyResW, r.x);
            PlayerPrefs.SetInt(KeyResH, r.y);
            ApplyScreen();
            Changed?.Invoke();
        }

        public static void SetQuality(int level)
        {
            QualityLevel = Mathf.Clamp(level, 0, Mathf.Max(0, QualitySettings.names.Length - 1));
            QualitySettings.SetQualityLevel(QualityLevel, true);
            PlayerPrefs.SetInt(KeyQuality, QualityLevel);
            Changed?.Invoke();
        }

        static void ApplyScreen()
        {
            if (Resolution.x <= 0 || Resolution.y <= 0) return;
            var mode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            // In the Editor the Game view ignores this call; in a build it switches the window.
            Screen.SetResolution(Resolution.x, Resolution.y, mode);
        }

        public static List<Vector2Int> AvailableResolutions()
        {
            var list = new List<Vector2Int>();
            foreach (var r in Screen.resolutions)
            {
                var v = new Vector2Int(r.width, r.height);
                if (v.x >= 800 && v.y >= 600 && !list.Contains(v)) list.Add(v);
            }
            if (list.Count == 0) list.Add(new Vector2Int(Screen.width, Screen.height));
            if (Resolution.x > 0 && !list.Contains(Resolution)) list.Add(Resolution);
            list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return list;
        }

        public static string QualityDisplayName(int level)
        {
            var names = QualitySettings.names;
            if (level < 0 || level >= names.Length) return "—";
            switch (names[level])
            {
                case "Very Low": return "Очень низкое";
                case "Low": return "Низкое";
                case "Medium": return "Среднее";
                case "High": return "Высокое";
                case "Very High": return "Очень высокое";
                case "Ultra": return "Ультра";
                default: return names[level];
            }
        }
    }
}

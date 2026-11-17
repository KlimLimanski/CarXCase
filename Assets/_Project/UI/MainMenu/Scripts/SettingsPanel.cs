using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NoirMenu
{
    /// <summary>Settings window: three volumes, resolution, fullscreen, graphics quality.</summary>
    public class SettingsPanel : MonoBehaviour
    {
        public Slider masterSlider;
        public Slider musicSlider;
        public Slider sfxSlider;
        public Text masterValue;
        public Text musicValue;
        public Text sfxValue;
        public OptionSelector resolution;
        public OptionSelector fullscreen;
        public OptionSelector quality;

        List<Vector2Int> resolutions = new List<Vector2Int>();
        float lastSfxPreview;
        bool wired;

        public Selectable FirstSelectable => masterSlider;

        void Wire()
        {
            if (wired) return;
            wired = true;
            masterSlider.onValueChanged.AddListener(v => { GameSettings.SetMasterVolume(v); SetPercent(masterValue, v); });
            musicSlider.onValueChanged.AddListener(v => { GameSettings.SetMusicVolume(v); SetPercent(musicValue, v); });
            sfxSlider.onValueChanged.AddListener(v =>
            {
                GameSettings.SetSfxVolume(v);
                SetPercent(sfxValue, v);
                if (Time.unscaledTime - lastSfxPreview > 0.15f)
                {
                    lastSfxPreview = Time.unscaledTime;
                    UiSfx.PlaySelect();
                }
            });
            resolution.ValueChanged += i => GameSettings.SetResolution(resolutions[i]);
            fullscreen.ValueChanged += i => GameSettings.SetFullscreen(i == 1);
            quality.ValueChanged += i => GameSettings.SetQuality(i);
        }

        /// <summary>Reads current settings into the controls (called each time the window opens).</summary>
        public void Refresh()
        {
            Wire();
            masterSlider.SetValueWithoutNotify(GameSettings.MasterVolume);
            musicSlider.SetValueWithoutNotify(GameSettings.MusicVolume);
            sfxSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
            SetPercent(masterValue, GameSettings.MasterVolume);
            SetPercent(musicValue, GameSettings.MusicVolume);
            SetPercent(sfxValue, GameSettings.SfxVolume);

            resolutions = GameSettings.AvailableResolutions();
            var labels = new List<string>();
            int current = resolutions.Count - 1;
            for (int i = 0; i < resolutions.Count; i++)
            {
                labels.Add(resolutions[i].x + " × " + resolutions[i].y);
                if (resolutions[i] == GameSettings.Resolution) current = i;
            }
            resolution.SetOptions(labels, current);

            fullscreen.SetOptions(new[] { "Выкл.", "Вкл." }, GameSettings.Fullscreen ? 1 : 0);

            var q = new List<string>();
            for (int i = 0; i < QualitySettings.names.Length; i++) q.Add(GameSettings.QualityDisplayName(i));
            quality.SetOptions(q, GameSettings.QualityLevel);
        }

        static void SetPercent(Text t, float v)
        {
            if (t) t.text = Mathf.RoundToInt(v * 100f) + "%";
        }
    }
}

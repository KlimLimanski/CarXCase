using UnityEngine;
using UnityEngine.UI;

namespace NoirMenu
{
    /// <summary>Volume controls for the settings window.</summary>
    public class SettingsPanel : MonoBehaviour
    {
        public Slider masterSlider;
        public Slider musicSlider;
        public Slider sfxSlider;
        public Text masterValue;
        public Text musicValue;
        public Text sfxValue;
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
        }

        static void SetPercent(Text t, float v)
        {
            if (t) t.text = Mathf.RoundToInt(v * 100f) + "%";
        }
    }
}

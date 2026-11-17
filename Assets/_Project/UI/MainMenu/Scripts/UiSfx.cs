using UnityEngine;

namespace NoirMenu
{
    /// <summary>Quiet interface sounds (hover / select). Volume follows the "Effects" setting.</summary>
    [RequireComponent(typeof(AudioSource))]
    public class UiSfx : MonoBehaviour
    {
        public AudioClip hover;
        public AudioClip select;

        static UiSfx instance;
        AudioSource source;
        float lastHover;

        /// <summary>While muted (e.g. during the intro fade) hover sounds are skipped.</summary>
        public static bool Muted = true;

        void Awake()
        {
            instance = this;
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public static void PlayHover()
        {
            if (instance == null || Muted || instance.hover == null) return;
            if (Time.unscaledTime - instance.lastHover < 0.05f) return;
            instance.lastHover = Time.unscaledTime;
            instance.source.PlayOneShot(instance.hover, GameSettings.SfxGain);
        }

        public static void PlaySelect()
        {
            if (instance == null || instance.select == null) return;
            instance.source.PlayOneShot(instance.select, GameSettings.SfxGain);
        }
    }
}

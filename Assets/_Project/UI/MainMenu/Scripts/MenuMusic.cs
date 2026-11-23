using System.Collections;
using UnityEngine;

namespace NoirMenu
{
    /// <summary>
    /// Main menu theme: starts automatically, loops seamlessly (AudioSource.loop on a
    /// decompressed clip), follows the "Music" volume setting and fades out on scene change.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class MenuMusic : MonoBehaviour
    {
        public float fadeInTime = 2.5f;

        AudioSource source;
        float fade;
        Coroutine fadeRoutine;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            source.ignoreListenerPause = true;
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
        }

        void Start()
        {
            if (source.clip == null)
            {
                Debug.LogWarning("[MenuMusic] No music clip assigned.");
                return;
            }
            if (source.clip.loadState == AudioDataLoadState.Unloaded) source.clip.LoadAudioData();
            source.Play();
            fadeRoutine = StartCoroutine(FadeTo(1f, fadeInTime));
        }

        void Update() => source.volume = fade * GameSettings.MusicGain;

        public IEnumerator FadeOut(float duration)
        {
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            yield return FadeTo(0f, duration);
            source.Stop();
        }

        IEnumerator FadeTo(float target, float duration)
        {
            float start = fade;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                // equal-power style curve sounds smoother than linear
                float k = t / duration;
                fade = Mathf.Lerp(start, target, target > start ? Mathf.Sin(k * Mathf.PI * 0.5f) : 1f - Mathf.Cos(k * Mathf.PI * 0.5f));
                yield return null;
            }
            fade = target;
        }
    }
}

using System.Collections;
using DetectiveGame.Core;
using UnityEngine;

namespace CarXCase.Apartment
{
    [RequireComponent(typeof(ClueObject), typeof(AudioSource))]
    public sealed class ApartmentAnsweringMachine : MonoBehaviour
    {
        [SerializeField] AudioClip message1915;
        [SerializeField] AudioClip message2140;
        [SerializeField] Light messageIndicator;
        [SerializeField] Renderer indicatorLens;

        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        MaterialPropertyBlock indicatorProperties;
        bool lit;

        public bool IsPlaying { get; private set; }
        SimpleUIHUD playbackHud;

        void Awake()
        {
            if (indicatorLens) indicatorProperties = new MaterialPropertyBlock();
            SetIndicator(false);
        }

        void Update()
        {
            bool pulse = IsPlaying || Mathf.PingPong(Time.unscaledTime * 1.8f, 1) > .5f;
            if (pulse != lit) SetIndicator(pulse);
        }

        void SetIndicator(bool on)
        {
            lit = on;
            if (messageIndicator) messageIndicator.enabled = on;
            if (!indicatorLens) return;
            indicatorLens.GetPropertyBlock(indicatorProperties);
            indicatorProperties.SetColor(EmissionColor, on ? new Color(.85f, .27f, .03f) : Color.black);
            indicatorLens.SetPropertyBlock(indicatorProperties);
        }

        public void Play(SimpleUIHUD hud)
        {
            if (IsPlaying || !hud) return;
            LocationAudio.Effect("Cassette button", .72f);
            IsPlaying = true;
            playbackHud = hud;
            hud.IsListening = true;
            StartCoroutine(PlayMessages(hud));
        }

        IEnumerator PlayMessages(SimpleUIHUD hud)
        {
            var audioSource = GetComponent<AudioSource>();
            if (message1915) audioSource.PlayOneShot(message1915);
            yield return Line(hud, "19:15 · [Гудки, глухой стук. Вдалеке — портовый гудок.]", message1915 ? message1915.length : 3.3f);

            if (message2140) audioSource.PlayOneShot(message2140);
            float scale = message2140 ? message2140.length / 13f : 1f;
            yield return Line(hud, "21:40 · Фёдор Павлович, передай Дмитрию, что в сказки мы не верим.", 4f * scale);
            yield return Line(hud, "Контейнер на двенадцатом пирсе вскрыт, бумаги пропали.", 3.5f * scale);
            yield return Line(hud, "Если до утра пятницы товар или пятнадцать тысяч не лягут на стол — разговор продолжим в его забегаловке.", 5.5f * scale);

            var clue = GetComponent<ClueObject>();
            clue.AddEntry();
            hud.ShowInspectionComment(clue.inspectionComment);
            hud.IsListening = false;
            playbackHud = null;
            IsPlaying = false;
        }

        static IEnumerator Line(SimpleUIHUD hud, string line, float seconds)
        {
            hud.ShowInspectionComment(line, seconds);
            yield return new WaitForSecondsRealtime(seconds);
        }

        void OnDisable()
        {
            StopAllCoroutines();
            if (IsPlaying) GetComponent<AudioSource>().Stop();
            if (playbackHud) playbackHud.IsListening = false;
            playbackHud = null;
            IsPlaying = false;
        }
    }
}

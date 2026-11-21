using System.Linq;
using CarXCase.Interaction;
using DetectiveGame.Core;
using UnityEngine;

namespace CarXCase.Investigation
{
    /// How to open the diary, in the same language as the in-world key prompts:
    /// the floating 3D key and a short caption, no panel. Appears large at the top of the frame
    /// (on the scene cue — Alexey stepping into the flat — or when a new page lands in the diary),
    /// then settles as a small key in the corner.
    [DisallowMultipleComponent]
    public sealed class JournalKeyHint : MonoBehaviour
    {
        [SerializeField] SimpleUIHUD hud;
        [Tooltip("Ждать сигнала сцены (вход в квартиру), а не показывать сразу после загрузки")]
        [SerializeField] bool waitForSceneCue = true;
        [Tooltip("Задержка первого показа, если сигнал сцены не нужен")]
        [SerializeField] float startDelay = 1.5f;
        [Tooltip("Сколько секунд держится большая подсказка")]
        [SerializeField] float bannerTime = 6f;
        [Tooltip("Яркость маленькой подсказки в углу")]
        [SerializeField, Range(0f, 1f)] float cornerOpacity = .8f;

        int knownCount = -1;
        int animationFrame = -1;
        bool started;
        float showFrom, bannerUntil, bannerBlend, visibility;
        GUIStyle textStyle;

        void Start()
        {
            if (!waitForSceneCue)
            {
                started = true;
                showFrom = Time.unscaledTime + startDelay;
                bannerUntil = showFrom + bannerTime;
                bannerBlend = 1f;
            }
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (!hud) hud = FindAnyObjectByType<SimpleUIHUD>();
            var state = InvestigationIntegration.State;
            if (!hud || !hud.isActiveAndEnabled || state == null) return;

            float now = Time.unscaledTime;
            int found = state.found.Count;
            if (knownCount >= 0 && found > knownCount)
            {
                bannerUntil = now + bannerTime;
                if (!started) bannerBlend = 1f;
                started = true;
            }
            knownCount = found;
            if (hud.JournalHintFresh && !started)
            {
                started = true;
                bannerUntil = now + bannerTime;
                bannerBlend = 1f;
            }
            if (found > 0) started = true;

            bool hidden = !started || now < showFrom
                || InvestigationIntegration.IsOpen || hud.IsShowingClue()
                || (QteManager.Instance && QteManager.Instance.IsQteActive());
            bool banner = now < bannerUntil;
            if (animationFrame != Time.frameCount)
            {
                animationFrame = Time.frameCount;
                float step = Time.unscaledDeltaTime * 3f;
                bannerBlend = Mathf.MoveTowards(bannerBlend, banner ? 1f : 0f, step);
                visibility = Mathf.MoveTowards(visibility, hidden ? 0f : 1f, step);
            }
            if (visibility <= .01f) return;

            float scale = Mathf.Clamp(Screen.height / 1080f, .7f, 1.8f);
            textStyle ??= new GUIStyle(GameTypography.Label) { alignment = TextAnchor.MiddleLeft, wordWrap = false };
            int unread = state.found.Count(r => r != null && !r.read);
            string key = KeyName((KeyCode)state.journalKey);
            var old = GUI.color;
            string bannerText = unread > 0 ? "Новая запись в дневнике" : "Открыть дневник";
            string cornerText = unread > 0 ? $"Дневник  ·  {unread}" : "Дневник";
            Draw(key, bannerText, cornerText, scale);
            GUI.color = old;
        }

        /// The key itself is the 3D model from the interaction prompts; only a short caption is drawn beside it.
        void Draw(string key, string bannerText, string cornerText, float scale)
        {
            const float cornerShare = .05f, bannerShare = .085f;
            float blend = Mathf.SmoothStep(0f, 1f, bannerBlend);
            float share = Mathf.Lerp(cornerShare, bannerShare, blend);
            float h = share * Screen.height;
            float alpha = visibility * Mathf.Lerp(cornerOpacity, 1f, blend);
            float bannerHeight = bannerShare * Screen.height;
            textStyle.fontSize = Mathf.RoundToInt(24f * scale);
            float bannerWidth = textStyle.CalcSize(new GUIContent(bannerText)).x;
            var bannerCentre = new Vector2((Screen.width - bannerWidth - bannerHeight * .35f) * .5f,
                Screen.height * .14f);
            var cornerCentre = Vector2.one * (34f * scale + cornerShare * Screen.height * .5f);
            Vector2 centre = Vector2.Lerp(cornerCentre, bannerCentre, blend);
            InteractionGlyphs.ShowOnScreen(this, new Vector2(centre.x / Screen.width, 1f - centre.y / Screen.height),
                share, key, alpha);

            string text = blend > .5f ? bannerText : cornerText;
            textStyle.fontSize = Mathf.RoundToInt(Mathf.Lerp(15.2f, 24f, blend) * scale);
            float gap = h * .35f;
            var textRect = new Rect(centre.x + h * .5f + gap, centre.y - h * .5f,
                textStyle.CalcSize(new GUIContent(text)).x + 8 * scale, h);
            GUI.color = new Color(0f, 0f, 0f, .75f * alpha);
            GUI.Label(new Rect(textRect.x + 1.5f * scale, textRect.y + 1.5f * scale, textRect.width, textRect.height), text, textStyle);
            GUI.color = new Color(.97f, .96f, .92f, .95f * alpha);
            GUI.Label(textRect, text, textStyle);
        }

        static string KeyName(KeyCode key) => key switch
        {
            KeyCode.Tab => "Tab",
            KeyCode.Escape => "Esc",
            _ => key.ToString().Replace("Alpha", "")
        };
    }
}

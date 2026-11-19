using System.Linq;
using DetectiveGame.Core;
using UnityEngine;

namespace CarXCase.Investigation
{
    /// How to open the diary, in the same language as the in-world key prompts:
    /// the floating 3D key and a short caption, no panel. Appears large at the top of the frame
    /// (on the scene cue — Alexey stepping into the flat — or when a new page lands in the diary),
    /// then settles as a small key in the corner.
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
        bool started;
        float showFrom, bannerUntil, bannerAlpha, cornerAlpha;
        GUIStyle textStyle;

        void Start()
        {
            if (!waitForSceneCue) { started = true; showFrom = Time.unscaledTime + startDelay; bannerUntil = showFrom + bannerTime; }
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (!hud) hud = FindAnyObjectByType<SimpleUIHUD>();
            var state = InvestigationIntegration.State;
            if (!hud || !hud.isActiveAndEnabled || state == null) return;

            float now = Time.unscaledTime;
            int found = state.found.Count;
            if (knownCount >= 0 && found > knownCount) { bannerUntil = now + bannerTime; started = true; }
            knownCount = found;
            if (hud.JournalHintFresh && !started) { started = true; bannerUntil = now + bannerTime; }
            if (found > 0) started = true;

            bool hidden = !started || now < showFrom
                || InvestigationIntegration.IsOpen || hud.IsShowingClue()
                || (QteManager.Instance && QteManager.Instance.IsQteActive());
            bool banner = !hidden && now < bannerUntil;
            float step = Time.unscaledDeltaTime * 3f;
            bannerAlpha = Mathf.MoveTowards(bannerAlpha, banner ? 1f : 0f, step);
            cornerAlpha = Mathf.MoveTowards(cornerAlpha, hidden || banner ? 0f : cornerOpacity, step);

            float scale = Mathf.Clamp(Screen.height / 1080f, .7f, 1.8f);
            textStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, wordWrap = false };
            int unread = state.found.Count(r => r != null && !r.read);
            string key = KeyName((KeyCode)state.journalKey);
            var old = GUI.color;

            if (bannerAlpha > .01f)
                Draw(key, unread > 0 ? "Новая запись в дневнике" : "Открыть дневник", 30, scale, bannerAlpha, true, now);
            if (cornerAlpha > .01f)
                Draw(key, unread > 0 ? $"Дневник  ·  {unread}" : "Дневник", 19, scale, cornerAlpha, false, now);
            GUI.color = old;
        }

        readonly object bannerKey = new object(), cornerKey = new object();

        /// The key itself is the 3D model from the interaction prompts; only a short caption is drawn beside it.
        void Draw(string key, string text, int fontSize, float scale, float alpha, bool centered, float now)
        {
            float share = centered ? .085f : .05f;
            float h = share * Screen.height;
            textStyle.fontSize = Mathf.RoundToInt(fontSize * .8f * scale);
            float textWidth = textStyle.CalcSize(new GUIContent(text)).x;
            float gap = h * .35f;
            float total = h + gap + textWidth;
            Vector2 centre = centered
                ? new Vector2((Screen.width - total) * .5f + h * .5f, Screen.height * .14f)
                : new Vector2(34 * scale + h * .5f, 34 * scale + h * .5f);
            if (alpha > .5f)
                CarXCase.Interaction.InteractionGlyphs.ShowOnScreen(centered ? bannerKey : cornerKey,
                    new Vector2(centre.x / Screen.width, 1f - centre.y / Screen.height), share, key);

            var textRect = new Rect(centre.x + h * .5f + gap, centre.y - h * .5f, textWidth + 8 * scale, h);
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

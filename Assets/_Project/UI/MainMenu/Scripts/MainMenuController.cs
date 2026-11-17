using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NoirMenu
{
    /// <summary>
    /// Main menu flow: intro fade, button actions, settings / credits windows,
    /// fade-to-black + music fade-out when entering the game, quit.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Scenes")]
        [Tooltip("Scene loaded by \"New game\" (must be in Build Profiles / Build Settings).")]
        public string newGameScene = "00_BrightonDrive_Models";

        [Header("Buttons")]
        public Button newGameButton;
        public Button continueButton;
        public Button settingsButton;
        public Button creditsButton;
        public Button quitButton;
        public Button settingsBackButton;
        public Button creditsBackButton;

        [Header("Windows")]
        public CanvasGroup mainGroup;
        public CanvasGroup settingsGroup;
        public CanvasGroup creditsGroup;
        public SettingsPanel settingsPanel;
        public Text creditsText;
        public TextAsset creditsSource;

        [Header("Atmosphere")]
        public ScreenFader fader;
        public MenuMusic music;
        public float menuAppearDelay = 0.7f;
        public float menuAppearTime = 1.3f;
        public float windowFadeTime = 0.35f;
        public float toGameFadeTime = 1.8f;

        CanvasGroup current;
        GameObject lastMenuSelection;
        bool busy = true;

        void Awake()
        {
            newGameButton.onClick.AddListener(NewGame);
            continueButton.onClick.AddListener(ContinueGame);
            settingsButton.onClick.AddListener(() => Open(settingsGroup, settingsButton));
            creditsButton.onClick.AddListener(() => Open(creditsGroup, creditsButton));
            quitButton.onClick.AddListener(Quit);
            settingsBackButton.onClick.AddListener(CloseWindow);
            creditsBackButton.onClick.AddListener(CloseWindow);

            if (creditsText && creditsSource) creditsText.text = creditsSource.text.Trim();

            UITween.SetVisible(mainGroup, false);
            UITween.SetVisible(settingsGroup, false);
            UITween.SetVisible(creditsGroup, false);
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            UiSfx.Muted = true;
        }

        IEnumerator Start()
        {
            RefreshContinue();
            yield return new WaitForSecondsRealtime(menuAppearDelay);
            mainGroup.blocksRaycasts = true;
            var rt = (RectTransform)mainGroup.transform;
            yield return UITween.Fade(mainGroup, 1f, menuAppearTime, rt, Vector2.zero, -14f, 0f);
            mainGroup.interactable = true;
            current = mainGroup;
            Select(newGameButton.gameObject);
            busy = false;
            UiSfx.Muted = false;
        }

        void RefreshContinue()
        {
            continueButton.interactable = SaveSystem.HasSave();
        }

        void Update()
        {
            if (busy) return;

            bool cancel = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                          || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
            if (cancel && current != mainGroup) { CloseWindow(); return; }

            // "E — Выбрать" hint drawn in the video: E activates the highlighted item.
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                var go = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
                var button = go ? go.GetComponent<Button>() : null;
                if (button && button.IsInteractable()) button.onClick.Invoke();
            }

            // Keep a highlighted item for keyboard / gamepad after a click on empty space.
            var es = EventSystem.current;
            if (es && (es.currentSelectedGameObject == null || !es.currentSelectedGameObject.activeInHierarchy))
            {
                if (current == mainGroup) Select(lastMenuSelection ? lastMenuSelection : newGameButton.gameObject);
                else if (current == settingsGroup) Select(settingsPanel.FirstSelectable.gameObject);
                else if (current == creditsGroup) Select(creditsBackButton.gameObject);
            }
            if (current == mainGroup && es && es.currentSelectedGameObject && es.currentSelectedGameObject.transform.IsChildOf(mainGroup.transform))
                lastMenuSelection = es.currentSelectedGameObject;
        }

        static void Select(GameObject go)
        {
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(go);
        }

        // ---------- Windows ----------

        void Open(CanvasGroup window, Button opener)
        {
            if (busy || window == null) return;
            UiSfx.PlaySelect();
            lastMenuSelection = opener.gameObject;
            if (window == settingsGroup) settingsPanel.Refresh();
            StartCoroutine(SwitchTo(window));
        }

        void CloseWindow()
        {
            if (busy || current == mainGroup) return;
            if (current == settingsGroup) GameSettings.Save();
            UiSfx.PlaySelect();
            StartCoroutine(SwitchTo(mainGroup));
        }

        IEnumerator SwitchTo(CanvasGroup next)
        {
            busy = true;
            var from = current;
            from.interactable = false;
            from.blocksRaycasts = false;
            Select(null);
            float outDir = next == mainGroup ? 10f : -10f;
            yield return UITween.Fade(from, 0f, windowFadeTime * 0.8f, (RectTransform)from.transform, Vector2.zero, 0f, outDir);
            ((RectTransform)from.transform).anchoredPosition = Vector2.zero;

            next.blocksRaycasts = true;
            yield return UITween.Fade(next, 1f, windowFadeTime, (RectTransform)next.transform, Vector2.zero, -outDir, 0f);
            next.interactable = true;
            current = next;

            if (next == mainGroup) Select(lastMenuSelection ? lastMenuSelection : newGameButton.gameObject);
            else if (next == settingsGroup) Select(settingsPanel.FirstSelectable.gameObject);
            else Select(creditsBackButton.gameObject);
            busy = false;
        }

        // ---------- Actions ----------

        void NewGame()
        {
            if (busy) return;
            if (!Application.CanStreamedLevelBeLoaded(newGameScene)) return;
            GameSession.StartNewGame();
            StartCoroutine(EnterGame(newGameScene));
        }

        void ContinueGame()
        {
            if (busy) return;
            var save = SaveSystem.Load();
            if (save == null) { RefreshContinue(); return; }
            SaveSystem.PendingLoad = save;
            string scene = Application.CanStreamedLevelBeLoaded(save.scene) ? save.scene : newGameScene;
            StartCoroutine(EnterGame(scene));
        }

        IEnumerator EnterGame(string sceneName)
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[MainMenu] Scene '{sceneName}' is not in the build list. Add it in File ▸ Build Profiles.");
                yield break;
            }
            busy = true;
            UiSfx.PlaySelect();
            mainGroup.interactable = false;
            Select(null);

            if (music) StartCoroutine(music.FadeOut(toGameFadeTime + 0.4f));
            yield return fader.FadeTo(1f, toGameFadeTime);
            yield return new WaitForSecondsRealtime(0.45f);   // let the music tail fade into silence
            SceneManager.LoadScene(sceneName);
        }

        void Quit()
        {
            if (busy) return;
            StartCoroutine(QuitRoutine());
        }

        IEnumerator QuitRoutine()
        {
            busy = true;
            UiSfx.PlaySelect();
            mainGroup.interactable = false;
            GameSettings.Save();
            if (music) StartCoroutine(music.FadeOut(0.9f));
            yield return fader.FadeTo(1f, 0.9f);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}

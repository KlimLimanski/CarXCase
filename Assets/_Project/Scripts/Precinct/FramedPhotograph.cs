using DetectiveGame.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CarXCase.Precinct
{
    public sealed class FramedPhotograph : MonoBehaviour, IInteractable
    {
        [SerializeField] Texture2D photograph;
        [SerializeField] Texture2D officeFloor;
        [SerializeField] OfficerController player;

        ThirdPersonCamera follow;
        bool viewing, resumeCamera, cursorWasLocked;
        int openedFrame;
        Canvas viewer;
        RawImage floorImage;


        public string InteractPrompt => "E  Осмотреть фотографию";
        public bool CanInteract => isActiveAndEnabled && photograph && player &&
            player.isActiveAndEnabled && !player.InputLocked && !viewing;

        public void Interact(SimpleUIHUD hud)
        {
            if (!CanInteract) return;

            if (!viewer) BuildViewer();
            viewer.gameObject.SetActive(true);
            viewing = true;
            openedFrame = Time.frameCount;
            player.LockInput(true);
            follow = player.cameraTransform ? player.cameraTransform.GetComponent<ThirdPersonCamera>() : null;
            resumeCamera = follow && follow.enabled;
            if (resumeCamera)
            {
                cursorWasLocked = Cursor.lockState == CursorLockMode.Locked;
                follow.enabled = false;
            }
        }

        void LateUpdate()
        {
            if (NoirMenu.GameSession.Paused) return;
            // Handle closing after the player's E interaction so the same press cannot reopen the frame.
            if (!viewing) return;
            floorImage.uvRect = new Rect(0f, 0f, 1.5f * Screen.width / Screen.height, 1.5f);
            if (openedFrame == Time.frameCount) return;
            var keys = Keyboard.current;
            if (keys != null && (keys.eKey.wasPressedThisFrame || keys.escapeKey.wasPressedThisFrame))
                Close();
        }

        void OnDisable() => Close();

        void Close()
        {
            if (!viewing) return;
            viewing = false;
            if (viewer) viewer.gameObject.SetActive(false);
            if (player) player.LockInput(false);
            if (resumeCamera && follow)
            {
                follow.enabled = true;
                follow.SetCursorLock(cursorWasLocked);
            }
        }

        void BuildViewer()
        {
            // Overlay UI samples the sRGB photograph through the normal color-managed UI pass.
            var root = new GameObject("Photograph inspection", typeof(RectTransform), typeof(Canvas));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            viewer = root.GetComponent<Canvas>();
            viewer.renderMode = RenderMode.ScreenSpaceOverlay;
            viewer.sortingOrder = 32000;

            floorImage = AddLayer<RawImage>("Office floor", root.transform);
            floorImage.texture = officeFloor;
            floorImage.color = new Color(.65f, .65f, .61f);
            var photo = AddLayer<RawImage>("Photograph", root.transform);
            photo.texture = photograph;
            var fit = photo.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = (float)photograph.width / photograph.height;

            var backing = AddLayer<Image>("Close hint", root.transform);
            backing.color = new Color(0f, 0f, 0f, .76f);
            var box = backing.rectTransform;
            box.anchorMin = box.anchorMax = box.pivot = Vector2.zero;
            box.anchoredPosition = new Vector2(12f, 12f);
            var label = AddLayer<Text>("Label", box);
            label.font = GameTypography.Face;
            label.fontSize = 14;
            label.alignment = TextAnchor.MiddleCenter;
            label.text = "E / Esc — закрыть";
            box.sizeDelta = new Vector2(Mathf.Ceil(label.preferredWidth) + 16f, 26f);
        }

        static T AddLayer<T>(string name, Transform parent) where T : Graphic
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var graphic = go.AddComponent<T>();
            graphic.raycastTarget = false;
            return graphic;
        }

        void OnDestroy()
        {
            if (viewer) Destroy(viewer.gameObject);
        }
    }
}

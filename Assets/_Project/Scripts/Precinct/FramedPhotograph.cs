using DetectiveGame.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CarXCase.Precinct
{
    public sealed class FramedPhotograph : MonoBehaviour, IInteractable
    {
        [SerializeField] Texture2D photograph;
        [SerializeField] OfficerController player;

        ThirdPersonCamera follow;
        bool viewing, resumeCamera, cursorWasLocked;
        int openedFrame;

        public string InteractPrompt => "E  Осмотреть фотографию";
        public bool CanInteract => isActiveAndEnabled && photograph && player &&
            player.isActiveAndEnabled && !player.InputLocked && !viewing;

        public void Interact(SimpleUIHUD hud)
        {
            if (!CanInteract) return;

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
            if (!viewing || openedFrame == Time.frameCount) return;
            var keys = Keyboard.current;
            if (keys != null && (keys.eKey.wasPressedThisFrame || keys.escapeKey.wasPressedThisFrame))
                Close();
        }

        void OnDisable() => Close();

        void Close()
        {
            if (!viewing) return;
            viewing = false;
            if (player) player.LockInput(false);
            if (resumeCamera && follow)
            {
                follow.enabled = true;
                follow.SetCursorLock(cursorWasLocked);
            }
        }

        void OnGUI()
        {
            if (NoirMenu.GameSession.Paused) return;
            if (!viewing) return;
            GUI.depth = -30000;
            var screen = new Rect(0f, 0f, Screen.width, Screen.height);
            GUI.color = Color.black;
            GUI.DrawTexture(screen, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.DrawTexture(screen, photograph, ScaleMode.ScaleToFit);
            var hint = new GUIContent("E / Esc — закрыть");
            var style = GameTypography.Label;
            var size = style.CalcSize(hint);
            var rect = new Rect(12f, Screen.height - size.y - 20f, size.x + 16f, size.y + 8f);
            GUI.color = new Color(0f, 0f, 0f, .76f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 8f, rect.y + 4f, size.x, size.y), hint, style);
        }
    }
}

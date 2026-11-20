using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using DetectiveGame.Core;

namespace CarXCase.Precinct
{
    /// A door that ends the level: [E] → fade to black → load the next scene.
    public sealed class SceneExit : MonoBehaviour, IInteractable
    {
        public string targetScene = "02_Precinct";
        public string actionText = "Поехать в участок";
        public string alternateScene;
        public string primaryDestinationLabel = "Бар";
        public string alternateDestinationLabel = "Ломбард";
        [Tooltip("When set, interacting shows this subtitle and keeps the door closed.")]
        public string blockedComment;
        [Tooltip("How many clues must be inspected before the exit unlocks (0 = always open).")]
        public int requiredClues = 3;
        public string[] requiredClueIds;
        public float fadeTime = 1.2f;

        ClueObject[] evidence = System.Array.Empty<ClueObject>();
        bool leaving;
        bool choosing;
        bool cursorWasLocked;
        OfficerController chooser;
        ThirdPersonCamera chooserCamera;
        float fade;

        void Start()
        {
            if (!UsesRequiredIds)
                evidence = System.Array.FindAll(FindObjectsByType<ClueObject>(), c => c.isActiveAndEnabled && c.addToJournal);
        }

        bool UsesRequiredIds => requiredClueIds != null && requiredClueIds.Length > 0;
        int RequiredCount => UsesRequiredIds ? requiredClueIds.Length : Mathf.Min(requiredClues, evidence.Length);

        int CountFound()
        {
            int found = 0;
            if (UsesRequiredIds)
            {
                foreach (string id in requiredClueIds)
                    if (ClueJournal.Has(id)) found++;
            }
            else
            {
                foreach (var clue in evidence)
                    if (clue && ClueJournal.Has(clue.clueId)) found++;
            }
            return found;
        }

        public bool CanInteract => isActiveAndEnabled && !leaving && !choosing;

        public string InteractPrompt
        {
            get
            {
                if (!string.IsNullOrEmpty(blockedComment)) return "E  Проверить дверь";
                int found = CountFound();
                int needed = RequiredCount;
                if (found < needed)
                    return "E  Продолжить осмотр"; // the key always shows; pressing tells how many clues are left
                return $"E  {actionText}  ·  Улики: {found}/{needed}";
            }
        }

        public void Interact(SimpleUIHUD hud)
        {
            if (!CanInteract) return;
            if (!string.IsNullOrEmpty(blockedComment))
            {
                if (hud) hud.ShowInspectionComment(blockedComment);
                return;
            }
            int found = CountFound();
            if (found < RequiredCount)
            {
                if (hud) hud.ShowInspectionComment($"Ещё рано уходить. Обязательных улик: {found}/{RequiredCount}.");
                return;
            }
            if (string.IsNullOrEmpty(alternateScene))
            {
                Choose(targetScene);
                return;
            }
            chooser = FindAnyObjectByType<OfficerController>();
            choosing = true;
            cursorWasLocked = Cursor.lockState == CursorLockMode.Locked;
            if (chooser)
            {
                chooser.LockInput(true);
                if (chooser.cameraTransform && chooser.cameraTransform.TryGetComponent(out chooserCamera))
                    chooserCamera.enabled = false;
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Update()
        {
            if (!choosing || Keyboard.current == null) return;
            var keys = Keyboard.current;
            if (keys.escapeKey.wasPressedThisFrame) CancelChoice();
            else if (keys.digit1Key.wasPressedThisFrame || keys.numpad1Key.wasPressedThisFrame) Choose(targetScene);
            else if (keys.digit2Key.wasPressedThisFrame || keys.numpad2Key.wasPressedThisFrame) Choose(alternateScene);
        }

        void CancelChoice()
        {
            choosing = false;
            if (chooser) chooser.LockInput(false);
            if (chooserCamera)
            {
                chooserCamera.enabled = true;
                chooserCamera.SetCursorLock(cursorWasLocked);
            }
        }

        void Choose(string scene)
        {
            if (leaving) return;
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Debug.LogError("Scene " + scene + " is not in Build Settings");
                return;
            }
            choosing = false;
            StartCoroutine(Leave(scene));
        }

        IEnumerator Leave(string scene)
        {
            leaving = true;
            var player = FindAnyObjectByType<OfficerController>();
            if (player) player.enabled = false;
            while (fade < 1) { fade = Mathf.MoveTowards(fade, 1, Time.unscaledDeltaTime / Mathf.Max(.01f, fadeTime)); yield return null; }
            SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
        }

        void OnGUI()
        {
            if (choosing)
            {
                float width = Mathf.Min(440f, Screen.width * .8f);
                GUILayout.BeginArea(new Rect((Screen.width - width) * .5f, Screen.height * .34f, width, 180f), GUI.skin.box);
                GUILayout.Label("Куда поехать?");
                if (GUILayout.Button(primaryDestinationLabel + "  [1]", GUILayout.Height(35f))) Choose(targetScene);
                if (GUILayout.Button(alternateDestinationLabel + "  [2]", GUILayout.Height(35f))) Choose(alternateScene);
                if (GUILayout.Button("Остаться  [Esc]", GUILayout.Height(28f))) CancelChoice();
                GUILayout.EndArea();
            }
            if (fade <= 0) return;
            GUI.depth = -100;
            var c = GUI.color; GUI.color = new Color(0, 0, 0, fade);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = c;
        }
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using DetectiveGame.Core;

namespace CarXCase.Precinct
{
    /// A door that ends the level: [E] → fade to black → load the next scene.
    public sealed class SceneExit : MonoBehaviour, IInteractable
    {
        public string targetScene = "02_Precinct";
        public string actionText = "Поехать в участок";
        [Tooltip("When set, interacting shows this subtitle and keeps the door closed.")]
        public string blockedComment;
        [Tooltip("How many clues must be inspected before the exit unlocks (0 = always open).")]
        public int requiredClues = 3;
        public string[] requiredClueIds;
        public float fadeTime = 1.2f;

        ClueObject[] evidence = System.Array.Empty<ClueObject>();
        bool leaving;
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

        public bool CanInteract => isActiveAndEnabled && !leaving;

        public string InteractPrompt
        {
            get
            {
                if (!string.IsNullOrEmpty(blockedComment)) return "E  Проверить дверь";
                int found = CountFound();
                int needed = RequiredCount;
                if (found < needed)
                    return $"Обязательные улики: {found}/{needed}  ·  Продолжить осмотр";
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
            if (!Application.CanStreamedLevelBeLoaded(targetScene))
            {
                Debug.LogError("Scene " + targetScene + " is not in Build Settings");
                return;
            }
            StartCoroutine(Leave());
        }

        IEnumerator Leave()
        {
            leaving = true;
            var player = FindAnyObjectByType<OfficerController>();
            if (player) player.enabled = false;
            while (fade < 1) { fade = Mathf.MoveTowards(fade, 1, Time.unscaledDeltaTime / Mathf.Max(.01f, fadeTime)); yield return null; }
            SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Single);
        }

        void OnGUI()
        {
            if (fade <= 0) return;
            GUI.depth = -100;
            var c = GUI.color; GUI.color = new Color(0, 0, 0, fade);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = c;
        }
    }
}

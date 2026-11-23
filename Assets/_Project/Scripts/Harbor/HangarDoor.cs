using System.Collections;
using CarXCase.Precinct;
using DetectiveGame.Core;
using UnityEngine;

public sealed class HangarDoor : MonoBehaviour, IInteractable
{
    [SerializeField] Transform destination;
    [SerializeField] bool leadsInside;

    HangarFinale finale;
    void Awake() => finale = destination ? destination.GetComponentInParent<HangarFinale>() : null;

    OfficerController crossing;
    float opacity;

    public bool CanInteract => isActiveAndEnabled && destination && !crossing;
    public string InteractPrompt => leadsInside && finale && finale.NeedsListening ? "E  Прислушаться" : leadsInside && !HangarKey.Found ? "E  Проверить замок" : leadsInside ? "E  Войти в ангар №4" : "E  Выйти на причал";

    public void Interact(SimpleUIHUD hud)
    {
        if (!CanInteract) return;
        var player = FindAnyObjectByType<OfficerController>();
        if (!player) return;

        if (player.InputLocked) return;
        if (leadsInside && finale && finale.NeedsListening)
        {
            hud?.ClearPrompt();
            finale.Listen(player);
            return;
        }
        if (leadsInside && !HangarKey.Found)
        {
            hud?.ShowInspectionComment("Заперто. Надо поискать ключ среди груза на причале.");
            return;
        }
        StartCoroutine(Cross(player, hud));
    }

    IEnumerator Cross(OfficerController player, SimpleUIHUD hud)
    {
        crossing = player;
        player.LockInput(true);
        hud?.ClearPrompt();
        LocationAudio.Effect("Door movement open", .55f);
        yield return Fade(1f, .35f);
        yield return new WaitForEndOfFrame();
        var previousPosition = player.transform.position;
        var controller = player.GetComponent<CharacterController>();
        bool enabledBefore = controller && controller.enabled;
        if (enabledBefore) controller.enabled = false;
        player.transform.SetPositionAndRotation(destination.position, destination.rotation);
        if (enabledBefore) controller.enabled = true;
        var camera = player.cameraTransform;
        if (camera && !camera.IsChildOf(player.transform))
        {
            camera.position += destination.position - previousPosition;
            if (camera.TryGetComponent<ThirdPersonCamera>(out var follow))
                follow.LookTowards(destination.position + Vector3.up * 1.35f + destination.forward * 6f);
        }

        if (finale && !leadsInside) finale.Leave();
        yield return new WaitForSecondsRealtime(.16f);
        yield return Fade(0f, .65f);
        player.LockInput(false);
        crossing = null;
        if (finale && leadsInside) finale.Enter(player);
    }

    IEnumerator Fade(float target, float duration)
    {
        while (opacity != target)
        {
            opacity = Mathf.MoveTowards(opacity, target, Time.unscaledDeltaTime / duration);
            yield return null;
        }
    }

    void OnDisable()
    {
        StopAllCoroutines();
        if (crossing) crossing.LockInput(false);
        crossing = null;
        opacity = 0;
    }

    void OnGUI()
    {
        if (opacity <= 0) return;
        int depth = GUI.depth;
        Color color = GUI.color;
        GUI.depth = -1000;
        GUI.color = new Color(0, 0, 0, Mathf.SmoothStep(0, 1, opacity));
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = color;
        GUI.depth = depth;
    }
}

using DetectiveGame.Core;
using NoirMenu;
using UnityEngine;

public sealed class HarborDockDevice : MonoBehaviour, IInteractable
{
    public enum Device { Bell, Beacon, Winch }
    public Device kind;
    public Transform movingPart;
    public Light beacon;
    public AudioSource mechanism;
    float elapsed, wheelAngle;
    bool moving, lit;
    public bool CanInteract => isActiveAndEnabled && !moving;
    public string InteractPrompt => kind switch
    {
        Device.Bell => "E  Ударить в рынду",
        Device.Beacon => lit ? "E  Выключить маячок" : "E  Включить маячок",
        _ => "E  Провернуть лебёдку"
    };
    public void Interact(SimpleUIHUD hud)
    {
        if (!CanInteract) return;
        if (kind == Device.Beacon)
        {
            lit = !lit;
            beacon.enabled = lit;
            LocationAudio.Effect(lit ? "Light switch on" : "Light switch off", .36f);
            return;
        }
        elapsed = 0;
        moving = true;
        mechanism.volume = GameSettings.SfxGain * .58f;
        mechanism.Play();
    }
    void Update()
    {
        if (lit) movingPart.Rotate(Vector3.up, 110f * Time.deltaTime, Space.Self);
        if (!moving) return;
        elapsed += Time.deltaTime;
        if (kind == Device.Bell)
        {
            movingPart.localRotation = Quaternion.Euler(0, 0, 17f * Mathf.Sin(elapsed * 12) * Mathf.Exp(-3 * elapsed));
            if (elapsed >= 2f) { movingPart.localRotation = Quaternion.identity; moving = false; }
        }
        else
        {
            float t = Mathf.Clamp01(elapsed / 1.4f);
            movingPart.localRotation = Quaternion.Euler(wheelAngle + 240f * Mathf.SmoothStep(0, 1, t), 0, 0);
            if (t >= 1f) { wheelAngle = (wheelAngle + 240f) % 360f; moving = false; }
        }
    }
}

using DetectiveGame.Core;
using UnityEngine;

public sealed class HangarKey : MonoBehaviour, IInteractable
{
    public const string Id = "harbor_hangar_key";
    public static bool Found => ClueJournal.Has(Id);
    public bool CanInteract => isActiveAndEnabled && !Found;
    public string InteractPrompt => "E  Взять ключ";
    void Start() { if (Found) gameObject.SetActive(false); }

    public void Interact(SimpleUIHUD hud)
    {
        if (!CanInteract) return;
        ClueJournal.Collect(Id, "Ключ от ангара", "Латунный ключ с грузового ящика на причале. На бирке выбит номер ангара.");
        hud?.ClearPrompt();
        hud?.ShowInspectionComment("Четвёртый. Похоже, от ангара.");
        gameObject.SetActive(false);
    }
}
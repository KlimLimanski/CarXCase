namespace DetectiveGame.Core
{
    /// Anything the player can use with [E] (doors, the evidence board, ...).
    public interface IInteractable
    {
        string InteractPrompt { get; }
        bool CanInteract { get; }
        void Interact(SimpleUIHUD hud);
    }
}

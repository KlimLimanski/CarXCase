using DetectiveGame.Core;
using UnityEngine;

namespace CarXCase.Precinct
{
    /// Starts an existing story conversation from exploration through the usual E interaction.
    public sealed class ConversationInteraction : MonoBehaviour, IInteractable
    {
        public Interrogation conversation;
        public string characterName = "Дмитрий";
        bool started;

        public bool CanInteract => isActiveAndEnabled && conversation && !started;
        public string InteractPrompt => "E  Поговорить · " + characterName;

        public void Interact(SimpleUIHUD hud)
        {
            if (!CanInteract) return;
            var player = FindAnyObjectByType<OfficerController>();
            if (!player) return;
            started = true;
            player.LockInput(true);
            player.enabled = false;
            if (hud) { hud.CloseJournal(); hud.ClearPrompt(); hud.enabled = false; }
            var follow = conversation.cam.GetComponent<ThirdPersonCamera>();
            if (follow) follow.enabled = false;
            player.transform.LookAt(new Vector3(conversation.suspect.transform.position.x,
                player.transform.position.y, conversation.suspect.transform.position.z));

            var heroHead = player.transform.position + Vector3.up * 1.55f;
            var npcHead = conversation.suspect.transform.position + Vector3.up * 1.6f;
            var direction = Vector3.ProjectOnPlane(npcHead - heroHead, Vector3.up).normalized;
            var side = Vector3.Cross(Vector3.up, direction);
            Place(conversation.camOnSuspect, heroHead - direction * .25f + side * .7f + Vector3.up * .08f, npcHead);
            Place(conversation.camOnHero, npcHead - direction * .35f - side * .75f + Vector3.up * .08f, heroHead);
            Place(conversation.camWide, (heroHead + npcHead) * .5f + side * 1.7f + Vector3.up * .2f,
                (heroHead + npcHead) * .5f - Vector3.up * .15f);
            conversation.Begin(player.transform);
        }

        static void Place(Transform shot, Vector3 position, Vector3 target)
        {
            if (!shot) return;
            shot.position = position;
            shot.LookAt(target);
        }
    }
}

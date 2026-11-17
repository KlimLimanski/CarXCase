using UnityEngine;
using UnityEngine.Events;
namespace ReferenceBar {
    public class BarInteractionStation : MonoBehaviour {
        public Transform playerPoint;
        public Transform bartenderPoint;
        public Transform lookAt;
        public float activationRadius = .85f;
        public UnityEvent onDialogueRequested = new UnityEvent();
        public bool TryUse(Vector3 playerPosition) {
            if (playerPoint == null) return false;
            Vector3 delta = playerPosition - playerPoint.position; delta.y = 0;
            if (delta.sqrMagnitude > activationRadius * activationRadius) return false;
            onDialogueRequested.Invoke(); return true;
        }
    }
}

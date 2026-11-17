using UnityEngine;
using UnityEngine.Events;

namespace ReferenceBar {
    public class BarInteractive : MonoBehaviour {
        public string kind;
        public bool locked;
        public float openAngle = -90;
        public UnityEvent onUse = new UnityEvent();
        public UnityEvent onLocked = new UnityEvent();
        public bool IsOpen { get; private set; }
        Quaternion closedRotation;
        Transform drawer;
        Vector3 drawerClosed;
        void Awake() {
            closedRotation = transform.localRotation;
            foreach (Transform t in GetComponentsInChildren<Transform>())
                if (t.name.StartsWith("Register_CashDrawer")) { drawer = t; drawerClosed = t.localPosition; break; }
        }
        public void Use() {
            if (locked) { onLocked.Invoke(); return; }
            IsOpen = !IsOpen;
            onUse.Invoke();
        }
        void Update() {
            if (kind == "Door") {
                var target = closedRotation * Quaternion.Euler(0, IsOpen ? openAngle : 0, 0);
                transform.localRotation = Quaternion.RotateTowards(transform.localRotation, target, 100 * Time.deltaTime);
            }
            if (kind == "CashRegister" && drawer != null)
                drawer.localPosition = Vector3.MoveTowards(drawer.localPosition, drawerClosed + (IsOpen ? Vector3.left * .12f : Vector3.zero), Time.deltaTime * .2f);
        }
    }
}

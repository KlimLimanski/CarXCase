using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using Unity.Cinemachine;

namespace ReferenceBar {
    [RequireComponent(typeof(CharacterController))]
    public class BarWalkthrough : MonoBehaviour {
        public Transform eye;
        public Camera gameCamera;
        public PlayableDirector sequence;
        public CinemachineBrain brain;
        public BarInteractionStation barStation;
        public float speed = 1.7f;
        float pitch, vertical;
        bool cinematic;
        [System.NonSerialized] public int inputFrames;
        [System.NonSerialized] public string lastCollision;
        void Awake() { GetComponent<CharacterController>().minMoveDistance=0; }
        void Start() { LockMouse(); }
        void LockMouse() { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        void Update() {
            if (NoirMenu.GameSession.Paused) return;
            var kb = Keyboard.current; var mouse = Mouse.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !cinematic) LockMouse();
            if (kb.f1Key.wasPressedThisFrame && sequence != null) {
                cinematic = !cinematic;
                brain.enabled = cinematic;
                if (cinematic) { sequence.time = 0; sequence.Play(); }
                else { sequence.Stop(); gameCamera.transform.SetPositionAndRotation(eye.position, eye.rotation); }
            }
            if (cinematic) return;
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked) {
                var delta = mouse.delta.ReadValue() * .085f;
                transform.Rotate(0, delta.x, 0);
                pitch = Mathf.Clamp(pitch - delta.y, -80, 80);
                eye.localRotation = Quaternion.Euler(pitch, 0, 0);
            }
            Vector2 input = Vector2.zero;
            if (kb.wKey.isPressed) input.y += 1;
            if (kb.sKey.isPressed) input.y -= 1;
            if (kb.dKey.isPressed) input.x += 1;
            if (kb.aKey.isPressed) input.x -= 1;
            input = Vector2.ClampMagnitude(input, 1);
            if(input.sqrMagnitude>0)inputFrames++;
            var cc = GetComponent<CharacterController>();
            if (cc.isGrounded && vertical < 0) vertical = -1;
            else vertical -= 9.81f * Time.deltaTime;
            cc.Move((transform.forward * input.y * speed + transform.right * input.x * speed + Vector3.up * vertical) * Time.deltaTime);
            gameCamera.transform.SetPositionAndRotation(eye.position, eye.rotation);
            if (kb.eKey.wasPressedThisFrame) {
                if (barStation != null && barStation.TryUse(transform.position)) return;
                if (Physics.Raycast(eye.position, eye.forward, out var hit, 2.0f)) {
                    var item = hit.collider.GetComponentInParent<BarInteractive>();
                    if (item != null) item.Use();
                }
            }
        }
        void OnControllerColliderHit(ControllerColliderHit hit) { lastCollision=hit.collider.name; }
    }
}

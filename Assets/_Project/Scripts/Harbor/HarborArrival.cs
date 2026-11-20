using DetectiveGame.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CarXCase.Harbor
{
    [RequireComponent(typeof(Camera), typeof(ThirdPersonCamera))]
    public sealed class HarborArrival : MonoBehaviour
    {
        public Camera overview, warehouse;
        public Vector3 waterStart = new(-46f, 4.2f, 5f);
        public Vector3 waterTravel = new(1.4f, -.2f, 2.2f);
        public Vector3 waterLook = new(-10f, 3f, 33f);

        const float WideTime = 3.4f, WaterTime = 2.9f, WarehouseTime = 3.1f, RevealTime = .5f;
        Camera lens;
        ThirdPersonCamera follow;
        OfficerController player;
        GameObject visual;
        Vector3 returnPosition;
        Quaternion returnRotation;
        float returnFov, elapsed, black;
        bool presenting, returning, wasLocked, hadHud, hadVisual;

        void Start()
        {
            follow = GetComponent<ThirdPersonCamera>();
            player = follow.target ? follow.target.GetComponent<OfficerController>() : null;
            if (!overview || !warehouse || !player || !follow.enabled) { enabled = false; return; }
            lens = GetComponent<Camera>();
            returnPosition = transform.position;
            returnRotation = transform.rotation;
            returnFov = lens.fieldOfView;
            wasLocked = player.InputLocked;
            hadHud = player.hud && player.hud.enabled;
            var body = player.transform.Find("Character_Visual");
            visual = body ? body.gameObject : null;
            hadVisual = visual && visual.activeSelf;
            presenting = true;
            player.LockInput(true);
            follow.enabled = false;
            if (hadHud) player.hud.enabled = false;
            if (hadVisual) visual.SetActive(false);
            Frame(0f);
        }

        void Update()
        {
            if (!presenting) return;
            elapsed += Time.deltaTime;
            if (!returning)
            {
                if (elapsed < WideTime + WaterTime + WarehouseTime && Keyboard.current?.spaceKey.wasPressedThisFrame != true)
                {
                    Frame(elapsed);
                    return;
                }
                ReturnCamera();
                elapsed = 0f;
            }
            black = 1f - Mathf.SmoothStep(0f, 1f, elapsed / RevealTime);
            if (elapsed >= RevealTime) enabled = false;
        }

        void Frame(float time)
        {
            Vector3 origin, travel, look;
            float duration, fov;
            if (time < WideTime)
            {
                duration = WideTime;
                origin = overview.transform.position;
                travel = -overview.transform.right * 2.8f + Vector3.down * .8f;
                look = origin + overview.transform.forward * 38f - overview.transform.right * 3f;
                fov = overview.fieldOfView - 12f;
            }
            else if (time < WideTime + WaterTime)
            {
                time -= WideTime;
                duration = WaterTime;
                origin = waterStart;
                travel = waterTravel;
                look = waterLook;
                fov = 49f;
            }
            else
            {
                time -= WideTime + WaterTime;
                duration = WarehouseTime;
                origin = warehouse.transform.position;
                travel = warehouse.transform.forward * 2.3f - warehouse.transform.right * .55f;
                look = origin + warehouse.transform.forward * 15f;
                fov = 48f;
            }
            float t = Mathf.SmoothStep(0f, 1f, time / duration);
            Vector3 position = origin + travel * t;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(look - position));
            lens.fieldOfView = fov - 1.5f * t;
            black = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Min(time, duration - time) / .28f);
        }

        void ReturnCamera()
        {
            returning = true;
            transform.SetPositionAndRotation(returnPosition, returnRotation);
            lens.fieldOfView = returnFov;
            if (hadVisual && visual) visual.SetActive(true);
            follow.enabled = true;
            follow.SetCursorLock(true);
        }

        void OnDisable()
        {
            if (!presenting) return;
            presenting = false;
            if (!player || !follow || !lens) return;
            if (!returning) ReturnCamera();
            player.LockInput(wasLocked);
            if (hadHud && player.hud) player.hud.enabled = true;
        }

        void OnGUI()
        {
            if (!presenting || black <= 0f || Event.current.type != EventType.Repaint) return;
            var color = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, black);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = color;
        }
    }
}

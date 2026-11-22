using System.Collections;
using UnityEngine;

namespace DetectiveGame.Core
{
    [RequireComponent(typeof(ThirdPersonCamera))]
    public sealed class LocationArrivalCutscene : MonoBehaviour
    {
        [SerializeField] Vector3 logoPosition;
        [SerializeField] Vector3 logoLookAt;
        [SerializeField] Vector3 openingPosition;
        [SerializeField] Vector3 openingLookAt;

        ThirdPersonCamera follow;
        OfficerController player;
        SimpleUIHUD hud;
        float black;
        bool playing, ownsShot, wasLocked, hadHud;

        IEnumerator Start()
        {
            follow = GetComponent<ThirdPersonCamera>();
            player = follow.target ? follow.target.GetComponent<OfficerController>() : null;
            if (!player || !follow.isActiveAndEnabled) yield break;

            hud = player.hud;
            hadHud = hud && hud.enabled;
            wasLocked = player.InputLocked;
            playing = true;
            black = 1f;
            player.LockInput(true);
            if (hadHud) hud.enabled = false;
            ownsShot = true;

            follow.BeginArrivalShot(logoPosition, logoLookAt);
            yield return FadeTo(0f, .65f);
            yield return new WaitForSecondsRealtime(.8f);
            yield return FadeTo(1f, .25f);

            follow.BeginArrivalShot(openingPosition, openingLookAt);
            yield return FadeTo(0f, .45f);

            follow.EndShot();
            ownsShot = false;
            while (follow.isActiveAndEnabled && follow.InShot) yield return null;

            enabled = false;
        }

        IEnumerator FadeTo(float target, float duration)
        {
            float start = black;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                black = Mathf.SmoothStep(start, target, elapsed / duration);
                yield return null;
                // A slow render must not consume the on-screen fade.
                elapsed += Mathf.Min(Time.unscaledDeltaTime, .05f);
            }
            black = target;
        }

        void OnDisable()
        {
            if (!playing) return;
            StopAllCoroutines();
            playing = false;
            if (ownsShot && follow) follow.EndShot();
            if (player) player.LockInput(wasLocked);
            if (hadHud && hud) hud.enabled = true;
        }

        void OnGUI()
        {
            if (!playing || black <= 0f || Event.current.type != EventType.Repaint) return;

            var color = GUI.color;
            var depth = GUI.depth;
            GUI.color = new Color(0f, 0f, 0f, black);
            GUI.depth = -100;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = color;
            GUI.depth = depth;
        }
    }
}

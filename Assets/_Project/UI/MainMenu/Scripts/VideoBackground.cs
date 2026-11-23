using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace NoirMenu
{
    /// <summary>
    /// Plays the menu video into a RawImage via a RenderTexture of the video's native size.
    /// Shows a poster (first frame) until the first video frame is ready, so there is no black flash.
    /// </summary>
    [RequireComponent(typeof(RawImage), typeof(VideoPlayer))]
    public class VideoBackground : MonoBehaviour
    {
        public Texture poster;

        RawImage image;
        VideoPlayer player;
        RenderTexture target;

        void Awake()
        {
            image = GetComponent<RawImage>();
            player = GetComponent<VideoPlayer>();
            if (poster) image.texture = poster;

            int w = player.clip ? (int)player.clip.width : 1672;
            int h = player.clip ? (int)player.clip.height : 940;
            target = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32) { name = "MenuVideo_RT" };
            target.Create();

            player.playOnAwake = false;
            player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            player.isLooping = true;
            player.skipOnDrop = true;
            player.waitForFirstFrame = true;
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = target;
            player.prepareCompleted += OnPrepared;
            player.errorReceived += (p, msg) => Debug.LogError("[VideoBackground] " + msg);
            player.Prepare();
        }

        void OnPrepared(VideoPlayer p)
        {
            p.Play();
            StartCoroutine(ShowWhenFirstFrameReady());
        }

        IEnumerator ShowWhenFirstFrameReady()
        {
            while (player.frame < 1) yield return null;
            image.texture = target;
        }

        void OnDestroy()
        {
            if (player) player.prepareCompleted -= OnPrepared;
            if (target)
            {
                target.Release();
                Destroy(target);
            }
        }
    }
}

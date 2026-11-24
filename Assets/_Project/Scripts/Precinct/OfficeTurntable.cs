using CarXCase.Apartment;
using CarXCase.Interaction;
using DetectiveGame.Core;
using NoirMenu;
using UnityEngine;

namespace CarXCase.Precinct
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class OfficeTurntable : MonoBehaviour, IInteractable, IGlyphAnchor
    {
        [SerializeField] RecordSpin platter;
        [SerializeField] Transform tonearm, switchPoint;
        [SerializeField] float playingAngle = 23f;

        AudioSource music;
        Quaternion armRest;
        float travel;
        bool switchedOn, needleDown;

        public bool CanInteract => isActiveAndEnabled && platter && music && music.clip;
        public string InteractPrompt => switchedOn ? "E  Остановить пластинку" : "E  Поставить пластинку";
        public Vector3 GlyphPosition => switchPoint ? switchPoint.position : transform.position;
        public bool IsPlaying => music && music.isPlaying;

        void Awake()
        {
            music = GetComponent<AudioSource>();
            if (tonearm) armRest = tonearm.localRotation;
        }

        public void Interact(SimpleUIHUD hud)
        {
            if (!CanInteract) return;
            switchedOn = !switchedOn;
            platter.playing = switchedOn;
            LocationAudio.Effect(switchedOn ? "Light switch on" : "Light switch off", .28f);
            if (!switchedOn)
            {
                needleDown = false;
                music.Stop();
            }
        }

        void Update()
        {
            music.volume = GameSettings.MusicGain * .46f * LocationAudio.BackgroundGain(gameObject.scene);
            if (tonearm)
            {
                travel = Mathf.MoveTowards(travel, switchedOn ? 1f : 0f, Time.deltaTime * 2.5f);
                var up = tonearm.parent.InverseTransformDirection(Vector3.up);
                tonearm.localRotation = Quaternion.AngleAxis(Mathf.SmoothStep(0f, playingAngle, travel), up) * armRest;
            }
            if (switchedOn && !needleDown && (!tonearm || travel >= 1f))
            {
                music.Play();
                needleDown = true;
            }
        }

        void OnDisable()
        {
            switchedOn = needleDown = false;
            travel = 0f;
            if (platter) platter.playing = false;
            if (tonearm) tonearm.localRotation = armRest;
            if (music) music.Stop();
        }
    }
}

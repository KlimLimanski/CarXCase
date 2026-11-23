using System.Collections.Generic;
using CarXCase.Apartment;
using CarXCase.Precinct;
using DetectiveGame.Core;
using NoirMenu;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class LocationAudio : MonoBehaviour
{
    static readonly Dictionary<string, string[]> Lists = new()
    {
        ["00_BrightonDrive_Models"] = new[] { "take-a-rest" },
        ["01_FatherApartment"] = new[] { "Lilith Nyx — Missing Person", "Normand Corbeil - Two Brothers", "take-a-rest" },
        ["02_Bar"] = new[] { "dark-saxophone", "Wez Devine — Funky Jazzy Flow", "WEZ DEVINE — Yield Hop" },
        ["03_Pawnshop"] = new[] { "Lilith Nyx — Gritty Noir", "Normand Corbeil - The Artful Dodger", "noir-jazz-detective-midnight-mystery" },
        ["04_Precinct"] = new[] { "Angelo Badalamenti - Theme from Twin Peaks-Fire Walk with Me" },
        ["05_BrightonHarbor"] = new[] { "sub_clair-suspense", "Wez Devine — Shadows of Doom", "Lilith Nyx — Fear and Paranoia", "suspense", "Lilith Nyx — Monster" }
    };
    AudioSource weather, feet, runningFeet, pulse, effects;
    readonly Dictionary<string, AudioClip> cues = new();
    AudioLowPassFilter rainFilter;
    readonly List<Bounds> rainWindows = new();
    bool driving, outdoors;
    HangarFinale harborFinale;
    OfficerController player;
    CharacterController body;
    ApartmentAnsweringMachine machine;
    OfficeTurntable turntable;
    public float ScoreLevel => (machine && machine.IsPlaying) || (turntable && turntable.IsPlaying) ? .07f :
        driving || gameObject.scene.name == "04_Precinct" ? .17f : .29f;
    static LocationAudio current;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        SceneManager.sceneLoaded -= Enter;
        SceneManager.sceneLoaded += Enter;
    }

    static AudioClip Clip(string folder, string name) => Resources.Load<AudioClip>("LocationAudio/" + folder + "/" + name);

    static void Enter(Scene scene, LoadSceneMode mode)
    {
        if (!Lists.TryGetValue(scene.name, out var tracks)) return;
        var go = new GameObject("Location audio");
        SceneManager.MoveGameObjectToScene(go, scene);
        var audio = go.AddComponent<LocationAudio>();
        current = audio;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (!audio.player) audio.player = root.GetComponentInChildren<OfficerController>(true);
            if (!audio.machine) audio.machine = root.GetComponentInChildren<ApartmentAnsweringMachine>(true);
            if (!audio.turntable) audio.turntable = root.GetComponentInChildren<OfficeTurntable>(true);
        }
        if (audio.player) audio.body = audio.player.GetComponent<CharacterController>();
        bool harbor = scene.name == "05_BrightonHarbor";
        SceneScore.StartFor(audio, tracks);
        audio.driving = scene.name == "00_BrightonDrive_Models";
        audio.outdoors = harbor;
        if (harbor) audio.harborFinale = FindAnyObjectByType<HangarFinale>();
        var rainObject = new GameObject("Rain ambience");
        rainObject.transform.SetParent(go.transform, false);
        audio.weather = rainObject.AddComponent<AudioSource>();
        audio.weather.playOnAwake = false;
        audio.weather.loop = true;
        audio.weather.volume = 0;
        audio.weather.clip = Clip("Effects", audio.driving ? "Drive rain" : "Rain And Thunder Storm");
        {
            audio.rainFilter = rainObject.AddComponent<AudioLowPassFilter>();
            audio.rainFilter.lowpassResonanceQ = 1f;
            audio.rainFilter.cutoffFrequency = audio.driving ? 3600f : 1550f;
            audio.rainFilter.enabled = !harbor;
        }
        if (!harbor && !audio.driving)
        {
            string opening = scene.name switch
            {
                "01_FatherApartment" => "Window_Glazing",
                "02_Bar" => "South_UnderWindow",
                "03_Pawnshop" => "Storefront sill",
                _ => "Rain_On_Glass"
            };
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == opening)
                        audio.rainWindows.Add(t.TryGetComponent<Renderer>(out var r) ? r.bounds : new Bounds(t.position, Vector3.zero));
        }
        audio.feet = audio.Source("steps-2");
        audio.runningFeet = audio.Source("running-on-concrete");
        audio.pulse = audio.Source("heartbeat");
        audio.effects = audio.Source();
        audio.effects.volume = GameSettings.SfxGain;
        foreach (var cue in new[] { "Door movement open", "Door movement close", "Cassette button", "Lock key press", "Keypad digit", "Gate unlocked", "Journal page turn", "Light switch on", "Light switch off" })
        {
            var clip = Clip("Effects", cue);
            if (!clip) continue;
            clip.LoadAudioData();
            audio.cues.Add(cue, clip);
        }
    }

    AudioSource Source(string loop = null)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.volume = 0;
        if (loop != null)
        {
            source.clip = Clip("Effects", loop);
            source.loop = true;
        }
        return source;
    }

    public static void Effect(string name, float gain = .5f)
    {
        if (!current || !current.effects) return;
        if (!current.cues.TryGetValue(name, out var clip)) clip = Clip("Effects", name);
        if (clip) current.effects.PlayOneShot(clip, gain);
    }

    void Update()
    {
        effects.volume = GameSettings.SfxGain;
        float rainLevel = outdoors ? .78f : .22f;
        if (outdoors && harborFinale)
        {
            rainFilter.enabled = harborFinale.UnderRoof;
            if (harborFinale.UnderRoof) rainLevel = .18f;
        }
        if (!outdoors && !driving)
        {
            var ear = player && player.cameraTransform ? player.cameraTransform : transform;
            float distanceSquared = float.PositiveInfinity;
            foreach (var window in rainWindows)
                distanceSquared = Mathf.Min(distanceSquared, window.SqrDistance(ear.position));
            float near = 1f / (1f + distanceSquared * .11f);
            rainLevel = Mathf.Lerp(.18f, .56f, near);
            rainFilter.cutoffFrequency = Mathf.Lerp(1550f, 4400f, near);
        }
        Loop(weather, true, machine && machine.IsPlaying ? rainLevel * .45f : rainLevel);
        bool grounded = player && player.isActiveAndEnabled && body && body.enabled && body.isGrounded && Time.timeScale > 0f;
        float speed = grounded ? Vector3.ProjectOnPlane(body.velocity, Vector3.up).magnitude : 0f;
        bool running = grounded && speed > (player.walkSpeed + player.runSpeed) * .5f;
        Loop(feet, speed > .28f && !running, .34f);
        Loop(runningFeet, running, 1f);
        Loop(pulse, QteManager.Instance && QteManager.Instance.IsQteActive(), .34f);
    }

    static void Loop(AudioSource source, bool audible, float gain)
    {
        if (!source.clip) return;
        float target = audible ? GameSettings.SfxGain * gain : 0;
        source.volume = Mathf.MoveTowards(source.volume, target, Time.unscaledDeltaTime * 2);
        if (target > 0 && !source.isPlaying) source.Play();
        if (source.volume == 0 && source.isPlaying) source.Stop();
    }
}

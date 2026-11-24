using UnityEngine;

namespace CarXCase.CameraFX
{
    /// Detroit-style operator feel for scripted shots: layered handheld drift, a breathing
    /// micro-roll and a faint focal "breath". Whatever drives the camera in Update (cutscenes,
    /// interrogations) sets the clean pose; this restores it before Update and adds the
    /// offset in LateUpdate, so the shake never feeds back into the shot smoothing.
    [DefaultExecutionOrder(-500)]
    [RequireComponent(typeof(Camera))]
    public sealed class CinematicHandheld : MonoBehaviour
    {
        [Min(0)] public float positionAmplitude = .012f;
        [Min(0)] public float rotationAmplitude = .45f;
        [Min(0)] public float rollAmplitude = .35f;
        [Min(0)] public float frequency = .32f;
        [Min(0)] public float fovBreath = .35f;
        [Tooltip("Ignore frames where a gameplay camera (ThirdPersonCamera) is driving.")]
        public Behaviour gameplayCamera;
        [Tooltip("Add a subtle operator drift to the final shoulder-camera rotation.")]
        public bool duringExploration;

        Camera cam;
        Vector3 basePosition; Quaternion baseRotation; float baseFov;
        bool applied;
        float seed;

        void Awake()
        {
            cam = GetComponent<Camera>();
            seed = Random.value * 100f;
        }

        void Update()
        {
            if (!applied) return;
            transform.SetPositionAndRotation(basePosition, baseRotation);
            cam.fieldOfView = baseFov;
            applied = false;
        }

        void LateUpdate()
        {
            if (gameplayCamera && gameplayCamera.isActiveAndEnabled) return;
            basePosition = transform.position;
            baseRotation = transform.rotation;
            baseFov = cam.fieldOfView;

            float t = Time.time * frequency + seed;
            Vector3 offset = transform.right * Noise(t, 1f) * positionAmplitude + transform.up * Noise(t, 2f) * positionAmplitude * .8f;
            Quaternion shake = Drift(t, 1f);
            transform.SetPositionAndRotation(basePosition + offset, baseRotation * shake);
            cam.fieldOfView = baseFov + Mathf.Sin(Time.time * .9f + seed) * fovBreath;
            applied = true;
        }

        // The follow camera supplies its collision-tested position and clean orbit each frame.
        public Quaternion ExplorationRotation(Quaternion rotation) => duringExploration && isActiveAndEnabled
            ? rotation * Drift(Time.time * frequency + seed, .7f) : rotation;

        Quaternion Drift(float t, float strength) => Quaternion.Euler(
            Noise(t, 3f) * rotationAmplitude * strength, Noise(t, 4f) * rotationAmplitude * strength,
            Noise(t, 5f) * rollAmplitude * strength);

        static float Noise(float t, float channel) =>
            (Mathf.PerlinNoise(t, channel) - .5f) * 1.5f + (Mathf.PerlinNoise(t * 3.1f, channel + 17f) - .5f) * .5f;
    }
}

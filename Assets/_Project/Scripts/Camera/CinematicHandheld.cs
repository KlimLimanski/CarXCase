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
            float N(float o) => (Mathf.PerlinNoise(t, o) - .5f) * 2f * .75f + (Mathf.PerlinNoise(t * 3.1f, o + 17f) - .5f) * 2f * .25f;
            Vector3 offset = transform.right * N(1f) * positionAmplitude + transform.up * N(2f) * positionAmplitude * .8f;
            Quaternion shake = Quaternion.Euler(N(3f) * rotationAmplitude, N(4f) * rotationAmplitude, N(5f) * rollAmplitude);
            transform.SetPositionAndRotation(basePosition + offset, baseRotation * shake);
            cam.fieldOfView = baseFov + Mathf.Sin(Time.time * .9f + seed) * fovBreath;
            applied = true;
        }
    }
}

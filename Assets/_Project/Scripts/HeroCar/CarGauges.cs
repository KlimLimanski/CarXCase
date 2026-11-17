using UnityEngine;

namespace CarXCase.HeroCar
{
    /// Drives the instrument cluster needles from the real movement of the car.
    /// Needles are modelled pointing at zero; they rotate around the dial normal (toward the driver).
    [ExecuteAlways]
    public sealed class CarGauges : MonoBehaviour
    {
        public Transform car;
        public Transform speedNeedle, tachNeedle, fuelNeedle, tempNeedle;
        [Tooltip("Нормаль циферблата в пространстве машины, направлена к водителю")]
        public Vector3 dialNormalInCar = new Vector3(0, 0.259f, -0.966f);
        public float maxMph = 120, maxRpm = 8000, bigSweep = 270, smallSweep = 120;
        [Tooltip("Множитель показаний спидометра (1 = реальная скорость машины в сцене)")]
        public float speedMultiplier = 1f;
        public float idleRpm = 750, rpmPerMph = 60;
        [Range(0, 1)] public float fuel = 0.34f, temperature = 0.5f;
        [Tooltip("Показание для превью в редакторе")]
        public float editorPreviewMph = 25;

        Quaternion s0, t0, f0, c0; bool cached; Vector3 lastPos; float mph, rpm, wobble;

        void Cache()
        {
            if (cached) return;
            if (speedNeedle) s0 = speedNeedle.localRotation; if (tachNeedle) t0 = tachNeedle.localRotation;
            if (fuelNeedle) f0 = fuelNeedle.localRotation; if (tempNeedle) c0 = tempNeedle.localRotation;
            cached = true; if (car) lastPos = car.position;
        }
        void OnEnable() { Cache(); }
        void OnDisable() { if (!cached) return; Set(speedNeedle, s0, 0); Set(tachNeedle, t0, 0); Set(fuelNeedle, f0, 0); Set(tempNeedle, c0, 0); }

        void LateUpdate()
        {
            Cache();
            float target;
            if (Application.isPlaying && car)
            {
                float dt = Mathf.Max(Time.deltaTime, 1e-4f);
                target = Vector3.Distance(car.position, lastPos) / dt * 2.23694f * speedMultiplier;
                lastPos = car.position;
                mph = Mathf.Lerp(mph, target, 1 - Mathf.Exp(-dt * 3f));
                wobble = Mathf.Sin(Time.time * 7.3f) * 0.25f + Mathf.Sin(Time.time * 2.1f) * 0.35f;
            }
            else mph = editorPreviewMph;
            rpm = idleRpm + mph * rpmPerMph + wobble * 20;
            Apply();
        }

        public void PreviewNow()
        {
            Cache(); mph = editorPreviewMph; rpm = idleRpm + mph * rpmPerMph; Apply();
        }

        public void Apply()
        {
            Set(speedNeedle, s0, Mathf.Clamp01((mph + wobble * 0.3f) / maxMph) * bigSweep);
            Set(tachNeedle, t0, Mathf.Clamp01(rpm / maxRpm) * bigSweep);
            Set(fuelNeedle, f0, fuel * smallSweep);
            Set(tempNeedle, c0, temperature * smallSweep);
        }

        void Set(Transform needle, Quaternion baseLocal, float degrees)
        {
            if (!needle || !needle.parent) return;
            Vector3 axisWorld = car ? car.TransformDirection(dialNormalInCar) : Vector3.back;
            Vector3 axisLocal = needle.parent.InverseTransformDirection(axisWorld).normalized;
            needle.localRotation = Quaternion.AngleAxis(degrees, axisLocal) * baseLocal;
        }
    }
}

using UnityEngine;

namespace CarXCase.Weather
{
    /// Drives the shared rain state: the car volume where no rain is drawn, the clock used by the
    /// glass shader and the windshield wipers (same timing as the wiper clearing in RainOnGlass).
    [ExecuteAlways]
    public sealed class RainAtmosphere : MonoBehaviour
    {
        [Header("Машина: внутри этого объёма дождь не рисуется")]
        public Transform car;
        public Vector3 cabinMin = new Vector3(-0.95f, -0.05f, -2.45f);
        public Vector3 cabinMax = new Vector3(0.95f, 1.98f, 2.45f);

        [Header("Дворники")]
        public bool wipersOn = true;
        public Transform[] wiperPivots;
        [Tooltip("Ось поворота каждого дворника в пространстве родителя")]
        public Vector3[] wiperAxes;
        public float sweepDegrees = 100f;
        public float upTime = 0.55f, downTime = 0.6f;
        [Tooltip("Полный цикл: взмах туда-обратно + пауза")]
        public float period = 2.6f;

        static readonly int CabinM = Shader.PropertyToID("_RainCabinWorldToLocal");
        static readonly int CabinMin = Shader.PropertyToID("_RainCabinMin");
        static readonly int CabinMax = Shader.PropertyToID("_RainCabinMax");
        static readonly int RainTime = Shader.PropertyToID("_RainTime");
        static readonly int WiperParams = Shader.PropertyToID("_RainWiperParams");

        Quaternion[] baseRot;

        void OnEnable()
        {
            if (wiperPivots == null) return;
            baseRot = new Quaternion[wiperPivots.Length];
            for (int i = 0; i < wiperPivots.Length; i++) if (wiperPivots[i]) baseRot[i] = wiperPivots[i].localRotation;
        }

        void OnDisable()
        {
            if (baseRot != null && wiperPivots != null)
                for (int i = 0; i < wiperPivots.Length && i < baseRot.Length; i++) if (wiperPivots[i]) wiperPivots[i].localRotation = baseRot[i];
            Shader.SetGlobalVector(CabinMin, Vector4.zero); Shader.SetGlobalVector(CabinMax, Vector4.zero);
        }

        public float Clock => Application.isPlaying ? Time.timeSinceLevelLoad : (float)(Time.realtimeSinceStartupAsDouble % 3600.0);

        public float Wiper01(float t)
        {
            if (!wipersOn) return 0;
            float T = Mathf.Max(period, upTime + downTime);
            float tau = t - Mathf.Floor(t / T) * T;
            if (tau < upTime) return 0.5f - 0.5f * Mathf.Cos(Mathf.PI * tau / upTime);
            if (tau < upTime + downTime) return 0.5f + 0.5f * Mathf.Cos(Mathf.PI * (tau - upTime) / downTime);
            return 0;
        }

        void LateUpdate()
        {
            if (NoirMenu.GameSession.Paused) return;
            float t = Clock;
            if (car)
            {
                Shader.SetGlobalMatrix(CabinM, car.worldToLocalMatrix);
                Shader.SetGlobalVector(CabinMin, cabinMin);
                Shader.SetGlobalVector(CabinMax, cabinMax);
            }
            Shader.SetGlobalFloat(RainTime, t);
            float sweep = wipersOn ? sweepDegrees * Mathf.Deg2Rad : 0.0001f;
            Shader.SetGlobalVector(WiperParams, new Vector4(upTime, downTime, Mathf.Max(period, upTime + downTime), sweep));

            if (wiperPivots == null || wiperAxes == null) return;
            if (baseRot == null || baseRot.Length != wiperPivots.Length) OnEnable();
            float a = Wiper01(t) * sweepDegrees;
            for (int i = 0; i < wiperPivots.Length && i < wiperAxes.Length; i++)
                if (wiperPivots[i]) wiperPivots[i].localRotation = Quaternion.AngleAxis(a, wiperAxes[i]) * baseRot[i];
        }
    }
}

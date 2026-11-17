using UnityEngine;

namespace CarXCase.Apartment
{
    [RequireComponent(typeof(Light))]
    public sealed class CrtScreenLight : MonoBehaviour
    {
        Light screenLight;
        float steadyIntensity;

        void Awake()
        {
            screenLight = GetComponent<Light>();
            steadyIntensity = screenLight.intensity;
        }

        void Update()
        {
            if (!screenLight.enabled) return;
            float time = Time.time;
            float flutter = Mathf.PerlinNoise1D(time * 9f) * .18f + Mathf.Sin(time * 13f) * .016f;
            screenLight.intensity = steadyIntensity * (.90f + flutter);
        }
    }
}

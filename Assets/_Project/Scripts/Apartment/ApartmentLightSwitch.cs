using System;
using DetectiveGame.Core;
using UnityEngine;

namespace CarXCase.Apartment
{
    public sealed class ApartmentLightSwitch : MonoBehaviour, IInteractable
    {
        [Serializable]
        struct EmissiveSurface
        {
            public Renderer renderer;
            public int materialIndex;
            [ColorUsage(false, true)] public Color litColor;
        }

        [SerializeField] Light[] lights = Array.Empty<Light>();
        [SerializeField] EmissiveSurface[] emissiveSurfaces = Array.Empty<EmissiveSurface>();
        [SerializeField] Transform rocker;
        [SerializeField] Renderer television;
        [SerializeField] Material televisionSignal;
        [SerializeField] int televisionMaterialIndex;

        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        MaterialPropertyBlock properties;
        Material[] poweredScreen, unpoweredScreen;
        Quaternion rockerOff;
        bool powered;

        public bool CanInteract => isActiveAndEnabled;
        public string InteractPrompt => powered ? "E  Выключить свет" : "E  Включить свет";

        void Awake()
        {
            if (rocker) rockerOff = rocker.localRotation;
            if (television && televisionSignal)
            {
                unpoweredScreen = television.sharedMaterials;
                if ((uint)televisionMaterialIndex < (uint)unpoweredScreen.Length)
                {
                    poweredScreen = (Material[])unpoweredScreen.Clone();
                    poweredScreen[televisionMaterialIndex] = televisionSignal;
                }
            }

            SetPowered(false);
        }

        public void Interact(SimpleUIHUD hud)
        {
            if (CanInteract) SetPowered(!powered);
        }

        void SetPowered(bool on)
        {
            powered = on;
            if (rocker)
                rocker.localRotation = rockerOff * Quaternion.AngleAxis(on ? 12f : 0f, Vector3.right);
            foreach (var light in lights)
                if (light) light.enabled = on;

            properties ??= new MaterialPropertyBlock();
            foreach (var surface in emissiveSurfaces)
            {
                if (!surface.renderer) continue;
                surface.renderer.GetPropertyBlock(properties, surface.materialIndex);
                properties.SetColor(EmissionColor, on ? surface.litColor : Color.black);
                surface.renderer.SetPropertyBlock(properties, surface.materialIndex);
            }

            if (television && poweredScreen != null)
                television.sharedMaterials = on ? poweredScreen : unpoweredScreen;
        }

        void OnDisable() => SetPowered(false);
    }
}

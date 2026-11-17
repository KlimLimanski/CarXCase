using System.Collections.Generic;
using UnityEngine;

namespace DetectiveGame.Core
{
    public class ClueObject : MonoBehaviour
    {
        public string clueId = "clue_safe";
        public string clueTitle = "Сейф отца";
        [TextArea(3, 6)]
        public string clueDescription = "Сейф распахнут настежь. Замок не взломан — код ввели вручную. Внутри пусто, пачки денег исчезли.";
        public bool isSuspiciousForBrothers = true;
        public bool addToJournal = true;
        [TextArea] public string inspectionComment;
        [TextArea] public string journalThought;

        public string InspectionAction =>
            TryGetComponent<CarXCase.Apartment.ApartmentAnsweringMachine>(out _) ? "Воспроизвести" : "Осмотреть";

        public Renderer targetRenderer;
        public Renderer[] highlightRenderers;
        public Color highlightColor = new Color(1f, 0.85f, 0.4f);

        sealed class HighlightSlot
        {
            public Renderer renderer;
            public int index;
            public int colorProperty;
            public Material material;
            public readonly MaterialPropertyBlock original = new MaterialPropertyBlock();
        }

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int LegacyColor = Shader.PropertyToID("_Color");
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        readonly List<HighlightSlot> highlightSlots = new List<HighlightSlot>();
        MaterialPropertyBlock highlightBlock;
        bool isHighlighted;
        public bool IsHighlighted => isHighlighted;

        private void Awake()
        {
            highlightBlock = new MaterialPropertyBlock();
            if (!targetRenderer) targetRenderer = GetComponent<Renderer>();
            var renderers = highlightRenderers != null && highlightRenderers.Length > 0 ? highlightRenderers : new[] { targetRenderer };
            foreach (var renderer in renderers)
            {
                if (!renderer) continue;
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var material = materials[i];
                    if (!material) continue;
                    int property = material.HasProperty(BaseColor) ? BaseColor : LegacyColor;
                    if (!material.HasProperty(property)) continue;
                    highlightSlots.Add(new HighlightSlot { renderer = renderer, index = i, colorProperty = property, material = material });
                }
            }
        }

        public void SetHighlight(bool highlight)
        {
            if (isHighlighted == highlight) return;
            isHighlighted = highlight;
            foreach (var slot in highlightSlots)
            {
                if (!slot.renderer) continue;
                if (!highlight)
                {
                    slot.renderer.SetPropertyBlock(slot.original.isEmpty ? null : slot.original, slot.index);
                    continue;
                }

                slot.renderer.GetPropertyBlock(slot.original, slot.index);
                slot.renderer.GetPropertyBlock(highlightBlock, slot.index);
                var color = slot.original.HasColor(slot.colorProperty) ? slot.original.GetColor(slot.colorProperty) : slot.material.GetColor(slot.colorProperty);
                var lifted = Color.Lerp(color * 1.13f, highlightColor, .14f);
                lifted.a = color.a;
                highlightBlock.SetColor(slot.colorProperty, lifted);
                if (slot.material.IsKeywordEnabled("_EMISSION"))
                {
                    var emission = slot.original.HasColor(EmissionColor) ? slot.original.GetColor(EmissionColor) : slot.material.GetColor(EmissionColor);
                    highlightBlock.SetColor(EmissionColor, emission + highlightColor * .033f);
                }
                slot.renderer.SetPropertyBlock(highlightBlock, slot.index);
            }
        }

        void OnDisable() => SetHighlight(false);

        public void AddEntry()
        {
            if (addToJournal) ClueJournal.Collect(clueId, clueTitle, clueDescription, journalThought);
        }

        public void Inspect(SimpleUIHUD ui)
        {
            if (TryGetComponent<CarXCase.Apartment.ApartmentAnsweringMachine>(out var machine))
            {
                machine.Play(ui);
                return;
            }

            AddEntry();
            if (!ui) return;
            if (!string.IsNullOrWhiteSpace(inspectionComment)) ui.ShowInspectionComment(inspectionComment);
            else ui.ShowClue(clueTitle, clueDescription);
        }
    }
}

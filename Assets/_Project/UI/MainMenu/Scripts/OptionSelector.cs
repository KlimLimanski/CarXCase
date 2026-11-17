using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NoirMenu
{
    /// <summary>
    /// "‹ value ›" selector: left/right keys (or clicking the arrows) step through options.
    /// Used for resolution, fullscreen and graphics quality.
    /// </summary>
    public class OptionSelector : Selectable
    {
        public Text valueText;
        public Button previousButton;
        public Button nextButton;
        public bool wrap = true;

        public event Action<int> ValueChanged;

        readonly List<string> options = new List<string>();
        public int Index { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            if (!Application.isPlaying) return;
            if (previousButton) previousButton.onClick.AddListener(() => { Step(-1); Focus(); });
            if (nextButton) nextButton.onClick.AddListener(() => { Step(1); Focus(); });
        }

        void Focus()
        {
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(gameObject);
        }

        public void SetOptions(IList<string> values, int index)
        {
            options.Clear();
            options.AddRange(values);
            Index = Mathf.Clamp(index, 0, Mathf.Max(0, options.Count - 1));
            Refresh();
        }

        public void Step(int delta)
        {
            if (options.Count == 0 || !IsInteractable()) return;
            int next = Index + delta;
            if (wrap) next = (next % options.Count + options.Count) % options.Count;
            else next = Mathf.Clamp(next, 0, options.Count - 1);
            if (next == Index) return;
            Index = next;
            Refresh();
            UiSfx.PlaySelect();
            ValueChanged?.Invoke(Index);
        }

        void Refresh()
        {
            if (valueText) valueText.text = options.Count > 0 ? options[Index] : "—";
        }

        public override void OnMove(AxisEventData eventData)
        {
            switch (eventData.moveDir)
            {
                case MoveDirection.Left: Step(-1); eventData.Use(); return;
                case MoveDirection.Right: Step(1); eventData.Use(); return;
                default: base.OnMove(eventData); return;
            }
        }
    }
}

using System;
using Gwent.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gwent.UI
{
    // "<  value  >": The Witcher 3's way of picking among a few options, in UI Toolkit (Gwent.uss, .stepper). The arrows
    // are buttons the keyboard can reach; an arrow with nothing further that way is disabled.
    // In UXML: <Gwent.UI.Stepper />, its options set from code.
    [UxmlElement]
    public partial class Stepper : VisualElement
    {
        public Button Left { get; private set; }
        public Button Right { get; private set; }
        public Label Label { get; private set; }
        public int Index { get; private set; }

        private string[] options = new string[0];
        private Action<int> changed;

        public Stepper()
        {
            AddToClassList("stepper");
            Left = Arrow("Left", "stepper__arrow--left", -1);
            Label = new Label { name = "Value", pickingMode = PickingMode.Ignore };
            Label.AddToClassList("stepper__value");
            Right = Arrow("Right", "stepper__arrow--right", 1);
            Add(Left);
            Add(Label);
            Add(Right);
        }

        // The options, the one shown, and what to do when another is picked
        public void Set(string[] values, int index, Action<int> on_change)
        {
            options = values;
            changed = on_change;
            Index = Mathf.Clamp(index, 0, Mathf.Max(0, options.Length - 1));
            Refresh();
        }

        // Shows another option, without a change being made
        public void Show(int index)
        {
            Index = Mathf.Clamp(index, 0, Mathf.Max(0, options.Length - 1));
            Refresh();
        }

        public void Step(int delta)
        {
            int next = Mathf.Clamp(Index + delta, 0, Mathf.Max(0, options.Length - 1));
            if (next == Index)
                return;
            Index = next;
            Refresh();
            if (changed != null)
                changed(Index);
        }

        private Button Arrow(string name, string side, int delta)
        {
            Button arrow = new Button { name = name };
            arrow.clicked += () =>
            {
                GameAudio.PlaySfx("ui_click");
                Step(delta);
                // At the last option this arrow turns off: the keyboard's focus goes to the other one, not nowhere
                if (!arrow.enabledSelf)
                    (delta < 0 ? Right : Left).Focus();
            };
            arrow.AddToClassList("stepper__arrow");
            arrow.AddToClassList(side);
            VisualElement icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("stepper__icon");
            arrow.Add(icon);
            return arrow;
        }

        private void Refresh()
        {
            Label.text = options.Length > 0 ? options[Index] : "";
            Left.SetEnabled(Index > 0);
            Right.SetEnabled(Index < options.Length - 1);
        }
    }
}

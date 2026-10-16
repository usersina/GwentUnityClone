using Gwent.Core;
using UnityEngine.UIElements;

namespace Gwent.UI
{
    // An on/off switch in UI Toolkit: a Toggle drawn as a pill whose knob slides across (Gwent.uss, .switch); gold when
    // on. Clicks with the menu's sound. In UXML: <Gwent.UI.Switch />
    [UxmlElement]
    public partial class Switch : Toggle
    {
        public Switch()
        {
            AddToClassList("switch");
            VisualElement knob = new VisualElement { name = "Knob", pickingMode = PickingMode.Ignore };
            knob.AddToClassList("switch__knob");
            this.Q(className: checkmarkUssClassName).Add(knob);
            this.RegisterValueChangedCallback(_ => GameAudio.PlaySfx("ui_click"));
        }
    }
}

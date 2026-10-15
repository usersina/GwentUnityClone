using Gwent.Core;
using UnityEngine.UIElements;

namespace Gwent.UI
{
    // A Witcher-style menu entry in UI Toolkit: serif text that lights up with a gold rule on its left and a fading band
    // when hovered or focused (Gwent.uss, .menu-item). Hovering focuses it, so the mouse and the keyboard share one
    // highlight, as in The Witcher 3. In UXML: <Gwent.UI.MenuEntry name="Item Play" text="Play" />
    [UxmlElement]
    public partial class MenuEntry : Button
    {
        public MenuEntry()
        {
            AddToClassList("menu-item");
            VisualElement rule = new VisualElement { name = "Rule", pickingMode = PickingMode.Ignore };
            rule.AddToClassList("menu-item__rule");
            Add(rule);
            RegisterCallback<PointerEnterEvent>(_ => Focus());
            clicked += () => GameAudio.PlaySfx("ui_click");
        }
    }
}

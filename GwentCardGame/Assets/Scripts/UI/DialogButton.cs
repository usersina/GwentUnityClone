using Gwent.Core;
using UnityEngine.UIElements;

namespace Gwent.UI
{
    // A dialog's button in UI Toolkit (Gwent.uss): .button--primary (gold fill, the choice the dialog is about) or
    // .button--secondary (gold outline: Back, No). Clicks with the menu's sound.
    // In UXML: <Gwent.UI.DialogButton name="Button Back" text="Back" class="button--secondary" />
    [UxmlElement]
    public partial class DialogButton : Button
    {
        public DialogButton()
        {
            AddToClassList("button");
            clicked += () => GameAudio.PlaySfx("ui_click");
        }
    }
}

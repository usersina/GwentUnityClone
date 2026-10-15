using Gwent.Core;
using UnityEditor;

namespace Gwent.EditorTools
{
    // Gwent > Simulate Touch Controls: the phone's controls in the editor, with the mouse (a click acts as a tap, a held
    // click as a finger held on a card). Remembered between sessions. What a scene sets up on its start (the custom
    // cursor, the keyboard shortcuts, the hints) follows on the next Play.
    [InitializeOnLoad]
    public static class TouchSimulation
    {
        private const string MenuPath = "Gwent/Simulate Touch Controls";
        private const string PrefKey = "Gwent.SimulateTouch";

        static TouchSimulation()
        {
            GameInput.SimulateTouch = EditorPrefs.GetBool(PrefKey, false);
        }

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            GameInput.SimulateTouch = !GameInput.SimulateTouch;
            EditorPrefs.SetBool(PrefKey, GameInput.SimulateTouch);
        }

        [MenuItem(MenuPath, true)]
        private static bool ShowState()
        {
            Menu.SetChecked(MenuPath, GameInput.SimulateTouch);
            return true;
        }
    }
}

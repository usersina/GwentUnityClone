using UnityEngine.InputSystem;

// The few direct input checks of the game, on the Input System package (UI clicks go through the EventSystem)
public static class GameInput
{
    // Right click or Escape (the Android back button arrives as Escape): close popups, skip the redraw phase
    public static bool CancelPressed()
    {
        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;
        return (mouse != null && mouse.rightButton.wasPressedThisFrame)
            || (keyboard != null && keyboard.escapeKey.wasPressedThisFrame);
    }

    // A second finger touching the screen (mobile equivalent of the right click)
    public static bool SecondFingerTapped()
    {
        Touchscreen touchscreen = Touchscreen.current;
        return touchscreen != null && touchscreen.touches.Count > 1 && touchscreen.touches[1].press.wasPressedThisFrame;
    }
}

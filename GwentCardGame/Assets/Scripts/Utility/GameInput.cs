using UnityEngine.InputSystem;

// The few direct input checks of the game, on the Input System package (UI clicks go through the EventSystem)
public static class GameInput
{
    // Right click or Escape: close popups
    public static bool CancelPressed()
    {
        return EscapePressed() || SecondaryPressed();
    }

    // Escape (the Android back button arrives as Escape): close the top dialog, otherwise open the pause menu
    public static bool EscapePressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
    }

    // Right click: close popups, skip the redraw phase
    public static bool SecondaryPressed()
    {
        Mouse mouse = Mouse.current;
        return mouse != null && mouse.rightButton.wasPressedThisFrame;
    }

    // A second finger touching the screen (mobile equivalent of the right click)
    public static bool SecondFingerTapped()
    {
        Touchscreen touchscreen = Touchscreen.current;
        return touchscreen != null && touchscreen.touches.Count > 1 && touchscreen.touches[1].press.wasPressedThisFrame;
    }
}

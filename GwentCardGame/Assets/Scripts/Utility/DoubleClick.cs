using UnityEngine;
using UnityEngine.InputSystem;

// Double-click (double-tap) detection shared by the clickable cards: two clicks on the same object within Interval
public class DoubleClick
{
    // Windows' default; each object counts its own clicks, so two quick clicks on different cards never combine
    public const float Interval = 0.5f;

    private double lastClick = double.NegativeInfinity;

    // Call on every click; true when this click completes a double-click (a third click then starts a new pair)
    public bool Register()
    {
        double now = ClickTime();
        if (now - lastClick <= Interval)
        {
            lastClick = double.NegativeInfinity;
            return true;
        }
        lastClick = now;
        return false;
    }

    // Forgets the last click: the next one starts a new pair
    public void Reset()
    {
        lastClick = double.NegativeInfinity;
    }

    // When the click happened: the time of the pointer's input event, so a frame that comes late (a hitch) does not
    // stretch the gap between two clicks
    private static double ClickTime()
    {
        Pointer pointer = Pointer.current;
        return pointer != null && pointer.lastUpdateTime > 0 ? pointer.lastUpdateTime : Time.realtimeSinceStartupAsDouble;
    }
}

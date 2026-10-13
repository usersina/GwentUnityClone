using UnityEngine;

// Double-click (double-tap) detection shared by the clickable cards: two clicks on the same object within Interval
public class DoubleClick
{
    // Windows' default; each object counts its own clicks, so two quick clicks on different cards never combine
    public const float Interval = 0.5f;

    private float lastClick = float.NegativeInfinity;

    // Call on every click; true when this click completes a double-click (a third click then starts a new pair)
    public bool Register()
    {
        float now = Time.unscaledTime;
        if (now - lastClick <= Interval)
        {
            lastClick = float.NegativeInfinity;
            return true;
        }
        lastClick = now;
        return false;
    }
}

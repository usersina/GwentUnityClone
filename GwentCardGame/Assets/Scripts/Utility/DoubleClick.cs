using UnityEngine;

// Double-click (double-tap) detection shared by the clickable cards: two clicks on the same object within Interval
public class DoubleClick
{
    // Desktop systems default to 0.4-0.5 s between the two clicks
    public const float Interval = 0.4f;

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

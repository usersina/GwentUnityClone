using UnityEngine;
using UnityEngine.UI;

// The Witcher 3's light on a focused card (the hovered hand card, the selected card in the showcase): a broad, warm
// band of light rises slowly from the bottom of the card, lingers at its top as it fades, and comes back after a
// while. Taken from the game's video, then slowed down.
public class CardShine : MonoBehaviour
{
    private const float RiseSeconds = 1.7f;
    private const float FadeInSeconds = 0.3f;
    private const float FadeOutSeconds = 0.6f;
    private static readonly Color Light = new Color(1f, 0.9f, 0.62f);

    private Image band;
    private float age;
    private float period;

    // Lights a card (the band is clipped to its rectangle): the first light after the delay, then every period
    public static GameObject Attach(RectTransform card, string name, float delay, float period)
    {
        RectTransform area = UIKit.Rect(name, card).Stretch();
        area.gameObject.AddComponent<RectMask2D>();
        CardShine shine = area.gameObject.AddComponent<CardShine>();
        shine.band = UIKit.NewImage(area, "Band", UITheme.Current.radial, new Color(Light.r, Light.g, Light.b, 0));
        RectTransform rect = shine.band.rectTransform;
        rect.anchorMin = new Vector2(-0.35f, 0);
        rect.anchorMax = new Vector2(1.35f, 0);
        rect.pivot = new Vector2(0.5f, 0.5f);
        shine.age = -delay;
        shine.period = Mathf.Max(period, RiseSeconds);
        return area.gameObject;
    }

    private void Update()
    {
        age += Time.unscaledDeltaTime;
        if (age >= period)
            age -= period;
        float height = ((RectTransform)transform).rect.height;
        float t = Mathf.Clamp01(age / RiseSeconds);
        float eased = t * t * (3 - 2 * t); // eases in and out: no sudden start
        RectTransform rect = band.rectTransform;
        rect.sizeDelta = new Vector2(0, height * 0.55f);
        rect.anchoredPosition = new Vector2(0, Mathf.Lerp(-0.15f, 1.1f, eased) * height);
        float alpha = age < 0 ? 0 : Mathf.Clamp01(age / FadeInSeconds) * (1 - Mathf.Clamp01((age - (RiseSeconds - FadeOutSeconds)) / FadeOutSeconds));
        band.color = new Color(Light.r, Light.g, Light.b, 0.3f * alpha);
    }
}

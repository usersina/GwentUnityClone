using UnityEngine;
using UnityEngine.UI;

// Breathes a graphic's alpha to draw the eye to it (unscaled time)
[RequireComponent(typeof(Graphic))]
public class UIPulse : MonoBehaviour
{
    private const float Speed = 4f;
    private const float Low = 0.35f;
    private Graphic graphic;
    private float baseAlpha;

    private void Awake()
    {
        graphic = GetComponent<Graphic>();
        baseAlpha = graphic.color.a;
    }

    private void Update()
    {
        float t = (Mathf.Sin(Time.unscaledTime * Speed) + 1) / 2;
        Color color = graphic.color;
        color.a = baseAlpha * Mathf.Lerp(Low, 1f, t);
        graphic.color = color;
    }
}

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The Witcher 3's notices across the middle of the board ("Your turn!", "Your opponent has passed", "You won the
// round!"): a dark band with gold edges, a title and an optional line under it. Notices queue up and show one after
// another, and the band never catches a click. Runs on game time, so pausing the game holds it.
public class BattleBanner : MonoBehaviour
{
    public const float Short = 1.3f;
    public const float Long = 2.4f;
    private const float FadeIn = 0.15f;
    private const float FadeOut = 0.3f;
    private const float BoardMiddle = 78; // the line between the two sides of the board, above the screen's middle

    private struct Notice
    {
        public string Title;
        public string Subtitle;
        public Color Color;
        public float Duration;
    }

    private static BattleBanner instance;
    private readonly Queue<Notice> queue = new Queue<Notice>();
    private CanvasGroup group;
    private TextMeshProUGUI title;
    private TextMeshProUGUI subtitle;
    private float age = -1; // -1: nothing showing
    private float duration;

    // True while a notice shows or waits for its turn
    public static bool Busy { get { return instance != null && (instance.age >= 0 || instance.queue.Count > 0); } }

    public static void Show(string title, string subtitle = null, Color? color = null, float duration = Short)
    {
        if (instance == null)
            instance = Create();
        instance.queue.Enqueue(new Notice { Title = title, Subtitle = subtitle, Color = color ?? UITheme.Current.goldBright, Duration = duration });
        GameLog.Info("Banner: " + title + (subtitle != null ? " (" + subtitle + ")" : ""));
    }

    private static BattleBanner Create()
    {
        UITheme theme = UITheme.Current;
        RectTransform band = UIKit.NewImage(UIOverlay.FixedRoot, "BattleBanner", null, new Color(0, 0, 0, 0.8f)).rectTransform;
        band.anchorMin = new Vector2(0, 0.5f);
        band.anchorMax = new Vector2(1, 0.5f);
        band.pivot = new Vector2(0.5f, 0.5f);
        band.anchoredPosition = new Vector2(0, BoardMiddle);
        band.sizeDelta = new Vector2(0, 150);
        foreach (float edge in new[] { 0f, 1f })
        {
            Image line = UIKit.NewImage(band, "Edge", theme.gradientH, UITheme.WithAlpha(theme.gold, 0.5f));
            line.rectTransform.anchorMin = new Vector2(0, edge);
            line.rectTransform.anchorMax = new Vector2(1, edge);
            line.rectTransform.sizeDelta = new Vector2(0, 2);
        }

        BattleBanner banner = band.gameObject.AddComponent<BattleBanner>();
        banner.title = UIKit.Text(band, "", UIKit.TextStyle.Title, TextAlignmentOptions.Center);
        banner.title.characterSpacing = 4;
        banner.subtitle = UIKit.Text(band, "", UIKit.TextStyle.Label, TextAlignmentOptions.Center);
        foreach (Graphic graphic in band.GetComponentsInChildren<Graphic>())
            graphic.raycastTarget = false;
        banner.group = band.gameObject.AddComponent<CanvasGroup>();
        banner.group.alpha = 0;
        banner.group.blocksRaycasts = false;
        banner.group.interactable = false;
        return banner;
    }

    private void Update()
    {
        if (age < 0)
        {
            if (queue.Count == 0)
                return;
            Notice next = queue.Dequeue();
            bool two_lines = !string.IsNullOrEmpty(next.Subtitle);
            title.text = next.Title;
            title.color = next.Color;
            title.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, two_lines ? 18 : 0), new Vector2(1600, 80));
            subtitle.text = two_lines ? next.Subtitle : "";
            subtitle.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -38), new Vector2(1600, 40));
            duration = next.Duration;
            age = 0;
        }

        age += Time.deltaTime;
        group.alpha = age < FadeIn ? age / FadeIn : Mathf.Clamp01((duration - age) / FadeOut);
        if (age >= duration)
        {
            age = -1;
            group.alpha = 0;
        }
    }
}

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A discard pile card by card (click a pile on the board). The battle waits while it is open; Escape, a right click
// or Close puts it away.
public class DiscardViewer : MonoBehaviour
{
    private const float CardWidth = 130;
    private const float CardHeight = CardWidth * 591f / 380f;
    private const float Gap = 14;
    private const int PerRow = 10;
    private const int VisibleRows = 3;

    private static DiscardViewer current;
    private float previousTimeScale;

    public static bool IsOpen { get { return current != null; } }

    public static void Open(List<int> cards, bool is_player)
    {
        if (current != null || cards == null || cards.Count == 0)
            return;
        RectTransform dimmer = UIKit.Dimmer(UIOverlay.Root, "DiscardViewer");
        current = dimmer.gameObject.AddComponent<DiscardViewer>();
        current.previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        current.Build(dimmer, cards, is_player);
        UIOverlay.PushModal(dimmer.gameObject, current.Closed);
    }

    private void Closed()
    {
        Time.timeScale = previousTimeScale > 0 ? previousTimeScale : 1f;
        if (current == this)
            current = null;
    }

    private void Build(RectTransform dimmer, List<int> cards, bool is_player)
    {
        int rows = (cards.Count + PerRow - 1) / PerRow;
        float grid_width = PerRow * CardWidth + (PerRow - 1) * Gap;
        float view_height = Mathf.Min(rows, VisibleRows) * (CardHeight + Gap) - Gap;
        RectTransform panel = UIKit.Panel(dimmer, "Panel").Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(grid_width + 100, view_height + 260));

        string whose = is_player ? "Your discard pile" : "Your opponent's discard pile";
        TextMeshProUGUI title = UIKit.Text(panel, whose + "  (" + cards.Count + ")", UIKit.TextStyle.Heading, TextAlignmentOptions.Center);
        title.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -56), new Vector2(grid_width, 60));
        UIKit.Ornament(panel, new Vector2(0.5f, 1), new Vector2(0, -100), 420);

        // The cards in a scroll view (the mouse wheel scrolls when there are more than three rows)
        RectTransform viewport = UIKit.Rect("Viewport", panel).Place(new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(grid_width, view_height), new Vector2(0.5f, 1));
        viewport.gameObject.AddComponent<RectMask2D>();
        Image hit = viewport.gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        RectTransform content = UIKit.Rect("Cards", viewport);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0, rows * (CardHeight + Gap) - Gap);
        GridLayoutGroup grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(CardWidth, CardHeight);
        grid.spacing = new Vector2(Gap, Gap);
        grid.childAlignment = TextAnchor.UpperCenter;
        foreach (int card_id in cards)
        {
            Image art = UIKit.NewImage(content, "Card " + card_id, SpriteCache.Load("Cards/List/591x380/" + card_id), Color.white);
            art.preserveAspect = true;
        }
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40;

        Button close = UIKit.Button(panel, "Close", UIKit.ButtonStyle.Secondary, () => UIOverlay.CloseModal(gameObject), new Vector2(220, 60));
        ((RectTransform)close.transform).Place(new Vector2(0.5f, 0), new Vector2(0, 56), new Vector2(220, 60));
        UIKit.Focus(close);
    }
}

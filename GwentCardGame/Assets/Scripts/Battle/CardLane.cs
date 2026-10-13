using System.Collections.Generic;
using UnityEngine;

// Lays out a line of cards (a board row, a hand, the weather slot) the way The Witcher 3 does: centred, with a small
// gap that shrinks once the line fills up, and every card glides to its new place when the line changes instead of
// jumping. A new card starts in its place (a flight or an effect brings it in). A hovered hand card stands higher.
[DisallowMultipleComponent]
public class CardLane : MonoBehaviour
{
    public Vector2 cellSize = new Vector2(87, 135);
    public float spacing = 6;
    public float margin;           // kept free at the ends before the gap starts shrinking
    public float hoverLift = 14;
    private const float Glide = 14f; // how quickly a card closes in on its place (exponential, per second)

    private readonly HashSet<RectTransform> placed = new HashSet<RectTransform>();
    private readonly List<RectTransform> cards = new List<RectTransform>();

    // Replaces a GridLayoutGroup by a lane with its cell size and gap
    public static CardLane Install(GameObject container, float margin)
    {
        UnityEngine.UI.GridLayoutGroup grid = container.GetComponent<UnityEngine.UI.GridLayoutGroup>();
        CardLane lane = container.GetComponent<CardLane>();
        if (lane == null)
            lane = container.AddComponent<CardLane>();
        if (grid != null)
        {
            lane.cellSize = grid.cellSize;
            lane.spacing = grid.spacing.x;
            grid.enabled = false;
            Object.Destroy(grid);
        }
        lane.margin = margin;
        return lane;
    }

    // Where a card of this lane will stand once it settles (local to the lane)
    public Vector2 TargetOf(RectTransform card)
    {
        Collect();
        int index = cards.IndexOf(card);
        return index < 0 ? card.anchoredPosition : Target(index, cards.Count, card);
    }

    private void Collect()
    {
        cards.Clear();
        foreach (Transform child in transform)
            if (child.gameObject.activeSelf && child.GetComponent<CardDisplay>() != null)
                cards.Add((RectTransform)child);
    }

    private Vector2 Target(int index, int count, RectTransform card)
    {
        float width = ((RectTransform)transform).rect.width;
        float gap = spacing;
        if (count > 1 && count * cellSize.x + (count - 1) * gap > width - margin)
            gap = (width - margin - count * cellSize.x) / (count - 1);
        float start = -(count * cellSize.x + (count - 1) * gap) / 2 + cellSize.x / 2;
        CardHover hover = card.GetComponent<CardHover>();
        return new Vector2(start + index * (cellSize.x + gap), hover != null && hover.isCardUp ? hoverLift : 0);
    }

    private void LateUpdate()
    {
        Collect();
        float blend = 1 - Mathf.Exp(-Glide * Time.deltaTime);
        for (int i = 0; i < cards.Count; i++)
        {
            RectTransform card = cards[i];
            Vector2 target = Target(i, cards.Count, card);
            if (placed.Add(card))
            {
                card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
                card.sizeDelta = cellSize;
                card.anchoredPosition = target;
            }
            else
                card.anchoredPosition = Vector2.Lerp(card.anchoredPosition, target, blend);
        }
        placed.RemoveWhere(card => card == null || card.parent != transform);
    }
}

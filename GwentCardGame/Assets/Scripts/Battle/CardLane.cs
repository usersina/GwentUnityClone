using System.Collections.Generic;
using UnityEngine;

// Lays out a line of cards (a board row, a hand, the weather slot) the way The Witcher 3 does: centred, with a small
// gap that shrinks once the line fills up, and every card glides to its new place when the line changes instead of
// jumping. A new card starts in its place (a flight or an effect brings it in). A hovered hand card stands higher.
// During a play's choreography, LaneHold keeps the room of cards that left and holds back the ones that arrived.
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

    // Puts a card in its place at once (its flight is about to bring it there); the others make room
    public void Snap(RectTransform card)
    {
        Collect();
        int index = cards.IndexOf(card);
        if (index < 0)
            return;
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
        card.sizeDelta = cellSize;
        card.anchoredPosition = Target(index, cards.Count, card);
        placed.Add(card);
    }

    // The line: the cards and the spacers of cards that left, but not the cards held out of it
    private void Collect()
    {
        cards.Clear();
        for (int i = 0; i < transform.childCount; i++) // by index: no enumerator to allocate every frame
        {
            Transform child = transform.GetChild(i);
            if (!child.gameObject.activeSelf)
                continue;
            LaneHold hold = child.GetComponent<LaneHold>();
            if (hold != null && hold.enabled ? hold.spacer : child.GetComponent<CardDisplay>() != null)
                cards.Add((RectTransform)child);
        }
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
            else if (card.anchoredPosition != target)
                card.anchoredPosition = (target - card.anchoredPosition).sqrMagnitude < 0.01f ? target : Vector2.Lerp(card.anchoredPosition, target, blend);
        }
        if (placed.Count > cards.Count)
            placed.RemoveWhere(gone ?? (gone = card => card == null || card.parent != transform)); // a card left: forget it
    }

    private System.Predicate<RectTransform> gone;
}

// The battle's choreography in a card lane: a spacer keeps the room of a card that left until it is taken away, and a
// card that arrived is held out of the line until its flight brings it in
public class LaneHold : MonoBehaviour
{
    public bool spacer;

    public static GameObject Spacer(Transform lane)
    {
        GameObject go = new GameObject("LaneSpacer", typeof(RectTransform));
        go.transform.SetParent(lane, false);
        go.AddComponent<LaneHold>().spacer = true;
        return go;
    }

    public static void Hold(GameObject card)
    {
        if (card.GetComponent<LaneHold>() == null)
            card.AddComponent<LaneHold>();
    }

    // A spacer goes (its line closes up); a held card joins its line (which makes room), standing in its place
    public static void Release(GameObject go)
    {
        LaneHold hold = go != null ? go.GetComponent<LaneHold>() : null;
        if (hold == null || !hold.enabled)
            return;
        if (hold.spacer)
        {
            go.transform.SetParent(null, false);
            Destroy(go);
            return;
        }
        hold.enabled = false;
        Destroy(hold);
        CardLane lane = go.transform.parent != null ? go.transform.parent.GetComponent<CardLane>() : null;
        if (lane != null)
            lane.Snap((RectTransform)go.transform);
    }
}

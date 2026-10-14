using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Moving cards over the board (game time, so the pause menu and the autoplay's speed apply). A flight is a "ghost"
// on the overlay (an image of the card, or the object of a card that left its place) gliding to the card's object
// in its new place, following it while its line makes room; the object shows once the ghost lands.
public static class CardMotion
{
    private static RectTransform layer;

    // The overlay's layer for the battle's moving cards and effects, cleared at once when a battle is cut short
    public static Transform Layer
    {
        get
        {
            if (layer == null)
            {
                layer = new GameObject("BoardEffects", typeof(RectTransform)).GetComponent<RectTransform>();
                layer.SetParent(UIOverlay.FixedRoot, false);
                layer.anchorMin = Vector2.zero;
                layer.anchorMax = Vector2.one;
                layer.offsetMin = layer.offsetMax = Vector2.zero; // the overlay's own frame: centred, same size
            }
            return layer;
        }
    }

    public static void ClearLayer()
    {
        if (layer == null)
            return;
        foreach (Transform child in layer)
            Object.Destroy(child.gameObject);
    }

    // Screen rectangle of a UI element, whatever canvas it is on
    public static Rect ScreenRect(RectTransform rect)
    {
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera camera = canvas == null || canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera;
        Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    // The overlay's layout (centre and size) of a screen rectangle
    public static void ToLayout(Rect screen_rect, out Vector2 center, out Vector2 size)
    {
        RectTransform root = (RectTransform)UIOverlay.FixedRoot;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen_rect.center, null, out center);
        size = screen_rect.size / root.GetComponent<Canvas>().scaleFactor;
    }

    // Puts an element of the overlay over a screen rectangle
    public static void Place(RectTransform rect, Rect screen_rect)
    {
        Vector2 center, size;
        ToLayout(screen_rect, out center, out size);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = center; // the overlay's root is centred
        rect.sizeDelta = size;
    }

    // An image of the card on the overlay, over a screen rectangle (never catches clicks)
    public static RectTransform Ghost(int card_id, Rect screen_rect)
    {
        Image art = UIKit.NewImage(Layer, "CardGhost", SpriteCache.Load("Cards/List/591x380/" + card_id), Color.white);
        art.preserveAspect = true;
        Place(art.rectTransform, screen_rect);
        return art.rectTransform;
    }

    // A card object taken out of its place onto the overlay, where it stood (it no longer catches clicks)
    public static RectTransform Lift(GameObject card)
    {
        RectTransform rect = (RectTransform)card.transform;
        Rect screen_rect = ScreenRect(rect);
        rect.SetParent(Layer, false);
        Place(rect, screen_rect);
        Group(card).blocksRaycasts = false;
        return rect;
    }

    // A hidden card does not catch the pointer either (unless it stays clickable: the selected card's empty slot)
    public static void Hide(GameObject card, bool hidden, bool clickable = false)
    {
        SetAlpha(card, hidden ? 0 : 1);
        if (card != null)
            Group(card).blocksRaycasts = !hidden || clickable;
    }

    public static void SetAlpha(GameObject card, float alpha)
    {
        if (card != null)
            Group(card).alpha = alpha;
    }

    private static CanvasGroup Group(GameObject card)
    {
        CanvasGroup group = card.GetComponent<CanvasGroup>();
        return group != null ? group : card.AddComponent<CanvasGroup>();
    }

    // Glides a ghost to the card object (followed as it moves) along a slight arc, shows the object and removes the
    // ghost. Eases out, as The Witcher 3's cards do.
    public static IEnumerator Fly(RectTransform ghost, GameObject target, float seconds, float arc = 0)
    {
        if (target != null)
        {
            Rect last = ScreenRect((RectTransform)target.transform);
            yield return Glide(ghost, () => target != null ? last = ScreenRect((RectTransform)target.transform) : last, seconds, arc);
        }
        Hide(target, false);
        if (ghost != null)
            Object.Destroy(ghost.gameObject);
    }

    // Glides a ghost to a screen rectangle (asked again every frame, as it may move), easing out along a slight arc
    public static IEnumerator Glide(RectTransform ghost, System.Func<Rect> to, float seconds, float arc = 0)
    {
        if (ghost == null)
            yield break;
        Vector2 from_pos = ghost.anchoredPosition, from_size = ghost.sizeDelta;
        float age = 0;
        while (age < seconds && ghost != null)
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / seconds);
            float eased = 1 - (1 - t) * (1 - t) * (1 - t);
            Vector2 to_pos, to_size;
            ToLayout(to(), out to_pos, out to_size);
            Vector2 path = to_pos - from_pos;
            Vector2 bend = path.sqrMagnitude > 1 ? new Vector2(path.y, -path.x).normalized * arc * Mathf.Sin(Mathf.PI * eased) : Vector2.zero;
            ghost.anchoredPosition = Vector2.LerpUnclamped(from_pos, to_pos, eased) + bend;
            ghost.sizeDelta = Vector2.LerpUnclamped(from_size, to_size, eased);
            yield return null;
        }
    }

    // Fades a ghost out where it stands (a card leaving the board for good)
    public static IEnumerator FadeOut(RectTransform ghost, float seconds)
    {
        if (ghost == null)
            yield break;
        CanvasGroup group = Group(ghost.gameObject);
        float age = 0;
        while (ghost != null && age < seconds)
        {
            age += Time.deltaTime;
            group.alpha = 1 - Mathf.Clamp01(age / seconds);
            yield return null;
        }
        if (ghost != null)
            Object.Destroy(ghost.gameObject);
    }

    // A card that appears without coming from anywhere: fades in from slightly larger
    public static void SettleIn(GameObject card)
    {
        CardSettle settle = card.GetComponent<CardSettle>();
        if (settle == null)
            settle = card.AddComponent<CardSettle>();
        settle.Restart();
    }
}

// A card appearing on the board: fades in from slightly larger
public class CardSettle : MonoBehaviour
{
    private const float Seconds = 0.25f;
    private CanvasGroup group;
    private float age;

    public void Restart()
    {
        group = GetComponent<CanvasGroup>();
        if (group == null)
            group = gameObject.AddComponent<CanvasGroup>();
        age = 0;
        enabled = true;
        Apply(0);
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / Seconds);
        Apply(1 - (1 - t) * (1 - t));
        if (t >= 1)
            enabled = false;
    }

    private void Apply(float eased)
    {
        group.alpha = eased;
        transform.localScale = Vector3.one * Mathf.Lerp(1.15f, 1f, eased);
    }

    private void OnDisable()
    {
        if (group != null)
            group.alpha = 1;
        transform.localScale = Vector3.one;
    }
}

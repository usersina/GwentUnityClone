using UnityEngine;
using UnityEngine.UI;

// Card animations of the battle (game time, so the pause menu and the autoplay's speed apply):
// - a played card flies from where it was (the hand, or the opponent's showcase) to its place on the board
// - any other card that appears on the board settles in (fades in from slightly larger)
// - the opponent's card is shown large for a moment before it is played
public static class CardMotion
{
    public const float FlightSeconds = 0.3f;
    public const float ShowcaseSeconds = 0.9f;

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

    public static void Fly(int card_id, Rect from, GameObject target)
    {
        GameObject ghost = Ghost("CardFlight", card_id, from);
        ghost.AddComponent<CardFlight>().Init(from, target);
    }

    public static void SettleIn(GameObject card)
    {
        CardSettle settle = card.GetComponent<CardSettle>();
        if (settle == null)
            settle = card.AddComponent<CardSettle>();
        settle.Restart();
    }

    // The opponent's card, large in the middle of the board; returns where it is, for the flight that follows
    public static Rect Showcase(int card_id)
    {
        float height = Screen.height * 0.42f;
        float width = height * 380f / 591f;
        Rect rect = new Rect(Screen.width * 0.53f - width / 2, Screen.height * 0.53f - height / 2, width, height);
        GameObject ghost = Ghost("CardShowcase", card_id, rect);
        ghost.AddComponent<CardShowcaseLife>();
        return rect;
    }

    private static GameObject Ghost(string name, int card_id, Rect screen_rect)
    {
        RectTransform root = (RectTransform)UIOverlay.FixedRoot;
        Image art = UIKit.NewImage(root, name, SpriteCache.Load("Cards/List/591x380/" + card_id), Color.white);
        art.raycastTarget = false;
        art.preserveAspect = true;
        Place(art.rectTransform, screen_rect);
        return art.gameObject;
    }

    // Puts a rect of the overlay over a screen rectangle
    public static void Place(RectTransform rect, Rect screen_rect)
    {
        RectTransform root = (RectTransform)rect.parent;
        Vector2 center;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen_rect.center, null, out center);
        float scale = root.GetComponentInParent<Canvas>().rootCanvas.scaleFactor;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = center; // the overlay's root is centered (pivot in the middle)
        rect.sizeDelta = screen_rect.size / scale;
    }
}

// A played card on its way: follows the target as the row lays it out, the target shows once it lands
public class CardFlight : MonoBehaviour
{
    private Rect from;
    private GameObject target;
    private CanvasGroup targetGroup;
    private float age;

    public void Init(Rect from_rect, GameObject target_card)
    {
        from = from_rect;
        target = target_card;
        targetGroup = target.GetComponent<CanvasGroup>();
        if (targetGroup == null)
            targetGroup = target.AddComponent<CanvasGroup>();
        targetGroup.alpha = 0;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / CardMotion.FlightSeconds);
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }
        float eased = 1 - (1 - t) * (1 - t) * (1 - t);
        Rect to = CardMotion.ScreenRect((RectTransform)target.transform);
        Rect now = new Rect(Vector2.Lerp(from.position, to.position, eased), Vector2.Lerp(from.size, to.size, eased));
        CardMotion.Place((RectTransform)transform, now);
        if (t >= 1)
        {
            targetGroup.alpha = 1;
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (targetGroup != null)
            targetGroup.alpha = 1;
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

// The opponent's showcased card: pops in, waits, and is gone by the time its flight starts
public class CardShowcaseLife : MonoBehaviour
{
    private float age;

    private void Update()
    {
        age += Time.deltaTime;
        float pop = Mathf.Clamp01(age / 0.15f);
        transform.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, 1 - (1 - pop) * (1 - pop));
        if (age >= CardMotion.ShowcaseSeconds)
            Destroy(gameObject);
    }
}

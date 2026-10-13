using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The redraw phase as in The Witcher 3: the hand as big cards in a carousel. A side card (or the arrow keys, A/D,
// the mouse wheel) comes to the front; the front card (or Enter) is redrawn. Double-clicking a card in the hand
// still works, and the carousel follows the hand.
public class RedrawPicker : MonoBehaviour
{
    public const float CardWidth = 300;
    private const float CardHeight = CardWidth * 591f / 380f;
    private const float Spacing = 245;     // between side cards
    private const float FrontGap = 45;     // extra room on each side of the front card
    private const float SideScale = 0.74f;
    private const int VisibleSide = 3;     // cards shown on each side of the front one
    private const float RedrawCooldown = 0.35f;

    private static RedrawPicker current;

    private SceneController controller;
    private Image details;
    private int detailsFor = -1;
    private readonly List<int> shownHand = new List<int>();
    private readonly List<CarouselCard> cards = new List<CarouselCard>();
    private int focus;
    private float lastRedraw = float.NegativeInfinity;
    private float scrolled;

    private class CarouselCard
    {
        public RectTransform rect;
        public CanvasGroup group;
        public Image glow;
    }

    // While open, hovering hand cards shows no big preview on the right
    public static bool IsOpen { get { return current != null; } }

    public int FocusIndex { get { return focus; } }
    public int CardCount { get { return cards.Count; } }
    public int FocusedCardId { get { return focus < shownHand.Count ? shownHand[focus] : -1; } }

    public RectTransform CardAt(int index)
    {
        return index >= 0 && index < cards.Count ? cards[index].rect : null;
    }

    public static RedrawPicker Create(Transform parent, SceneController controller, Vector2 center)
    {
        RectTransform root = UIKit.Rect("RedrawPicker", parent).Place(new Vector2(0.5f, 0.5f), center, new Vector2(1920, CardHeight + 60));
        // A soft dark pool behind the cards separates them from the board
        Image backdrop = UIKit.NewImage(root, "Backdrop", UITheme.Current.radial, new Color(0, 0, 0, 0.75f));
        backdrop.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2300, CardHeight + 260));
        RedrawPicker picker = root.gameObject.AddComponent<RedrawPicker>();
        picker.controller = controller;
        // The front card's description under it, like The Witcher 3's tooltip
        picker.details = UIKit.NewImage(root, "Details", null, Color.white);
        picker.details.preserveAspect = true;
        picker.details.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -CardHeight / 2 - 12 - 95), new Vector2(230, 190));
        picker.Rebuild();
        current = picker;
        return picker;
    }

    private void OnDestroy()
    {
        if (current == this)
            current = null;
    }

    private void Update()
    {
        if (!SameHand())
            Rebuild();
        HandleInput();
        Animate(1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f));
        UpdateDetails();
    }

    private void UpdateDetails()
    {
        int id = FocusedCardId;
        if (id == detailsFor)
            return;
        detailsFor = id;
        Sprite sprite = id >= 0 ? SpriteCache.Load(CardHover.EffectBoxPath(controller.Db.Get(id))) : null;
        details.sprite = sprite;
        details.enabled = sprite != null;
    }

    //----------------------------------------Cards----------------------------------------//
    private bool SameHand()
    {
        List<int> hand = controller.PlayerInfo.HandList;
        if (hand.Count != shownHand.Count)
            return false;
        for (int i = 0; i < hand.Count; i++)
            if (hand[i] != shownHand[i])
                return false;
        return true;
    }

    private void Rebuild()
    {
        foreach (CarouselCard card in cards)
            Destroy(card.rect.gameObject);
        cards.Clear();
        shownHand.Clear();
        shownHand.AddRange(controller.PlayerInfo.HandList);

        UITheme theme = UITheme.Current;
        for (int i = 0; i < shownHand.Count; i++)
        {
            RectTransform holder = UIKit.Rect("Card " + shownHand[i], transform).Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardWidth, CardHeight));
            Image glow = UIKit.NewImage(holder, "Glow", theme.softShadow, UITheme.WithAlpha(theme.goldBright, 0));
            glow.rectTransform.Stretch(-34, -34, -34, -34);
            Image shadow = UIKit.NewImage(holder, "Shadow", theme.softShadow, new Color(0, 0, 0, 0.7f));
            shadow.rectTransform.Stretch(-16, -26, -10, -34);
            Image art = UIKit.NewImage(holder, "Art", SpriteCache.Load("Cards/List/591x380/" + shownHand[i]), Color.white, true);
            art.rectTransform.Stretch();
            art.preserveAspect = true;

            CanvasGroup group = holder.gameObject.AddComponent<CanvasGroup>();
            holder.gameObject.AddComponent<RedrawPickerCard>().Init(this, i);
            cards.Add(new CarouselCard { rect = holder, group = group, glow = glow });
        }
        focus = Mathf.Clamp(focus, 0, Mathf.Max(0, cards.Count - 1));
        Animate(1f);
    }

    private void Animate(float blend)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            int offset = i - focus;
            int distance = Mathf.Abs(offset);
            // Side cards start past the front card's half width plus the gap, then every Spacing
            float x = offset == 0 ? 0 : Mathf.Sign(offset) * (FrontGap + CardWidth * (1 + SideScale) / 2 + (distance - 1) * Spacing);
            float scale = offset == 0 ? 1f : SideScale;
            float alpha = offset == 0 ? 1f : distance > VisibleSide ? 0f : 0.9f - 0.18f * (distance - 1);
            CarouselCard card = cards[i];
            card.rect.anchoredPosition = Vector2.Lerp(card.rect.anchoredPosition, new Vector2(x, 0), blend);
            card.rect.localScale = Vector3.Lerp(card.rect.localScale, Vector3.one * scale, blend);
            card.group.alpha = Mathf.Lerp(card.group.alpha, alpha, blend);
            card.group.blocksRaycasts = distance <= VisibleSide;
            card.glow.color = Color.Lerp(card.glow.color, UITheme.WithAlpha(UITheme.Current.goldBright, offset == 0 ? 0.85f : 0), blend);
        }
        // Drawn from the outside in, so the front card is on top (sibling 0 is the backdrop)
        List<int> order = new List<int>();
        for (int i = 0; i < cards.Count; i++)
            order.Add(i);
        order.Sort((a, b) => Mathf.Abs(b - focus).CompareTo(Mathf.Abs(a - focus)));
        for (int i = 0; i < order.Count; i++)
            if (cards[order[i]].rect.GetSiblingIndex() != i + 1)
                cards[order[i]].rect.SetSiblingIndex(i + 1);
    }

    //----------------------------------------Input----------------------------------------//
    public void OnCardClicked(int index)
    {
        if (index == focus)
            RedrawFocused();
        else
            SetFocus(index);
    }

    public void SetFocus(int index)
    {
        int next = Mathf.Clamp(index, 0, cards.Count - 1);
        if (next == focus)
            return;
        focus = next;
        GameAudio.PlaySfx("card_select");
    }

    public void RedrawFocused()
    {
        if (Time.unscaledTime - lastRedraw < RedrawCooldown || focus >= shownHand.Count)
            return;
        lastRedraw = Time.unscaledTime;
        controller.RedrawPlayerCard(shownHand[focus]);
    }

    private void HandleInput()
    {
        if (UIOverlay.HasModal || !controller.PlayerInfo.CanExchange)
            return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
                SetFocus(focus - 1);
            if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
                SetFocus(focus + 1);
            // Enter redraws, unless a button elsewhere has keyboard focus (Enter presses that button)
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            bool other_button = selected != null && !selected.transform.IsChildOf(transform);
            if (!other_button && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
                RedrawFocused();
        }
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            scrolled += mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scrolled) >= 1f)
            {
                SetFocus(focus + (scrolled > 0 ? -1 : 1));
                scrolled = 0;
            }
        }
    }
}

// Click on a card of the redraw carousel (left button, or a tap)
public class RedrawPickerCard : MonoBehaviour, IPointerClickHandler
{
    private RedrawPicker picker;
    private int index;

    public void Init(RedrawPicker owner, int card_index)
    {
        picker = owner;
        index = card_index;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            picker.OnCardClicked(index);
    }
}

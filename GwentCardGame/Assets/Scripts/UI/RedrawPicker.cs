using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The redraw phase as in The Witcher 3: the hand as big cards in a carousel. A side card (or the arrow keys, A/D,
// the mouse wheel) comes to the front; the front card (or Enter) is redrawn; Done (or Space) keeps the hand.
// Double-clicking a card in the hand still works, and the carousel follows the hand.
public class RedrawPicker : MonoBehaviour
{
    public const float CardWidth = 300;
    private const float CardHeight = CardWidth * 591f / 380f;
    private const float Spacing = 245;     // between side cards
    private const float FrontGap = 45;     // extra room on each side of the front card
    private const float SideScale = 0.74f;
    private const int VisibleSide = 3;     // cards shown on each side of the front one
    private const float RedrawCooldown = 0.35f;
    private const float RedrawFlightSeconds = 0.3f;

    private static RedrawPicker current;

    private SceneController controller;
    private Image details;
    private int detailsFor = -1;
    private TextMeshProUGUI counter;
    private int counterShows = -1;
    private readonly List<int> shownHand = new List<int>();
    private readonly List<CarouselCard> cards = new List<CarouselCard>();
    private int focus;
    private float lastRedraw = float.NegativeInfinity;
    private float scrolled;
    private int arriving = -1; // the card coming up from the deck into its place, hidden until it lands
    private int flights;       // redraws still on their way
    private float frontSince;  // when the front card came forward
    private int frontCard = -1;
    private bool detailsPlaced;

    private class CarouselCard
    {
        public RectTransform rect;
        public CanvasGroup group;
        public Image glow;
    }

    // While open, hovering hand cards shows no big preview on the right
    public static bool IsOpen { get { return current != null; } }

    // A card is redrawn only once it has been at the front for a moment: a quick double-click on a card further away
    // brings it forward and does not swap it by accident
    public static bool IsReadyToRedraw(int card_id)
    {
        return current != null && current.FocusedCardId == card_id && Time.unscaledTime - current.frontSince >= DoubleClick.Interval;
    }

    // Brings a card of the hand to the front (a click on it in the hand)
    public static void Bring(int card_id)
    {
        if (current != null && current.shownHand.Contains(card_id))
            current.SetFocus(current.shownHand.IndexOf(card_id));
    }

    // A redrawn card is still on its way (the phase ends once it has landed)
    public static bool IsAnimating { get { return current != null && current.flights > 0; } }

    public int FocusIndex { get { return focus; } }
    public int CardCount { get { return cards.Count; } }
    public int FocusedCardId { get { return focus < shownHand.Count ? shownHand[focus] : -1; } }

    public RectTransform CardAt(int index)
    {
        return index >= 0 && index < cards.Count ? cards[index].rect : null;
    }

    // The whole redraw phase UI: a dark band "Choose a card to redraw. 0/2" (with the Done button) above the
    // carousel. Returns its root, which the battle destroys when the phase ends.
    public static GameObject ShowPhase(SceneController controller)
    {
        UITheme theme = UITheme.Current;
        RectTransform root = UIKit.Rect("MulliganPrompt", UIOverlay.Root).Stretch();
        RectTransform band = UIKit.NewImage(root, "Band", null, new Color(0, 0, 0, 0.78f)).rectTransform;
        band.anchorMin = new Vector2(0, 1);
        band.anchorMax = new Vector2(1, 1);
        band.pivot = new Vector2(0.5f, 0.5f);
        band.anchoredPosition = new Vector2(0, -150);
        band.sizeDelta = new Vector2(0, 116);
        foreach (float edge in new[] { 0f, 1f })
        {
            Image line = UIKit.NewImage(band, "Edge", theme.gradientH, UITheme.WithAlpha(theme.gold, 0.5f));
            line.rectTransform.anchorMin = new Vector2(0, edge);
            line.rectTransform.anchorMax = new Vector2(1, edge);
            line.rectTransform.sizeDelta = new Vector2(0, 2);
        }

        TextMeshProUGUI counter = UIKit.Text(band, "", UIKit.TextStyle.Heading, TextAlignmentOptions.Center);
        counter.fontSize = 36;
        counter.color = theme.text;
        counter.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -38), new Vector2(1100, 50));
        string how = GameInput.IsTouch
            ? "Tap a card to bring it forward, tap it again to redraw it"
            : "Click a card to bring it forward, click it again (or Enter) to redraw it. Right-click or Space keeps your hand";
        TextMeshProUGUI hint = UIKit.Text(band, how, UIKit.TextStyle.Caption, TextAlignmentOptions.Center);
        hint.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -82), new Vector2(1300, 34));

        Button done = UIKit.Button(band, "Done", UIKit.ButtonStyle.Primary, controller.FinishCardExchange, new Vector2(200, 60));
        ((RectTransform)done.transform).Place(new Vector2(1f, 0.5f), new Vector2(-190, 0), new Vector2(200, 60));
        if (!GameInput.IsTouch)
        {
            // Space is Done too (as it passes in the battle: "I'm done")
            done.GetComponentInChildren<TextMeshProUGUI>().text = "<size=72%>[Space]</size>  Done";
            ((RectTransform)done.transform).sizeDelta = new Vector2(250, 60);
        }

        RedrawPicker picker = Create(root, controller, new Vector2(0, 50));
        picker.counter = counter;
        picker.UpdateCounter();
        return root.gameObject;
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
        if (FocusedCardId != frontCard)
        {
            frontCard = FocusedCardId;
            frontSince = Time.unscaledTime;
        }
        if (!detailsPlaced)
        {
            // The front card's description where the card info's description goes: at the bottom right
            PlaceOnScreen(details.rectTransform, CardShowcase.DescriptionScreenRect());
            detailsPlaced = true;
        }
        HandleInput();
        Animate(1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f));
        UpdateDetails();
        UpdateCounter();
    }

    private void UpdateCounter()
    {
        int redrawn = controller.PlayerInfo.CardsExchanged;
        if (counter == null || counterShows == redrawn)
            return;
        counterShows = redrawn;
        counter.text = "Choose a card to redraw. " + redrawn + "/2";
    }

    private void UpdateDetails()
    {
        int id = FocusedCardId;
        if (id == detailsFor)
            return;
        detailsFor = id;
        string description = id >= 0 ? CardHover.EffectBoxPath(controller.Db.Get(id)) : null; // none for a plain unit
        Sprite sprite = description != null ? SpriteCache.Load(description) : null;
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
        // A redraw replaces one card in place: as in The Witcher 3, it goes down into the deck and the new card comes
        // up from the deck into its place
        int replaced = ReplacedIndex();
        Rect slot = replaced >= 0 ? CardMotion.ScreenRect(cards[replaced].rect) : new Rect();
        int redrawn = replaced >= 0 ? shownHand[replaced] : -1;

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
        if (replaced >= 0)
            StartCoroutine(RedrawFlight(redrawn, slot, replaced));
        Animate(1f);
    }

    private int ReplacedIndex()
    {
        List<int> hand = controller.PlayerInfo.HandList;
        if (hand.Count != shownHand.Count || cards.Count != shownHand.Count)
            return -1;
        int changed = -1;
        for (int i = 0; i < hand.Count; i++)
            if (hand[i] != shownHand[i])
            {
                if (changed >= 0)
                    return -1;
                changed = i;
            }
        return changed;
    }

    private IEnumerator RedrawFlight(int redrawn, Rect slot, int index)
    {
        flights++;
        arriving = index;
        Rect deck = controller.DeckScreenRect(true);
        yield return Glide(Ghost(redrawn, slot), () => deck, true);
        if (index < cards.Count)
        {
            RectTransform target = cards[index].rect;
            yield return Glide(Ghost(shownHand[index], deck), () => target != null ? CardMotion.ScreenRect(target) : deck, true);
        }
        if (arriving == index)
            arriving = -1;
        flights--;
    }

    // An image of a card on the carousel's canvas (above the board), over a screen rectangle
    private RectTransform Ghost(int card_id, Rect screen_rect)
    {
        Image art = UIKit.NewImage(transform, "RedrawGhost", SpriteCache.Load("Cards/List/591x380/" + card_id), Color.white);
        art.preserveAspect = true;
        PlaceOnScreen(art.rectTransform, screen_rect);
        return art.rectTransform;
    }

    private void PlaceOnScreen(RectTransform rect, Rect screen_rect)
    {
        Vector2 min, max;
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, screen_rect.min, null, out min);
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, screen_rect.max, null, out max);
        Vector2 pivot_offset = Vector2.Scale(((RectTransform)transform).rect.size, ((RectTransform)transform).pivot - new Vector2(0.5f, 0.5f));
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = (min + max) / 2 + pivot_offset; // local points are from the pivot, anchors from the centre
        rect.sizeDelta = max - min;
    }

    // Glides a ghost to a screen rectangle (easing out), then removes it
    private IEnumerator Glide(RectTransform ghost, Func<Rect> to, bool remove)
    {
        Rect from = CardMotion.ScreenRect(ghost);
        float age = 0;
        while (age < RedrawFlightSeconds && ghost != null)
        {
            age += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(age / RedrawFlightSeconds);
            float eased = 1 - (1 - t) * (1 - t) * (1 - t);
            Rect target = to();
            PlaceOnScreen(ghost, new Rect(Vector2.LerpUnclamped(from.position, target.position, eased), Vector2.LerpUnclamped(from.size, target.size, eased)));
            yield return null;
        }
        if (remove && ghost != null)
            Destroy(ghost.gameObject);
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
            card.group.alpha = i == arriving ? 0 : Mathf.Lerp(card.group.alpha, alpha, blend);
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
        if (index != focus)
            SetFocus(index);
        else if (IsReadyToRedraw(FocusedCardId))
            RedrawFocused();
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
        // Not a card still on its way or just brought forward (the same guards as a click)
        if (Time.unscaledTime - lastRedraw < RedrawCooldown || focus >= shownHand.Count || IsAnimating || !IsReadyToRedraw(FocusedCardId))
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
            bool other_button = selected != null && selected.activeInHierarchy && !selected.transform.IsChildOf(transform);
            if (!other_button && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
                RedrawFocused();
            // Space keeps the hand, like the Done button
            if (keyboard.spaceKey.wasPressedThisFrame)
                controller.FinishCardExchange();
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

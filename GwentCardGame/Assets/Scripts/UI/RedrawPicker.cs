using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The redraw phase as in The Witcher 3: the hand in the carousel. A side card (or the arrow keys, A/D, the mouse
// wheel) comes to the front; the front card (or Enter) is redrawn; Done (or Space) keeps the hand. Double-clicking a
// card in the hand still works, and the carousel follows the hand.
public class RedrawPicker : CardCarousel
{
    private const float RedrawCooldown = 0.35f;
    private const float RedrawFlightSeconds = 0.3f;

    private static RedrawPicker current;

    private SceneController controller;
    private TextMeshProUGUI counter;
    private int counterShows = -1;
    private float lastRedraw = float.NegativeInfinity;
    private int flights; // redraws still on their way

    // While open, hovering hand cards shows no big preview on the right
    public static bool IsOpen { get { return current != null; } }

    // A card is redrawn only once it has been at the front for a moment: a quick double-click on a card further away
    // brings it forward and does not swap it by accident
    public static bool IsReadyToRedraw(int card_id)
    {
        return current != null && current.FocusedCardId == card_id && current.FrontIsReady;
    }

    // Brings a card of the hand to the front (a click on it in the hand)
    public static void Bring(int card_id)
    {
        if (current != null && current.shown.Contains(card_id))
            current.SetFocus(current.shown.IndexOf(card_id));
    }

    // A redrawn card is still on its way (the phase ends once it has landed)
    public static bool IsAnimating { get { return current != null && current.flights > 0; } }

    // The whole redraw phase UI: a dark band "Choose a card to redraw. 0/2" (with the Done button) above the
    // carousel. Returns its root, which the battle destroys when the phase ends.
    public static GameObject ShowPhase(SceneController controller)
    {
        RectTransform root = UIKit.Rect("MulliganPrompt", UIOverlay.Root).Stretch();
        string how = GameInput.IsTouch
            ? "Tap a card to bring it forward, tap it again to redraw it"
            : "Click a card to bring it forward, click it again (or Enter) to redraw it. Right-click or Space keeps your hand";
        TextMeshProUGUI counter;
        Button done = Band(root, "", how, "Done", controller.FinishCardExchange, out counter);
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
        RedrawPicker picker = Create<RedrawPicker>(parent, center);
        picker.name = "RedrawPicker";
        picker.controller = controller;
        picker.Rebuild(controller.PlayerInfo.HandList);
        current = picker;
        return picker;
    }

    private void OnDestroy()
    {
        if (current == this)
            current = null;
    }

    protected override void Update()
    {
        if (!SameHand())
            FollowHand();
        base.Update();
        UpdateCounter();
    }

    protected override bool TakesInput()
    {
        return base.TakesInput() && controller.PlayerInfo.CanExchange;
    }

    private void UpdateCounter()
    {
        int redrawn = controller.PlayerInfo.CardsExchanged;
        if (counter == null || counterShows == redrawn)
            return;
        counterShows = redrawn;
        counter.text = "Choose a card to redraw. " + redrawn + "/2";
    }

    //----------------------------------------Cards----------------------------------------//
    private bool SameHand()
    {
        List<int> hand = controller.PlayerInfo.HandList;
        if (hand.Count != shown.Count)
            return false;
        for (int i = 0; i < hand.Count; i++)
            if (hand[i] != shown[i])
                return false;
        return true;
    }

    // A redraw replaces one card in place: as in The Witcher 3, it goes down into the deck and the new card comes up
    // from the deck into its place
    private void FollowHand()
    {
        int replaced = ReplacedIndex();
        Rect slot = replaced >= 0 ? CardMotion.ScreenRect(CardAt(replaced)) : new Rect();
        int redrawn = replaced >= 0 ? shown[replaced] : -1;
        Rebuild(controller.PlayerInfo.HandList);
        if (replaced >= 0)
            StartCoroutine(RedrawFlight(redrawn, slot, replaced));
    }

    private int ReplacedIndex()
    {
        List<int> hand = controller.PlayerInfo.HandList;
        if (hand.Count != shown.Count || CardCount != shown.Count)
            return -1;
        int changed = -1;
        for (int i = 0; i < hand.Count; i++)
            if (hand[i] != shown[i])
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
        hidden = index;
        Rect deck = controller.DeckScreenRect(true);
        yield return Glide(Ghost(redrawn, slot), () => deck, true);
        if (index < CardCount)
        {
            RectTransform target = CardAt(index);
            yield return Glide(Ghost(shown[index], deck), () => target != null ? CardMotion.ScreenRect(target) : deck, true);
        }
        if (hidden == index)
            hidden = -1;
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

    //----------------------------------------Input----------------------------------------//
    protected override void Choose()
    {
        RedrawFocused();
    }

    public void RedrawFocused()
    {
        // Not a card still on its way or just brought forward (the same guards as a click)
        if (Time.unscaledTime - lastRedraw < RedrawCooldown || focus >= shown.Count || IsAnimating || !IsReadyToRedraw(FocusedCardId))
            return;
        lastRedraw = Time.unscaledTime;
        controller.RedrawPlayerCard(shown[focus]);
    }

    protected override void HandleKeys(Keyboard keyboard)
    {
        base.HandleKeys(keyboard);
        // Space keeps the hand, like the Done button
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            controller.FinishCardExchange();
    }
}

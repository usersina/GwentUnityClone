using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Full-screen card picker (medic, leader abilities). Always has a Skip button so it can never trap the player.
// Keys: Left/Right (A/D) go through the cards, Enter chooses one; Down goes to Skip (Enter presses it), Up comes back.
public class PanelManager : MonoBehaviour
{
    private const string FocusMark = "KeyFocus";

    public GameObject container;
    public GameObject cardBtn;

    private Button skipButton;
    private int focus = -1;
    private static int keyFrame = -1;

    // Enter chose a card this frame: the battle's own keys leave it alone (the picked card waits for its place)
    public static bool UsedKeyThisFrame { get { return keyFrame == Time.frameCount; } }

    // The card the keys are on (-1: none)
    public int Focus { get { return focus; } }

    public void ShowToPlay(string field, List<int> discard_list, bool is_player_card)
    {
        Show(field, discard_list, "play", is_player_card);
    }

    // Show the deck/discard then pick one card to add to the hand
    public void ShowToDraw(string field, List<int> deck_list, bool is_player_card)
    {
        Show(field, deck_list, "draw", is_player_card);
    }

    // Showing the opponent's hand
    public void ShowToView(List<int> hand_list, bool is_player_card)
    {
        Show(null, hand_list, "view", is_player_card);
    }

    private void Show(string field, List<int> card_ids, string type, bool is_player_card)
    {
        ClearContainer();
        focus = -1;
        foreach (int card_id in card_ids)
        {
            GameObject instantiatedCardbtn = Instantiate(cardBtn);
            CardBtn btn = instantiatedCardbtn.GetComponent<CardBtn>();
            btn.cardId = card_id;
            btn.type = type;
            btn.field = field;
            btn.isPlayerCard = is_player_card;
            instantiatedCardbtn.GetComponent<Image>().sprite = SpriteCache.Load("Cards/List/591x380/" + card_id);
            instantiatedCardbtn.transform.SetParent(container.transform, false);
        }

        ResizeContainer(card_ids.Count);
        EnsureSkipButton(type == "view" ? "Close" : "Skip");
    }

    private void ClearContainer()
    {
        for (int i = 0; i < container.transform.childCount; i++)
            Destroy(container.transform.GetChild(i).gameObject);
        // Do not remove this, otherwise count will still get the previous frame's count (NOT 0);
        container.transform.DetachChildren();
    }

    private void EnsureSkipButton(string label)
    {
        if (skipButton == null)
        {
            skipButton = UIOverlay.CreateButton(transform, label, new Vector2(0.5f, 0f), new Vector2(0, 70), new Vector2(240, 70), OnSkip);
            skipButton.gameObject.name = "SkipButton";
            skipButton.navigation = new Navigation { mode = Navigation.Mode.None }; // the arrows are the picker's
        }
        skipButton.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = label;
        skipButton.transform.SetAsLastSibling();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || UIOverlay.HasModal || container.transform.childCount == 0)
            return;
        bool on_skip = skipButton != null && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == skipButton.gameObject;
        if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
            SetFocus(focus < 0 ? 0 : focus - 1);
        if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
            SetFocus(focus < 0 ? 0 : focus + 1);
        if ((keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) && !on_skip)
        {
            SetFocus(-1);
            UIKit.Focus(skipButton);
        }
        if ((keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) && on_skip)
            SetFocus(0);
        // Enter on Skip is the button's
        if ((keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) && !on_skip && focus >= 0)
        {
            keyFrame = Time.frameCount;
            CardBtn chosen = container.transform.GetChild(focus).GetComponent<CardBtn>();
            SetFocus(-1);
            chosen.Choose();
        }
    }

    // Frames the card the keys are on (-1: none) and scrolls it into view
    private void SetFocus(int index)
    {
        int count = container.transform.childCount;
        int next = index < 0 || count == 0 ? -1 : Mathf.Clamp(index, 0, count - 1);
        if (next == focus)
            return;
        if (focus >= 0 && focus < count)
        {
            Transform mark = container.transform.GetChild(focus).Find(FocusMark);
            if (mark != null)
            {
                mark.SetParent(null, false);
                Destroy(mark.gameObject);
            }
        }
        focus = next;
        if (focus < 0)
            return;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
        GameAudio.PlaySfx("card_select");
        UIKit.Frame(container.transform.GetChild(focus), FocusMark, UITheme.Current.goldBright, 5, 4);
        ScrollRect scroll = container.GetComponentInParent<ScrollRect>();
        if (scroll != null && count > 3)
            scroll.horizontalNormalizedPosition = (float)focus / (count - 1);
    }

    private void OnSkip()
    {
        SceneController.Instance.CancelPanel();
    }

    public void ResizeContainer(int cardsNum)
    {
        RectTransform containerTr = container.GetComponent<RectTransform>();
        // Up to 3 cards fit the view: they are centred in it; more scroll sideways
        container.GetComponent<UnityEngine.UI.GridLayoutGroup>().childAlignment = cardsNum <= 3 ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
        cardsNum -= 3;                                           // 3 Cards already have space
        float width = Mathf.Max(0, (cardsNum * 380) + (cardsNum - 1) * 25);   // Card width: 380, Spacing: 25
        containerTr.offsetMin = new Vector2(0, 0);
        containerTr.offsetMax = new Vector2(width, 0);
    }
}

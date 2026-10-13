using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Full-screen card picker (medic, leader abilities). Always has a Skip button so it can never trap the player.
public class PanelManager : MonoBehaviour
{
    public GameObject container;
    public GameObject cardBtn;

    private Button skipButton;

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
        }
        skipButton.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = label;
        skipButton.transform.SetAsLastSibling();
    }

    private void OnSkip()
    {
        SceneController.Instance.CancelPanel();
    }

    public void ResizeContainer(int cardsNum)
    {
        RectTransform containerTr = container.GetComponent<RectTransform>();
        cardsNum -= 3;                                           // 3 Cards already have space
        float width = (cardsNum * 380) + (cardsNum - 1) * 25;   // Card width: 380, Spacing: 25
        containerTr.offsetMin = new Vector2(0, 0);
        containerTr.offsetMax = new Vector2(width, 0);
    }
}

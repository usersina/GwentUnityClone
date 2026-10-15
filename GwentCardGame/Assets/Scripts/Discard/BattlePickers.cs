using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The battle's card pickers (a medic's revival, the leaders' picks, Emhyr's look at the opponent's hand), shown as The
// Witcher 3 does: the carousel under a title band (CardPicker). Skip (Close for a look) gives up the choice, so a picker
// can never trap the player. The panel itself only keeps the board from taking clicks while it is open.
public class BattlePickers : MonoBehaviour
{
    private CardPicker picker;
    private readonly List<int> cards = new List<int>();
    private string field;
    private string type;
    private bool isPlayerCard;

    // The cards on offer, and the carousel showing them
    public IList<int> Cards { get { return cards.AsReadOnly(); } }
    public CardPicker Picker { get { return picker; } }

    private void Awake()
    {
        // The scene's scroll view and dark backdrop gave way to the carousel; the panel still catches the board's clicks
        Transform legacy = transform.Find("ScrollView");
        if (legacy != null)
            legacy.gameObject.SetActive(false);
        Image backdrop = GetComponent<Image>();
        if (backdrop != null)
            backdrop.color = Color.clear;
    }

    private void OnDisable()
    {
        ClosePicker();
    }

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
        ClosePicker();
        this.field = field;
        this.type = type;
        isPlayerCard = is_player_card;
        cards.Clear();
        cards.AddRange(card_ids);
        bool look = type == "view";
        picker = CardPicker.Open(UIOverlay.Root, Title(type, field), cards, look ? (Action<int>)null : Choose, look ? "Close" : "Skip", OnSkip);
    }

    private static string Title(string type, string field)
    {
        switch (type)
        {
            case "play":
                return field == "deck_list" ? "Choose a weather card to play from your deck" : "Choose a unit to bring back from your discard pile";
            case "draw":
                if (field == "opp_discard_list")
                    return "Choose a unit to take from your opponent's discard pile";
                return field == "discard_list" ? "Choose a unit to take back from your discard pile" : "Choose a card to take from your deck";
            default:
                return "Randomly selected cards from your opponent's hand"; // The Witcher 3's words
        }
    }

    private void ClosePicker()
    {
        if (picker != null)
            Destroy(picker.transform.parent.gameObject);
        picker = null;
    }

    // Skip / Close: give up the choice and end the turn
    private void OnSkip()
    {
        BattleController.Instance.CancelPanel();
    }

    // A card was picked
    public void Choose(int card_id)
    {
        BattleController controller = BattleController.Instance;
        switch (type)
        {
            // A medic or a weather leader: the card is placed on the board next
            case "play":
                GameAudio.PlaySfx("ability_medic");
                GameLog.Info("Choosing card to play: " + card_id);
                controller.TakePickedCardToPlay(field, card_id, isPlayerCard);
                GameObject card = controller.CreateCardObject(controller.GetCard(card_id), isPlayerCard);
                card.transform.SetParent(controller.TempObject != null ? controller.TempObject.transform : null, false);
                controller.selectedCard = card;
                controller.HighlightField(card.GetComponent<CardFace>().Card, isPlayerCard);
                controller.ShowcasePicked(card_id, field, isPlayerCard);
                controller.SetHider(true);
                gameObject.SetActive(false);
                UIOverlay.Toast("Now choose where to play " + controller.GetCard(card_id).name);
                break;

            // A leader's pick: the card goes to the hand and the turn ends
            case "draw":
                GameAudio.PlaySfx("card_draw");
                GameLog.Info("Choosing on: " + card_id);
                gameObject.SetActive(false);
                controller.PickCardToHand(field, card_id, isPlayerCard);
                break;

            // Emhyr's look at the opponent's hand
            default:
                GameAudio.PlaySfx("card_select");
                gameObject.SetActive(false);
                controller.EndTurn(isPlayerCard);
                break;
        }
    }
}

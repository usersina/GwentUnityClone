using System;
using System.Collections.Generic;
using Gwent.Core;
using Gwent.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Gwent.Battle
{
    // Where a picker's cards come from
    public enum PickSource { Deck, Discard, OpponentDiscard, OpponentHand }

    // The battle's card pickers (a medic's revival, the leaders' picks, Emhyr's look at the opponent's hand), shown as The
    // Witcher 3 does: the carousel under a title band (CardPicker). Skip (Close for a look) gives up the choice, so a picker
    // can never trap the player. The panel itself only keeps the board from taking clicks while it is open.
    public class BattlePickers : MonoBehaviour
    {
        // A card to play (a medic's revival, a weather leader), to take into the hand (a leader's pick), or a look
        private enum Mode { Play, Draw, View }

        private CardPicker picker;
        private readonly List<int> cards = new List<int>();
        private PickSource field;
        private Mode mode;
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

        public void ShowToPlay(PickSource field, List<int> discard_list, bool is_player_card)
        {
            Show(field, discard_list, Mode.Play, is_player_card);
        }

        // Show the deck/discard then pick one card to add to the hand
        public void ShowToDraw(PickSource field, List<int> deck_list, bool is_player_card)
        {
            Show(field, deck_list, Mode.Draw, is_player_card);
        }

        // Showing the opponent's hand
        public void ShowToView(List<int> hand_list, bool is_player_card)
        {
            Show(PickSource.OpponentHand, hand_list, Mode.View, is_player_card);
        }

        private void Show(PickSource field, List<int> card_ids, Mode mode, bool is_player_card)
        {
            ClosePicker();
            this.field = field;
            this.mode = mode;
            isPlayerCard = is_player_card;
            cards.Clear();
            cards.AddRange(card_ids);
            bool look = mode == Mode.View;
            picker = CardPicker.Open(UIOverlay.Root, Title(mode, field), cards, look ? (Action<int>)null : Choose, look ? "Close" : "Skip", OnSkip);
        }

        private static string Title(Mode mode, PickSource field)
        {
            switch (mode)
            {
                case Mode.Play:
                    return field == PickSource.Deck ? "Choose a weather card to play from your deck" : "Choose a unit to bring back from your discard pile";
                case Mode.Draw:
                    if (field == PickSource.OpponentDiscard)
                        return "Choose a unit to take from your opponent's discard pile";
                    return field == PickSource.Discard ? "Choose a unit to take back from your discard pile" : "Choose a card to take from your deck";
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

        // Skip / Close: give up the choice and end the turn. Skipping loses something (a medic's revival, a leader already
        // spent, Destroyer of Worlds' draw after its two discards): asked first, the safe answer focused, so a click or a
        // key too many cannot throw it away. Closing a look (Emhyr) loses nothing.
        private void OnSkip()
        {
            if (mode == Mode.View)
            {
                BattleController.Instance.CancelPanel();
                return;
            }
            UIOverlay.Confirm(SkipQuestion(mode, field), "Skip", "Keep choosing", () => BattleController.Instance.CancelPanel());
        }

        private static string SkipQuestion(Mode mode, PickSource field)
        {
            if (mode == Mode.Play)
                return field == PickSource.Deck ? "Play no weather card? Your leader ability is spent, and your turn ends."
                    : "Bring no unit back? The medic stays, but its ability is lost, and your turn ends.";
            switch (field)
            {
                case PickSource.Deck:
                    return "Draw no card? The two cards you discarded are gone, and your turn ends.";
                case PickSource.OpponentDiscard:
                    return "Take nothing from your opponent's discard pile? Your leader ability is spent, and your turn ends.";
                default:
                    return "Take no card back? Your leader ability is spent, and your turn ends.";
            }
        }

        // A card was picked
        public void Choose(int card_id)
        {
            BattleController controller = BattleController.Instance;
            switch (mode)
            {
                // A medic or a weather leader: the card is placed on the board next
                case Mode.Play:
                    GameAudio.PlaySfx("ability_medic");
                    GameLog.Event(LogCategory.Battle, "picked", "card", controller.GetCard(card_id).name, "from", field, "to", "play");
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
                case Mode.Draw:
                    GameAudio.PlaySfx("card_draw");
                    GameLog.Event(LogCategory.Battle, "picked", "card", controller.GetCard(card_id).name, "from", field, "to", "hand");
                    gameObject.SetActive(false);
                    controller.PickCardToHand(field, card_id, isPlayerCard);
                    break;

                // Emhyr's look at the opponent's hand
                default:
                    GameAudio.PlaySfx("card_select");
                    gameObject.SetActive(false);
                    controller.EndLook();
                    break;
            }
        }
    }
}

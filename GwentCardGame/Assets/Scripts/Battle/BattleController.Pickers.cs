using Gwent.Core;
using Gwent.Rules;
using UnityEngine;

namespace Gwent.Battle
{
    // The battle's pickers: panels of cards to choose from (a medic's revival, the leaders' picks), and Eredin's discard
    public partial class BattleController
    {
        private BattlePickers OpenPanel()
        {
            pendingAction = PendingAction.PanelChoice;
            Panel.SetActive(true);
            return Panel.GetComponent<BattlePickers>();
        }

        // A card was chosen in a picker to play (medic / weather from deck): it is moved to the hand, then placed on the board
        public void TakePickedCardToPlay(PickSource field, int card_id, bool is_player_turn)
        {
            BattleSide info = Info(is_player_turn);
            switch (field)
            {
                case PickSource.Deck:
                    info.DeckList.Remove(card_id);
                    SetDeckObject(info, is_player_turn);
                    break;
                case PickSource.Discard:
                    info.DiscardList.Remove(card_id);
                    SetDiscardObject(info.DiscardList, is_player_turn);
                    break;
            }

            // Removed from the hand again when placed
            info.HandList.Add(card_id);
            pendingAction = PendingAction.PlacePickedCard;
        }

        // A card was chosen in a picker to take (leader abilities): it goes to the hand and the turn ends
        public void PickCardToHand(PickSource field, int card_id, bool is_player_turn)
        {
            TakeToHand(field, card_id, is_player_turn);
            EndTurn(is_player_turn);
        }

        private void TakeToHand(PickSource field, int card_id, bool is_player_turn)
        {
            BattleSide info = Info(is_player_turn);
            BattleSide opp_info = Opponent(is_player_turn);

            switch (field)
            {
                case PickSource.Deck:
                    info.DeckList.Remove(card_id);
                    SetDeckObject(info, is_player_turn);
                    break;
                case PickSource.Discard:
                    info.DiscardList.Remove(card_id);
                    SetDiscardObject(info.DiscardList, is_player_turn);
                    break;
                case PickSource.OpponentDiscard:
                    opp_info.DiscardList.Remove(card_id);
                    SetDiscardObject(opp_info.DiscardList, !is_player_turn);
                    break;
            }

            info.HandList.Add(card_id);
            info.CanDiscard = false;
            SetHandObject(info.HandList, is_player_turn);
        }

        // Panel "Skip" button: give up the choice and end the turn (nothing is ever left half done)
        public void CancelPanel()
        {
            Panel.SetActive(false);
            GameAudio.PlaySfx("ui_click");
            DeselectCard();
            RefreshBoard();
            if (battleState == BattleState.PlayerTurn)
                EndTurn(true);
            else
                pendingAction = PendingAction.None;
        }

        // Discard (Eredin, Destroyer of Worlds: discard 2 then pick 1 from the deck)
        public void DiscardCard(int card_id, bool is_player_card, int hand_index = -1)
        {
            BattleSide info = Info(is_player_card);
            if (!TakeFromHand(info.HandList, card_id, hand_index))
                return;

            GameAudio.PlaySfx("card_discard");
            GameLog.Event(LogCategory.Battle, "card_discarded", "side", is_player_card ? "player" : "enemy", "card", Db.Get(card_id).name);
            AbilityRules.Discard(info, card_id); // a discarded Cow still calls the Bovine Defense Force, next round (The Witcher 3)
            SetHandObject(info.HandList, is_player_card);
            SetDiscardObject(info.DiscardList, is_player_card);
            AnnounceCows(false); // a Cow discarded: its Bovine Defense Force comes next round, said now
        }

        // Initialize the Deck List on screen
        public void OpenDeckPicker(bool is_player_turn)
        {
            BattleSide info = Info(is_player_turn);
            info.CanDiscard = false;
            if (info.DeckList.Count > 0)
                OpenPanel().ShowToDraw(PickSource.Deck, info.DeckList, is_player_turn);
            else
                EndTurn(is_player_turn);
        }
    }
}

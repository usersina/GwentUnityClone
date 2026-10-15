using Gwent.Rules;
using UnityEngine;

// The battle's pickers: panels of cards to choose from (a medic's revival, the leaders' picks), and Eredin's discard
public partial class BattleController
{
    private BattlePickers OpenPanel()
    {
        pendingAction = PendingAction.PanelChoice;
        Panel.SetActive(true);
        return Panel.GetComponent<BattlePickers>();
    }

    // A card was chosen in a "play" panel (medic / weather from deck): it is moved to the hand, then placed on the board
    public void TakePickedCardToPlay(string field, int card_id, bool is_player_turn)
    {
        BattleSide info = Info(is_player_turn);
        GameLog.Info("Playing from: " + field);
        switch (field)
        {
            case "deck_list":
                info.DeckList.Remove(card_id);
                SetDeckObject(info, is_player_turn);
                break;
            case "discard_list":
                info.DiscardList.Remove(card_id);
                SetDiscardObject(info.DiscardList, is_player_turn);
                break;
        }

        // Removed from the hand again when placed
        info.HandList.Add(card_id);
        pendingAction = PendingAction.PlacePickedCard;
    }

    // A card was chosen in a "draw" panel (leader abilities): it goes to the hand and the turn ends
    public void PickCardToHand(string field, int card_id, bool is_player_turn)
    {
        TakeToHand(field, card_id, is_player_turn);
        EndTurn(is_player_turn);
    }

    private void TakeToHand(string field, int card_id, bool is_player_turn)
    {
        BattleSide info = Info(is_player_turn);
        BattleSide opp_info = Opponent(is_player_turn);

        GameLog.Info("Picking from: " + field);
        switch (field)
        {
            case "deck_list":
                info.DeckList.Remove(card_id);
                SetDeckObject(info, is_player_turn);
                break;
            case "discard_list":
                info.DiscardList.Remove(card_id);
                SetDiscardObject(info.DiscardList, is_player_turn);
                break;
            case "opp_discard_list":
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
    public void DiscardCard(int card_id, bool is_player_card)
    {
        BattleSide info = Info(is_player_card);
        if (!info.HandList.Remove(card_id))
            return;

        GameAudio.PlaySfx("card_discard");
        AbilityRules.Discard(info, card_id); // a discarded Cow still calls the Bovine Defense Force (The Witcher 3)
        SetHandObject(info.HandList, is_player_card);
        SetDiscardObject(info.DiscardList, is_player_card);
    }

    // Initialize the Deck List on screen
    public void OpenDeckPicker(bool is_player_turn)
    {
        BattleSide info = Info(is_player_turn);
        info.CanDiscard = false;
        if (info.DeckList.Count > 0)
            OpenPanel().ShowToDraw("deck_list", info.DeckList, is_player_turn);
        else
            EndTurn(is_player_turn);
    }
}

using System.Collections.Generic;
using System.Linq;
using Gwent.Rules;
using UnityEngine;

// The battle's pickers (panels of cards to choose from) and leader abilities
public partial class SceneController
{
    //----------------------------------------------PANELS (pickers)---------------------------------------------------//
    private PanelManager OpenPanel()
    {
        pendingAction = PendingAction.PanelChoice;
        Panel.SetActive(true);
        return Panel.GetComponent<PanelManager>();
    }

    // A card was chosen in a "play" panel (medic / weather from deck): it is moved to the hand, then placed on the board
    public void PlayBtnCleanUp(string field, int card_id, bool is_player_turn)
    {
        MainInfo info = Info(is_player_turn);
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
    public void ActuallyPickCard(string field, int card_id, bool is_player_turn)
    {
        TakeToHand(field, card_id, is_player_turn);
        TurnCallBack(is_player_turn);
    }

    private void TakeToHand(string field, int card_id, bool is_player_turn)
    {
        MainInfo info = Info(is_player_turn);
        MainInfo opp_info = Opponent(is_player_turn);

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
        if (battleState == BattleState.PLAYERTURN)
            TurnCallBack(true);
        else
            pendingAction = PendingAction.None;
    }

    // Discard (Eredin, Bringer of Death: discard 2 then pick 1 from the deck)
    public void DiscardCard(int card_id, bool is_player_card)
    {
        MainInfo info = Info(is_player_card);
        if (!info.HandList.Remove(card_id))
            return;

        GameAudio.PlaySfx("card_discard");
        info.DiscardList.Add(card_id);
        SetHandObject(info.HandList, is_player_card);
        SetDiscardObject(info.DiscardList, is_player_card);
    }

    // Initialize the Deck List on screen
    public void DeckCardPicker(bool is_player_turn)
    {
        MainInfo info = Info(is_player_turn);
        info.CanDiscard = false;
        if (info.DeckList.Count > 0)
            OpenPanel().ShowToDraw("deck_list", info.DeckList, is_player_turn);
        else
            TurnCallBack(is_player_turn);
    }

    //----------------------------------------------LEADERS---------------------------------------------------//
    private enum LeaderOutcome { Unavailable, Instant, Pending }

    public bool CanUseLeader(bool is_player)
    {
        MainInfo info = Info(is_player);
        if (!info.canLeader || info.leaderDisabled || Db.Get(info.Leader).row != Rows.LeaderActive)
            return false;
        return is_player ? IsPlayerActionAllowed : battleState == BattleState.ENEMYTURN;
    }

    // Activate the leader's ACTIVE ability. Does nothing (and keeps the leader) when it would have no effect.
    public void ActivateLeader(int leader_id, bool is_player_turn)
    {
        // Only a side's own leader: clicking the opponent's crown must not apply their ability for the player
        if (leader_id != Info(is_player_turn).Leader)
        {
            GameAudio.PlaySfx("invalid");
            return;
        }
        if (!CanUseLeader(is_player_turn))
        {
            GameAudio.PlaySfx("invalid");
            if (is_player_turn && Info(true).leaderDisabled)
                UIOverlay.Toast("Your leader ability was cancelled by the opponent's leader");
            return;
        }

        string reason;
        if (is_player_turn)
            DeselectCard(); // a selected card goes back to the hand (it would stay raised, still selected)
        CardShowcase.Clear();
        board.BeginChange();
        LeaderOutcome outcome = ApplyLeader(leader_id, is_player_turn, out reason);
        if (outcome == LeaderOutcome.Unavailable)
        {
            board.EndChange().Flush();
            GameAudio.PlaySfx("invalid");
            if (is_player_turn)
                UIOverlay.Toast(reason);
            return;
        }

        GameAudio.PlaySfx("leader");
        Info(is_player_turn).canLeader = false;
        View(is_player_turn).Leader.MarkUsed();

        RefreshBoard();
        BoardChange change = board.EndChange();

        // Pending abilities end the turn once their choice is made
        if (outcome == LeaderOutcome.Instant)
            Present(change, presenter.Play(change, -1, is_player_turn, false), () => TurnCallBack(is_player_turn));
        else
            change.Flush();
    }

    private LeaderOutcome ApplyLeader(int leader_id, bool is_player_turn, out string reason)
    {
        MainInfo info = Info(is_player_turn);

        // An ability that would do nothing is not used (The Witcher 3): the leader stays available
        reason = LeaderRules.Unavailable(CurrentBoard(), leader_id, is_player_turn);
        if (reason != null)
            return LeaderOutcome.Unavailable;

        string weather_row = LeaderIds.WeatherPickRow(leader_id);
        if (weather_row != null)
        {
            reason = "No matching weather card left in your deck";
            return PickWeatherCard(weather_row, is_player_turn) ? LeaderOutcome.Pending : LeaderOutcome.Unavailable;
        }

        // Horns, scorches, clear weather, agile: resolved by the rules right away
        if (LeaderRules.IsInstant(leader_id))
            return LeaderRules.ApplyInstant(CurrentBoard(), leader_id, is_player_turn, out reason) ? LeaderOutcome.Instant : LeaderOutcome.Unavailable;

        switch (leader_id)
        {
            case LeaderIds.EredinBringerOfDeath:
                if (!is_player_turn)
                {
                    reason = "The AI does not use this ability";
                    return LeaderOutcome.Unavailable;
                }
                info.CanDiscard = true;
                info.CardsDiscarded = 0;
                pendingAction = PendingAction.DiscardCards;
                UIOverlay.Toast((Application.isMobilePlatform ? "Double-tap" : "Double-click") + " 2 cards to discard them", 4f);
                return LeaderOutcome.Pending;
            case LeaderIds.EredinDestroyerOfWorlds:
                return RestoreToHand("discard_list", info.DiscardList, is_player_turn, out reason);
            case LeaderIds.EmhyrTheRelentless:
                return RestoreToHand("opp_discard_list", Opponent(is_player_turn).DiscardList, is_player_turn, out reason);
            case LeaderIds.EmhyrEmperorOfNilfgaard:
                reason = "Your opponent has no cards in hand";
                return LookHandRandom(3, is_player_turn) ? LeaderOutcome.Pending : LeaderOutcome.Unavailable;
        }
        reason = "This leader ability is passive";
        return LeaderOutcome.Unavailable;
    }

    // Plays a weather card from deck ("all": any weather, Clear Weather too); false when there is none
    public bool PickWeatherCard(string row, bool is_player_turn)
    {
        if (!is_player_turn)
            return false;
        List<int> weather_cards = LeaderRules.WeatherCardsFor(PlayerInfo, row, Db);
        if (weather_cards.Count == 0)
            return false;
        OpenPanel().ShowToPlay("deck_list", weather_cards, true);
        return true;
    }

    // A unit from a discard pile (no heroes or special cards) to the hand: chosen in a panel, or at random under
    // Emhyr, Invader of the North (both players)
    private LeaderOutcome RestoreToHand(string field, List<int> discard, bool is_player_turn, out string reason)
    {
        List<int> units_list = AbilityRules.MedicCandidates(discard, Db);
        reason = "No unit to restore";
        if (units_list.Count == 0)
            return LeaderOutcome.Unavailable;
        if (randomMedic || !is_player_turn)
        {
            int card_id = randomMedic ? units_list[Rng.Shared.Next(units_list.Count)] : AiBrain.ChooseRestoreTarget(units_list, Info(false), Db);
            TakeToHand(field, card_id, is_player_turn);
            return LeaderOutcome.Instant;
        }
        OpenPanel().ShowToDraw(field, units_list, true);
        return LeaderOutcome.Pending;
    }

    // View random cards from the opponent's hand; false when the hand is empty
    public bool LookHandRandom(int cards_num, bool is_player_turn)
    {
        MainInfo opp_info = Opponent(is_player_turn);
        if (!is_player_turn || opp_info.HandList.Count < 1)
            return false;
        List<int> random_list = Rng.Shuffle(opp_info.HandList).Take(cards_num).ToList();
        OpenPanel().ShowToView(random_list, true);
        return true;
    }

    // AI leader that plays a weather card from its deck (chosen by the AI)
    public bool AiUseWeatherLeader(int weather_card_id)
    {
        if (!CanUseLeader(false) || LeaderIds.WeatherPickRow(EnemyInfo.Leader) == null || !EnemyInfo.DeckList.Remove(weather_card_id))
            return false;
        EnemyInfo.canLeader = false;
        View(false).Leader.MarkUsed();
        GameAudio.PlaySfx("leader");
        EnemyInfo.HandList.Add(weather_card_id);
        PlayCard(weather_card_id, Rows.Weather, false);
        return true;
    }
}

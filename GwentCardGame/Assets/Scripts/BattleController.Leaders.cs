using System.Collections.Generic;
using System.Linq;
using Gwent.Rules;
using UnityEngine;

// The leaders' abilities: choosing the leader, its targets, using it (the player's and the AI's)
public partial class BattleController
{
    //----------------------------------------------LEADERS---------------------------------------------------//
    private enum LeaderOutcome { Unavailable, Instant, Pending }

    // The player's leader is chosen like a card first (The Witcher 3): it shows on the right and lights where its
    // ability acts. A second click on it, a click on a lit place, Enter or X uses it; Escape, a right click,
    // another card or passing puts it back. A click alone never spends the ability.
    public bool LeaderSelected { get; private set; }
    private readonly List<GameObject> leaderTargets = new List<GameObject>();

    // The places the chosen leader lit
    public IList<GameObject> LeaderTargets { get { return leaderTargets.AsReadOnly(); } }

    // Why the player's leader ability would do nothing now (null when it would act)
    public string LeaderProblem()
    {
        return LeaderRules.Unavailable(CurrentBoard(), PlayerInfo.Leader, true);
    }

    // A click on the player's leader (its card or its button), a tap, or X
    public void LeaderClicked()
    {
        if (LeaderSelected)
            UseSelectedLeader();
        else
            SelectLeader();
    }

    private void SelectLeader()
    {
        if (!CanUseLeader(true))
        {
            GameAudio.PlaySfx("invalid");
            if (PlayerInfo.leaderDisabled)
                UIOverlay.Toast("Your leader ability was cancelled by the opponent's leader");
            return;
        }
        // An ability that would do nothing is not chosen: why, at once
        string reason = LeaderProblem();
        if (reason != null)
        {
            GameAudio.PlaySfx("invalid");
            UIOverlay.Toast(reason);
            return;
        }
        DeselectCard(); // a selected card goes back to the hand
        GameAudio.PlaySfx("card_select");
        LeaderSelected = true;
        GameObject card = LeaderCard();
        if (card != null)
        {
            card.GetComponent<CardHover>().TranslateUp();
            CardShowcase.Select(card, Db.Get(PlayerInfo.Leader), false);
        }
        leaderTargets.AddRange(board.MarkLeaderTargets(PlayerInfo.Leader));
    }

    // Uses the chosen leader
    public void UseSelectedLeader()
    {
        if (LeaderSelected)
            ActivateLeader(PlayerInfo.Leader, true);
    }

    // A click on a place while the leader is chosen: a lit place uses it (true)
    public bool TryUseLeaderOn(GameObject place)
    {
        if (!LeaderSelected || !leaderTargets.Contains(place))
            return false;
        UseSelectedLeader();
        return true;
    }

    public bool IsLeaderTarget(GameObject place)
    {
        return LeaderSelected && leaderTargets.Contains(place);
    }

    // The leader goes back to its place: down, its places lit off (the showcase is the caller's)
    private void ReleaseLeader()
    {
        if (!LeaderSelected)
            return;
        LeaderSelected = false;
        GameObject card = LeaderCard();
        if (card != null)
            card.GetComponent<CardHover>().TranslateDown();
        board.UnmarkLeaderTargets(leaderTargets);
        leaderTargets.Clear();
        LightOffField(true);
        LightOffField(false);
    }

    // The player's leader card on the board
    private GameObject LeaderCard()
    {
        CardHover card = View(true).LeaderHolder.GetComponentInChildren<CardHover>();
        return card != null ? card.gameObject : null;
    }

    public bool CanUseLeader(bool is_player)
    {
        BattleSide info = Info(is_player);
        if (!info.canLeader || info.leaderDisabled || Db.Get(info.Leader).row != Rows.LeaderActive)
            return false;
        return is_player ? IsPlayerActionAllowed : battleState == BattleState.EnemyTurn;
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
            DeselectCard(false); // the chosen leader (or a selected card) leaves the showcase: the ability takes over
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
            Present(change, presenter.Play(change, -1, is_player_turn, false), () => EndTurn(is_player_turn));
        else
            change.Flush();
    }

    private LeaderOutcome ApplyLeader(int leader_id, bool is_player_turn, out string reason)
    {
        BattleSide info = Info(is_player_turn);

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
            case LeaderIds.EredinDestroyerOfWorlds:
                if (!is_player_turn)
                {
                    reason = "The AI does not use this ability";
                    return LeaderOutcome.Unavailable;
                }
                info.CanDiscard = true;
                info.CardsDiscarded = 0;
                pendingAction = PendingAction.DiscardCards;
                UIOverlay.Toast((GameInput.IsTouch ? "Double-tap" : "Double-click") + " 2 cards to discard them", 4f);
                return LeaderOutcome.Pending;
            case LeaderIds.EredinBringerOfDeath:
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
        BattleSide opp_info = Opponent(is_player_turn);
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

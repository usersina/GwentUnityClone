using System;
using System.Collections.Generic;

namespace Gwent.Rules
{
    // Both sides plus the shared weather board. The lists are shared with the battle, so changes apply directly.
    public class Board
    {
        public PlayerState Player;
        public PlayerState Enemy;
        public List<int> Weather = new List<int>();
        public bool DoubleSpies;
        public bool RandomMedic;
        public CardDatabase Db;
        public Random Rnd = Rng.Shared;

        public PlayerState Side(bool is_player) { return is_player ? Player : Enemy; }
        public PlayerState Opponent(bool is_player) { return is_player ? Enemy : Player; }

        public void Recalculate()
        {
            StrengthCalculator.Recalculate(Player, Weather, DoubleSpies, Db);
            StrengthCalculator.Recalculate(Enemy, Weather, DoubleSpies, Db);
        }

        public AiContext AiContextFor(bool is_player)
        {
            return new AiContext { Me = Side(is_player), Opponent = Opponent(is_player), Weather = Weather, DoubleSpies = DoubleSpies, RandomMedic = RandomMedic, Db = Db };
        }
    }

    public enum PlayOutcome { Done, WaitingForMedicChoice }

    // Resolving a card once it has left the hand
    public static class PlayRules
    {
        // manual_medic: the side picks its medic target itself (the human player), otherwise the AI picks
        public static PlayOutcome Resolve(Board board, Card card, string field_row, bool is_player, bool manual_medic)
        {
            PlayerState me = board.Side(is_player);
            PlayerState opp = board.Opponent(is_player);

            if (card.IsUnit)
                return ResolveUnit(board, card, field_row, is_player, manual_medic);

            switch (card.row)
            {
                case Rows.Special: // Commander's Horn on the chosen row (one per row)
                    string row = Rows.IsUnitRow(field_row) ? field_row : Rows.Close;
                    if (me.HasHorn(row))
                        me.DiscardList.Add(card._id);
                    else
                        me.SpecialRow(row).Add(card._id);
                    break;
                case Rows.Weather:
                    AbilityRules.PlayWeather(me, opp, board.Weather, card);
                    break;
                default: // one_time: scorch (decoy goes through PlayDecoy)
                    me.DiscardList.Add(card._id);
                    if (card.ability == Abilities.Scorch)
                    {
                        board.Recalculate(); // Scorch AFTER modifiers
                        AbilityRules.Scorch(board.Player, board.Enemy, board.Db);
                    }
                    break;
            }
            board.Recalculate();
            return PlayOutcome.Done;
        }

        private static PlayOutcome ResolveUnit(Board board, Card card, string field_row, bool is_player, bool manual_medic)
        {
            PlayerState me = board.Side(is_player);
            PlayerState opp = board.Opponent(is_player);
            string row = card.row == Rows.Agile ? (Rows.IsUnitRow(field_row) ? field_row : Rows.Close) : card.row;
            if (!Rows.IsUnitRow(row))
                row = Rows.Close;

            PlayerState owner = me;
            int revived = -1;
            PlayOutcome outcome = PlayOutcome.Done;

            if (card.ability == Abilities.Spy)
            {
                // The spy user draws two cards, the spy goes to the opponent's side
                RoundRules.Draw(me, 2);
                owner = opp;
            }
            else if (card.ability == Abilities.Muster)
                AbilityRules.Muster(me, card, row, board.Db);
            else if (card.ability == Abilities.Medic)
            {
                if (board.RandomMedic && !card.unique) // Emhyr, Invader of the North (hero medics still choose)
                    revived = AbilityRules.TakeRandomMedicTarget(me, board.Rnd, board.Db);
                else if (manual_medic)
                {
                    if (AbilityRules.MedicCandidates(me.DiscardList, board.Db).Count > 0)
                        outcome = PlayOutcome.WaitingForMedicChoice;
                }
                else
                {
                    revived = AiBrain.ChooseMedicTarget(me, board.Db);
                    if (revived != -1)
                        me.DiscardList.Remove(revived);
                }
            }

            owner.UnitRow(row).Add(card._id);
            board.Recalculate();

            string scorch_row = Abilities.RowScorchTarget(card.ability);
            if (scorch_row != null)
            {
                AbilityRules.ScorchRow(opp, scorch_row, board.Db);
                board.Recalculate();
            }

            // A restored unit is played like any other (spies draw, musters muster, medics chain)
            if (revived != -1)
            {
                Card revived_card = board.Db.Get(revived);
                string revived_row = revived_card.row == Rows.Agile
                    ? AbilityRules.BestAgileRow(me, revived, board.Weather, board.DoubleSpies, board.Db)
                    : revived_card.row;
                return ResolveUnit(board, revived_card, revived_row, is_player, manual_medic);
            }
            return outcome;
        }

        // Decoy: the target goes back to the hand and the decoy takes its place (decoy already out of the hand)
        public static void Decoy(Board board, int target_id, string row, bool is_player, int decoy_id)
        {
            PlayerState info = board.Side(is_player);
            if (Rows.IsUnitRow(row) && info.UnitRow(row).Remove(target_id))
            {
                info.HandList.Add(target_id);
                info.UnitRow(row).Add(decoy_id);
            }
            else
                info.DiscardList.Add(decoy_id);
            board.Recalculate();
        }
    }

    public static class LeaderRules
    {
        // Why the leader's ability cannot be used right now, or null when it can. As in The Witcher 3, an ability
        // that would do nothing cannot be used (the leader is never wasted), and passive leaders are never used.
        public static string Unavailable(Board board, int leader_id, bool is_player)
        {
            PlayerState me = board.Side(is_player);
            PlayerState opp = board.Opponent(is_player);

            string weather_row = LeaderIds.WeatherPickRow(leader_id);
            if (weather_row != null)
                return WeatherCardsFor(me, weather_row, board.Db).Count > 0 ? null : "No matching weather card left in your deck";

            string horn_row = LeaderIds.HornRow(leader_id);
            if (horn_row != null)
                return me.HasHorn(horn_row) ? "That row already has a Commander's Horn" : null;

            string scorch_row = LeaderIds.ScorchRow(leader_id);
            if (scorch_row != null)
            {
                board.Recalculate();
                string row_name = scorch_row == Rows.Close ? "close combat" : scorch_row == Rows.Range ? "ranged" : "siege";
                return opp.RowScore(scorch_row) >= 10 && AbilityRules.HasScorchTarget(opp, scorch_row, board.Db)
                    ? null : "Your opponent's " + row_name + " row needs a strength of 10 or more";
            }

            switch (leader_id)
            {
                case LeaderIds.FoltestLordCommander:
                    return board.Weather.Count > 0 ? null : "There is no weather to clear";
                case LeaderIds.FrancescaHopeOfTheAenSeidhe:
                    return AbilityRules.OptimizeAgile(me.Clone(), board.Weather, board.DoubleSpies, board.Db) > 0
                        ? null : "None of your agile units would gain strength by moving";
                case LeaderIds.EmhyrEmperorOfNilfgaard:
                    return opp.HandList.Count > 0 ? null : "Your opponent has no cards in hand";
                case LeaderIds.EmhyrTheRelentless:
                    return AbilityRules.MedicCandidates(opp.DiscardList, board.Db).Count > 0 ? null : "No unit in your opponent's discard pile";
                case LeaderIds.EredinDestroyerOfWorlds: // discard 2, draw 1 of your choice
                    return me.HandList.Count >= 2 && me.DeckList.Count > 0 ? null : "You need 2 cards in your hand and 1 in your deck";
                case LeaderIds.EredinBringerOfDeath: // restore a unit from your discard pile to your hand
                    return AbilityRules.MedicCandidates(me.DiscardList, board.Db).Count > 0 ? null : "No unit in your discard pile to restore";
            }
            return "This leader ability is passive";
        }

        // Weather cards a "play a weather card from your deck" leader can pick ("all": any weather, Clear Weather too)
        public static List<int> WeatherCardsFor(PlayerState info, string row, CardDatabase db)
        {
            List<int> cards = new List<int>();
            foreach (int card_id in info.DeckList)
            {
                if (cards.Contains(card_id) || db.Get(card_id).row != Rows.Weather)
                    continue;
                if (row == "all" || card_id == CardIds.WeatherFor(row))
                    cards.Add(card_id);
            }
            return cards;
        }

        // Leader abilities that resolve immediately (no choice to make)
        public static bool IsInstant(int leader_id)
        {
            return LeaderIds.HornRow(leader_id) != null || LeaderIds.ScorchRow(leader_id) != null
                || leader_id == LeaderIds.FoltestLordCommander || leader_id == LeaderIds.FrancescaHopeOfTheAenSeidhe;
        }

        // Returns false (nothing happened) when the ability cannot be used right now
        public static bool ApplyInstant(Board board, int leader_id, bool is_player, out string reason)
        {
            PlayerState info = board.Side(is_player);
            reason = null;

            string horn_row = LeaderIds.HornRow(leader_id);
            if (horn_row != null)
            {
                reason = "That row already has a Commander's Horn";
                return AbilityRules.PlaceLeaderHorn(info, horn_row);
            }

            string scorch_row = LeaderIds.ScorchRow(leader_id);
            if (scorch_row != null)
            {
                board.Recalculate();
                AbilityRules.ScorchRow(board.Opponent(is_player), scorch_row, board.Db);
                return true;
            }

            switch (leader_id)
            {
                case LeaderIds.FoltestLordCommander:
                    AbilityRules.ClearWeather(board.Player, board.Enemy, board.Weather);
                    return true;
                case LeaderIds.FrancescaHopeOfTheAenSeidhe:
                    AbilityRules.OptimizeAgile(info, board.Weather, board.DoubleSpies, board.Db);
                    return true;
            }
            reason = "This leader ability needs a choice";
            return false;
        }
    }
}

using System.Collections.Generic;

namespace Gwent.Rules
{
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

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

            Row? weather_row = LeaderIds.WeatherPickRow(leader_id);
            if (weather_row != null)
                return WeatherCardsFor(me, weather_row.Value, board.Db).Count > 0 ? null : "No matching weather card left in your deck";

            Row? horn_row = LeaderIds.HornRow(leader_id);
            if (horn_row != null)
                return me.HasHorn(horn_row.Value) ? "That row already has a Commander's Horn"
                    : me.SlotTaken(horn_row.Value) ? "That row's horn slot holds a Mardroeme" : null;

            Row? scorch_row = LeaderIds.ScorchRow(leader_id);
            if (scorch_row != null)
            {
                board.Recalculate();
                string row_name = scorch_row == Row.Close ? "close combat" : scorch_row == Row.Range ? "ranged" : "siege";
                if (opp.RowScore(scorch_row.Value) < 10)
                    return "Your opponent's " + row_name + " row needs a strength of 10 or more";
                // 10 or more, but all of it heroes (who count toward it, and are immune)
                return AbilityRules.HasScorchTarget(opp, scorch_row.Value, board.Db)
                    ? null : "Your opponent's " + row_name + " row holds only heroes: they are immune to scorch";
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
                case LeaderIds.CrachAnCraite: // both discard piles back into the decks
                    return me.DiscardList.Count + opp.DiscardList.Count > 0 ? null : "Both discard piles are empty";
            }
            return "This leader ability is passive";
        }

        // Weather cards a "play a weather card from your deck" leader can pick (Row.Weather: any weather, Clear Weather too)
        public static List<int> WeatherCardsFor(PlayerState info, Row row, CardDatabase db)
        {
            List<int> cards = new List<int>();
            foreach (int card_id in info.DeckList)
            {
                if (cards.Contains(card_id) || db.Get(card_id).row != Row.Weather)
                    continue;
                if (row == Row.Weather || card_id == CardIds.WeatherFor(row))
                    cards.Add(card_id);
            }
            return cards;
        }

        // Leader abilities that resolve immediately (no choice to make)
        public static bool IsInstant(int leader_id)
        {
            return LeaderIds.HornRow(leader_id) != null || LeaderIds.ScorchRow(leader_id) != null
                || leader_id == LeaderIds.FoltestLordCommander || leader_id == LeaderIds.FrancescaHopeOfTheAenSeidhe
                || leader_id == LeaderIds.CrachAnCraite;
        }

        // Returns false (nothing happened) when the ability cannot be used right now
        public static bool ApplyInstant(Board board, int leader_id, bool is_player, out string reason)
        {
            PlayerState info = board.Side(is_player);
            reason = null;

            Row? horn_row = LeaderIds.HornRow(leader_id);
            if (horn_row != null)
            {
                reason = info.HasHorn(horn_row.Value) ? "That row already has a Commander's Horn" : "That row's horn slot holds a Mardroeme";
                return AbilityRules.PlaceLeaderHorn(info, horn_row.Value);
            }

            Row? scorch_row = LeaderIds.ScorchRow(leader_id);
            if (scorch_row != null)
            {
                board.Recalculate();
                AbilityRules.ScorchRow(board.Opponent(is_player), scorch_row.Value, board.Db);
                board.Recalculate(); // a killed Cow's Defense Force arrived: horn and weather count for it at once
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
                case LeaderIds.CrachAnCraite:
                    if (Unavailable(board, leader_id, is_player) != null)
                    {
                        reason = "Both discard piles are empty";
                        return false;
                    }
                    AbilityRules.ShuffleDiscardsIntoDecks(board);
                    return true;
            }
            reason = "This leader ability needs a choice";
            return false;
        }
    }
}

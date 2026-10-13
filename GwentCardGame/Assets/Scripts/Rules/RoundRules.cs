using System;
using System.Collections.Generic;

namespace Gwent.Rules
{
    public enum RoundWinner { Player, Enemy, Draw }

    public static class RoundRules
    {
        // Higher total wins. Nilfgaard automatically wins a round that ends in a draw (unless both are Nilfgaard).
        public static RoundWinner DecideRound(int player_total, int enemy_total, string player_faction, string enemy_faction)
        {
            if (player_total > enemy_total)
                return RoundWinner.Player;
            if (enemy_total > player_total)
                return RoundWinner.Enemy;

            bool player_nf = player_faction == Factions.Nilfgaard;
            bool enemy_nf = enemy_faction == Factions.Nilfgaard;
            if (player_nf && !enemy_nf)
                return RoundWinner.Player;
            if (enemy_nf && !player_nf)
                return RoundWinner.Enemy;
            return RoundWinner.Draw;
        }

        // Records the scores and removes the loser's life (both lose one on a draw)
        public static void ApplyRoundResult(PlayerState player, PlayerState enemy, RoundWinner winner)
        {
            player.RoundsScore.Add(player.TotalScore());
            enemy.RoundsScore.Add(enemy.TotalScore());
            if (winner != RoundWinner.Player)
                player.Lives = Math.Max(0, player.Lives - 1);
            if (winner != RoundWinner.Enemy)
                enemy.Lives = Math.Max(0, enemy.Lives - 1);
        }

        // Sends one side's board to its discard pile at the end of a round.
        // Monsters keep one random unit on the board; a Cow leaving the board summons the Bovine Defense Force.
        public static void CleanupBoard(PlayerState info, Random rnd, CardDatabase db)
        {
            int kept_card = -1;
            string kept_row = null;
            if (info.Faction == Factions.Monsters && info.UnitsOnBoard() > 0)
            {
                int pick = rnd.Next(info.UnitsOnBoard());
                foreach (string row in Rows.Units)
                {
                    List<int> units = info.UnitRow(row);
                    if (pick < units.Count)
                    {
                        kept_card = units[pick];
                        kept_row = row;
                        break;
                    }
                    pick -= units.Count;
                }
            }

            bool there_is_cow = false;
            foreach (string row in Rows.Units)
            {
                List<int> units = info.UnitRow(row);
                if (units.Contains(CardIds.Cow))
                    there_is_cow = true;
                info.DiscardList.AddRange(units);
                units.Clear();

                // A leader horn is not a real card: it simply disappears
                List<int> specials = info.SpecialRow(row);
                if (info.LeaderHornRows.Contains(row))
                    specials.Remove(CardIds.CommanderHorn);
                info.DiscardList.AddRange(specials);
                specials.Clear();
            }
            info.LeaderHornRows.Clear();

            info.DiscardList.AddRange(info.WeatherPlayed);
            info.WeatherPlayed.Clear();

            if (kept_card != -1)
            {
                info.DiscardList.RemoveAt(info.DiscardList.LastIndexOf(kept_card));
                info.UnitRow(kept_row).Add(kept_card);
            }

            if (there_is_cow)
                info.CloseList.Add(CardIds.BovineDefenseForce);
        }

        // Draws up to num cards from the top of the deck, returns how many were drawn
        public static int Draw(PlayerState info, int num)
        {
            int drawn = Math.Min(Math.Max(num, 0), info.DeckList.Count);
            for (int i = 0; i < drawn; i++)
                info.HandList.Add(info.DeckList[i]);
            info.DeckList.RemoveRange(0, drawn);
            return drawn;
        }
    }
}

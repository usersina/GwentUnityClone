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
        // Monsters keep one random unit on the board; each Cow that leaves summons a Bovine Defense Force for the
        // next round. Returns the unit Monsters kept (-1 if none).
        public static int CleanupBoard(PlayerState info, Random rnd, CardDatabase db)
        {
            // The unit Monsters keep (never a hero or a decoy) is taken off its row while the rest goes
            int kept_card = -1;
            string kept_row = null;
            if (info.Faction == Factions.Monsters)
            {
                List<KeyValuePair<string, int>> eligible = new List<KeyValuePair<string, int>>();
                foreach (string row in Rows.Units)
                    for (int i = 0; i < info.UnitRow(row).Count; i++)
                    {
                        Card card = db.Get(info.UnitRow(row)[i]);
                        if (!card.unique && !card.IsSpecial)
                            eligible.Add(new KeyValuePair<string, int>(row, i));
                    }
                if (eligible.Count > 0)
                {
                    KeyValuePair<string, int> pick = eligible[rnd.Next(eligible.Count)];
                    kept_row = pick.Key;
                    kept_card = info.UnitRow(kept_row)[pick.Value];
                    info.UnitRow(kept_row).RemoveAt(pick.Value);
                }
            }

            foreach (string row in Rows.Units)
            {
                List<int> units = info.UnitRow(row);
                foreach (int card_id in units)
                    AbilityRules.Discard(info, card_id);
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
                info.UnitRow(kept_row).Add(kept_card);

            // Cards summoned during the round (a scorched Cow) or by this cleanup arrive now
            info.CloseList.AddRange(info.PendingSummons);
            info.PendingSummons.Clear();
            return kept_card;
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

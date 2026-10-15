using System;
using System.Collections.Generic;

namespace Gwent.Rules
{
    public enum RoundWinner { Player, Enemy, Draw }

    public static class RoundRules
    {
        // The Witcher 3: a side with no card in hand and no leader ability left to use passes automatically (it is done
        // for the round, as if it had passed)
        public static bool OutOfPlays(PlayerState info, CardDatabase db)
        {
            return info.HandList.Count == 0 && (!info.canLeader || info.leaderDisabled || db.Get(info.Leader).row != Row.LeaderActive);
        }

        // In the battle, an unused leader whose ability would do nothing right now does not keep it going either
        // (The Witcher 3 checks that the ability can be applied)
        public static bool OutOfPlays(Board board, bool is_player)
        {
            PlayerState info = board.Side(is_player);
            return OutOfPlays(info, board.Db) || info.HandList.Count == 0 && LeaderRules.Unavailable(board, info.Leader, is_player) != null;
        }

        // Higher total wins. Nilfgaard automatically wins a round that ends in a draw (unless both are Nilfgaard).
        public static RoundWinner DecideRound(int player_total, int enemy_total, Faction player_faction, Faction enemy_faction)
        {
            if (player_total > enemy_total)
                return RoundWinner.Player;
            if (enemy_total > player_total)
                return RoundWinner.Enemy;

            bool player_nf = player_faction == Faction.Nilfgaard;
            bool enemy_nf = enemy_faction == Faction.Nilfgaard;
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
            player.RoundsWon.Add(winner == RoundWinner.Player);
            enemy.RoundsWon.Add(winner == RoundWinner.Enemy);
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
            Row kept_row = Row.Close; // meaningful only with a kept card
            if (info.Faction == Faction.Monsters)
            {
                List<KeyValuePair<Row, int>> eligible = new List<KeyValuePair<Row, int>>();
                foreach (Row row in Rows.Units)
                    for (int i = 0; i < info.UnitRow(row).Count; i++)
                    {
                        Card card = db.Get(info.UnitRow(row)[i]);
                        if (!card.unique && !card.IsSpecial)
                            eligible.Add(new KeyValuePair<Row, int>(row, i));
                    }
                if (eligible.Count > 0)
                {
                    KeyValuePair<Row, int> pick = eligible[rnd.Next(eligible.Count)];
                    kept_row = pick.Key;
                    kept_card = info.UnitRow(kept_row)[pick.Value];
                    info.UnitRow(kept_row).RemoveAt(pick.Value);
                }
            }

            foreach (Row row in Rows.Units)
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

            // Cards summoned by a discard during the round (a Cow discarded by Destroyer of Worlds) or by this cleanup
            // arrive now (a killed Cow's came at once)
            info.CloseList.AddRange(info.PendingSummons);
            info.PendingSummons.Clear();
            return kept_card;
        }

        // Redraw at the start of the battle, as in The Witcher 3: the top card of the deck takes the card's place in the
        // hand, and the card goes to the bottom of the deck (so it can't come straight back). hand_index: its place in the
        // hand when known (with two alike, the one chosen), else its first copy. Returns the new card, or -1 (nothing
        // changes) when the deck is empty or the card is not in the hand.
        public static int Redraw(PlayerState info, int card_id, int hand_index = -1)
        {
            int index = hand_index >= 0 && hand_index < info.HandList.Count && info.HandList[hand_index] == card_id ? hand_index : info.HandList.IndexOf(card_id);
            if (index < 0 || info.DeckList.Count == 0)
                return -1;
            int drawn = info.DeckList[0];
            info.DeckList.RemoveAt(0);
            info.HandList[index] = drawn;
            info.DeckList.Add(card_id);
            return drawn;
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

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

        // The end of a round, both sides having passed: the totals decide it (Nilfgaard wins a tie), the loser loses a life
        // (both on a draw), the passes are over. Returns the round's winner and the totals.
        public static RoundWinner EndRound(Board board, out int player_total, out int enemy_total)
        {
            return EndRound(board, null, out player_total, out enemy_total);
        }

        // The same, the round given to one side whatever the totals (the other ran out of time: given)
        public static RoundWinner EndRound(Board board, RoundWinner? given, out int player_total, out int enemy_total)
        {
            board.Recalculate();
            player_total = board.Player.TotalScore();
            enemy_total = board.Enemy.TotalScore();
            RoundWinner winner = given ?? DecideRound(player_total, enemy_total, board.Player.Faction, board.Enemy.Faction);
            ApplyRoundResult(board.Player, board.Enemy, winner);
            board.Player.hasPassed = board.Enemy.hasPassed = false;
            return winner;
        }

        // The battle's result once a side has no life left (null while it goes on)
        public static RoundWinner? BattleResult(Board board)
        {
            if (board.Player.Lives > 0 && board.Enemy.Lives > 0)
                return null;
            return board.Player.Lives == board.Enemy.Lives ? RoundWinner.Draw : board.Player.Lives > 0 ? RoundWinner.Player : RoundWinner.Enemy;
        }

        // The board cleared for the next round (CleanupBoard, both sides; the weather goes), and the Northern Realms' perk:
        // the round's winner draws a card. kept: the unit Monsters kept (-1), drew: whether the side drew.
        public static void ClearForNextRound(Board board, RoundWinner winner, out int player_kept, out int enemy_kept, out bool player_drew, out bool enemy_drew)
        {
            player_kept = CleanupBoard(board.Player, board.Rnd, board.Db);
            enemy_kept = CleanupBoard(board.Enemy, board.Rnd, board.Db);
            board.Weather.Clear();
            player_drew = enemy_drew = false;
            if (winner != RoundWinner.Draw && board.Side(winner == RoundWinner.Player).Faction == Faction.NorthernRealms)
            {
                bool drew = Draw(board.Side(winner == RoundWinner.Player), 1) > 0;
                player_drew = drew && winner == RoundWinner.Player;
                enemy_drew = drew && winner == RoundWinner.Enemy;
            }
            board.Recalculate();
        }

        // Skellige's perk as the new round opens, each side's (SkelligeReinforcements): the units placed (empty: none)
        public static void Reinforce(Board board, out List<int> player_placed, out List<int> enemy_placed)
        {
            player_placed = SkelligeReinforces(board.Player) ? SkelligeReinforcements(board, true) : new List<int>();
            enemy_placed = SkelligeReinforces(board.Enemy) ? SkelligeReinforcements(board, false) : new List<int>();
        }

        // Who starts the next round (The Witcher 3): the round's winner; after a draw, whoever passed last
        public static bool PlayerStarts(RoundWinner winner, bool last_passer_player)
        {
            return winner == RoundWinner.Player || winner == RoundWinner.Draw && last_passer_player;
        }

        // Sends one side's board to its discard pile at the end of a round.
        // Monsters keep one random unit on the board; each Cow that leaves summons a Bovine Defense Force for the
        // next round, each Kambi a Hemdall. Returns the unit Monsters kept (-1 if none).
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
            // arrive now, on their rows (a killed Cow's came at once)
            foreach (int card_id in info.PendingSummons)
                info.UnitRow(db.Get(card_id).row).Add(card_id);
            info.PendingSummons.Clear();
            return kept_card;
        }

        // Whether a side's faction perk brings units back as this round opens: Skellige's, at the start of the third
        public static bool SkelligeReinforces(PlayerState info)
        {
            return info.Faction == Faction.Skellige && info.RoundsScore.Count == 2;
        }

        // Skellige's perk, at the start of the third round: two random units of the side's discard pile come back onto
        // the battlefield, each on its own row (an agile one on the row where it is stronger). No heroes, and no spies
        // (they would fight for the other side). They don't use their abilities as they come (no medic, muster or
        // scorch), but count as usual (bonds, morale). Returns the units placed.
        public static List<int> SkelligeReinforcements(Board board, bool is_player)
        {
            PlayerState info = board.Side(is_player);
            List<int> units = AbilityRules.MedicCandidates(info.DiscardList, board.Db);
            units.RemoveAll(id => board.Db.Get(id).ability == Ability.Spy);
            List<int> placed = new List<int>();
            while (placed.Count < 2 && units.Count > 0)
            {
                int index = board.Rnd.Next(units.Count);
                int card_id = units[index];
                units.RemoveAt(index);
                Card card = board.Db.Get(card_id);
                Row row = card.row == Row.Agile ? AbilityRules.BestAgileRow(info, card_id, board.Weather, board.DoubleSpies, board.Db) : card.row;
                info.DiscardList.Remove(card_id);
                info.UnitRow(row).Add(card_id);
                placed.Add(card_id);
            }
            AbilityRules.Transform(info, board.Db);
            board.Recalculate();
            return placed;
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

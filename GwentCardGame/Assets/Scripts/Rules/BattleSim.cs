using System;
using System.Collections.Generic;

namespace Gwent.Rules
{
    // The rest of a battle without the scene, in the same steps as SceneController and AIManager: a side's move,
    // passing, the end of a round and the next round's opening. The Hard AI plays battles out with it (AiSearch).
    public static class BattleSim
    {
        // One of the AI's actions, as AIManager.Execute carries it out (SceneController.PlayCard, PlayDecoy,
        // AiUseWeatherLeader, ActivateLeader). An action that cannot be carried out passes, as in the battle.
        public static void Apply(Board board, AiAction action, bool is_player)
        {
            PlayerState me = board.Side(is_player);
            switch (action.Type)
            {
                case AiActionType.PlayCard:
                    if (!me.HandList.Contains(action.CardId))
                        break;
                    Card card = board.Db.Get(action.CardId);
                    if (card.ability == Abilities.Decoy)
                    {
                        if (action.TargetId < 0 || !Rows.IsUnitRow(action.Row))
                            break;
                        me.HandList.Remove(action.CardId);
                        PlayRules.Decoy(board, action.TargetId, action.Row, is_player, action.CardId);
                        return;
                    }
                    me.HandList.Remove(action.CardId);
                    PlayRules.Resolve(board, card, action.Row, is_player, false);
                    return;

                case AiActionType.UseLeader:
                    if (UseLeader(board, action, is_player))
                        return;
                    break;
            }
            me.hasPassed = true;
        }

        private static bool UseLeader(Board board, AiAction action, bool is_player)
        {
            PlayerState me = board.Side(is_player);
            if (!me.canLeader || me.leaderDisabled || LeaderRules.Unavailable(board, me.Leader, is_player) != null)
                return false;

            if (LeaderIds.WeatherPickRow(me.Leader) != null)
            {
                if (!me.DeckList.Remove(action.CardId))
                    return false;
                me.canLeader = false;
                PlayRules.Resolve(board, board.Db.Get(action.CardId), Rows.Weather, is_player, false);
                return true;
            }
            if (me.Leader == LeaderIds.EredinBringerOfDeath || me.Leader == LeaderIds.EmhyrTheRelentless)
            {
                List<int> pile = me.Leader == LeaderIds.EmhyrTheRelentless ? board.Opponent(is_player).DiscardList : me.DiscardList;
                List<int> units = AbilityRules.MedicCandidates(pile, board.Db);
                int card_id = board.RandomMedic ? units[board.Rnd.Next(units.Count)] : AiBrain.ChooseRestoreTarget(units, me, board.Db);
                pile.Remove(card_id);
                me.HandList.Add(card_id);
                me.canLeader = false;
                return true;
            }
            string reason;
            if (!LeaderRules.IsInstant(me.Leader) || !LeaderRules.ApplyInstant(board, me.Leader, is_player, out reason))
                return false; // the AI does not use the other abilities
            me.canLeader = false;
            return true;
        }

        // Plays the battle on to its end, `policy` deciding for both sides. player_turn: whose turn it is;
        // last_passer_player: who passed last so far (after a drawn round, they start the next one). Returns the
        // battle's winner, Draw when both run out of lives together (or the battle never ends).
        public static RoundWinner PlayOut(Board board, bool player_turn, bool last_passer_player, Func<Board, bool, AiAction> policy, int max_turns = 300)
        {
            for (int turn = 0; turn < max_turns; turn++)
            {
                if (board.Player.hasPassed && board.Enemy.hasPassed)
                {
                    RoundWinner? battle = EndRound(board, ref player_turn, ref last_passer_player);
                    if (battle.HasValue)
                        return battle.Value;
                    continue;
                }

                // A side with nothing left to play passes when its turn comes; a side that has passed is skipped
                PlayerState me = board.Side(player_turn);
                if (!me.hasPassed)
                {
                    if (RoundRules.OutOfPlays(board, player_turn))
                        me.hasPassed = true;
                    else
                        Apply(board, policy(board, player_turn), player_turn);
                    if (me.hasPassed)
                        last_passer_player = player_turn;
                }
                player_turn = !player_turn;
            }
            return RoundWinner.Draw;
        }

        // SceneController.ManageOutcome, PrepareNextRound and RoundOpening; returns the battle's winner once it is over
        private static RoundWinner? EndRound(Board board, ref bool player_turn, ref bool last_passer_player)
        {
            board.Recalculate();
            RoundWinner winner = RoundRules.DecideRound(board.Player.TotalScore(), board.Enemy.TotalScore(), board.Player.Faction, board.Enemy.Faction);
            RoundRules.ApplyRoundResult(board.Player, board.Enemy, winner);
            board.Player.hasPassed = board.Enemy.hasPassed = false;
            if (board.Player.Lives == 0 || board.Enemy.Lives == 0)
                return board.Player.Lives == board.Enemy.Lives ? RoundWinner.Draw : board.Player.Lives > 0 ? RoundWinner.Player : RoundWinner.Enemy;

            RoundRules.CleanupBoard(board.Player, board.Rnd, board.Db);
            RoundRules.CleanupBoard(board.Enemy, board.Rnd, board.Db);
            board.Weather.Clear();
            if (winner != RoundWinner.Draw && board.Side(winner == RoundWinner.Player).Faction == Factions.NorthernRealms)
                RoundRules.Draw(board.Side(winner == RoundWinner.Player), 1);

            player_turn = winner == RoundWinner.Player || (winner == RoundWinner.Draw && last_passer_player);
            // A side that starts a round with no card in hand sits it out
            foreach (bool is_player in new[] { true, false })
                if (board.Side(is_player).HandList.Count == 0)
                {
                    board.Side(is_player).hasPassed = true;
                    last_passer_player = is_player;
                }
            return null;
        }
    }
}

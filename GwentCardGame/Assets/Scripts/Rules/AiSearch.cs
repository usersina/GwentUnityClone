using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Gwent.Rules
{
    // Hard: looks ahead by playing the battle out. Each move worth considering is tried in a few dozen complete
    // battles (BattleSim), both sides then playing as the Normal AI, and the move that wins the most of them is played.
    // It plays fair: it never reads the opponent's hand. The cards the opponent could hold (its hand and its deck
    // together: the cards of its deck not seen yet) are dealt anew for each battle, and the AI's own deck is
    // reshuffled.
    public static class AiSearch
    {
        public static int Rollouts = 24;        // battles played out per move (8 still beat Normal two times in three)
        private const int MinRollouts = 4;      // at the least, however slow the device
        public static int TimeLimitMs = 1500;   // then the battles played so far decide (0.4 to 1.4 s for an opening move
                                                // on a desktop, less later in the battle)
        private const int StrongestMoves = 5;   // the moves considered: the Normal AI's own, passing, the moves that add
        private const int CheapestMoves = 2;    // the most strength now, and the cheapest that add any (to bait)
        private const double NormalBonus = 0.5; // another move must win more often than the Normal AI's own

        public static AiAction Decide(AiContext ctx)
        {
            AiAction normal = AiBrain.DecideNormal(ctx);
            Board root = new Board
            {
                Player = ctx.Me.Clone(),
                Enemy = ctx.Opponent.Clone(),
                Weather = new List<int>(ctx.Weather),
                DoubleSpies = ctx.DoubleSpies,
                RandomMedic = ctx.RandomMedic,
                Db = ctx.Db,
                Rnd = new Random(ctx.Seed),
            };
            // Ahead after the opponent has passed: passing wins the round, nothing beats it
            if (normal.Type == AiActionType.Pass && root.Enemy.hasPassed && Margin(root) >= Needed(root))
                return normal;
            List<AiAction> moves = Shortlist(root, normal);
            if (moves.Count < 2)
                return normal;

            Random rnd = new Random(ctx.Seed);
            double[] wins = new double[moves.Count];
            Func<Board, bool, AiAction> normal_policy = (board, is_player) => AiBrain.DecideNormal(board.AiContextFor(is_player));
            Stopwatch watch = Stopwatch.StartNew();
            for (int i = 0; i < Rollouts && (i < MinRollouts || watch.ElapsedMilliseconds < TimeLimitMs); i++)
            {
                int deal = rnd.Next();
                for (int m = 0; m < moves.Count; m++)
                {
                    // Every move faces the same guessed hands and draws: the moves are compared, not the luck
                    Board board = Guess(root, new Random(deal));
                    BattleSim.Apply(board, moves[m], true);
                    RoundWinner winner = BattleSim.PlayOut(board, false, board.Player.hasPassed, normal_policy);
                    wins[m] += winner == RoundWinner.Player ? 1 : winner == RoundWinner.Draw ? 0.5 : 0;
                }
            }

            int best = moves.FindIndex(m => Same(m, normal));
            wins[best] += NormalBonus;
            for (int m = 0; m < moves.Count; m++)
                if (wins[m] > wins[best])
                    best = m;
            return moves[best];
        }

        //----------------------------------------The moves considered----------------------------------------//
        private static List<AiAction> Shortlist(Board root, AiAction normal)
        {
            List<AiAction> shortlist = new List<AiAction> { normal };
            Action<AiAction> add = move =>
            {
                if (!shortlist.Any(m => Same(m, move)))
                    shortlist.Add(move);
            };
            add(AiAction.Pass());

            int before = Margin(root);
            var scored = Moves(root).Select(move =>
            {
                Board board = Copy(root, new Random(1));
                BattleSim.Apply(board, move, true);
                return new { Move = move, Gain = Margin(board) - before };
            }).ToList();
            foreach (var move in scored.OrderByDescending(m => m.Gain).Take(StrongestMoves))
                add(move.Move);
            foreach (var move in scored.Where(m => m.Gain > 0 && m.Move.Type == AiActionType.PlayCard).OrderBy(m => m.Gain).Take(CheapestMoves))
                add(move.Move);
            return shortlist;
        }

        // Every move the AI can make (as the board's Player): each card on each row it can go to (a decoy on each
        // unit it can take back), and the leader
        public static List<AiAction> Moves(Board board)
        {
            List<AiAction> moves = new List<AiAction>();
            PlayerState me = board.Player;
            foreach (int card_id in me.HandList.Distinct())
            {
                Card card = board.Db.Get(card_id);
                if (card.IsUnit)
                {
                    if (card.row == Rows.Agile)
                    {
                        moves.Add(AiAction.Play(card_id, Rows.Close));
                        moves.Add(AiAction.Play(card_id, Rows.Range));
                    }
                    else
                        moves.Add(AiAction.Play(card_id, Rows.IsUnitRow(card.row) ? card.row : Rows.Close));
                }
                else if (card.ability == Abilities.Decoy)
                {
                    foreach (string row in Rows.Units)
                        foreach (int target in me.UnitRow(row).Distinct())
                        {
                            Card target_card = board.Db.Get(target);
                            if (target_card.IsUnit && !target_card.unique)
                                moves.Add(AiAction.Play(card_id, row, target));
                        }
                }
                else if (card.row == Rows.Special)
                {
                    foreach (string row in Rows.Units)
                        if (!me.HasHorn(row))
                            moves.Add(AiAction.Play(card_id, row));
                }
                else if (!card.IsLeader)
                    moves.Add(AiAction.Play(card_id, Rows.Weather)); // weather, scorch
            }

            if (me.canLeader && !me.leaderDisabled && LeaderRules.Unavailable(board, me.Leader, true) == null)
            {
                string weather_row = LeaderIds.WeatherPickRow(me.Leader);
                if (weather_row != null)
                    foreach (int card_id in LeaderRules.WeatherCardsFor(me, weather_row, board.Db))
                        moves.Add(AiAction.Leader(card_id));
                else if (LeaderRules.IsInstant(me.Leader) || me.Leader == LeaderIds.EredinBringerOfDeath || me.Leader == LeaderIds.EmhyrTheRelentless)
                    moves.Add(AiAction.Leader());
            }
            return moves;
        }

        //----------------------------------------Helpers----------------------------------------//
        // The opponent's unseen cards dealt anew, the AI's deck reshuffled
        private static Board Guess(Board root, Random rnd)
        {
            Board board = Copy(root, rnd);
            PlayerState opponent = board.Enemy;
            List<int> unseen = Shuffle(opponent.HandList.Concat(opponent.DeckList).OrderBy(card_id => card_id), rnd); // (sorted: which were in hand cannot matter)
            int in_hand = opponent.HandList.Count;
            opponent.HandList = unseen.GetRange(0, in_hand);
            opponent.DeckList = unseen.GetRange(in_hand, unseen.Count - in_hand);
            board.Player.DeckList = Shuffle(board.Player.DeckList, rnd);
            return board;
        }

        private static Board Copy(Board board, Random rnd)
        {
            return new Board
            {
                Player = board.Player.Clone(),
                Enemy = board.Enemy.Clone(),
                Weather = new List<int>(board.Weather),
                DoubleSpies = board.DoubleSpies,
                RandomMedic = board.RandomMedic,
                Db = board.Db,
                Rnd = rnd,
            };
        }

        private static List<int> Shuffle(IEnumerable<int> cards, Random rnd)
        {
            List<int> list = new List<int>(cards);
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                int swap = list[i];
                list[i] = list[j];
                list[j] = swap;
            }
            return list;
        }

        private static int Margin(Board board)
        {
            board.Recalculate();
            return board.Player.TotalScore() - board.Enemy.TotalScore();
        }

        // Nilfgaard wins a tied round (unless both sides are Nilfgaard)
        private static int Needed(Board board)
        {
            return board.Player.Faction == Factions.Nilfgaard && board.Enemy.Faction != Factions.Nilfgaard ? 0 : 1;
        }

        private static bool Same(AiAction a, AiAction b)
        {
            return a.Type == b.Type && a.CardId == b.CardId && a.Row == b.Row && a.TargetId == b.TargetId;
        }
    }
}

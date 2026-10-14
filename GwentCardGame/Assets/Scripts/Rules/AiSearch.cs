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
        public static int TimeLimitMs = 1500;   // then the battles played so far decide (an opening move takes 0.15 to
                                                // 0.35 s in the editor on a desktop, a later one less)
        private const int StrongestMoves = 5;   // the moves considered: the Normal AI's own, passing, the moves that add
        private const int CheapestMoves = 2;    // the most strength now, and the cheapest that add any (to bait)
        private const double NormalBonus = 0.5; // another move must win more often than the Normal AI's own

        public static AiAction Decide(AiContext ctx)
        {
            Thinking thinking = Begin(ctx, TimeLimitMs);
            thinking.Step(int.MaxValue);
            return thinking.Result;
        }

        // The decision taken in steps (where there is no other thread to think on, a browser: the battle goes on between
        // steps of a few milliseconds). time_limit_ms counts the time spent in the steps only.
        public static Thinking Begin(AiContext ctx, int time_limit_ms)
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
                return new Thinking(normal);
            List<AiAction> moves = Shortlist(root, normal);
            if (moves.Count < 2)
                return new Thinking(normal);
            return new Thinking(normal, moves, root, new Random(ctx.Seed), time_limit_ms);
        }

        public class Thinking
        {
            private readonly AiAction normal;
            private readonly List<AiAction> moves;
            private readonly double[] wins;
            private readonly Board root;
            private readonly Random rnd;
            private readonly int timeLimitMs;
            private readonly Stopwatch busy = new Stopwatch();
            private readonly Func<Board, bool, AiAction> normalPolicy = (board, is_player) => AiBrain.DecideNormal(board.AiContextFor(is_player));
            private int round, move, deal;

            public bool Done { get; private set; }
            public AiAction Result { get; private set; }

            internal Thinking(AiAction decided)
            {
                Result = decided;
                Done = true;
            }

            internal Thinking(AiAction normal, List<AiAction> moves, Board root, Random rnd, int time_limit_ms)
            {
                this.normal = normal;
                this.moves = moves;
                this.root = root;
                this.rnd = rnd;
                timeLimitMs = time_limit_ms;
                wins = new double[moves.Count];
            }

            // Plays battles out for about budget_ms (a battle at least); true once the move is chosen
            public bool Step(int budget_ms)
            {
                if (Done)
                    return true;
                Stopwatch step = Stopwatch.StartNew();
                busy.Start();
                try
                {
                    do
                    {
                        if (move == 0)
                        {
                            // A round: one battle per move, while there are rounds left and time for them
                            if (round >= Rollouts || round >= MinRollouts && busy.ElapsedMilliseconds >= timeLimitMs)
                            {
                                Choose();
                                return true;
                            }
                            deal = rnd.Next();
                        }
                        // Every move faces the same guessed hands and draws: the moves are compared, not the luck
                        Board board = Guess(root, new Random(deal));
                        BattleSim.Apply(board, moves[move], true);
                        RoundWinner winner = BattleSim.PlayOut(board, false, board.Player.hasPassed, normalPolicy);
                        wins[move] += winner == RoundWinner.Player ? 1 : winner == RoundWinner.Draw ? 0.5 : 0;
                        if (++move == moves.Count)
                        {
                            move = 0;
                            round++;
                        }
                    }
                    while (step.ElapsedMilliseconds < budget_ms);
                }
                finally
                {
                    busy.Stop();
                }
                return false;
            }

            private void Choose()
            {
                int best = moves.FindIndex(m => Same(m, normal));
                wins[best] += NormalBonus;
                for (int m = 0; m < moves.Count; m++)
                    if (wins[m] > wins[best])
                        best = m;
                Result = moves[best];
                Done = true;
            }
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
        // The opponent's unseen cards dealt anew, the AI's deck reshuffled. On the same board every time (per thread):
        // a battle played out is over before the next one is dealt.
        [ThreadStatic] private static Board guessed;
        [ThreadStatic] private static List<int> unseen;

        private static Board Guess(Board root, Random rnd)
        {
            Board board = guessed ?? (guessed = new Board { Player = new PlayerState(), Enemy = new PlayerState(), Weather = new List<int>() });
            board.Player.CopyFrom(root.Player);
            board.Enemy.CopyFrom(root.Enemy);
            board.Weather.Clear();
            board.Weather.AddRange(root.Weather);
            board.DoubleSpies = root.DoubleSpies;
            board.RandomMedic = root.RandomMedic;
            board.Db = root.Db;
            board.Rnd = rnd;

            PlayerState opponent = board.Enemy;
            List<int> cards = unseen ?? (unseen = new List<int>());
            cards.Clear();
            cards.AddRange(opponent.HandList);
            cards.AddRange(opponent.DeckList);
            cards.Sort(); // which were in the hand cannot matter
            Shuffle(cards, rnd);
            int in_hand = opponent.HandList.Count;
            opponent.HandList.Clear();
            opponent.DeckList.Clear();
            for (int i = 0; i < cards.Count; i++)
                (i < in_hand ? opponent.HandList : opponent.DeckList).Add(cards[i]);
            Shuffle(board.Player.DeckList, rnd);
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

        // Fisher-Yates, in place
        private static void Shuffle(List<int> list, Random rnd)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                int swap = list[i];
                list[i] = list[j];
                list[j] = swap;
            }
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

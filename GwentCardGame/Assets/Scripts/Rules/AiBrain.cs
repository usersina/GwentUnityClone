using System;
using System.Collections.Generic;
using System.Linq;

namespace Gwent.Rules
{
    public enum AiActionType { PlayCard, UseLeader, Pass }

    public struct AiAction
    {
        public AiActionType Type;
        public int CardId;      // Card to play, or the weather card a leader pulls from the deck
        public Row Row;      // Unit row, special row for a horn, Row.Weather for weather/scorch cards
        public int TargetId;    // Decoy target

        public static AiAction Pass()
        {
            return new AiAction { Type = AiActionType.Pass, CardId = -1, TargetId = -1 };
        }

        public static AiAction Play(int card_id, Row row, int target_id = -1)
        {
            return new AiAction { Type = AiActionType.PlayCard, CardId = card_id, Row = row, TargetId = target_id };
        }

        public static AiAction Leader(int weather_card_id = -1)
        {
            return new AiAction { Type = AiActionType.UseLeader, CardId = weather_card_id, Row = Row.Weather, TargetId = -1 };
        }

        public override string ToString()
        {
            return Type + (Type == AiActionType.Pass ? "" : " card=" + CardId + " row=" + Row + (TargetId >= 0 ? " target=" + TargetId : ""));
        }
    }

    // How well the AI plays (Settings > Game). The rules are the same for all three.
    public enum AiDifficulty { Easy, Normal, Hard }

    // Snapshot of the board from the AI's point of view
    public class AiContext
    {
        public PlayerState Me;
        public PlayerState Opponent;
        public List<int> Weather = new List<int>();
        public bool DoubleSpies;
        public bool RandomMedic;
        public CardDatabase Db;
        public AiDifficulty Difficulty = AiDifficulty.Normal;
        public int Seed;        // Easy's slips and Hard's look-ahead: the same position and seed, the same move
    }

    // Decides the AI's move. Every candidate move is simulated on copies of the board, so weather,
    // horns, morale, bonds and scorch are all accounted for. Always returns an action (Pass at worst).
    // Normal is described below; Easy plays like a beginner (DecideEasy); Hard looks ahead (AiSearch).
    public static class AiBrain
    {
        private const int DrawValue = 10;       // What drawing one extra card is worth
        private const int SpecialCost = 6;
        private const int LeaderCost = 8;
        private const int MinLeaderGain = 6;

        private class Candidate
        {
            public AiAction Action;
            public int Gain;        // Change of (AI total - opponent total), plus card-advantage bonuses
            public int Board;       // The change of (AI total - opponent total) alone: what can win a round now
            public int Cost;        // How much we'd rather keep this card for later
            public bool IsUnit;
            public bool IsSpy;
            public bool OffBoard;   // brings a card to the hand: no board change, never the move that wins a round
        }

        public static AiAction Decide(AiContext ctx)
        {
            switch (ctx.Difficulty)
            {
                case AiDifficulty.Easy:
                    return DecideEasy(ctx);
                case AiDifficulty.Hard:
                    return AiSearch.Decide(ctx);
                default:
                    return DecideNormal(ctx);
            }
        }

        public static AiAction DecideNormal(AiContext ctx)
        {
            StrengthCalculator.Recalculate(ctx.Me, ctx.Weather, ctx.DoubleSpies, ctx.Db);
            StrengthCalculator.Recalculate(ctx.Opponent, ctx.Weather, ctx.DoubleSpies, ctx.Db);

            List<Candidate> candidates = BuildCandidates(ctx);
            if (candidates.Count == 0)
                return AiAction.Pass();

            int advantage = ctx.Me.TotalScore() - ctx.Opponent.TotalScore();
            // Done: passed, or nothing left to play (an empty hand with a leader it can use is not done)
            Board seen_from_opponent = new Board { Player = ctx.Opponent, Enemy = ctx.Me, Weather = ctx.Weather, DoubleSpies = ctx.DoubleSpies, RandomMedic = ctx.RandomMedic, Db = ctx.Db };
            bool opponent_done = ctx.Opponent.hasPassed || RoundRules.OutOfPlays(seen_from_opponent, true);
            // Nilfgaard wins a tied round (unless both sides are Nilfgaard)
            int needed = ctx.Me.Faction == Faction.Nilfgaard && ctx.Opponent.Faction != Faction.Nilfgaard ? 0 : 1;

            if (opponent_done)
            {
                if (advantage >= needed)
                    return AiAction.Pass(); // Already winning the round

                Candidate winning = candidates.Where(c => !c.OffBoard && advantage + c.Board >= needed)
                    .OrderBy(c => c.Cost).ThenByDescending(c => c.Board).FirstOrDefault();
                if (winning != null)
                    return winning.Action;

                int potential = candidates.Where(c => c.Board > 0 && !c.OffBoard).Sum(c => c.Board);
                if (ctx.Me.Lives > 1 && advantage + potential < needed)
                    return AiAction.Pass(); // Cannot catch up: keep the cards for the next round
                return BestOrPass(candidates.Where(c => !c.OffBoard).ToList());
            }

            // Spies first: extra cards are worth more early on
            Candidate spy = candidates.Where(c => c.IsSpy && c.Gain > 0).OrderByDescending(c => c.Gain).FirstOrDefault();
            if (spy != null)
                return spy.Action;

            bool has_units = candidates.Any(c => c.IsUnit);
            if (!has_units && advantage > 0 && ctx.Me.Lives > 1)
                return AiAction.Pass(); // Do not waste specials when already ahead (on the last life, they may still be needed)

            if (ctx.Me.Lives <= 1)
            {
                // Final round: nothing to save cards for
                if (advantage < needed)
                    return CheapestWinningOrBest(candidates, advantage, needed);
                return WeakestPositiveOrPass(candidates);
            }

            // Two lives left: play conservatively
            int units_in_hand = ctx.Me.HandList.Count(id => ctx.Db.Get(id).IsUnit);
            if (advantage > 25 || advantage < -30)
                return AiAction.Pass(); // Comfortable lead, or too far behind to be worth the cards
            if (units_in_hand < 5)
                return AiAction.Pass(); // Keep the remaining units for the next rounds
            if (advantage < needed)
                return CheapestWinningOrBest(candidates, advantage, needed); // Behind: catch up as cheaply as possible
            return WeakestPositiveOrPass(candidates);                         // Ahead: keep the pressure with weak cards
        }

        // Easy: a beginner. It fights every round with the move that adds the most strength right now, round 1 included,
        // so it runs short of cards later; a spy is just a weak card to it, and now and then it plays a card that is not
        // its best. It still stops once it is ahead after the opponent has passed, and keeps its cards when it could
        // not catch up.
        private const int EasySlipPercent = 25;

        private static AiAction DecideEasy(AiContext ctx)
        {
            StrengthCalculator.Recalculate(ctx.Me, ctx.Weather, ctx.DoubleSpies, ctx.Db);
            StrengthCalculator.Recalculate(ctx.Opponent, ctx.Weather, ctx.DoubleSpies, ctx.Db);

            List<Candidate> on_board = BuildCandidates(ctx).Where(c => !c.OffBoard && c.Board > 0).OrderByDescending(c => c.Board).ToList();
            int advantage = ctx.Me.TotalScore() - ctx.Opponent.TotalScore();
            Board seen_from_opponent = new Board { Player = ctx.Opponent, Enemy = ctx.Me, Weather = ctx.Weather, DoubleSpies = ctx.DoubleSpies, RandomMedic = ctx.RandomMedic, Db = ctx.Db };
            bool opponent_done = ctx.Opponent.hasPassed || RoundRules.OutOfPlays(seen_from_opponent, true);
            int needed = ctx.Me.Faction == Faction.Nilfgaard && ctx.Opponent.Faction != Faction.Nilfgaard ? 0 : 1;

            if (on_board.Count == 0 || opponent_done && advantage >= needed)
                return AiAction.Pass();
            if (opponent_done && ctx.Me.Lives > 1 && advantage + on_board.Sum(c => c.Board) < needed)
                return AiAction.Pass();

            Random rnd = new Random(ctx.Seed);
            if (rnd.Next(100) < EasySlipPercent)
                return on_board[rnd.Next(on_board.Count)].Action;
            return on_board[0].Action;
        }

        private static AiAction CheapestWinningOrBest(List<Candidate> candidates, int advantage, int needed)
        {
            List<Candidate> on_board = candidates.Where(c => !c.OffBoard).ToList();
            Candidate winning = on_board.Where(c => advantage + c.Board >= needed)
                .OrderBy(c => c.Cost).ThenByDescending(c => c.Board).FirstOrDefault();
            return winning != null ? winning.Action : BestOrPass(on_board);
        }

        private static AiAction WeakestPositiveOrPass(List<Candidate> candidates)
        {
            Candidate weakest = candidates.Where(c => c.Gain > 0).OrderBy(c => c.Cost).ThenByDescending(c => c.Gain).FirstOrDefault();
            return weakest != null ? weakest.Action : AiAction.Pass();
        }

        private static AiAction BestOrPass(List<Candidate> candidates)
        {
            Candidate best = candidates.OrderByDescending(c => c.Gain).ThenBy(c => c.Cost).FirstOrDefault();
            return best != null && best.Gain > 0 ? best.Action : AiAction.Pass();
        }

        //----------------------------------------Candidates----------------------------------------//
        private static List<Candidate> BuildCandidates(AiContext ctx)
        {
            List<Candidate> candidates = new List<Candidate>();
            List<int> hand = ctx.Me.HandList;
            for (int i = 0; i < hand.Count; i++)
            {
                int card_id = hand[i];
                if (hand.IndexOf(card_id) < i)
                    continue; // each card once
                Card card;
                if (!ctx.Db.TryGet(card_id, out card) || card.IsLeader)
                    continue;
                Candidate candidate = card.IsUnit ? UnitCandidate(ctx, card) : SpecialCandidate(ctx, card);
                if (candidate != null)
                    candidates.Add(candidate);
            }

            Candidate leader = LeaderCandidate(ctx);
            if (leader != null)
                candidates.Add(leader);
            return candidates;
        }

        private static readonly Row[] AgileRows = { Row.Close, Row.Range };

        private static Candidate UnitCandidate(AiContext ctx, Card card)
        {
            if (card.ability == Ability.Spy)
            {
                Row spy_row = Rows.IsUnitRow(card.row) ? card.row : Row.Close;
                int board = Simulate(ctx, (me, opp, weather) => opp.UnitRow(spy_row).Add(card._id));
                int gain = board + DrawValue * Math.Min(2, ctx.Me.DeckList.Count);
                return new Candidate { Action = AiAction.Play(card._id, spy_row), Gain = gain, Board = board, Cost = 0, IsUnit = true, IsSpy = true };
            }

            Candidate best = null;
            bool agile = card.row == Row.Agile;
            for (int r = 0; r < (agile ? 2 : 1); r++)
            {
                Row row = agile ? AgileRows[r] : card.row;
                if (!Rows.IsUnitRow(row))
                    continue;
                Row chosen_row = row;
                int drawn = 0;
                int board = Simulate(ctx, (me, opp, weather) => drawn = PlayUnit(ctx, me, opp, weather, card, chosen_row));
                int gain = board + DrawValue * drawn; // spies a medic brought back
                if (best == null || gain > best.Gain)
                    best = new Candidate { Action = AiAction.Play(card._id, chosen_row), Gain = gain, Board = board, Cost = card.strength + (card.unique ? 5 : 0), IsUnit = true };
            }
            return best;
        }

        // Unit placement with the abilities that change the board right away; a medic's unit is played the same way
        // (so medics chain). Returns the cards drawn by spies brought back along the way.
        private static int PlayUnit(AiContext ctx, PlayerState me, PlayerState opp, List<int> weather, Card card, Row row, int depth = 0)
        {
            int drawn = 0;
            me.HandList.Remove(card._id);
            if (card.ability == Ability.Muster)
                AbilityRules.Muster(me, card, row, ctx.Db);
            if (card.ability == Ability.Spy)
            {
                opp.UnitRow(row).Add(card._id);
                drawn += Math.Min(2, me.DeckList.Count);
            }
            else
                me.UnitRow(row).Add(card._id);

            // A medic brings back the best unit (not counted under Invader of the North: then the unit is random)
            if (card.ability == Ability.Medic && depth < 5 && !ctx.RandomMedic)
            {
                int revived = ChooseMedicTarget(me, ctx.Db);
                if (revived != -1)
                {
                    Card revived_card = ctx.Db.Get(revived);
                    me.DiscardList.Remove(revived);
                    Row revived_row = Rows.IsUnitRow(revived_card.row) ? revived_card.row
                        : AbilityRules.BestAgileRow(me, revived, weather, ctx.DoubleSpies, ctx.Db);
                    drawn += PlayUnit(ctx, me, opp, weather, revived_card, revived_row, depth + 1);
                }
            }

            Row? scorch_row = Abilities.RowScorchTarget(card.ability);
            if (scorch_row != null)
            {
                StrengthCalculator.Recalculate(opp, weather, ctx.DoubleSpies, ctx.Db);
                AbilityRules.ScorchRow(opp, scorch_row.Value, ctx.Db);
            }
            return drawn;
        }

        private static Candidate SpecialCandidate(AiContext ctx, Card card)
        {
            if (card.ability == Ability.CommanderHorn)
            {
                Candidate best = null;
                foreach (Row row in Rows.Units)
                {
                    if (ctx.Me.HasHorn(row))
                        continue;
                    Row chosen_row = row;
                    int gain = Simulate(ctx, (me, opp, weather) => me.SpecialRow(chosen_row).Add(CardIds.CommanderHorn));
                    if (best == null || gain > best.Gain)
                        best = new Candidate { Action = AiAction.Play(card._id, chosen_row), Gain = gain, Board = gain, Cost = SpecialCost };
                }
                return best;
            }

            if (card.row == Row.Weather)
            {
                int gain = Simulate(ctx, (me, opp, weather) => AbilityRules.PlayWeather(me, opp, weather, card));
                return new Candidate { Action = AiAction.Play(card._id, Row.Weather), Gain = gain, Board = gain, Cost = SpecialCost };
            }

            if (card.ability == Ability.Scorch)
            {
                int gain = Simulate(ctx, (me, opp, weather) => AbilityRules.Scorch(me, opp, ctx.Db));
                return new Candidate { Action = AiAction.Play(card._id, Row.Weather), Gain = gain, Board = gain, Cost = SpecialCost };
            }

            if (card.ability == Ability.Decoy)
            {
                Row target_row;
                int target = ChooseDecoyTarget(ctx.Me, ctx.Db, out target_row);
                if (target == -1)
                    return null;
                Card target_card = ctx.Db.Get(target);
                int bonus = target_card.ability == Ability.Spy ? DrawValue : 4;
                int board = Simulate(ctx, (me, opp, weather) =>
                {
                    me.UnitRow(target_row).Remove(target);
                    me.UnitRow(target_row).Add(CardIds.Decoy);
                });
                return new Candidate { Action = AiAction.Play(card._id, target_row, target), Gain = bonus + board, Board = board, Cost = SpecialCost };
            }
            return null;
        }

        private static Candidate LeaderCandidate(AiContext ctx)
        {
            PlayerState me = ctx.Me;
            if (!me.canLeader || me.leaderDisabled)
                return null;

            int leader = me.Leader;
            Board board = new Board { Player = me, Enemy = ctx.Opponent, Weather = ctx.Weather, DoubleSpies = ctx.DoubleSpies, RandomMedic = ctx.RandomMedic, Db = ctx.Db };
            if (LeaderRules.Unavailable(board, leader, true) != null)
                return null;
            int gain = int.MinValue;
            int weather_card = -1;

            Row? weather_row = LeaderIds.WeatherPickRow(leader);
            Row? horn_row = LeaderIds.HornRow(leader);
            Row? scorch_row = LeaderIds.ScorchRow(leader);
            if (weather_row != null)
            {
                foreach (int card_id in LeaderRules.WeatherCardsFor(me, weather_row.Value, ctx.Db))
                {
                    Card card = ctx.Db.Get(card_id);
                    int card_gain = Simulate(ctx, (m, o, w) => AbilityRules.PlayWeather(m, o, w, card));
                    if (card_gain > gain)
                    {
                        gain = card_gain;
                        weather_card = card_id;
                    }
                }
            }
            else if (horn_row != null)
            {
                if (!me.HasHorn(horn_row.Value))
                    gain = Simulate(ctx, (m, o, w) => AbilityRules.PlaceLeaderHorn(m, horn_row.Value));
            }
            else if (scorch_row != null)
                gain = Simulate(ctx, (m, o, w) => AbilityRules.ScorchRow(o, scorch_row.Value, ctx.Db));
            else if (leader == LeaderIds.FoltestLordCommander)
                gain = Simulate(ctx, (m, o, w) => AbilityRules.ClearWeather(m, o, w));
            else if (leader == LeaderIds.FrancescaHopeOfTheAenSeidhe)
                gain = Simulate(ctx, (m, o, w) => AbilityRules.OptimizeAgile(m, w, ctx.DoubleSpies, ctx.Db));
            else if (leader == LeaderIds.EredinBringerOfDeath || leader == LeaderIds.EmhyrTheRelentless)
            {
                // A unit from a discard pile to the hand: worth a card, more for a strong one or a spy
                List<int> pile = leader == LeaderIds.EmhyrTheRelentless ? ctx.Opponent.DiscardList : me.DiscardList;
                int target = ChooseRestoreTarget(AbilityRules.MedicCandidates(pile, ctx.Db), me, ctx.Db);
                if (target == -1)
                    return null;
                Card card = ctx.Db.Get(target);
                int value = DrawValue + card.strength / 2 + (card.ability == Ability.Spy || card.ability == Ability.Medic ? DrawValue : 0);
                return new Candidate { Action = AiAction.Leader(), Gain = value, Cost = LeaderCost, OffBoard = true };
            }

            if (gain < MinLeaderGain)
                return null;
            return new Candidate { Action = AiAction.Leader(weather_card), Gain = gain, Board = gain, Cost = LeaderCost };
        }

        // Applies a move to copies of the board and returns the change of (AI total - opponent total). The copies are
        // the same objects every time (per thread: Hard thinks on another one), filled anew for each move tried.
        [ThreadStatic] private static PlayerState simulatedMe, simulatedOpponent;
        [ThreadStatic] private static List<int> simulatedWeather;

        private static int Simulate(AiContext ctx, Action<PlayerState, PlayerState, List<int>> apply)
        {
            int before = ctx.Me.TotalScore() - ctx.Opponent.TotalScore();
            PlayerState me = (simulatedMe ?? (simulatedMe = new PlayerState())).CopyFrom(ctx.Me);
            PlayerState opp = (simulatedOpponent ?? (simulatedOpponent = new PlayerState())).CopyFrom(ctx.Opponent);
            List<int> weather = simulatedWeather ?? (simulatedWeather = new List<int>());
            weather.Clear();
            weather.AddRange(ctx.Weather);
            bool double_spies = ctx.DoubleSpies;

            // The copies carry the strengths the decision began with (as `before` does): only the move's result is
            // recalculated
            apply(me, opp, weather);
            StrengthCalculator.Recalculate(me, weather, double_spies, ctx.Db);
            StrengthCalculator.Recalculate(opp, weather, double_spies, ctx.Db);
            return (me.TotalScore() - opp.TotalScore()) - before;
        }

        //----------------------------------------Choices made for the AI----------------------------------------//
        // Medic: a spy (two cards) while the deck has cards, then a medic (it chains), then a unit that scorches a
        // row, otherwise the strongest unit
        public static int ChooseMedicTarget(PlayerState me, CardDatabase db)
        {
            return ChooseRestoreTarget(AbilityRules.MedicCandidates(me.DiscardList, db), me, db);
        }

        public static int ChooseRestoreTarget(List<int> candidates, PlayerState me, CardDatabase db)
        {
            // The first of the best, in the pile's order
            int best = -1, best_priority = 0, best_strength = 0;
            foreach (int card_id in candidates)
            {
                Card card = db.Get(card_id);
                int priority = RestorePriority(card, me);
                if (best == -1 || priority > best_priority || priority == best_priority && card.strength > best_strength)
                {
                    best = card_id;
                    best_priority = priority;
                    best_strength = card.strength;
                }
            }
            return best;
        }

        private static int RestorePriority(Card card, PlayerState me)
        {
            if (card.ability == Ability.Spy)
                return me.DeckList.Count > 0 ? 3 : 0;
            if (card.ability == Ability.Medic)
                return 2;
            return Abilities.RowScorchTarget(card.ability) != null ? 1 : 0;
        }

        // Decoy: take back an enemy spy on our side (to replay it), else one of our medics, else a unit that
        // scorches a row (to scorch again)
        public static int ChooseDecoyTarget(PlayerState me, CardDatabase db, out Row row)
        {
            Func<Card, bool>[] wanted =
            {
                card => card.ability == Ability.Spy,
                card => card.ability == Ability.Medic,
                card => Abilities.IsRowScorch(card.ability),
            };
            foreach (Func<Card, bool> want in wanted)
                foreach (Row unit_row in Rows.Units)
                    foreach (int card_id in me.UnitRow(unit_row))
                    {
                        Card card = db.Get(card_id);
                        if (want(card) && card.IsUnit && !card.unique)
                        {
                            row = unit_row;
                            return card_id;
                        }
                    }
            row = Row.Close; // no target: the row means nothing
            return -1;
        }
    }
}

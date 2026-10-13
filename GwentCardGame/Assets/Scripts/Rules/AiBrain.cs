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
        public string Row;      // Unit row, special row for a horn, Rows.Weather for weather/scorch cards
        public int TargetId;    // Decoy target

        public static AiAction Pass()
        {
            return new AiAction { Type = AiActionType.Pass, CardId = -1, TargetId = -1 };
        }

        public static AiAction Play(int card_id, string row, int target_id = -1)
        {
            return new AiAction { Type = AiActionType.PlayCard, CardId = card_id, Row = row, TargetId = target_id };
        }

        public static AiAction Leader(int weather_card_id = -1)
        {
            return new AiAction { Type = AiActionType.UseLeader, CardId = weather_card_id, Row = Rows.Weather, TargetId = -1 };
        }

        public override string ToString()
        {
            return Type + (Type == AiActionType.Pass ? "" : " card=" + CardId + " row=" + Row + (TargetId >= 0 ? " target=" + TargetId : ""));
        }
    }

    // Snapshot of the board from the AI's point of view
    public class AiContext
    {
        public PlayerState Me;
        public PlayerState Opponent;
        public List<int> Weather = new List<int>();
        public bool DoubleSpies;
        public bool RandomMedic;
        public CardDatabase Db;
    }

    // Decides the AI's move. Every candidate move is simulated on copies of the board, so weather,
    // horns, morale, bonds and scorch are all accounted for. Always returns an action (Pass at worst).
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
            public int Cost;        // How much we'd rather keep this card for later
            public bool IsUnit;
            public bool IsSpy;
        }

        public static AiAction Decide(AiContext ctx)
        {
            StrengthCalculator.Recalculate(ctx.Me, ctx.Weather, ctx.DoubleSpies, ctx.Db);
            StrengthCalculator.Recalculate(ctx.Opponent, ctx.Weather, ctx.DoubleSpies, ctx.Db);

            List<Candidate> candidates = BuildCandidates(ctx);
            if (candidates.Count == 0)
                return AiAction.Pass();

            int advantage = ctx.Me.TotalScore() - ctx.Opponent.TotalScore();
            bool opponent_done = ctx.Opponent.hasPassed || ctx.Opponent.HandList.Count == 0;

            if (opponent_done)
            {
                if (advantage > 0)
                    return AiAction.Pass(); // Already winning the round

                Candidate winning = candidates.Where(c => advantage + c.Gain > 0)
                    .OrderBy(c => c.Cost).ThenByDescending(c => c.Gain).FirstOrDefault();
                if (winning != null)
                    return winning.Action;

                int potential = candidates.Where(c => c.Gain > 0).Sum(c => c.Gain);
                if (ctx.Me.Lives > 1 && advantage + potential <= 0)
                    return AiAction.Pass(); // Cannot catch up: keep the cards for the next round
                return BestOrPass(candidates);
            }

            // Spies first: extra cards are worth more early on
            Candidate spy = candidates.Where(c => c.IsSpy && c.Gain > 0).OrderByDescending(c => c.Gain).FirstOrDefault();
            if (spy != null)
                return spy.Action;

            bool has_units = candidates.Any(c => c.IsUnit);
            if (!has_units && advantage > 0)
                return AiAction.Pass(); // Do not waste specials when already ahead

            if (ctx.Me.Lives <= 1)
            {
                // Final round: nothing to save cards for
                if (advantage <= 0)
                    return CheapestWinningOrBest(candidates, advantage);
                return WeakestPositiveOrPass(candidates);
            }

            // Two lives left: play conservatively
            int units_in_hand = ctx.Me.HandList.Count(id => ctx.Db.Get(id).IsUnit);
            if (advantage > 25 || advantage < -30)
                return AiAction.Pass(); // Comfortable lead, or too far behind to be worth the cards
            if (units_in_hand < 5)
                return AiAction.Pass(); // Keep the remaining units for the next rounds
            if (advantage < 0)
                return CheapestWinningOrBest(candidates, advantage); // Behind: catch up as cheaply as possible
            return WeakestPositiveOrPass(candidates);                 // Ahead: keep the pressure with weak cards
        }

        private static AiAction CheapestWinningOrBest(List<Candidate> candidates, int advantage)
        {
            Candidate winning = candidates.Where(c => advantage + c.Gain > 0)
                .OrderBy(c => c.Cost).ThenByDescending(c => c.Gain).FirstOrDefault();
            return winning != null ? winning.Action : BestOrPass(candidates);
        }

        private static AiAction WeakestPositiveOrPass(List<Candidate> candidates)
        {
            Candidate weakest = candidates.Where(c => c.Gain > 0).OrderBy(c => c.Cost).ThenByDescending(c => c.Gain).FirstOrDefault();
            return weakest != null ? weakest.Action : AiAction.Pass();
        }

        private static AiAction BestOrPass(List<Candidate> candidates)
        {
            Candidate best = candidates.OrderByDescending(c => c.Gain).ThenBy(c => c.Cost).First();
            return best.Gain > 0 ? best.Action : AiAction.Pass();
        }

        //----------------------------------------Candidates----------------------------------------//
        private static List<Candidate> BuildCandidates(AiContext ctx)
        {
            List<Candidate> candidates = new List<Candidate>();
            foreach (int card_id in ctx.Me.HandList.Distinct())
            {
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

        private static Candidate UnitCandidate(AiContext ctx, Card card)
        {
            if (card.ability == Abilities.Spy)
            {
                string spy_row = Rows.IsUnitRow(card.row) ? card.row : Rows.Close;
                int gain = Simulate(ctx, (me, opp, weather) => opp.UnitRow(spy_row).Add(card._id));
                gain += DrawValue * Math.Min(2, ctx.Me.DeckList.Count);
                return new Candidate { Action = AiAction.Play(card._id, spy_row), Gain = gain, Cost = 0, IsUnit = true, IsSpy = true };
            }

            Candidate best = null;
            string[] rows = card.row == Rows.Agile ? new[] { Rows.Close, Rows.Range } : new[] { card.row };
            foreach (string row in rows)
            {
                if (!Rows.IsUnitRow(row))
                    continue;
                string chosen_row = row;
                int gain = Simulate(ctx, (me, opp, weather) => PlayUnit(ctx, me, opp, weather, card, chosen_row));
                if (best == null || gain > best.Gain)
                    best = new Candidate { Action = AiAction.Play(card._id, chosen_row), Gain = gain, Cost = card.strength + (card.unique ? 5 : 0), IsUnit = true };
            }
            return best;
        }

        // Unit placement with the abilities that change the board right away
        private static void PlayUnit(AiContext ctx, PlayerState me, PlayerState opp, List<int> weather, Card card, string row)
        {
            me.HandList.Remove(card._id);
            if (card.ability == Abilities.Muster)
                AbilityRules.Muster(me, card, row, ctx.Db);
            me.UnitRow(row).Add(card._id);

            if (card.ability == Abilities.Medic)
            {
                int revived = ChooseMedicTarget(me, ctx.Db);
                if (revived != -1)
                {
                    Card revived_card = ctx.Db.Get(revived);
                    me.DiscardList.Remove(revived);
                    string revived_row = Rows.IsUnitRow(revived_card.row) ? revived_card.row : Rows.Close;
                    if (revived_card.ability == Abilities.Spy)
                        opp.UnitRow(revived_row).Add(revived);
                    else
                        me.UnitRow(revived_row).Add(revived);
                }
            }

            string scorch_row = Abilities.RowScorchTarget(card.ability);
            if (scorch_row != null)
            {
                StrengthCalculator.Recalculate(opp, weather, ctx.DoubleSpies, ctx.Db);
                AbilityRules.ScorchRow(opp, scorch_row, ctx.Db);
            }
        }

        private static Candidate SpecialCandidate(AiContext ctx, Card card)
        {
            if (card.ability == Abilities.CommanderHorn)
            {
                Candidate best = null;
                foreach (string row in Rows.Units)
                {
                    if (ctx.Me.HasHorn(row))
                        continue;
                    string chosen_row = row;
                    int gain = Simulate(ctx, (me, opp, weather) => me.SpecialRow(chosen_row).Add(CardIds.CommanderHorn));
                    if (best == null || gain > best.Gain)
                        best = new Candidate { Action = AiAction.Play(card._id, chosen_row), Gain = gain, Cost = SpecialCost };
                }
                return best;
            }

            if (card.row == Rows.Weather)
            {
                int gain = Simulate(ctx, (me, opp, weather) => AbilityRules.PlayWeather(me, opp, weather, card));
                return new Candidate { Action = AiAction.Play(card._id, Rows.Weather), Gain = gain, Cost = SpecialCost };
            }

            if (card.ability == Abilities.Scorch)
            {
                int gain = Simulate(ctx, (me, opp, weather) => AbilityRules.Scorch(me, opp, ctx.Db));
                return new Candidate { Action = AiAction.Play(card._id, Rows.Weather), Gain = gain, Cost = SpecialCost };
            }

            if (card.ability == Abilities.Decoy)
            {
                string target_row;
                int target = ChooseDecoyTarget(ctx.Me, ctx.Db, out target_row);
                if (target == -1)
                    return null;
                Card target_card = ctx.Db.Get(target);
                int bonus = target_card.ability == Abilities.Spy ? DrawValue : 4;
                int gain = bonus + Simulate(ctx, (me, opp, weather) =>
                {
                    me.UnitRow(target_row).Remove(target);
                    me.UnitRow(target_row).Add(CardIds.Decoy);
                });
                return new Candidate { Action = AiAction.Play(card._id, target_row, target), Gain = gain, Cost = SpecialCost };
            }
            return null;
        }

        private static Candidate LeaderCandidate(AiContext ctx)
        {
            PlayerState me = ctx.Me;
            if (!me.canLeader || me.leaderDisabled)
                return null;

            int leader = me.Leader;
            int gain = int.MinValue;
            int weather_card = -1;

            string weather_row = LeaderIds.WeatherPickRow(leader);
            string horn_row = LeaderIds.HornRow(leader);
            string scorch_row = LeaderIds.ScorchRow(leader);
            if (weather_row != null)
            {
                foreach (int card_id in me.DeckList.Distinct())
                {
                    Card card = ctx.Db.Get(card_id);
                    if (card.row != Rows.Weather || card.ability == Abilities.ClearWeather)
                        continue;
                    if (weather_row != "all" && CardIds.WeatherFor(weather_row) != card_id)
                        continue;
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
                if (!me.HasHorn(horn_row))
                    gain = Simulate(ctx, (m, o, w) => AbilityRules.PlaceLeaderHorn(m, horn_row));
            }
            else if (scorch_row != null)
                gain = Simulate(ctx, (m, o, w) => AbilityRules.ScorchRow(o, scorch_row, ctx.Db));
            else if (leader == LeaderIds.FoltestLordCommander)
                gain = Simulate(ctx, (m, o, w) => AbilityRules.ClearWeather(m, o, w));
            else if (leader == LeaderIds.EredinTheTreacherous && !ctx.DoubleSpies)
                gain = Simulate(ctx, (m, o, w) => { }, true);
            else if (leader == LeaderIds.FrancescaHopeOfTheAenSeidhe)
                gain = Simulate(ctx, (m, o, w) => AbilityRules.OptimizeAgile(m, w, ctx.DoubleSpies, ctx.Db));

            if (gain < MinLeaderGain)
                return null;
            return new Candidate { Action = AiAction.Leader(weather_card), Gain = gain, Cost = LeaderCost };
        }

        // Applies a move to copies of the board and returns the change of (AI total - opponent total)
        private static int Simulate(AiContext ctx, Action<PlayerState, PlayerState, List<int>> apply, bool double_spies_after = false)
        {
            int before = ctx.Me.TotalScore() - ctx.Opponent.TotalScore();
            PlayerState me = ctx.Me.Clone();
            PlayerState opp = ctx.Opponent.Clone();
            List<int> weather = new List<int>(ctx.Weather);
            bool double_spies = ctx.DoubleSpies || double_spies_after;

            StrengthCalculator.Recalculate(me, weather, double_spies, ctx.Db);
            StrengthCalculator.Recalculate(opp, weather, double_spies, ctx.Db);
            apply(me, opp, weather);
            StrengthCalculator.Recalculate(me, weather, double_spies, ctx.Db);
            StrengthCalculator.Recalculate(opp, weather, double_spies, ctx.Db);
            return (me.TotalScore() - opp.TotalScore()) - before;
        }

        //----------------------------------------Choices made for the AI----------------------------------------//
        // Medic: a spy brings two cards, otherwise the strongest unit
        public static int ChooseMedicTarget(PlayerState me, CardDatabase db)
        {
            List<int> candidates = AbilityRules.MedicCandidates(me.DiscardList, db);
            if (candidates.Count == 0)
                return -1;
            int spy = candidates.FirstOrDefault(id => db.Get(id).ability == Abilities.Spy && me.DeckList.Count > 0);
            if (spy != 0 && db.Get(spy).ability == Abilities.Spy)
                return spy;
            return candidates.OrderByDescending(id => db.Get(id).strength).First();
        }

        // Decoy: take back an enemy spy on our side (to replay it), else one of our medics
        public static int ChooseDecoyTarget(PlayerState me, CardDatabase db, out string row)
        {
            foreach (string ability in new[] { Abilities.Spy, Abilities.Medic })
                foreach (string unit_row in Rows.Units)
                    foreach (int card_id in me.UnitRow(unit_row))
                    {
                        Card card = db.Get(card_id);
                        if (card.ability == ability && card.IsUnit && !card.unique)
                        {
                            row = unit_row;
                            return card_id;
                        }
                    }
            row = null;
            return -1;
        }
    }
}

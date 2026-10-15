using System;
using System.Collections.Generic;

namespace Gwent.Rules
{
    // Card abilities that move cards between lists. Strengths must be up to date before scorching.
    public static class AbilityRules
    {
        //----------------------------------------Scorch----------------------------------------//
        // Scorch card: destroy the strongest non-hero unit(s) on the whole battlefield
        public static int Scorch(PlayerState first, PlayerState second, CardDatabase db)
        {
            int top_strength = -1;
            foreach (PlayerState info in new[] { first, second })
                foreach (Row row in Rows.Units)
                    top_strength = Math.Max(top_strength, StrongestNonHero(info, row, db));

            if (top_strength < 0)
                return 0;

            int destroyed = 0;
            foreach (PlayerState info in new[] { first, second })
            {
                List<int> avengers = new List<int>();
                foreach (Row row in Rows.Units)
                    destroyed += DestroyWithStrength(info, row, top_strength, db, avengers);
                Avenge(info, avengers, db);
            }
            return destroyed;
        }

        // Row scorch (unit ability or leader): destroy the strongest non-hero unit(s) of a row if it totals 10 or more
        public static int ScorchRow(PlayerState target, Row row, CardDatabase db)
        {
            if (target.RowScore(row) < 10)
                return 0;
            int top_strength = StrongestNonHero(target, row, db);
            if (top_strength < 0)
                return 0;
            List<int> avengers = new List<int>();
            int destroyed = DestroyWithStrength(target, row, top_strength, db, avengers);
            Avenge(target, avengers, db);
            return destroyed;
        }

        private static int StrongestNonHero(PlayerState info, Row row, CardDatabase db)
        {
            int top_strength = -1;
            List<int> units = info.UnitRow(row);
            List<int> strengths = info.ModifiedStrengths(row);
            for (int i = 0; i < units.Count && i < strengths.Count; i++)
                if (CanBeScorched(db.Get(units[i])) && strengths[i] > top_strength)
                    top_strength = strengths[i];
            return top_strength;
        }

        // Whether a row has a unit a scorch could destroy
        public static bool HasScorchTarget(PlayerState info, Row row, CardDatabase db)
        {
            return StrongestNonHero(info, row, db) >= 0;
        }

        // Heroes are immune, and a decoy lying on a row is not a unit
        private static bool CanBeScorched(Card card)
        {
            return !card.unique && !card.IsSpecial;
        }

        // Destroys the row's non-hero units of that strength; the cards their killing calls are added to avengers
        private static int DestroyWithStrength(PlayerState info, Row row, int strength, CardDatabase db, List<int> avengers)
        {
            List<int> units = info.UnitRow(row);
            List<int> base_strengths = info.BaseStrengths(row);
            List<int> strengths = info.ModifiedStrengths(row);
            int destroyed = 0;
            for (int i = units.Count - 1; i >= 0; i--)
            {
                if (i >= strengths.Count || strengths[i] != strength || !CanBeScorched(db.Get(units[i])))
                    continue;
                int card_id = units[i];
                units.RemoveAt(i);
                if (i < base_strengths.Count)
                    base_strengths.RemoveAt(i);
                strengths.RemoveAt(i);
                ToDiscardPile(info, card_id);
                if (AvengerOf(card_id) != -1)
                    avengers.Add(AvengerOf(card_id)); // killed: its avenger comes at once (Avenge)
                destroyed++;
            }
            return destroyed;
        }

        //----------------------------------------Avenger----------------------------------------//
        // The card a unit summons when it goes to the discard pile (-1 if none): the Cow calls the Bovine Defense Force.
        // Killed (scorched: by the Scorch card, a unit's or a leader's row scorch), it takes the Cow's place at once, in the
        // close combat row: kill the Cow, get the punishment. Discarded any other way (cleared at a round's end, discarded
        // from the hand by Destroyer of Worlds), it arrives when the board clears for the next round, so never if the battle
        // ends first. A decoy taking the Cow back to the hand calls nothing.
        public static int AvengerOf(int card_id)
        {
            return card_id == CardIds.Cow ? CardIds.BovineDefenseForce : -1;
        }

        // The cards a scorch's killing called, on the close combat row now (once the scorch is over: never scorched by it)
        private static void Avenge(PlayerState info, List<int> avengers, CardDatabase db)
        {
            foreach (int card_id in avengers)
            {
                info.CloseList.Add(card_id);
                info.CloseStrengthL.Add(db.Get(card_id).strength); // until the next recalculation
                info.MCloseStrength.Add(db.Get(card_id).strength);
            }
        }

        // A card discarded without being killed (cleared at a round's end, or from the hand by Destroyer of Worlds) goes
        // to the discard pile, a Cow's avenger coming next round
        public static void Discard(PlayerState info, int card_id)
        {
            ToDiscardPile(info, card_id);
            if (AvengerOf(card_id) != -1)
                info.PendingSummons.Add(AvengerOf(card_id));
        }

        // A summoned card is not a real card: it vanishes instead of going to the discard pile (so a medic cannot bring
        // it back)
        private static void ToDiscardPile(PlayerState info, int card_id)
        {
            if (card_id != CardIds.BovineDefenseForce)
                info.DiscardList.Add(card_id);
        }

        //----------------------------------------Muster----------------------------------------//
        // "vampire_katakan" => "vampire", "arachas_1" => "arachas"
        public static string MusterGroup(string idstr)
        {
            if (string.IsNullOrEmpty(idstr))
                return "";
            int index = idstr.IndexOf('_');
            return index > 0 ? idstr.Substring(0, index) : idstr;
        }

        public static bool IsMusterRelative(Card played, Card other)
        {
            if (other._id == played._id)
                return true;
            // The Arachas Behemoth and Gaunter O'Dimm call their family, but the family does not call them
            if (other._id == CardIds.ArachasBehemoth || other._id == CardIds.GaunterODimm)
                return false;
            // Only other muster cards of the same family (Havekar Healer is not a Havekar Smuggler)
            return other.ability == Ability.Muster && MusterGroup(other._idstr) == MusterGroup(played._idstr);
        }

        // Play every copy and muster relative from the deck and the hand (the played card itself is already out of the hand)
        public static List<int> Muster(PlayerState info, Card played, Row played_row, CardDatabase db)
        {
            List<int> mustered = new List<int>();
            foreach (List<int> source in new[] { info.DeckList, info.HandList })
            {
                for (int i = source.Count - 1; i >= 0; i--)
                {
                    Card card = db.Get(source[i]);
                    if (!IsMusterRelative(played, card))
                        continue;
                    Row row = Rows.IsUnitRow(card.row) ? card.row : played_row;
                    info.UnitRow(row).Add(card._id);
                    mustered.Add(card._id);
                    source.RemoveAt(i);
                }
            }
            return mustered;
        }

        //----------------------------------------Medic----------------------------------------//
        // Units a medic can bring back (no heroes, specials or leaders)
        public static List<int> MedicCandidates(IEnumerable<int> discard, CardDatabase db)
        {
            List<int> returned_list = new List<int>();
            foreach (int card_id in discard)
            {
                Card card = db.Get(card_id);
                if (card.IsUnit && !card.unique && db.Contains(card_id))
                    returned_list.Add(card_id);
            }
            return returned_list;
        }

        // Emhyr, Invader of the North: take a random restorable unit out of the discard pile (-1 if none)
        public static int TakeRandomMedicTarget(PlayerState info, Random rnd, CardDatabase db)
        {
            List<int> units = MedicCandidates(info.DiscardList, db);
            if (units.Count == 0)
                return -1;
            int card_id = units[rnd.Next(units.Count)];
            info.DiscardList.Remove(card_id);
            return card_id;
        }

        //----------------------------------------Agile----------------------------------------//
        // Francesca, Hope of the Aen Seidhe, as The Witcher 3 does it: each non-hero agile unit, the ranged row's
        // first, moves to the other row when its own strength would be higher there (not the side's total: a morale
        // booster moving away helps the others, not itself). Returns how many moved.
        public static int OptimizeAgile(PlayerState info, IList<int> weather, bool double_spies, CardDatabase db)
        {
            // Listed first, then moved one by one, each judged on the board as it is by then
            List<KeyValuePair<Row, int>> units = new List<KeyValuePair<Row, int>>();
            foreach (Row row in new[] { Row.Range, Row.Close })
                foreach (int card_id in info.UnitRow(row))
                {
                    Card card = db.Get(card_id);
                    if (card.row == Row.Agile && !card.unique)
                        units.Add(new KeyValuePair<Row, int>(row, card_id));
                }

            int moves = 0;
            foreach (KeyValuePair<Row, int> unit in units)
            {
                Row from = unit.Key, to = from == Row.Close ? Row.Range : Row.Close;
                StrengthCalculator.Recalculate(info, weather, double_spies, db);
                int before = StrengthOf(info, from, unit.Value);
                info.UnitRow(from).Remove(unit.Value);
                info.UnitRow(to).Add(unit.Value);
                StrengthCalculator.Recalculate(info, weather, double_spies, db);
                if (StrengthOf(info, to, unit.Value) > before)
                    moves++;
                else
                {
                    info.UnitRow(to).RemoveAt(info.UnitRow(to).Count - 1);
                    info.UnitRow(from).Add(unit.Value);
                }
            }
            StrengthCalculator.Recalculate(info, weather, double_spies, db);
            return moves;
        }

        // A unit's strength on a row (copies of a card on the same row are equally strong)
        private static int StrengthOf(PlayerState info, Row row, int card_id)
        {
            int index = info.UnitRow(row).LastIndexOf(card_id);
            List<int> strengths = info.ModifiedStrengths(row);
            return index >= 0 && index < strengths.Count ? strengths[index] : 0;
        }

        // Row that gives the side the higher total when an agile unit is added
        public static Row BestAgileRow(PlayerState info, int card_id, IList<int> weather, bool double_spies, CardDatabase db)
        {
            PlayerState close = info.Clone();
            close.CloseList.Add(card_id);
            StrengthCalculator.Recalculate(close, weather, double_spies, db);
            PlayerState range = info.Clone();
            range.RangeList.Add(card_id);
            StrengthCalculator.Recalculate(range, weather, double_spies, db);
            return range.TotalScore() > close.TotalScore() ? Row.Range : Row.Close;
        }

        //----------------------------------------Weather----------------------------------------//
        // Puts a weather card on the shared weather board; duplicates go straight to the owner's discard pile
        public static void PlayWeather(PlayerState owner, PlayerState opponent, List<int> weather_board, Card card)
        {
            if (card.ability == Ability.ClearWeather)
            {
                ClearWeather(owner, opponent, weather_board);
                owner.DiscardList.Add(card._id);
                return;
            }

            if (weather_board.Contains(card._id))
            {
                owner.DiscardList.Add(card._id);
                return;
            }
            weather_board.Add(card._id);
            owner.WeatherPlayed.Add(card._id);
        }

        // Every weather card on the board goes back to the discard pile of whoever played it
        public static void ClearWeather(PlayerState first, PlayerState second, List<int> weather_board)
        {
            foreach (PlayerState info in new[] { first, second })
            {
                info.DiscardList.AddRange(info.WeatherPlayed);
                info.WeatherPlayed.Clear();
            }
            weather_board.Clear();
        }

        //----------------------------------------Commander's Horn----------------------------------------//
        // Leader horn: no physical card, but shown as a horn in the special slot for the round
        public static bool PlaceLeaderHorn(PlayerState info, Row row)
        {
            if (info.HasHorn(row))
                return false;
            info.SpecialRow(row).Add(CardIds.CommanderHorn);
            info.LeaderHornRows.Add(row);
            return true;
        }
    }
}

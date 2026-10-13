using System.Collections.Generic;

namespace Gwent.Rules
{
    // Computes the modified strength of every unit on the board, in The Witcher 3's order:
    // weather -> Eredin's doubled spies -> tight bond -> morale boost -> Dandelion / Commander's Horn.
    // Heroes take no modifier, and neither does a decoy lying on a row.
    public static class StrengthCalculator
    {
        public static void Recalculate(PlayerState info, IList<int> weather, bool double_spies, CardDatabase db)
        {
            foreach (string row in Rows.Units)
            {
                List<Card> cards = db.GetCards(info.UnitRow(row));
                List<int> base_strengths = new List<int>(cards.Count);
                foreach (Card card in cards)
                    base_strengths.Add(card.strength);

                List<int> modified = ComputeRow(cards, IsWeathered(row, weather), info.HasHorn(row), double_spies);
                info.SetStrengths(row, base_strengths, modified);
            }
        }

        public static bool IsWeathered(string row, IList<int> weather)
        {
            return weather != null && weather.Contains(CardIds.WeatherFor(row));
        }

        public static List<int> ComputeRow(IList<Card> cards, bool weathered, bool has_horn, bool double_spies)
        {
            int count = cards.Count;
            List<int> strengths = new List<int>(count);
            foreach (Card card in cards)
                strengths.Add(card.strength);

            // Weather: non-hero units drop to 1
            if (weathered)
                for (int i = 0; i < count; i++)
                    if (!cards[i].unique && strengths[i] > 1)
                        strengths[i] = 1;

            // Eredin, The Treacherous: spies are doubled (both players)
            if (double_spies)
                for (int i = 0; i < count; i++)
                    if (cards[i].ability == Abilities.Spy && !cards[i].unique)
                        strengths[i] *= 2;

            // Tight bond: multiplied by the number of identical bonded cards in the row
            for (int i = 0; i < count; i++)
            {
                if (cards[i].ability != Abilities.TightBond)
                    continue;
                int bonds = 0;
                for (int j = 0; j < count; j++)
                    if (cards[j].ability == Abilities.TightBond && cards[j]._id == cards[i]._id)
                        bonds++;
                strengths[i] *= bonds;
            }

            // Morale boost: +1 to every other non-hero unit, per booster
            for (int b = 0; b < count; b++)
            {
                if (cards[b].ability != Abilities.MoraleBoost)
                    continue;
                for (int i = 0; i < count; i++)
                    if (i != b && Modifiable(cards[i]))
                        strengths[i] += 1;
            }

            // Dandelion (unit horn): doubles the other non-hero units unless a real horn is already there.
            // Doublers only double each other when there are several of them.
            if (!has_horn)
            {
                int doublers = 0;
                foreach (Card card in cards)
                    if (card.ability == Abilities.UnitHorn)
                        doublers++;

                if (doublers > 0)
                    for (int i = 0; i < count; i++)
                    {
                        if (cards[i].ability == Abilities.UnitHorn)
                        {
                            if (doublers > 1)
                                strengths[i] *= 2;
                        }
                        else if (Modifiable(cards[i]))
                            strengths[i] *= 2;
                    }
            }

            // Commander's Horn: doubles every non-hero unit of the row
            if (has_horn)
                for (int i = 0; i < count; i++)
                    if (Modifiable(cards[i]))
                        strengths[i] *= 2;

            return strengths;
        }

        private static bool Modifiable(Card card)
        {
            return !card.unique && !card.IsSpecial;
        }
    }
}

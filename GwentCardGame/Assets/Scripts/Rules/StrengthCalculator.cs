using System;
using System.Collections.Generic;

namespace Gwent.Rules
{
    // Computes the modified strength of every unit on the board, in The Witcher 3's order:
    // weather -> Eredin's doubled spies -> tight bond -> morale boost -> Dandelion / Commander's Horn.
    // Heroes take no modifier, and neither does a decoy lying on a row. Under King Bran, bad weather only halves a side's
    // units instead of making them 1.
    public static class StrengthCalculator
    {
        // The strength lists are filled in place (the AI recalculates thousands of times a move)
        public static void Recalculate(PlayerState info, IList<int> weather, bool double_spies, CardDatabase db)
        {
            List<Card> cards = cardBuffer ?? (cardBuffer = new List<Card>());
            foreach (Row row in Rows.Units)
            {
                cards.Clear();
                foreach (int card_id in info.UnitRow(row))
                    cards.Add(db.Get(card_id));
                List<int> base_strengths = info.BaseStrengths(row);
                List<int> modified = info.ModifiedStrengths(row);
                if (base_strengths == null || modified == null)
                {
                    base_strengths = new List<int>();
                    modified = new List<int>();
                    info.SetStrengths(row, base_strengths, modified);
                }
                base_strengths.Clear();
                foreach (Card card in cards)
                    base_strengths.Add(card.strength);
                ComputeRow(cards, IsWeathered(row, weather), info.HasHorn(row), double_spies, modified, HalvesWeather(info));
            }
            cards.Clear();
        }

        [ThreadStatic] private static List<Card> cardBuffer; // (the Hard AI thinks on another thread)

        public static bool IsWeathered(Row row, IList<int> weather)
        {
            if (weather == null)
                return false;
            for (int i = 0; i < weather.Count; i++)
                if (CardIds.Weathers(weather[i], row))
                    return true;
            return false;
        }

        // King Bran's passive (unless the White Flame cancels it)
        public static bool HalvesWeather(PlayerState info)
        {
            return info.Leader == LeaderIds.KingBran && !info.leaderDisabled;
        }

        public static List<int> ComputeRow(IList<Card> cards, bool weathered, bool has_horn, bool double_spies, bool half_weather = false)
        {
            return ComputeRow(cards, weathered, has_horn, double_spies, new List<int>(cards.Count), half_weather);
        }

        // Into the given list
        private static List<int> ComputeRow(IList<Card> cards, bool weathered, bool has_horn, bool double_spies, List<int> strengths, bool half_weather)
        {
            int count = cards.Count;
            strengths.Clear();
            for (int i = 0; i < count; i++)
                strengths.Add(cards[i].strength);

            // Weather: non-hero units drop to 1 (under King Bran, to half their strength, rounded down, at least 1)
            if (weathered)
                for (int i = 0; i < count; i++)
                    if (!cards[i].unique && strengths[i] > 1)
                        strengths[i] = half_weather ? Math.Max(1, strengths[i] / 2) : 1;

            // Eredin, The Treacherous: spies are doubled (both players)
            if (double_spies)
                for (int i = 0; i < count; i++)
                    if (cards[i].ability == Ability.Spy && !cards[i].unique)
                        strengths[i] *= 2;

            // Tight bond: multiplied by the number of identical bonded cards in the row
            for (int i = 0; i < count; i++)
            {
                if (cards[i].ability != Ability.TightBond)
                    continue;
                int bonds = 0;
                for (int j = 0; j < count; j++)
                    if (cards[j].ability == Ability.TightBond && cards[j]._id == cards[i]._id)
                        bonds++;
                strengths[i] *= bonds;
            }

            // Morale boost: +1 to every other non-hero unit, per booster
            for (int b = 0; b < count; b++)
            {
                if (cards[b].ability != Ability.MoraleBoost)
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
                for (int i = 0; i < count; i++)
                    if (cards[i].ability == Ability.UnitHorn)
                        doublers++;

                if (doublers > 0)
                    for (int i = 0; i < count; i++)
                    {
                        if (cards[i].ability == Ability.UnitHorn)
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

using System.Collections.Generic;

namespace Gwent.Rules
{
    // The Witcher 3's order for a hand and for the cards on a row: special cards first (by card id), then units by
    // printed strength, then by card id
    public static class CardOrder
    {
        public static List<int> Sorted(IEnumerable<int> card_ids, CardDatabase db)
        {
            List<int> sorted = new List<int>(card_ids);
            sorted.Sort((a, b) => Compare(db.Get(a), db.Get(b)));
            return sorted;
        }

        public static int Compare(Card a, Card b)
        {
            if (a._id == b._id)
                return 0;
            if (!a.IsUnit && !b.IsUnit)
                return a._id - b._id;
            if (!a.IsUnit)
                return -1;
            if (!b.IsUnit)
                return 1;
            return a.strength != b.strength ? a.strength - b.strength : a._id - b._id;
        }
    }
}

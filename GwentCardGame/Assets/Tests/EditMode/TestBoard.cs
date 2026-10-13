using System.Collections.Generic;
using System.Linq;
using Gwent.Rules;

namespace Gwent.Rules.Tests
{
    // Small helpers to build board positions in tests
    public static class TestBoard
    {
        public static CardDatabase Db { get { return TestCardData.Db; } }

        public static PlayerState Player(string faction = Factions.NorthernRealms, int leader = LeaderIds.FoltestKingOfTemeria)
        {
            return new PlayerState { Faction = faction, Leader = leader };
        }

        public static void Recalc(List<int> weather, params PlayerState[] players)
        {
            foreach (PlayerState player in players)
                StrengthCalculator.Recalculate(player, weather ?? new List<int>(), false, Db);
        }

        public static List<int> Strengths(params int[] card_ids)
        {
            return card_ids.Select(id => Db.Get(id).strength).ToList();
        }

        public static int CountAll(params PlayerState[] players)
        {
            return players.Sum(p => p.AllCards().Count());
        }
    }
}

using System.Collections.Generic;

namespace Gwent.Rules
{
    // A deck: its faction, name, leader and cards (read from and written to its file through DeckFile)
    //  Deck deck = new Deck { Faction = Faction.NorthernRealms, Name = "My Best Deck", Leader = 51, Cards = { 1, 2, 3 } };
    public class Deck
    {
        public Faction Faction;
        public string Name = null;
        public int Leader = 0;
        public List<int> Cards = new List<int>();
    }
}

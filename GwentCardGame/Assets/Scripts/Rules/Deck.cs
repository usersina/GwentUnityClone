using System;
using System.Collections.Generic;

namespace Gwent.Rules
{
    // Deck Structure (one json file per deck)
    //  Deck deck = new Deck { Faction = "NR", Name = "My Best Deck", Leader = 51, Cards = { 1, 2, 3 } };
    [Serializable]
    public class Deck
    {
        public string Faction = null;
        public string Name = null;
        public int Leader = 0;
        public List<int> Cards = new List<int>();
    }
}

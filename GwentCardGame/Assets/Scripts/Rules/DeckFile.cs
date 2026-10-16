using System;
using System.Collections.Generic;

namespace Gwent.Rules
{
    // A deck as its file writes it (one JSON file per deck): the faction by its code ("NR")
    [Serializable]
    public class DeckFile
    {
        public string Faction = null;
        public string Name = null;
        public int Leader = 0;
        public List<int> Cards = new List<int>();

        public static DeckFile Of(Deck deck)
        {
            return new DeckFile { Faction = deck.Faction.Code(), Name = deck.Name, Leader = deck.Leader, Cards = new List<int>(deck.Cards) };
        }

        // The deck; null when its faction is no faction's code (a damaged file)
        public Deck ToDeck()
        {
            Faction faction;
            if (!Factions.TryParse(Faction, out faction))
                return null;
            return new Deck { Faction = faction, Name = Name, Leader = Leader, Cards = Cards ?? new List<int>() };
        }
    }
}

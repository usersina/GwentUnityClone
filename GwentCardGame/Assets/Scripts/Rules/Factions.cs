namespace Gwent.Rules
{
    public static class Factions
    {
        public static readonly Faction[] Playable = { Faction.NorthernRealms, Faction.Nilfgaard, Faction.Scoiatael, Faction.Monsters, Faction.Skellige };

        // Its code in the card database and the deck files, which also names the deck folders and the factions' art
        public static string Code(this Faction faction)
        {
            switch (faction)
            {
                case Faction.NorthernRealms: return "NR";
                case Faction.Nilfgaard: return "NF";
                case Faction.Scoiatael: return "SC";
                case Faction.Monsters: return "M";
                case Faction.Skellige: return "SK";
                case Faction.Neutral: return "N";
                default: return "Special";
            }
        }

        // The faction of a code (false for a code no faction has)
        public static bool TryParse(string code, out Faction faction)
        {
            foreach (Faction f in new[] { Faction.NorthernRealms, Faction.Nilfgaard, Faction.Scoiatael, Faction.Monsters, Faction.Skellige, Faction.Neutral, Faction.Special })
                if (f.Code() == code)
                {
                    faction = f;
                    return true;
                }
            faction = Faction.Neutral;
            return false;
        }

        public static string DisplayName(this Faction faction)
        {
            switch (faction)
            {
                case Faction.Monsters: return "Monsters";
                case Faction.Nilfgaard: return "Nilfgaardian Empire";
                case Faction.NorthernRealms: return "Northern Realms";
                case Faction.Scoiatael: return "Scoia'tael";
                case Faction.Skellige: return "Skellige";
                default: return faction.Code();
            }
        }

        // The faction's ability (its perk, always in effect), as the deck builder, the battle's side panels and the rules
        // tell it
        public static string Ability(this Faction faction)
        {
            switch (faction)
            {
                case Faction.NorthernRealms: return "Draw a card from your deck whenever you win a round.";
                case Faction.Nilfgaard: return "Win any round that ends in a draw (unless your opponent is Nilfgaard too).";
                case Faction.Scoiatael: return "Decide who goes first at the start of the battle.";
                case Faction.Monsters: return "Keep one random unit on the board after each round.";
                case Faction.Skellige: return "Two random units of your discard pile (no heroes or spies) return to the board in the third round.";
                default: return null;
            }
        }
    }
}

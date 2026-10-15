namespace Gwent.Rules
{
    public static class Factions
    {
        public static readonly Faction[] Playable = { Faction.NorthernRealms, Faction.Nilfgaard, Faction.Scoiatael, Faction.Monsters };

        // Its code in the card database and the deck files, which also names the deck folders and the factions' art
        public static string Code(this Faction faction)
        {
            switch (faction)
            {
                case Faction.NorthernRealms: return "NR";
                case Faction.Nilfgaard: return "NF";
                case Faction.Scoiatael: return "SC";
                case Faction.Monsters: return "M";
                case Faction.Neutral: return "N";
                default: return "Special";
            }
        }

        // The faction of a code (false for a code no faction has)
        public static bool TryParse(string code, out Faction faction)
        {
            foreach (Faction f in new[] { Faction.NorthernRealms, Faction.Nilfgaard, Faction.Scoiatael, Faction.Monsters, Faction.Neutral, Faction.Special })
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
                default: return faction.Code();
            }
        }
    }
}

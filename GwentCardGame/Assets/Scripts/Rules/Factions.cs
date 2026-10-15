namespace Gwent.Rules
{
    public static class Factions
    {
        public const string NorthernRealms = "NR";
        public const string Nilfgaard = "NF";
        public const string Scoiatael = "SC";
        public const string Monsters = "M";
        public const string Neutral = "N";
        public const string Special = "Special";

        public static readonly string[] Playable = { NorthernRealms, Nilfgaard, Scoiatael, Monsters };

        public static string DisplayName(string faction)
        {
            switch (faction)
            {
                case Monsters: return "Monsters";
                case Nilfgaard: return "Nilfgaardian Empire";
                case NorthernRealms: return "Northern Realms";
                case Scoiatael: return "Scoia'tael";
                default: return faction;
            }
        }
    }
}

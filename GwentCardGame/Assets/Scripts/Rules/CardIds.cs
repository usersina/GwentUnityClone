namespace Gwent.Rules
{
    public static class CardIds
    {
        public const int ArachasBehemoth = 2;
        public const int BovineDefenseForce = 9;
        public const int Cow = 16;
        public const int GaunterODimm = 65;
        public const int Dandelion = 22;
        public const int BitingFrost = 200;
        public const int ClearWeather = 201;
        public const int CommanderHorn = 202;
        public const int Decoy = 203;
        public const int ImpenetrableFog = 204;
        public const int Scorch = 205;
        public const int TorrentialRain = 206;

        // Weather card affecting each unit row
        public static int WeatherFor(string row)
        {
            switch (row)
            {
                case Rows.Close: return BitingFrost;
                case Rows.Range: return ImpenetrableFog;
                case Rows.Siege: return TorrentialRain;
                default: return -1;
            }
        }
    }
}

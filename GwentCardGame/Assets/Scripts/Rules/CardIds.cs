namespace Gwent.Rules
{
    public static class CardIds
    {
        public const int ArachasBehemoth = 2;
        public const int BovineDefenseForce = 9;
        public const int Cow = 16;
        public const int GaunterODimm = 65;
        public const int Dandelion = 22;
        public const int Berserker = 147;
        public const int Cerys = 150;
        public const int ClanDrummondShieldMaiden = 154;
        public const int Hemdall = 161;
        public const int Kambi = 164;
        public const int TransformedVildkaarl = 170;
        public const int TransformedYoungVildkaarl = 171;
        public const int YoungBerserker = 174;
        public const int BitingFrost = 200;
        public const int ClearWeather = 201;
        public const int CommanderHorn = 202;
        public const int Decoy = 203;
        public const int ImpenetrableFog = 204;
        public const int Scorch = 205;
        public const int TorrentialRain = 206;
        public const int Mardroeme = 207;
        public const int SkelligeStorm = 208;

        // Weather card affecting each unit row
        public static int WeatherFor(Row row)
        {
            switch (row)
            {
                case Row.Close: return BitingFrost;
                case Row.Range: return ImpenetrableFog;
                case Row.Siege: return TorrentialRain;
                default: return -1;
            }
        }

        // Whether a weather card lies on a row: its own row's, or Skellige Storm on the ranged and siege rows
        public static bool Weathers(int weather_id, Row row)
        {
            return weather_id == WeatherFor(row) || weather_id == SkelligeStorm && (row == Row.Range || row == Row.Siege);
        }

        // Cards no deck holds, which only a card's ability brings: the Cow's Bovine Defense Force, Kambi's Hemdall and the
        // Berserkers' bears. They leave the game instead of going to a discard pile (no medic or leader brings them back);
        // a bear's Berserker goes there instead.
        public static bool IsSummoned(int card_id)
        {
            return card_id == BovineDefenseForce || card_id == Hemdall || card_id == TransformedVildkaarl || card_id == TransformedYoungVildkaarl;
        }

        // The units an Avenger calls (AbilityRules.AvengerOf)
        public static bool IsAvenger(int card_id)
        {
            return card_id == BovineDefenseForce || card_id == Hemdall;
        }

        // The Berserker a bear was (-1 for any other card)
        public static int BerserkerOf(int card_id)
        {
            switch (card_id)
            {
                case TransformedVildkaarl: return Berserker;
                case TransformedYoungVildkaarl: return YoungBerserker;
                default: return -1;
            }
        }

        // The bear a Berserker turns into on a row with a Mardroeme (-1 for any other card)
        public static int TransformedOf(int card_id)
        {
            switch (card_id)
            {
                case Berserker: return TransformedVildkaarl;
                case YoungBerserker: return TransformedYoungVildkaarl;
                default: return -1;
            }
        }
    }
}

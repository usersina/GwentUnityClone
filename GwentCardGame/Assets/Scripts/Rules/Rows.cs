namespace Gwent.Rules
{
    public static class Rows
    {
        // Unit rows
        public const string Close = "close";
        public const string Range = "range";
        public const string Siege = "siege";
        // Card.row values that are not board rows
        public const string Agile = "close_range";
        public const string Weather = "weather";
        public const string OneTime = "one_time";
        public const string Special = "special";
        public const string LeaderActive = "active";
        public const string LeaderPassive = "passive";

        public static readonly string[] Units = { Close, Range, Siege };

        public static bool IsUnitRow(string row)
        {
            return row == Close || row == Range || row == Siege;
        }
    }
}

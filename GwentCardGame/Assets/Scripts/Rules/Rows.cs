namespace Gwent.Rules
{
    public static class Rows
    {
        public static readonly Row[] Units = { Row.Close, Row.Range, Row.Siege };

        public static bool IsUnitRow(this Row row)
        {
            return row == Row.Close || row == Row.Range || row == Row.Siege;
        }

        // Its code in the card database, which also names the rows' art
        public static string Code(this Row row)
        {
            switch (row)
            {
                case Row.Close: return "close";
                case Row.Range: return "range";
                case Row.Siege: return "siege";
                case Row.Agile: return "close_range";
                case Row.Weather: return "weather";
                case Row.OneTime: return "one_time";
                case Row.Special: return "special";
                case Row.LeaderActive: return "active";
                default: return "passive";
            }
        }

        // The row of a code (false for a code no row has)
        public static bool TryParse(string code, out Row row)
        {
            for (row = Row.Close; row <= Row.LeaderPassive; row++)
                if (row.Code() == code)
                    return true;
            row = Row.Close;
            return false;
        }
    }
}

namespace Gwent.Rules
{
    // Where a card goes: one of the board's three rows, close or ranged for an agile unit, or for the cards that take
    // no row, what kind of card it is
    public enum Row { Close, Range, Siege, Agile, Weather, OneTime, Special, LeaderActive, LeaderPassive }
}

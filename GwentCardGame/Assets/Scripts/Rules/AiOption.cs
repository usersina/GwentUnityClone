namespace Gwent.Rules
{
    // A move the AI weighed, for the log of its decision: the Normal AI's estimate of it, and with Hard, the share of the
    // battles it won when played out (-1 when it was not)
    public struct AiOption
    {
        public AiAction Action;
        public int Gain;        // change of (own total - opponent's total), plus card-advantage bonuses
        public int Board;       // the change of the totals alone
        public int Cost;        // how much it would rather keep the card
        public double WinRate;  // Hard: battles won (a draw counts half) / battles played out after the move
        public int Battles;     // Hard: battles played out after the move
    }
}

namespace Gwent.Rules
{
    public static class Abilities
    {
        // Its code in the card database ("" for none), which also names the abilities' art
        public static string Code(this Ability ability)
        {
            switch (ability)
            {
                case Ability.Leader: return "leader";
                case Ability.Spy: return "spy";
                case Ability.Medic: return "medic";
                case Ability.Muster: return "muster";
                case Ability.MoraleBoost: return "morale_boost";
                case Ability.TightBond: return "tight_bond";
                case Ability.UnitHorn: return "commander_horn";
                case Ability.CommanderHorn: return "double_strength";
                case Ability.ClearWeather: return "clear_weather";
                case Ability.ReduceClose: return "reduce_close";
                case Ability.ReduceRange: return "reduce_range";
                case Ability.ReduceSiege: return "reduce_siege";
                case Ability.Decoy: return "swap_card";
                case Ability.Scorch: return "kill_strongest";
                case Ability.ScorchClose: return "scorch_close";
                case Ability.ScorchRange: return "scorch_range";
                case Ability.ScorchSiege: return "scorch_siege";
                default: return "";
            }
        }

        // The ability of a code: none for null or "" (false for a code no ability has)
        public static bool TryParse(string code, out Ability ability)
        {
            for (ability = Ability.None; ability <= Ability.ScorchSiege; ability++)
                if (ability.Code() == (code ?? ""))
                    return true;
            ability = Ability.None;
            return false;
        }

        // Scorch on one of the enemy's rows: destroys its strongest unit(s) there
        public static bool IsRowScorch(this Ability ability)
        {
            return ability == Ability.ScorchClose || ability == Ability.ScorchRange || ability == Ability.ScorchSiege;
        }

        // The row a row scorch burns (null for any other ability)
        public static Row? RowScorchTarget(this Ability ability)
        {
            switch (ability)
            {
                case Ability.ScorchClose: return Row.Close;
                case Ability.ScorchRange: return Row.Range;
                case Ability.ScorchSiege: return Row.Siege;
                default: return null;
            }
        }
    }
}

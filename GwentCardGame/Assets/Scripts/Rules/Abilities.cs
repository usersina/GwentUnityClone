using System;

namespace Gwent.Rules
{
    public static class Abilities
    {
        public const string Leader = "leader";
        public const string Spy = "spy";
        public const string Medic = "medic";
        public const string Muster = "muster";
        public const string MoraleBoost = "morale_boost";
        public const string TightBond = "tight_bond";
        public const string UnitHorn = "commander_horn";    // Dandelion: horn on his row
        public const string CommanderHorn = "double_strength";
        public const string ClearWeather = "clear_weather";
        public const string ReduceClose = "reduce_close";
        public const string ReduceRange = "reduce_range";
        public const string ReduceSiege = "reduce_siege";
        public const string Decoy = "swap_card";
        public const string Scorch = "kill_strongest";

        // "scorch_close", "scorch_range", "scorch_siege": destroy the enemy's strongest unit(s) on that row
        public static bool IsRowScorch(string ability)
        {
            return !string.IsNullOrEmpty(ability) && ability.StartsWith("scorch_", StringComparison.Ordinal);
        }

        public static string RowScorchTarget(string ability)
        {
            return IsRowScorch(ability) ? ability.Substring("scorch_".Length) : null;
        }
    }
}

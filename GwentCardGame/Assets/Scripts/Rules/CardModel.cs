using System;
using System.Collections.Generic;

// Pure game data and rules: no UnityEngine dependency so it can be unit tested in isolation.
namespace Gwent.Rules
{
    // Card structure (one entry of Resources/Cards/cards_db.json)
    [Serializable]
    public class Card
    {
        public string _idstr = null;
        public string name = null;
        public int _id = -1;
        public string faction = null;
        public bool unique = false;
        public int strength = 0;
        public string row = null;
        public string ability = null;

        public bool IsSpecial { get { return faction == Factions.Special; } }
        public bool IsLeader { get { return ability == Abilities.Leader; } }
        public bool IsUnit { get { return !IsSpecial && !IsLeader; } }
        public bool HasAbility(string ability_name) { return ability == ability_name; }
    }

    // List of above Card (root object of cards_db.json)
    [Serializable]
    public class CardsDB
    {
        public List<Card> Cards = new List<Card>();
    }

    // Deck Structure (one json file per deck)
    //  Deck deck = new Deck { Faction = "NR", Name = "My Best Deck", Leader = 51, Cards = { 1, 2, 3 } };
    [Serializable]
    public class Deck
    {
        public string Faction = null;
        public string Name = null;
        public int Leader = 0;
        public List<int> Cards = new List<int>();
    }

    public static class Factions
    {
        public const string NorthernRealms = "NR";
        public const string Nilfgaard = "NF";
        public const string Scoiatael = "SC";
        public const string Monsters = "M";
        public const string Neutral = "N";
        public const string Special = "Special";

        public static readonly string[] Playable = { NorthernRealms, Nilfgaard, Scoiatael, Monsters };
    }

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

    public static class CardIds
    {
        public const int BovineDefenseForce = 9;
        public const int Cow = 16;
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

    // Leader ids, named after the card (see Resources/Cards/EffectBox/Leader/<id> for the in-game text)
    public static class LeaderIds
    {
        public const int EmhyrEmperorOfNilfgaard = 33;      // Look at 3 random cards from the opponent's hand
        public const int EmhyrHisImperialMajesty = 34;      // Play a Torrential Rain from your deck
        public const int EmhyrInvaderOfTheNorth = 35;       // Passive: medics restore a random unit (both players)
        public const int EmhyrTheRelentless = 36;           // Draw a card from the opponent's discard pile
        public const int EmhyrTheWhiteFlame = 37;           // Passive: cancel the opponent's leader ability
        public const int EredinTheTreacherous = 40;         // Double the strength of all spies (both players)
        public const int EredinBringerOfDeath = 41;         // Discard 2 cards, draw 1 of your choice from your deck
        public const int EredinCommanderOfTheRedRiders = 42; // Play any weather card from your deck
        public const int EredinDestroyerOfWorlds = 43;      // Restore a card from your discard pile to your hand
        public const int EredinKingOfTheWildHunt = 44;      // Commander's Horn on your close row
        public const int FoltestKingOfTemeria = 51;         // Play an Impenetrable Fog from your deck
        public const int FoltestLordCommander = 52;         // Clear all weather
        public const int FoltestSonOfMedell = 53;           // Scorch the enemy range row (if it totals 10+)
        public const int FoltestTheSiegemaster = 54;        // Commander's Horn on your siege row
        public const int FoltestTheSteelForged = 55;        // Scorch the enemy siege row (if it totals 10+)
        public const int FrancescaDaisyOfTheValley = 57;    // Passive: draw an extra card at the start of the battle
        public const int FrancescaHopeOfTheAenSeidhe = 58;  // Move agile units to their best row
        public const int FrancescaPurebloodElf = 59;        // Play a Biting Frost from your deck
        public const int FrancescaQueenOfDolBlathanna = 60; // Scorch the enemy close row (if it totals 10+)
        public const int FrancescaTheBeautiful = 61;        // Commander's Horn on your range row

        public static readonly Dictionary<string, int[]> ByFaction = new Dictionary<string, int[]>
        {
            { Factions.NorthernRealms, new[] { 51, 52, 53, 54, 55 } },
            { Factions.Nilfgaard, new[] { 33, 34, 35, 36, 37 } },
            { Factions.Scoiatael, new[] { 57, 58, 59, 60, 61 } },
            { Factions.Monsters, new[] { 40, 41, 42, 43, 44 } },
        };

        public static int DefaultFor(string faction)
        {
            int[] leaders;
            return ByFaction.TryGetValue(faction ?? "", out leaders) ? leaders[0] : FoltestKingOfTemeria;
        }

        public static bool BelongsTo(int leader_id, string faction)
        {
            int[] leaders;
            return ByFaction.TryGetValue(faction ?? "", out leaders) && Array.IndexOf(leaders, leader_id) >= 0;
        }

        // Leaders whose ability plays a weather card from the deck, and which weather row they look for
        public static string WeatherPickRow(int leader_id)
        {
            switch (leader_id)
            {
                case EredinCommanderOfTheRedRiders: return "all";
                case EmhyrHisImperialMajesty: return Rows.Siege;
                case FoltestKingOfTemeria: return Rows.Range;
                case FrancescaPurebloodElf: return Rows.Close;
                default: return null;
            }
        }

        // Leaders whose ability is a Commander's Horn on a fixed row
        public static string HornRow(int leader_id)
        {
            switch (leader_id)
            {
                case EredinKingOfTheWildHunt: return Rows.Close;
                case FoltestTheSiegemaster: return Rows.Siege;
                case FrancescaTheBeautiful: return Rows.Range;
                default: return null;
            }
        }

        // Leaders whose ability scorches a fixed enemy row
        public static string ScorchRow(int leader_id)
        {
            switch (leader_id)
            {
                case FoltestSonOfMedell: return Rows.Range;
                case FoltestTheSteelForged: return Rows.Siege;
                case FrancescaQueenOfDolBlathanna: return Rows.Close;
                default: return null;
            }
        }
    }
}

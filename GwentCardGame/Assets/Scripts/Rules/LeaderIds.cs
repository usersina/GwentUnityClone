using System;
using System.Collections.Generic;

namespace Gwent.Rules
{
    // Leader ids, named after the card (see Resources/Cards/EffectBox/Leader/<id> for the in-game text)
    public static class LeaderIds
    {
        public const int EmhyrEmperorOfNilfgaard = 33;      // Look at 3 random cards from the opponent's hand
        public const int EmhyrHisImperialMajesty = 34;      // Play a Torrential Rain from your deck
        public const int EmhyrInvaderOfTheNorth = 35;       // Passive: medics restore a random unit (both players)
        public const int EmhyrTheRelentless = 36;           // Draw a card from the opponent's discard pile
        public const int EmhyrTheWhiteFlame = 37;           // Passive: cancel the opponent's leader ability
        public const int EredinTheTreacherous = 40;         // Passive: spies are doubled (both players)
        public const int EredinBringerOfDeath = 41;         // Restore a card from your discard pile to your hand
        public const int EredinCommanderOfTheRedRiders = 42; // Commander's Horn on your close row
        public const int EredinDestroyerOfWorlds = 43;      // Discard 2 cards, draw 1 of your choice from your deck
        public const int EredinKingOfTheWildHunt = 44;      // Play any weather card from your deck (Clear Weather too)
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

        public static readonly Dictionary<Faction, int[]> ByFaction = new Dictionary<Faction, int[]>
        {
            { Faction.NorthernRealms, new[] { 51, 52, 53, 54, 55 } },
            { Faction.Nilfgaard, new[] { 33, 34, 35, 36, 37 } },
            { Faction.Scoiatael, new[] { 57, 58, 59, 60, 61 } },
            { Faction.Monsters, new[] { 40, 41, 42, 43, 44 } },
        };

        public static int DefaultFor(Faction faction)
        {
            int[] leaders;
            return ByFaction.TryGetValue(faction, out leaders) ? leaders[0] : FoltestKingOfTemeria;
        }

        public static bool BelongsTo(int leader_id, Faction faction)
        {
            int[] leaders;
            return ByFaction.TryGetValue(faction, out leaders) && Array.IndexOf(leaders, leader_id) >= 0;
        }

        // Leaders whose ability plays a weather card from the deck, and which weather row they look for
        public static Row? WeatherPickRow(int leader_id)
        {
            switch (leader_id)
            {
                case EredinKingOfTheWildHunt: return Row.Weather; // any weather card
                case EmhyrHisImperialMajesty: return Row.Siege;
                case FoltestKingOfTemeria: return Row.Range;
                case FrancescaPurebloodElf: return Row.Close;
                default: return null;
            }
        }

        // Leaders whose ability is a Commander's Horn on a fixed row
        public static Row? HornRow(int leader_id)
        {
            switch (leader_id)
            {
                case EredinCommanderOfTheRedRiders: return Row.Close;
                case FoltestTheSiegemaster: return Row.Siege;
                case FrancescaTheBeautiful: return Row.Range;
                default: return null;
            }
        }

        // Leaders whose ability scorches a fixed enemy row
        public static Row? ScorchRow(int leader_id)
        {
            switch (leader_id)
            {
                case FoltestSonOfMedell: return Row.Range;
                case FoltestTheSteelForged: return Row.Siege;
                case FrancescaQueenOfDolBlathanna: return Row.Close;
                default: return null;
            }
        }
    }
}

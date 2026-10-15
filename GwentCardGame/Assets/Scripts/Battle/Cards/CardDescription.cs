using System.Collections.Generic;
using Gwent.Rules;

namespace Gwent.Battle
{
    // What a card's description box says (DescriptionBox shows it), as The Witcher 3's: the ability's name and icon, then
    // what it does. A special card's box is the card's own; a leader's is its "Leader Ability", a hero's says what a hero
    // is (neither has an icon); a plain unit has none.
    public class CardDescription
    {
        public readonly string Title;
        public readonly string Text;
        public readonly string Icon; // under Resources/Cards/DescriptionIcon, or null

        // A leader's ability, as its description box and How to play say it (null for an id that is no leader)
        public static string LeaderAbility(int leader_id)
        {
            string text;
            return Leaders.TryGetValue(leader_id, out text) ? text : null;
        }

        private CardDescription(string title, string text, string icon = null)
        {
            Title = title;
            Text = text;
            Icon = icon;
        }

        // null for a plain unit
        public static CardDescription For(Card card)
        {
            CardDescription description;
            if (card.faction == Faction.Special)
                return Specials.TryGetValue(card._idstr, out description) ? description : null;
            if (card.ability == Ability.Leader)
                return Leaders.TryGetValue(card._id, out string text) ? new CardDescription("Leader Ability", text) : null;
            if (card.ability != Ability.None)
                return Abilities.TryGetValue(card.ability, out description) ? description : null;
            int avenger = AbilityRules.AvengerOf(card._id);
            if (avenger != -1)
                return Avenger(avenger == CardIds.Hemdall ? "Hemdall" : "the Bovine Defense Force");
            if (card.unique)
                return Hero;
            if (card.row == Row.Agile)
                return Agile;
            return null;
        }

        private static readonly CardDescription Hero = new CardDescription("Hero",
            "Not affected by any special cards or abilities.");

        private static readonly CardDescription Agile = new CardDescription("Agile",
            "Can be placed in either the Close Combat or the Ranged Combat row.", "agile"); // Francesca, Hope of the Aen Seidhe moves it later

        // The Cow's and Kambi's
        private static CardDescription Avenger(string avenger)
        {
            return new CardDescription("Avenger", "Killed by a scorch, it calls " + avenger + " to your Close Combat row at once. "
                + "Discarded any other way, " + (avenger == "Hemdall" ? "he" : "it") + " comes at the start of the next round.", "avenger");
        }

        private static readonly CardDescription Mardroeme = new CardDescription("Mardroeme",
            "Triggers transformation of all Berserker cards on the same row.", "mardroeme");

        private static readonly CardDescription CommanderHorn = new CardDescription("Commander's Horn",
            "Doubles the strength of all unit cards in that row. Limited to 1 per row.", "commander_horn");

        // Dandelion: his horn doubles the others in his row (not himself), and a Commander's Horn may still join it
        private static readonly CardDescription UnitHorn = new CardDescription("Commander's Horn",
            "Doubles the strength of the other units in its row.", "commander_horn");

        // Special cards, by their id string
        private static readonly Dictionary<string, CardDescription> Specials = new Dictionary<string, CardDescription>
        {
            { "biting_frost", new CardDescription("Biting Frost", "Sets the strength of all Close Combat cards to 1 for both players.", "biting_frost") },
            { "impenetrable_fog", new CardDescription("Impenetrable Fog", "Sets the strength of all Ranged Combat cards to 1 for both players.", "impenetrable_fog") },
            { "torrential_rain", new CardDescription("Torrential Rain", "Sets the strength of all Siege Combat cards to 1 for both players.", "torrential_rain") },
            { "skellige_storm", new CardDescription("Skellige Storm", "Sets the strength of all Ranged and Siege Combat cards to 1 for both players.", "skellige_storm") },
            { "clear_weather", new CardDescription("Clear Weather", "Removes all weather effects (Biting Frost, Impenetrable Fog, Torrential Rain and Skellige Storm).", "clear_weather") },
            { "mardroeme", Mardroeme }, // in a row's horn slot
            { "commander_horn", CommanderHorn },
            { "decoy", new CardDescription("Decoy", "Swap with a card on the battlefield to return it to your hand.", "decoy") },
            { "scorch", new CardDescription("Scorch", "Discard after playing. Kills the strongest card(s) on the battlefield.", "scorch") },
        };

        // Units' abilities
        private static readonly Dictionary<Ability, CardDescription> Abilities = new Dictionary<Ability, CardDescription>
        {
            { Ability.Spy, new CardDescription("Spy", "Place on your opponent's battlefield (counts towards your opponent's total) and draw 2 cards from your deck.", "spy") },
            { Ability.Medic, new CardDescription("Medic", "Choose one card from your discard pile and play it instantly (no heroes or special cards).", "medic") },
            { Ability.Muster, new CardDescription("Muster", "Find any cards with the same name in your deck and hand, and play them instantly.", "muster") },
            { Ability.MoraleBoost, new CardDescription("Morale Boost", "Adds +1 to all units in the row (excluding itself).", "morale_boost") },
            { Ability.TightBond, new CardDescription("Tight Bond", "Place next to a card with the same name to double the strength of both cards.", "tight_bond") },
            { Ability.UnitHorn, UnitHorn },
            { Ability.ScorchClose, RowScorch("Close Combat") },
            { Ability.ScorchRange, RowScorch("Ranged Combat") },
            { Ability.ScorchSiege, RowScorch("Siege") },
            { Ability.Scorch, new CardDescription("Scorch", "Kills the strongest card(s) on the battlefield as it is played (never itself).", "scorch") }, // the Clan Dimun Pirate
            { Ability.Berserker, new CardDescription("Berserker", "Transforms into a bear when a Mardroeme card is on its row.", "berserker") },
            { Ability.Mardroeme, Mardroeme }, // Ermion
        };

        private static CardDescription RowScorch(string row)
        {
            return new CardDescription("Scorch", "Destroys your enemy's strongest " + row + " unit(s) if the combined strength of all their "
                + row + " units is 10 or more.", "scorch");
        }

        // Leaders' abilities, by card id (LeaderIds)
        private static readonly Dictionary<int, string> Leaders = new Dictionary<int, string>
        {
            { LeaderIds.EmhyrEmperorOfNilfgaard, "Look at 3 random cards from your opponent's hand." },
            { LeaderIds.EmhyrHisImperialMajesty, "Pick a Torrential Rain card from your deck and play it instantly." },
            { LeaderIds.EmhyrInvaderOfTheNorth, "Medics and leader abilities that restore a unit pick a random one (a hero medic still chooses). Affects both players." },
            { LeaderIds.EmhyrTheRelentless, "Draw a unit (not a hero) from your opponent's discard pile." },
            { LeaderIds.EmhyrTheWhiteFlame, "Cancels your opponent's leader ability." },
            { LeaderIds.EredinTheTreacherous, "Doubles the strength of all spy cards. Affects both players." },
            { LeaderIds.EredinBringerOfDeath, "Restore a unit (not a hero) from your discard pile to your hand." },
            { LeaderIds.EredinCommanderOfTheRedRiders, "Doubles the strength of all your Close Combat units (unless a Commander's Horn is also on that row)." },
            { LeaderIds.EredinDestroyerOfWorlds, "Discard 2 cards and draw 1 card of your choice from your deck." },
            { LeaderIds.EredinKingOfTheWildHunt, "Pick any weather card from your deck and play it instantly." },
            { LeaderIds.FoltestKingOfTemeria, "Pick an Impenetrable Fog card from your deck and play it instantly." },
            { LeaderIds.FoltestLordCommander, "Clears all weather effects (Biting Frost, Impenetrable Fog, Torrential Rain and Skellige Storm)." },
            { LeaderIds.FoltestSonOfMedell, "Destroys your enemy's strongest Ranged Combat unit(s) if the combined strength of all their Ranged Combat units is 10 or more." },
            { LeaderIds.FoltestTheSiegemaster, "Doubles the strength of all your Siege units (unless a Commander's Horn is also on that row)." },
            { LeaderIds.FoltestTheSteelForged, "Destroys your enemy's strongest Siege unit(s) if the combined strength of all their Siege units is 10 or more." },
            { LeaderIds.FrancescaDaisyOfTheValley, "Draw an extra card at the beginning of the battle." },
            { LeaderIds.FrancescaHopeOfTheAenSeidhe, "Moves agile units to whichever valid row maximizes their strength (units already on their best row stay)." },
            { LeaderIds.FrancescaPurebloodElf, "Pick a Biting Frost card from your deck and play it instantly." },
            { LeaderIds.FrancescaQueenOfDolBlathanna, "Destroys your enemy's strongest Close Combat unit(s) if the combined strength of all their Close Combat units is 10 or more." },
            { LeaderIds.FrancescaTheBeautiful, "Doubles the strength of all your Ranged Combat units (unless a Commander's Horn is also on that row)." },
            { LeaderIds.CrachAnCraite, "Shuffles all cards from each player's discard pile back into their decks." },
            { LeaderIds.KingBran, "Your units only lose half their strength in bad weather." },
        };
    }
}

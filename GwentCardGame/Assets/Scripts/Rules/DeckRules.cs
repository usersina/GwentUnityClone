using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gwent.Rules
{
    public static class DeckRules
    {
        public const int MinUnits = 22;
        public const int MaxSpecials = 10;
        public const int MaxCopies = 3;
        public const int MaxNameLength = 30;
        public const string EmptyDeckName = "__emptyname__";

        //----------------------------------------Validation----------------------------------------//
        public static List<string> Validate(Deck deck, CardDatabase db)
        {
            List<string> errors = new List<string>();
            if (deck == null)
            {
                errors.Add("Deck could not be read");
                return errors;
            }
            if (Array.IndexOf(Factions.Playable, deck.Faction) < 0)
                errors.Add("Unknown faction '" + deck.Faction + "'");
            if (!LeaderIds.BelongsTo(deck.Leader, deck.Faction))
                errors.Add("Leader does not belong to the deck's faction");

            int units = 0;
            int specials = 0;
            foreach (IGrouping<int, int> group in (deck.Cards ?? new List<int>()).GroupBy(x => x))
            {
                Card card;
                if (!db.TryGet(group.Key, out card) || card.IsLeader)
                {
                    errors.Add("Unknown card id " + group.Key);
                    continue;
                }
                int copies = group.Count();
                if (card.IsSpecial)
                    specials += copies;
                else
                    units += copies;
                if (!card.IsSpecial && card.faction != Factions.Neutral && card.faction != deck.Faction)
                    errors.Add(card.name + " does not belong to this faction");
                if (card.unique && copies > 1)
                    errors.Add("Hero " + card.name + " can only be added once");
                else if (copies > MaxCopies)
                    errors.Add("At most " + MaxCopies + " copies of " + card.name);
            }
            if (units < MinUnits)
                errors.Add("Needs at least " + MinUnits + " unit cards (has " + units + ")");
            if (specials > MaxSpecials)
                errors.Add("At most " + MaxSpecials + " special cards (has " + specials + ")");
            return errors;
        }

        public static bool IsValid(Deck deck, CardDatabase db)
        {
            return Validate(deck, db).Count == 0;
        }

        // Drops unknown card ids and fixes an invalid leader so a hand-edited deck can still be loaded
        public static Deck Sanitize(Deck deck, CardDatabase db)
        {
            if (deck == null)
                return null;
            deck.Cards = (deck.Cards ?? new List<int>()).Where(id => db.Contains(id) && !db.Get(id).IsLeader).ToList();
            if (Array.IndexOf(Factions.Playable, deck.Faction) >= 0 && !LeaderIds.BelongsTo(deck.Leader, deck.Faction))
                deck.Leader = LeaderIds.DefaultFor(deck.Faction);
            return deck;
        }

        //----------------------------------------Deck names----------------------------------------//
        // "  My   Deck " => "My Deck" (control characters removed, length capped)
        public static string NormalizeDisplayName(string text)
        {
            if (text == null)
                return "";
            StringBuilder builder = new StringBuilder();
            foreach (char c in text)
                if (!char.IsControl(c))
                    builder.Append(c);
            string[] words = builder.ToString().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            string name = string.Join(" ", words);
            return name.Length > MaxNameLength ? name.Substring(0, MaxNameLength).TrimEnd() : name;
        }

        // Display name => safe file name without extension ("My Deck!" => "my_deck"), null if nothing usable remains
        public static string ToFileName(string display_name)
        {
            string name = NormalizeDisplayName(display_name).ToLowerInvariant();
            StringBuilder builder = new StringBuilder();
            foreach (char c in name)
            {
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-')
                    builder.Append(c);
                else if (c == ' ' || c == '_')
                    builder.Append('_');
            }
            // "__empty__" / "__emptyname__" are reserved for the built-in empty deck
            if (builder.ToString().StartsWith("__", StringComparison.Ordinal))
                return null;
            string file_name = builder.ToString().Trim('_');
            return file_name.Length == 0 ? null : file_name;
        }

        // Two decks clash when they would share a file or only differ by letter case
        public static bool NamesCollide(string first, string second)
        {
            if (string.Equals(NormalizeDisplayName(first), NormalizeDisplayName(second), StringComparison.OrdinalIgnoreCase))
                return true;
            string first_file = ToFileName(first);
            return first_file != null && first_file == ToFileName(second);
        }
    }
}

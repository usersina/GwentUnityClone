using System.Collections.Generic;
using System.Linq;
using Gwent.Rules;
using UnityEngine;

namespace Gwent.Core
{
    // The AI's decks (Resources/Decks/AI), one per opponent. The player picks one before the battle or leaves it to
    // chance, and the choice is remembered. They are also the player's presets in the deck builder: picked as they are,
    // never changed (an edit makes the player's own copy).
    public static class OpponentDecks
    {
        private const string Folder = "Decks/AI";
        private const string PrefKey = "opponentDeck";

        // Set by the autoplay smoke test to go through every opponent (not saved)
        public static string Override;

        public static List<TextAsset> All()
        {
            return Resources.LoadAll<TextAsset>(Folder).OrderBy(asset => asset.name).ToList();
        }

        // The deck builder's path of a preset (see DeckStorage)
        public static string PresetPath(TextAsset deck)
        {
            return DeckStorage.ResourcePrefix + Folder + "/" + deck.name;
        }

        public static bool IsPreset(string path)
        {
            return path != null && path.StartsWith(DeckStorage.ResourcePrefix + Folder + "/");
        }

        // A faction's presets, by name
        public static List<string> PresetPaths(Faction faction)
        {
            List<string> paths = new List<string>();
            foreach (TextAsset asset in All())
            {
                Deck deck = CardLibrary.ParseDeck(asset.text, asset.name);
                if (deck != null && deck.Faction == faction)
                    paths.Add(PresetPath(asset));
            }
            return paths;
        }

        // The chosen deck's resource name, "" for a random opponent
        public static string Chosen
        {
            get { return PlayerPrefs.GetString(PrefKey, ""); }
            set
            {
                PlayerPrefs.SetString(PrefKey, value ?? "");
                PlayerPrefs.Save();
            }
        }

        // The deck for the next battle: the chosen one while it exists, otherwise a random one
        public static TextAsset ForBattle()
        {
            List<TextAsset> all = All();
            if (all.Count == 0)
                return null;
            string wanted = !string.IsNullOrEmpty(Override) ? Override : Chosen;
            TextAsset chosen = all.FirstOrDefault(asset => asset.name == wanted);
            return chosen != null ? chosen : all[Rng.Shared.Next(all.Count)];
        }
    }
}

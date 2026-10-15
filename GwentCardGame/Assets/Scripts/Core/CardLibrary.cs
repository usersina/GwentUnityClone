using Gwent.Rules;
using UnityEngine;

namespace Gwent.Core
{
    // The card database, parsed once and shared by the deck builder and the battle
    public static class CardLibrary
    {
        private const string DefaultResourcePath = "Cards/cards_db";
        private static CardDatabase database;

        public static CardDatabase Get(TextAsset source = null)
        {
            if (database != null)
                return database;

            if (source == null)
                source = Resources.Load<TextAsset>(DefaultResourcePath);
            CardsFile cards_db = JsonUtility.FromJson<CardsFile>(source.text);
            database = new CardDatabase(cards_db.Cards.ConvertAll(entry => entry.ToCard()));
            return database;
        }

        // Reads a deck file's JSON; returns null (and logs) instead of throwing on a damaged file
        public static Deck ParseDeck(string json, string source_name)
        {
            try
            {
                DeckFile file = JsonUtility.FromJson<DeckFile>(json);
                if (file == null || file.Cards == null)
                    throw new System.FormatException("not a deck");
                Deck deck = file.ToDeck();
                if (deck == null)
                    throw new System.FormatException("unknown faction '" + file.Faction + "'");
                return deck;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Could not read deck " + source_name + ": " + e.Message);
                return null;
            }
        }
    }
}

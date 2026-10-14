using Gwent.Rules;
using UnityEngine;

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
        CardsDB cards_db = JsonUtility.FromJson<CardsDB>(source.text);
        database = new CardDatabase(cards_db.Cards);
        return database;
    }

    // Reads a deck json; returns null (and logs) instead of throwing on a corrupt file
    public static Deck ParseDeck(string json, string source_name)
    {
        try
        {
            Deck deck = JsonUtility.FromJson<Deck>(json);
            if (deck == null || deck.Cards == null)
                throw new System.FormatException("not a deck");
            return deck;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Could not read deck " + source_name + ": " + e.Message);
            return null;
        }
    }
}

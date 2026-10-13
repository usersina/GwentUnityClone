using System.Collections.Generic;
using System.Linq;
using Gwent.Rules;
using UnityEngine;

// The AI's decks (Resources/Decks/AI), one per opponent. The player picks one before the battle or leaves it to
// chance, and the choice is remembered.
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

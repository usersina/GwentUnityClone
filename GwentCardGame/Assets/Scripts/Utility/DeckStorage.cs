using System.Collections.Generic;
using System.IO;
using Gwent.Rules;
using UnityEngine;

// Deck files on disk: <persistentDataPath>/Decks/<faction>/<name>.json
public static class DeckStorage
{
    public static string Root
    {
        get { return Path.Combine(Application.persistentDataPath, "Decks"); }
    }

    public static string FactionFolder(string faction)
    {
        return Path.Combine(Root, faction);
    }

    public static void EnsureDirectories()
    {
        foreach (string faction in Factions.Playable)
            Directory.CreateDirectory(FactionFolder(faction));
    }

    // All deck files of a faction, sorted by file name
    public static List<string> DeckPaths(string faction)
    {
        List<string> paths = new List<string>();
        string folder = FactionFolder(faction);
        if (!Directory.Exists(folder))
            return paths;
        foreach (string path in Directory.GetFiles(folder, "*.json"))
            paths.Add(path);
        paths.Sort(System.StringComparer.OrdinalIgnoreCase);
        return paths;
    }

    // Null if the file is missing or not a deck
    public static Deck Load(string path)
    {
        try
        {
            return CardLibrary.ParseDeck(File.ReadAllText(path), path);
        }
        catch (System.Exception e) when (e is IOException || e is System.UnauthorizedAccessException || e is System.ArgumentException)
        {
            Debug.LogWarning("Could not read deck " + path + ": " + e.Message);
            return null;
        }
    }

    // Writes atomically (temp file + replace) so a crash cannot leave half a deck behind
    public static bool Save(Deck deck, string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(deck));
            if (File.Exists(path))
                File.Delete(path);
            File.Move(temp, path);
            return true;
        }
        catch (System.Exception e) when (e is IOException || e is System.UnauthorizedAccessException || e is System.ArgumentException)
        {
            Debug.LogError("Could not save deck " + path + ": " + e.Message);
            return false;
        }
    }

    public static bool Delete(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (System.Exception e) when (e is IOException || e is System.UnauthorizedAccessException || e is System.ArgumentException)
        {
            Debug.LogError("Could not delete deck " + path + ": " + e.Message);
            return false;
        }
    }

    // Same file, whatever the separators / case (Windows paths saved by older versions)
    public static bool SamePath(string a, string b)
    {
        return !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b)
            && string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), System.StringComparison.OrdinalIgnoreCase);
    }

    public static string PathFor(string faction, string file_name)
    {
        return Path.Combine(FactionFolder(faction), file_name + ".json");
    }
}

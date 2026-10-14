using UnityEngine;

// Lives in the Menu scene: prepares the deck folders and remembers the deck used in battle
public class ApplicationModel : MonoBehaviour
{
    static public string playerDeckPath;

    private void Start()
    {
        DeckStorage.EnsureDirectories();
        GameLog.Info("Decks directory is: " + DeckStorage.Root);
        GetDeckPref();
        GameLog.Info("Starting, selected deck: " + playerDeckPath);
    }

    // Gets the last selected player deck (ignored if the file is gone)
    public void GetDeckPref()
    {
        playerDeckPath = null;
        string path = PlayerPrefs.GetString("playerDeckPath", "");
        if ((path.EndsWith(".json") || OpponentDecks.IsPreset(path)) && DeckStorage.Exists(path))
            playerDeckPath = path;
    }
}

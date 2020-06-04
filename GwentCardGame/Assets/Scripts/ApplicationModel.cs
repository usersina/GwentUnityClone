
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class ApplicationModel : MonoBehaviour
{
    static public string playerDeckPath;
    static public string playerImagePath;

    private void Start()
    {
        CheckDirectories();
        GetDeckPref();
        //ClearKey("playerDeckPath");
        Debug.Log("Starting, selected deck: " + playerDeckPath);
    }

    // Check deck directories and create if missing
    public void CheckDirectories()
    {
        string deckDir = Path.Combine(Application.persistentDataPath, "Decks");
        Debug.Log("Decks directory is: " + deckDir);

        string NRDecks = Path.Combine(deckDir, "NR");
        string NFDecks = Path.Combine(deckDir, "NF");
        string SCDecks = Path.Combine(deckDir, "SC");
        string MDecks = Path.Combine(deckDir, "M");

        // Create directories if !exist
        if (!Directory.Exists(deckDir))
            Directory.CreateDirectory(deckDir);
        if (!Directory.Exists(NRDecks))
            Directory.CreateDirectory(NRDecks);
        if (!Directory.Exists(NFDecks))
            Directory.CreateDirectory(NFDecks);
        if (!Directory.Exists(SCDecks))
            Directory.CreateDirectory(SCDecks);
        if (!Directory.Exists(MDecks))
            Directory.CreateDirectory(MDecks);
    }

    // Gets the last selected player deck
    public void GetDeckPref()
    {
        playerDeckPath = null;
        if (PlayerPrefs.HasKey("playerDeckPath"))
        {
            if (PlayerPrefs.GetString("playerDeckPath").EndsWith(".json") && File.Exists(PlayerPrefs.GetString("playerDeckPath")))
                playerDeckPath = PlayerPrefs.GetString("playerDeckPath");
        }
    }


    // DEV ONLY
    public void ClearKey(string name)
    {
        PlayerPrefs.DeleteKey(name);
    }
}

using UnityEngine;

// Lives in the Menu scene: prepares the deck folders and reads back the deck chosen for battle
public class MenuStartup : MonoBehaviour
{
    private void Start()
    {
        DeckStorage.EnsureDirectories();
        GameLog.Info("Decks directory is: " + DeckStorage.Root);
        DeckStorage.LoadBattleDeck();
        GameLog.Info("Starting, selected deck: " + DeckStorage.BattleDeckPath);
    }
}

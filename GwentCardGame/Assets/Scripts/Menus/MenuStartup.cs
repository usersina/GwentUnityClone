using Gwent.Core;
using UnityEngine;

namespace Gwent.Menus
{
    // Lives in the Menu scene: prepares the deck folders and reads back the deck chosen for battle
    public class MenuStartup : MonoBehaviour
    {
        private void Start()
        {
            DeckStorage.EnsureDirectories();
            GameLog.Event(LogCategory.Deck, "decks_folder", "path", DeckStorage.Root);
            DeckStorage.LoadBattleDeck();
            GameLog.Event(LogCategory.Deck, "battle_deck", "path", DeckStorage.BattleDeckPath ?? "default");
        }
    }
}

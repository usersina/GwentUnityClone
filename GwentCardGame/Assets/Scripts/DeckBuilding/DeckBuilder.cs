using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gwent.Core;
using Gwent.Rules;
using Gwent.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Gwent.DeckBuilding
{
    // Deck builder: owns the card database, the deck files and the currently edited deck
    public class DeckBuilder : MonoBehaviour
    {
        public const string EmptyDeckResourcePath = "resource://Decks/__empty__";

        public static DeckBuilder Instance { get; private set; }

        public TextAsset CardsDatabase;
        public DeckSummary deckSummary;
        public CardGrid deckCollection;

        public string currentDeckPath;

        // The deck folders path in the local disk

        // A list of deck paths for each faction
        public List<string> NRDecks;
        public List<string> NFDecks;
        public List<string> SCDecks;
        public List<string> MDecks;

        // Runtime state, never saved with the scene: every card (each card has unique stats), the deck being edited
        [System.NonSerialized] public List<Card> allCards;
        [System.NonSerialized] public Deck my_deck;

        public bool IsReady { get; private set; }
        public CardDatabase Db { get; private set; }

        private TextMeshProUGUI battleDeckStatus;

        // The line telling which deck is used in battle (and why the edited one isn't)
        public string BattleDeckStatus { get { return battleDeckStatus != null ? battleDeckStatus.text : ""; } }

        // Loads card data and the selected deck before any other deck builder script starts
        private void Awake()
        {
            Instance = this;
            Db = CardLibrary.Get(CardsDatabase);
            allCards = Db.All;

            DeckStorage.EnsureDirectories();

            // Get Player Deck
            currentDeckPath = EmptyDeckResourcePath;
            if (DeckStorage.Exists(DeckStorage.BattleDeckPath)) // a deck file, or a preset
                currentDeckPath = DeckStorage.BattleDeckPath;
            GameLog.Event(LogCategory.Deck, "builder_open", "deck", currentDeckPath);

            my_deck = LoadDeckFromPath(currentDeckPath);
            if (my_deck == null)
            {
                currentDeckPath = EmptyDeckResourcePath;
                my_deck = LoadDeckFromPath(EmptyDeckResourcePath);
            }
            PopulateDeckAssets();
            OpenOnDeckFaction();
            IsReady = true;
        }

        // The builder opens on the faction of the deck it shows: its header panel and both card lists (they start set up
        // for the first faction, the header only places itself in its Start)
        private void OpenOnDeckFaction()
        {
            foreach (CardGrid collection in FindObjectsByType<CardGrid>())
                collection.OpenOn(my_deck.Faction);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            GameAudio.PlayDeckMusic();
            ConfigureMobileDeckBuilder();
            UpdateBattleDeckStatus();
        }

        private void ConfigureMobileDeckBuilder()
        {
            if (!GameInput.IsTouch)
                return;

            if (EventSystem.current != null)
                EventSystem.current.pixelDragThreshold = Mathf.Max(EventSystem.current.pixelDragThreshold, 20);

            foreach (TMP_InputField input in FindObjectsByType<TMP_InputField>())
            {
                input.shouldHideMobileInput = false;
                input.shouldHideSoftKeyboard = false;
                input.onSelect.AddListener(delegate { input.ActivateInputField(); });
            }

            ExpandTouchTarget("AddBtn", 52f, 52f);
            ExpandTouchTarget("RenameBtn", 52f, 52f);
            ExpandTouchTarget("DeleteBtn", 52f, 52f);
        }

        private void ExpandTouchTarget(string objectName, float minimumWidth, float minimumHeight)
        {
            GameObject buttonObject = GameObject.Find(objectName);
            if (buttonObject == null)
                return;

            RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
            if (rectTransform == null)
                return;

            rectTransform.sizeDelta = new Vector2(
                Mathf.Max(rectTransform.sizeDelta.x, minimumWidth),
                Mathf.Max(rectTransform.sizeDelta.y, minimumHeight));
        }

        //-----------------------------------------------Decks----------------------------------------------------//
        // The deck shown is a preset (an AI deck): picked as it is, never renamed, deleted or changed
        public bool ShowsPreset { get { return OpponentDecks.IsPreset(currentDeckPath); } }

        // Null when the file cannot be read; unknown card ids are dropped
        public Deck LoadDeckFromPath(string path)
        {
            Deck deck;
            if (path == EmptyDeckResourcePath)
            {
                TextAsset deckAsset = Resources.Load<TextAsset>("Decks/__empty__");
                deck = deckAsset != null ? CardLibrary.ParseDeck(deckAsset.text, path) : null;
                if (deck == null)
                    deck = new Deck { Faction = Faction.NorthernRealms, Name = DeckRules.EmptyDeckName, Leader = LeaderIds.FoltestKingOfTemeria };
            }
            else
                deck = DeckStorage.Load(path);
            return DeckRules.Sanitize(deck, Db);
        }

        // Rescans the deck folders (call every time a deck is added, renamed or deleted)
        public void PopulateDeckAssets()
        {
            NRDecks = DeckStorage.DeckPaths(Faction.NorthernRealms);
            NFDecks = DeckStorage.DeckPaths(Faction.Nilfgaard);
            SCDecks = DeckStorage.DeckPaths(Faction.Scoiatael);
            MDecks = DeckStorage.DeckPaths(Faction.Monsters);
        }

        public List<string> DeckPathsOf(Faction faction)
        {
            switch (faction)
            {
                case Faction.Nilfgaard: return NFDecks;
                case Faction.Scoiatael: return SCDecks;
                case Faction.Monsters: return MDecks;
                default: return NRDecks;
            }
        }

        public Card GetCard(int card_id)
        {
            return Db.Get(card_id);
        }

        public List<Card> GetCardsFromDeck(Deck deck)
        {
            return Db.GetCards(deck.Cards.Where(Db.Contains));
        }

        //----------------------------------Card Fetching--------------------------------------//
        // The units of a list: of a faction (null: any), that can go on a row (null: any; agile units go on close and
        // ranged), heroes only if asked
        public List<Card> GetUnitCards(List<Card> cards_list, Faction? faction = null, Row? row = null, bool getUnique = false)
        {
            return cards_list.Where(card =>
                card.IsUnit
                && (faction == null || card.faction == faction)
                && (row == null || card.row == row || (card.row == Row.Agile && (row == Row.Close || row == Row.Range)))
                && (!getUnique || card.unique)).ToList();
        }

        public List<Card> GetSpecialCards(List<Card> cards_list)
        {
            return cards_list.Where(card => card.IsSpecial).ToList();
        }

        public List<Card> MergedList(params List<Card>[] lists)
        {
            List<Card> returned_list = new List<Card>(lists.Sum(list => list.Count));
            foreach (List<Card> list in lists)
                returned_list.AddRange(list);
            return returned_list;
        }

        public int GetUnitsStrength(List<Card> units_list)
        {
            return units_list.Sum(card => card.strength);
        }

        public int GetSpecialsOccurence(Deck deck)
        {
            return GetSpecialCards(GetCardsFromDeck(deck)).Count;
        }

        //------------------------------------Middle Action Buttons---------------------------------------------------//
        // Reference: "+" button
        public void AddDeck(TMP_InputField input)
        {
            if (input == null)
                return;

            input.DeactivateInputField();
            string name = DeckRules.NormalizeDisplayName(input.text);
            string file_name = DeckRules.ToFileName(name);
            if (file_name == null)
            {
                RejectName("Enter a deck name with letters or numbers");
                return;
            }
            if (System.Array.IndexOf(Factions.Playable, my_deck.Faction) < 0)
                return;
            if (NameTaken(name, null))
            {
                RejectName("A deck called \"" + name + "\" already exists for this faction");
                return;
            }

            Deck newDeck = new Deck
            {
                Faction = my_deck.Faction, // Same faction as the currently selected
                Name = name,
                Leader = LeaderIds.BelongsTo(my_deck.Leader, my_deck.Faction) ? my_deck.Leader : LeaderIds.DefaultFor(my_deck.Faction),
            };
            string newPath = DeckStorage.PathFor(newDeck.Faction, file_name);
            if (!DeckStorage.Save(newDeck, newPath))
            {
                RejectName("Could not save the deck");
                return;
            }

            GameLog.Event(LogCategory.Deck, "deck_added", "path", newPath);
            GameAudio.PlaySfx("deck_save");
            input.text = "";
            PopulateDeckAssets();
            deckSummary.LoadDecks(false);
            deckSummary.SelectDeckPath(newPath);
        }

        // Reference: pencil button
        public void RenameDeck(TMP_InputField input)
        {
            if (input == null)
                return;

            input.DeactivateInputField();
            if (my_deck.Name == DeckRules.EmptyDeckName || currentDeckPath == EmptyDeckResourcePath)
            {
                RejectName("Select a deck to rename first");
                return;
            }
            if (ShowsPreset)
            {
                RejectName("A preset keeps its name: change it, and it becomes your own deck to rename");
                return;
            }
            string name = DeckRules.NormalizeDisplayName(input.text);
            string file_name = DeckRules.ToFileName(name);
            if (file_name == null)
            {
                RejectName("Enter a deck name with letters or numbers");
                return;
            }
            if (NameTaken(name, currentDeckPath))
            {
                RejectName("Another deck is already called \"" + name + "\"");
                return;
            }
            if (System.Array.IndexOf(Factions.Playable, my_deck.Faction) < 0)
                return; // a hand-edited faction must not choose the folder the deck moves to

            string oldPath = currentDeckPath;
            string newPath = DeckStorage.PathFor(my_deck.Faction, file_name);
            Deck renamed = new Deck { Faction = my_deck.Faction, Name = name, Leader = my_deck.Leader, Cards = new List<int>(my_deck.Cards) };

            // Write the new file first, then remove the old one: a failure never loses the deck
            if (!DeckStorage.Save(renamed, newPath))
            {
                RejectName("Could not rename the deck");
                return;
            }
            if (!DeckStorage.SamePath(oldPath, newPath))
                DeckStorage.Delete(oldPath);
            if (DeckStorage.SamePath(DeckStorage.SavedBattleDeck, oldPath))
                SetBattleDeck(newPath);

            GameLog.Event(LogCategory.Deck, "deck_renamed", "from", oldPath, "to", newPath);
            GameAudio.PlaySfx("deck_save");
            input.text = "";
            my_deck = renamed;
            currentDeckPath = newPath;
            PopulateDeckAssets();
            deckSummary.LoadDecks(false);
            deckSummary.SelectDeckPath(newPath);
        }

        // Reference: trash button (asks for confirmation first)
        public void DeleteDeck()
        {
            if (deckSummary.deckPicker.options.Count <= 0 || currentDeckPath == EmptyDeckResourcePath)
                return;
            if (ShowsPreset)
            {
                GameAudio.PlaySfx("invalid");
                UIOverlay.Toast("Presets can't be deleted");
                return;
            }

            string filePath = currentDeckPath;
            UIOverlay.Confirm("Delete the deck \"" + my_deck.Name + "\"?\nThis cannot be undone.", "Delete", "Cancel", () => ConfirmDelete(filePath));
        }

        public void ConfirmDelete(string filePath)
        {
            if (!DeckStorage.Delete(filePath))
            {
                UIOverlay.Toast("Could not delete the deck");
                return;
            }
            GameAudio.PlaySfx("deck_remove");
            GameLog.Event(LogCategory.Deck, "deck_deleted", "path", filePath);
            if (DeckStorage.SamePath(DeckStorage.SavedBattleDeck, filePath))
                SetBattleDeck(null);

            PopulateDeckAssets();
            // To make sure deck list is cleared as well
            deckCollection.UpdateSelectedFaction();
        }

        // A preset is never changed: its first edit (already made to the deck shown) is saved as the player's own copy,
        // "The Monster Horde (copy)", which is the deck shown and edited from then on
        public void KeepPresetEdit()
        {
            if (!ShowsPreset)
                return;
            string name = DeckRules.NormalizeDisplayName(my_deck.Name + " (copy)");
            for (int n = 2; NameTaken(name, null); n++)
                name = DeckRules.NormalizeDisplayName(my_deck.Name + " (copy " + n + ")");
            Deck copy = new Deck { Faction = my_deck.Faction, Name = name, Leader = my_deck.Leader, Cards = new List<int>(my_deck.Cards) };
            string path = DeckStorage.PathFor(copy.Faction, DeckRules.ToFileName(name));
            if (!DeckStorage.Save(copy, path))
            {
                RejectName("Could not save the deck");
                return;
            }
            GameLog.Event(LogCategory.Deck, "preset_copied", "path", path);
            GameAudio.PlaySfx("deck_save");
            my_deck = copy;
            currentDeckPath = path;
            PopulateDeckAssets();
            deckSummary.LoadDecks(false);
            deckSummary.SelectDeckPath(path);
            UIOverlay.Toast("Presets stay as they are: your changes are saved in your own deck, \"" + name + "\"", 4f);
        }

        // Same display name (ignoring case/spaces) or same file name as another deck of the faction
        private bool NameTaken(string name, string ignored_path)
        {
            string file_name = DeckRules.ToFileName(name);
            foreach (string path in DeckPathsOf(my_deck.Faction))
            {
                if (ignored_path != null && DeckStorage.SamePath(path, ignored_path))
                    continue;
                if (Path.GetFileNameWithoutExtension(path) == file_name)
                    return true;
                Deck other = DeckStorage.Load(path);
                if (other != null && DeckRules.NamesCollide(name, other.Name))
                    return true;
            }
            return false;
        }

        private static void RejectName(string message)
        {
            GameAudio.PlaySfx("invalid");
            UIOverlay.Toast(message);
        }

        //----------------------Battle deck (the deck used when pressing Play)---------------------//
        // Called whenever the edited deck changes: a valid deck becomes the battle deck, an invalid one stops being it
        public void UpdateBattleDeck()
        {
            bool real_deck = currentDeckPath != EmptyDeckResourcePath && my_deck.Name != DeckRules.EmptyDeckName;
            if (real_deck && IsDeckValid(my_deck))
                SetBattleDeck(currentDeckPath);
            else if (real_deck && DeckStorage.SamePath(DeckStorage.SavedBattleDeck, currentDeckPath))
                SetBattleDeck(null);
            UpdateBattleDeckStatus();
        }

        private void SetBattleDeck(string path)
        {
            DeckStorage.SaveBattleDeck(path);
            GameLog.Event(LogCategory.Deck, "battle_deck_chosen", "path", path ?? "default");
        }

        // Line at the bottom of the screen telling which deck will be used in battle
        public void UpdateBattleDeckStatus()
        {
            if (battleDeckStatus == null)
            {
                // Fits between the two "Show neutrals" toggles; long messages shrink instead of overlapping them
                battleDeckStatus = UIOverlay.CreateText(UIOverlay.FixedRoot, "", 26, new Vector2(0.5f, 0f), new Vector2(0, 42), new Vector2(560, 72));
                battleDeckStatus.textWrappingMode = TextWrappingModes.Normal;
                battleDeckStatus.enableAutoSizing = true;
                battleDeckStatus.fontSizeMin = 14;
                battleDeckStatus.fontSizeMax = 26;
            }

            string battle_path = DeckStorage.BattleDeckPath;
            Deck battle_deck = DeckStorage.Exists(battle_path) ? DeckStorage.Load(battle_path) : null;
            string battle_name = battle_deck == null ? "default (Northern Realms)" : battle_deck.Name + (OpponentDecks.IsPreset(battle_path) ? " (preset)" : "");

            bool editing_real_deck = currentDeckPath != EmptyDeckResourcePath && my_deck.Name != DeckRules.EmptyDeckName;
            if (editing_real_deck && !IsDeckValid(my_deck))
            {
                string problem = DeckRules.Validate(my_deck, Db).FirstOrDefault();
                battleDeckStatus.text = "Not battle-ready: " + problem + "\nBattle deck: " + battle_name;
                battleDeckStatus.color = UITheme.Current.danger;
            }
            else
            {
                battleDeckStatus.text = "Battle deck: " + battle_name;
                battleDeckStatus.color = UIOverlay.Gold;
            }
            if (ShowsPreset)
                battleDeckStatus.text += "\nA preset: changing it makes your own copy";
            // A preset is never renamed or deleted: those buttons dim
            foreach (string control in new[] { "RenameBtn", "DeleteBtn" })
            {
                GameObject button = GameObject.Find(control);
                if (button != null && button.GetComponent<UnityEngine.UI.Button>() != null)
                    button.GetComponent<UnityEngine.UI.Button>().interactable = !ShowsPreset;
            }
        }

        //----------------------Helper Methods---------------------//
        public void WriteDeckToFile(Deck deck, string file_path)
        {
            if (DeckStorage.IsResource(file_path))
            {
                Debug.LogWarning("Skipping write to a deck of the game's own: " + file_path);
                return;
            }

            if (DeckStorage.Save(deck, file_path))
                GameLog.Event(LogCategory.Deck, "deck_saved", "path", file_path, "cards", my_deck.Cards.Count, "leader", Db.Get(my_deck.Leader).name);
            else
                UIOverlay.Toast("Could not save the deck");
        }

        public bool IsDeckValid(Deck deck)
        {
            return DeckRules.IsValid(deck, Db);
        }

        //--------To Main Menu
        public void ToMainMenu()
        {
            GameAudio.PlaySfx("ui_click");
            SceneManager.LoadScene(SceneNames.Menu);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DeckController : MonoBehaviour
{

    public TextAsset CardsDatabase;
    public MiddleManager middleManager;
    public CollectionManager deckCollection;

    public string currentDeckPath;

    // The deck folders path in the local disk
    public List<string> DecksPath = new List<string>();// 0:NR | 1:NF | 2:SC | 3:M

    // A list of deck paths for each faction
    public List<string> NRDecks;
    public List<string> NFDecks;
    public List<string> SCDecks;
    public List<string> MDecks;

    //[HideInInspector] // List of all cards (each card has unique stats)
    public List<Card> allCards;
    public Deck my_deck;
    //public Deck first_timer;

    // Loads Cards Data (Database and loaded deck)
    private void Start()
    {
        string deckDir = Path.Combine(Application.persistentDataPath, "Decks");
        DecksPath = new List<string> {
        Path.Combine(deckDir, "NR"),
        Path.Combine(deckDir, "NF"),
        Path.Combine(deckDir, "SC"),
        Path.Combine(deckDir, "M") };

        // Get Player Deck
        currentDeckPath = Path.Combine(Application.streamingAssetsPath, "__empty__.json");
        if (!string.IsNullOrEmpty(ApplicationModel.playerDeckPath))
            currentDeckPath = ApplicationModel.playerDeckPath;
        Debug.Log("DeckController: Current deck path is: " + currentDeckPath);

        SetUpData();
        //first_timer = my_deck;
    }

    //-----------------------------------------------Functions----------------------------------------------------//
    private void SetUpData()
    {
        // Get the all the game cards
        CardsDB cards_db = JsonUtility.FromJson<CardsDB>(CardsDatabase.text);
        allCards = cards_db.Cards;

        my_deck = LoadDeckFromPath(currentDeckPath);
        PopulateDeckAssets();
    }

    public Deck LoadDeckFromPath(string path)
    {
        return JsonUtility.FromJson<Deck>(File.ReadAllText(path));
    }

    // TODO: Make sure to call every time a deck is added
    public void PopulateDeckAssets()
    {
        NRDecks.Clear();
        NFDecks.Clear();
        SCDecks.Clear();
        MDecks.Clear();

        for (int i = 0; i < DecksPath.Count; i++) // For each faction
        {
            DirectoryInfo info = new DirectoryInfo(DecksPath[i]);
            FileInfo[] fileInfos = info.GetFiles();

            if (fileInfos.Length < 0) // No decks found in the folder
                return;

            foreach (FileInfo file in fileInfos)
            {
                if (file.ToString().EndsWith(".json"))
                {
                    //string filePath = GetRelativePath(file.ToString());
                    string filePath = file.ToString();
                    switch (i)
                    {
                        case 0: // NR
                            NRDecks.Add(filePath);
                            break;
                        case 1: // NF
                            NFDecks.Add(filePath);
                            break;
                        case 2: // SC
                            SCDecks.Add(filePath);
                            break;
                        case 3: // M
                            MDecks.Add(filePath);
                            break;
                    }
                }
            }
        }
    }

    public Card GetCardStats(int card_id)
    {
        Card my_card = new Card();
        for (int i = 0; i < allCards.Count; i++)
        {
            if (allCards[i]._id == card_id)
            {
                my_card = allCards[i];
            }
        }
        return my_card;
    }

    public List<Card> GetCardsFromDeck(Deck deck)
    {
        List<Card> returned_list = new List<Card>();

        foreach (int card_id in deck.Cards)
            returned_list.Add(GetCardStats(card_id));

        return returned_list;
    }

    //----------------------------------Card Fetching--------------------------------------//
    // cards_list: Source list | faction: Card faction | row: close/range/siege
    public List<Card> GetUnitCards(List<Card> cards_list, string faction = "all", string row = "all", bool getUnique = false)
    {
        List<Card> returned_list = new List<Card>();
        bool isRow;
        bool isUnique;
        switch (faction)
        {
            case "all": // Return all unit cards
                foreach (Card card in cards_list)
                {
                    if (row == "all")
                        isRow = true;
                    else
                        isRow = card.row.Contains(row);

                    if (!getUnique)
                        isUnique = true;
                    else
                        isUnique = card.unique;

                    if ((card.faction != "Special") && isRow && card.ability != "leader" && isUnique)
                        returned_list.Add(card);
                }
                break;
            case "NR": // Return all Northern Realms Units
                foreach (Card card in cards_list)
                {
                    if (row == "all")
                        isRow = true;
                    else
                        isRow = card.row.Contains(row);

                    if (!getUnique)
                        isUnique = true;
                    else
                        isUnique = card.unique;

                    if ((card.faction == "NR") && isRow && card.ability != "leader" && isUnique)
                        returned_list.Add(card);
                }
                break;
            case "NF": // Return all Nilfgaard Units
                foreach (Card card in cards_list)
                {
                    if (row == "all")
                        isRow = true;
                    else
                        isRow = card.row.Contains(row);

                    if (!getUnique)
                        isUnique = true;
                    else
                        isUnique = card.unique;

                    if ((card.faction == "NF") && isRow && card.ability != "leader" && isUnique)
                        returned_list.Add(card);
                }
                break;
            case "M": // Return all Monsters Units
                foreach (Card card in cards_list)
                {
                    if (row == "all")
                        isRow = true;
                    else
                        isRow = card.row.Contains(row);

                    if (!getUnique)
                        isUnique = true;
                    else
                        isUnique = card.unique;

                    if ((card.faction == "M") && isRow && card.ability != "leader" && isUnique)
                        returned_list.Add(card);
                }
                break;
            case "SC": // Returns all scoiatel Units
                foreach (Card card in cards_list)
                {
                    if (row == "all")
                        isRow = true;
                    else
                        isRow = card.row.Contains(row);

                    if (!getUnique)
                        isUnique = true;
                    else
                        isUnique = card.unique;

                    if ((card.faction == "SC") && isRow && card.ability != "leader" && isUnique)
                        returned_list.Add(card);
                }
                break;
            case "N": // Returns all Neutral Units
                foreach (Card card in cards_list)
                {
                    if (row == "all")
                        isRow = true;
                    else
                        isRow = card.row.Contains(row);

                    if (!getUnique)
                        isUnique = true;
                    else
                        isUnique = card.unique;

                    if ((card.faction == "N") && isRow && card.ability != "leader" && isUnique)
                        returned_list.Add(card);
                }
                break;
            default:
                Debug.LogError("GetUnitCards: Unexpected value: " + faction);
                break;
        }
        return returned_list;
    }

    public List<Card> GetSpecialCards(List<Card> cards_list)
    {
        List<Card> returned_list = new List<Card>();
        foreach (Card card in cards_list)
            if (card.faction == "Special")
                returned_list.Add(card);
        return returned_list;
    }

    public List<Card> MergedList(params List<Card>[] lists)
    {
        int listLength = 0;
        foreach (List<Card> cards_list in lists)
            listLength += cards_list.Count;
        List<Card> returned_list = new List<Card>(listLength);
        foreach (List<Card> list in lists)
            returned_list.AddRange(list);
        return returned_list;
    }

    public int GetUnitsStrength(List<Card> units_list)
    {
        int returned_int = 0;
        foreach (Card card in units_list)
            returned_int += card.strength;
        return returned_int;
    }

    public int GetSpecialsOccurence(Deck deck)
    {
        List<Card> my_cards = GetCardsFromDeck(deck);
        return GetSpecialCards(my_cards).Count;
    }

    //------------------------------------------Classes and JSON Related------------------------------------------//
    // Card Structure
    [Serializable] // Means the following class can be added to a list
    public class Card
    {
        public string _idstr = null;
        public string name = null;
        public int _id = -1;
        public string faction = null;
        public bool unique = false;
        public int strength = 0;
        public string row = null;
        public string ability = null;
    }

    // List of above Card
    public class CardsDB
    {
        public List<Card> Cards = new List<Card>();
    }

    // Deck Structure
    public class Deck
    {
        // Usage Example:
        //
        //Deck deck = new Deck
        //{
        //    Faction = "NR",
        //    Name = "My Best Deck",
        //    Cards = { 1, 2, 3, 4 } (card ids)
        //};
        //string json = JsonUtility.ToJson(deck);
        public string Faction = null;
        public string Name = null;
        public int Leader = 0;
        public List<int> Cards = new List<int>();
    }

    //------------------------------------Middle Action Buttons---------------------------------------------------//
    public void AddDeck(TMP_InputField input)
    {
        input.text = FormatInput(input.text);
        if (string.IsNullOrEmpty(input.text))
            return;

        // Check if name already exists; if not store new name
        for (int i = 0; i < middleManager.deckPicker.options.Count; i++)
        {
            //Debug.Log("Option" + i + ": " + middleManager.deckPicker.options[i].text);
            string name = middleManager.deckPicker.options[i].text;

            if (input.text == name)
                return; // Name already exists
        }

        Debug.Log("Adding deck: " + input.text);
        Deck newDeck = new Deck
        {
            Faction = my_deck.Faction, // Same faction as the currently selected 
            Name = input.text,
            Leader = my_deck.Leader,
            Cards = { }
        };

        Debug.Log("New Deck: Faction: " + newDeck.Faction);
        string newPath;
        string fileName = FormatDeckName(input.text) + ".json";
        switch (newDeck.Faction)
        {
            case "NR":
                newPath = Path.Combine(DecksPath[0], fileName);
                break;
            case "NF":
                newPath = Path.Combine(DecksPath[1], fileName);
                break;
            case "SC":
                newPath = Path.Combine(DecksPath[2], fileName);
                break;
            case "M":
                newPath = Path.Combine(DecksPath[3], fileName);
                break;
            default:
                newPath = Path.Combine(Application.persistentDataPath, "/Decks/errorDeck.json");
                break;
        }
        WriteDeckToFile(newDeck, newPath);
        PopulateDeckAssets();
        middleManager.LoadDecks();

        // Get the deck of the name input.text
        int index = -1;
        for (int i = 0; i < middleManager.deckPicker.options.Count; i++)
        {
            string name = middleManager.deckPicker.options[i].text;
            if (input.text == name)
                index = i;
        }
        input.text = "";
        middleManager.deckPicker.value = index;
    }

    // NOTE: .meta warning (prolly not important)
    public void RenameDeck(TMP_InputField input)
    {
        input.text = FormatInput(input.text);
        if (string.IsNullOrEmpty(input.text))
            return;
        if (my_deck.Name == "__emptyname__")
            return;

        Debug.Log("Renaming deck to: " + input.text);

        my_deck.Name = input.text;

        // Change the file name
        string newPath;
        string fileName = FormatDeckName(input.text) + ".json";
        switch (my_deck.Faction)
        {
            case "NR":
                newPath = DecksPath[0] + "/" + fileName;
                break;
            case "NF":
                newPath = DecksPath[1] + "/" + fileName;
                break;
            case "SC":
                newPath = DecksPath[2] + "/" + fileName;
                break;
            case "M":
                newPath = DecksPath[3] + "/" + fileName;
                break;
            default:
                newPath = Application.streamingAssetsPath + "/Decks/errorDeck.json";
                break;
        }

        // Save the deck with the new name
        WriteDeckToFile(my_deck, currentDeckPath);

        // Rename file
        Debug.Log("Old deck path: " + currentDeckPath);
        Debug.Log("Renaming file to: " + newPath);
        File.Move(currentDeckPath, newPath);
        PopulateDeckAssets();

        // Refresh in UI
        middleManager.LoadDecks();

        // Select the new deck
        // Get the deck of the name input.text
        int index = -1;
        for (int i = 0; i < middleManager.deckPicker.options.Count; i++)
        {
            string name = middleManager.deckPicker.options[i].text;
            if (input.text == name)
                index = i;
        }

        // Clear the field
        input.text = "";
        middleManager.deckPicker.value = index;
    }

    public void DeleteDeck()
    {
        if (middleManager.deckPicker.options.Count <= 0)
            return;

        int index = middleManager.deckPicker.value;
        string filePath;
        switch (my_deck.Faction)
        {
            case "NR":
                filePath = NRDecks[index];
                break;
            case "NF":
                filePath = NFDecks[index];
                break;
            case "SC":
                filePath = SCDecks[index];
                break;
            case "M":
                filePath = MDecks[index];
                break;
            default:
                filePath = "Invalid path";
                break;
        }

        File.Delete(filePath);
        Debug.Log("Deleting: " + filePath);

        PopulateDeckAssets();
        // To make sure deck list is cleared as well
        deckCollection.UpdateSelectedFaction();
    }

    //----------------------Helper Methods---------------------//
    // Get relative path from absolute path
    public string FormatDeckName(string deck_name)
    {
        deck_name = deck_name.Trim();
        deck_name = deck_name.ToLower();
        deck_name = deck_name.Replace(" ", "_");
        return deck_name;
    }

    public string FormatInput(string text)
    {
        text = text.Trim();
        var demo = text.Split(' ').Where(s => !string.IsNullOrWhiteSpace(s));
        text = string.Join(" ", demo);
        return text;
    }

    public void WriteDeckToFile(Deck deck, string file_path)
    {
        string json = JsonUtility.ToJson(deck);
        //Debug.Log(json);
        File.WriteAllText(file_path, json);
        Debug.Log("Deck saved correctly under: " + file_path);
    }

    public bool IsDeckValid(Deck deck)
    {
        bool isValid = false;

        int units_number = GetUnitCards(GetCardsFromDeck(deck)).Count();
        Debug.Log("Units number in deck: " + units_number);

        if (units_number >= 22)
            isValid = true;

        return isValid;
    }

    //--------To Main Menu
    public void ToMainMenu()
    {
        SceneManager.LoadScene(0);
    }
}

using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static DeckController;

public class MiddleManager : MonoBehaviour
{
    public DeckController deckController;
    public CollectionManager deckCollection;
    public GameObject leaderGo;
    public TMP_Dropdown deckPicker;
    public Deck loadedDeck;
    public List<Card> deckCards;
    public List<TextMeshProUGUI> deckInfo;

    public List<Deck> myDecks = new List<Deck>();
    public List<string> myPaths = new List<string>();

    IEnumerator Start()
    {
        yield return new WaitForSeconds(0.3f);
        InitializeDeck();
        LoadDecks();

        //yield return new WaitForSeconds(0.2f);
        //Deck first_timer = deckController.first_timer;
        //Debug.Log("Deck to select: " + first_timer.Name);
        ////Switch to the panel of the selected deck(first time only)
        //int index = -1;
        //for (int i = 0; i < deckPicker.options.Count; i++)
        //{
        //    string name = deckPicker.options[i].text;
        //    if (first_timer.Name == name)
        //        index = i;
        //}
        //deckPicker.value = index;
    }

    // Updates the deck UI and Leader from deckController.my_deck
    public void InitializeDeck()
    {
        loadedDeck = deckController.my_deck;
        deckCards = deckController.GetCardsFromDeck(loadedDeck);
        UIUpdateDeckInfo();
        UIUpdateLeader();
    }

    // Load Decks in the DropDown
    public void LoadDecks()
    {
        // Clear Dropdown
        deckPicker.ClearOptions();

        List<TMP_Dropdown.OptionData> decks_list_drop = new List<TMP_Dropdown.OptionData>();
        List<string> deck_paths_list;

        switch (deckController.my_deck.Faction)
        {
            case "NR":
                deck_paths_list = deckController.NRDecks;
                break;
            case "NF":
                deck_paths_list = deckController.NFDecks;
                break;
            case "SC":
                deck_paths_list = deckController.SCDecks;
                break;
            case "M":
                deck_paths_list = deckController.MDecks;
                break;
            default:
                deck_paths_list = deckController.NRDecks;
                break;
        }

        myDecks.Clear();
        myPaths.Clear();

        // Add decks to options and to global deck list
        foreach (string path in deck_paths_list)
        {
            Deck auxdeck = deckController.LoadDeckFromPath(path);
            TMP_Dropdown.OptionData dropItem = new TMP_Dropdown.OptionData(auxdeck.Name);
            myDecks.Add(auxdeck);
            myPaths.Add(path);
            decks_list_drop.Add(dropItem);
        }
        deckPicker.AddOptions(decks_list_drop);

        // Reload the first deck just in case
        if (deck_paths_list.Count > 0)
            deckCollection.OnDeckChange(myDecks[0], myPaths[0]);
        else
        {
            // Clear the collection (my_deck is empty at this point: Cleared from UpdateSelectedFaction)
            deckCollection.OnDeckChange(deckController.my_deck, Path.Combine(Application.streamingAssetsPath, "__empty__.json"));
        }
        InitializeDeck();
    }


    public void OnOptionChanged(int index)
    {
        //Debug.Log("Option Changed to: " + myDecks[index].Name);
        deckCollection.OnDeckChange(myDecks[index], myPaths[index]);
        InitializeDeck();
    }

    // Sets the leader stats and ui
    public void UIUpdateLeader()
    {
        Card leader_card = deckController.GetCardStats(loadedDeck.Leader);
        leaderGo.GetComponent<CardStats>().name = leader_card.name;
        leaderGo.GetComponent<CardStats>()._id = leader_card._id;
        leaderGo.GetComponent<CardStats>()._idstr = leader_card._idstr;
        leaderGo.GetComponent<CardStats>().faction = leader_card.faction;
        leaderGo.GetComponent<CardStats>().unique = leader_card.unique;
        leaderGo.GetComponent<CardStats>().strength = leader_card.strength;
        leaderGo.GetComponent<CardStats>().row = leader_card.row;
        leaderGo.GetComponent<CardStats>().ability = leader_card.ability;

        leaderGo.GetComponent<Image>().sprite = Resources.Load<Sprite>("Cards/List/591x380/" + loadedDeck.Leader);
        leaderGo.transform.Find("Name").GetComponent<TextMeshProUGUI>().text = leader_card.name;
    }

    // Updates the middle info numbers
    public void UIUpdateDeckInfo()
    {
        int helperNumber;

        // Total number of cards
        deckInfo[0].text = loadedDeck.Cards.Count.ToString();

        // Unit Cards number (Minimum: 22)
        helperNumber = deckController.GetUnitCards(deckCards, "all", "all", false).Count;
        if (helperNumber < 22)
        {
            deckInfo[1].color = new Color32(245, 2, 0, 255); // Red
            deckInfo[1].text = helperNumber.ToString() + "/22";
        }
        else
        {
            deckInfo[1].color = new Color32(255, 188, 0, 255); // Golden Orange
            deckInfo[1].text = helperNumber.ToString();
        }

        // Special Cards number (Maximum: 10)
        helperNumber = deckController.GetSpecialCards(deckCards).Count;
        deckInfo[2].text = helperNumber.ToString() + "/10";

        // Total Strengths
        deckInfo[3].text = deckController.GetUnitsStrength(deckController.GetUnitCards(deckCards, "all", "all", false)).ToString();

        // Hero Cards number
        deckInfo[4].text = deckController.GetUnitCards(deckCards, "all", "all", true).Count.ToString();
    }

}

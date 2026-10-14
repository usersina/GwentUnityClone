using System.Collections;
using System.Collections.Generic;
using Gwent.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        // Long names ("Siege Engines of Temeria (preset)") shrink to fit rather than lose their end
        foreach (TMP_Text text in new[] { deckPicker.captionText, deckPicker.itemText })
            if (text != null)
            {
                text.enableAutoSizing = true;
                text.fontSizeMax = text.fontSize;
                text.fontSizeMin = 14;
            }
        // DeckController loads everything in Awake; this only guards against a disabled controller
        yield return new WaitUntil(() => deckController.IsReady);
        string selected_path = deckController.currentDeckPath;
        InitializeDeck();
        LoadDecks(false);
        // Keep the deck that was selected last time instead of jumping to the first one
        SelectDeckPath(selected_path);
    }

    // Updates the deck UI and Leader from deckController.my_deck
    public void InitializeDeck()
    {
        loadedDeck = deckController.my_deck;
        deckCards = deckController.GetCardsFromDeck(loadedDeck);
        UIUpdateDeckInfo();
        UIUpdateLeader();
    }

    // Load Decks in the DropDown and show the first one.
    // select_for_battle: the shown deck becomes the battle deck (if valid). False when another deck gets selected right after.
    public void LoadDecks(bool select_for_battle = true)
    {
        // Clear Dropdown
        deckPicker.ClearOptions();

        List<TMP_Dropdown.OptionData> decks_list_drop = new List<TMP_Dropdown.OptionData>();
        // The player's decks, then the faction's presets (the AI's decks)
        List<string> deck_paths_list = new List<string>(deckController.DeckPathsOf(deckController.my_deck.Faction));
        deck_paths_list.AddRange(OpponentDecks.PresetPaths(deckController.my_deck.Faction));

        myDecks.Clear();
        myPaths.Clear();

        // Add decks to options and to global deck list (unreadable files are skipped)
        int skipped = 0;
        foreach (string path in deck_paths_list)
        {
            Deck auxdeck = deckController.LoadDeckFromPath(path);
            if (auxdeck == null)
            {
                skipped++;
                continue;
            }
            myDecks.Add(auxdeck);
            myPaths.Add(path);
            decks_list_drop.Add(new TMP_Dropdown.OptionData(OpponentDecks.IsPreset(path) ? auxdeck.Name + " (preset)" : auxdeck.Name));
        }
        deckPicker.AddOptions(decks_list_drop);
        if (skipped > 0)
            UIOverlay.Toast(skipped + " deck file(s) could not be read and were skipped");

        // Reload the first deck just in case
        if (myDecks.Count > 0)
            deckCollection.OnDeckChange(myDecks[0], myPaths[0], select_for_battle);
        else
        {
            // Clear the collection (my_deck is empty at this point: Cleared from UpdateSelectedFaction)
            deckCollection.OnDeckChange(deckController.my_deck, DeckController.EmptyDeckResourcePath, select_for_battle);
        }
        InitializeDeck();
    }


    // Selects a deck of the dropdown by file path (after adding/renaming/deleting)
    public void SelectDeckPath(string path)
    {
        int index = myPaths.FindIndex(p => DeckStorage.SamePath(p, path));
        if (index < 0)
            return;
        deckPicker.SetValueWithoutNotify(index);
        deckCollection.OnDeckChange(myDecks[index], myPaths[index]);
        InitializeDeck();
    }

    public void OnOptionChanged(int index)
    {
        if (index < 0 || index >= myDecks.Count || index >= myPaths.Count)
            return;

        GameAudio.PlaySfx("deck_select");
        deckCollection.OnDeckChange(myDecks[index], myPaths[index]);
        InitializeDeck();
    }

    // Sets the leader stats and ui
    public void UIUpdateLeader()
    {
        Card leader_card = deckController.GetCardStats(loadedDeck.Leader);
        CardStats stats = leaderGo.GetComponent<CardStats>();
        stats.name = leader_card.name;
        stats._id = leader_card._id;
        stats._idstr = leader_card._idstr;
        stats.faction = leader_card.faction;
        stats.unique = leader_card.unique;
        stats.strength = leader_card.strength;
        stats.row = leader_card.row;
        stats.ability = leader_card.ability;

        leaderGo.GetComponent<Image>().sprite = SpriteCache.Load("Cards/List/591x380/" + loadedDeck.Leader);
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
        if (helperNumber < DeckRules.MinUnits)
        {
            deckInfo[1].color = UITheme.Current.danger;
            deckInfo[1].text = helperNumber.ToString() + "/" + DeckRules.MinUnits;
        }
        else
        {
            deckInfo[1].color = UITheme.Current.gold;
            deckInfo[1].text = helperNumber.ToString();
        }

        // Special Cards number (Maximum: 10)
        helperNumber = deckController.GetSpecialCards(deckCards).Count;
        deckInfo[2].text = helperNumber.ToString() + "/" + DeckRules.MaxSpecials;

        // Total Strengths
        deckInfo[3].text = deckController.GetUnitsStrength(deckController.GetUnitCards(deckCards, "all", "all", false)).ToString();

        // Hero Cards number
        deckInfo[4].text = deckController.GetUnitCards(deckCards, "all", "all", true).Count.ToString();
    }

}

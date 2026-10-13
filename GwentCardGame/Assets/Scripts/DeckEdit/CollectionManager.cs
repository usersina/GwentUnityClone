using DanielLochner.Assets.SimpleScrollSnap;
using System.Collections;
using System.Collections.Generic;
using Gwent.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CollectionManager : MonoBehaviour
{
    // The factions of the header's panels, in order, and their perks
    public static readonly string[] HeaderFactions = { Factions.NorthernRealms, Factions.Nilfgaard, Factions.Scoiatael, Factions.Monsters };
    private static readonly string[] Perks =
    {
        "Draw a card from your deck whenever you win a round",
        "Automatically win the round that ends in a draw",
        "Choose who goes first at the start of the battle",
        "Keep one random unit on the board after each round",
    };

    public DeckController deckController;
    public SimpleScrollSnap headerSnap;
    public Toggle toggleBtn;

    public TextMeshProUGUI subtitle;
    public GameObject CCardPrefab;
    public GameObject cardsArea;
    public GameObject cardDetails;

    public int selectedTab;
    public string faction = Factions.NorthernRealms;
    public TextMeshProUGUI factionPerk;

    public List<Image> currentSprites;
    public List<Sprite> idleSprites;
    public List<Sprite> selectedSprites;

    [HideInInspector]
    public List<Card> CardsToDisplay;

    public MiddleManager middleManager;

    IEnumerator Start()
    {
        ClearCardsArea();
        // DeckController loads everything in Awake; this only guards against a disabled controller
        yield return new WaitUntil(() => deckController.IsReady);

        // Collection Cards or player deck
        if (transform.CompareTag("Player"))
            LoadFromDeck();
        else
            CardsToDisplay = deckController.allCards;

        SetCardsArea(UnitMergerHelper(faction, "all"));
    }

    // Reference: Menu Buttons
    public void HandleClick(int index)
    {
        if (selectedTab != index)
        {
            Deselect(selectedTab);
            selectedTab = index;
            Select(selectedTab);
        }
    }

    public void Deselect(int index)
    {
        currentSprites[index].sprite = idleSprites[index];
    }

    public void Select(int index, bool resetScroll = true)
    {
        string selected_row;

        // UI: Text and Icon
        currentSprites[index].sprite = selectedSprites[index];
        switch (index)
        {
            case 0:
                subtitle.text = "All Cards";
                selected_row = "all";
                break;
            case 1:
                subtitle.text = "Close Units";
                selected_row = Rows.Close;
                break;
            case 2:
                subtitle.text = "Ranged Units";
                selected_row = Rows.Range;
                break;
            case 3:
                subtitle.text = "Siege Units";
                selected_row = Rows.Siege;
                break;
            case 4:
                subtitle.text = "Heroes";
                selected_row = "heroes";
                break;
            case 5:
                subtitle.text = "Specials";
                selected_row = "specials";
                break;
            default:
                selected_row = "none";
                break;
        }

        // GAME: Instantiated Cards
        ManageCardsArea(selected_row, resetScroll);
    }

    //------------------------------------------------------------Functions-----------------------------------------------//
    // Sets Deck faction and Cards from my_deck
    private void LoadFromDeck()
    {
        faction = deckController.my_deck.Faction;
        CardsToDisplay = deckController.GetCardsFromDeck(deckController.my_deck);
    }

    private void ClearCardsArea()
    {
        for (int i = 0; i < cardsArea.transform.childCount; i++)
        {
            Destroy(cardsArea.transform.GetChild(i).gameObject);
        }

        // Do not remove this, otherwise count will still get the previous frame's count (NOT 0);
        cardsArea.transform.DetachChildren();
    }

    private void SetCardsArea(List<Card> cards_list)
    {
        ClearCardsArea();
        foreach (Card card in cards_list)
        {
            // Bovine defense force cannot be added to a deck (it is summoned by the Cow)
            if (card._id == CardIds.BovineDefenseForce)
                continue;

            GameObject instantiatedCard = Instantiate(CCardPrefab);
            instantiatedCard.name = card._id.ToString();
            CardStats stats = instantiatedCard.GetComponent<CardStats>();
            stats.name = card.name;
            stats._id = card._id;
            stats._idstr = card._idstr;
            stats.faction = card.faction;
            stats.unique = card.unique;
            stats.strength = card.strength;
            stats.row = card.row;
            stats.ability = card.ability;

            instantiatedCard.GetComponent<Image>().sprite = SpriteCache.Load("Cards/List/591x380/" + card._id);
            instantiatedCard.transform.SetParent(cardsArea.transform, false);
        }
    }

    private void ResetScrolling()
    {
        ScrollRect scroll = cardsArea.transform.parent.GetComponent<ScrollRect>();
        scroll.StopMovement();
        scroll.verticalNormalizedPosition = 1; // back to the top, at any resolution
    }

    private void ManageCardsArea(string selected_row, bool resetScroll = true)
    {
        if (resetScroll)
            ResetScrolling();

        switch (selected_row)
        {
            case "specials":
                List<Card> specials_list = deckController.GetSpecialCards(CardsToDisplay);
                specials_list.Sort((a, b) => a._id.CompareTo(b._id));
                SetCardsArea(specials_list);
                break;
            case "heroes":
                SetCardsArea(UnitMergerHelper(faction, "all", true));
                break;
            default:
                switch (selected_row)
                {
                    case "all":
                        SetCardsArea(UnitMergerHelper(faction, "all"));
                        break;
                    case Rows.Close:
                    case Rows.Range:
                    case Rows.Siege:
                        SetCardsArea(UnitMergerHelper(faction, selected_row));
                        break;
                    default:
                        Debug.LogError("ManageCardsArea: Unexpected value: " + selected_row);
                        break;
                }
                break;
        }
    }

    private List<Card> UnitMergerHelper(string fc, string rw, bool ishero = false) // faction | row (to avoid global variables confusion)
    {
        List<Card> returned_list;
        if (toggleBtn.isOn)
        {
            returned_list = deckController.MergedList(
            deckController.GetUnitCards(CardsToDisplay, fc, rw, ishero),
            deckController.GetUnitCards(CardsToDisplay, "N", rw, ishero)
            );
        }
        else
        {
            returned_list = deckController.MergedList(
            deckController.GetUnitCards(CardsToDisplay, fc, rw, ishero)
            );
        }

        // Sort By strength, if strength is equal then sort by id
        // (To get cards with similar names besides each other in deck collection)
        returned_list.Sort((x, y) =>
        {
            var ret = x.strength.CompareTo(y.strength);
            if (ret == 0) ret = x._id.CompareTo(y._id);
            return ret;
        });

        if (rw == "all" && !ishero) // Add special Cards to all cards
        {
            List<Card> specials_list = deckController.GetSpecialCards(CardsToDisplay);
            specials_list.Sort((a, b) => a._id.CompareTo(b._id));
            returned_list = deckController.MergedList(returned_list, specials_list);
        }

        return returned_list;
    }

    //----------------------------------------------------Event Handlers---------------------------------------------------//
    // Opens on a faction (the deck's, before the header places itself): its cards and its perk
    public void OpenOn(string deck_faction)
    {
        int panel = System.Array.IndexOf(HeaderFactions, deck_faction);
        if (panel < 0)
            return;
        faction = deck_faction;
        if (factionPerk != null)
            factionPerk.text = Perks[panel];
        if (headerSnap != null)
            headerSnap.startingPanel = panel;
    }

    // Reference: ScrollSnap Header On Panel Changed
    public void UpdateSelectedFaction()
    {
        int panel = headerSnap.CurrentPanel;
        if (panel >= 0 && panel < HeaderFactions.Length)
        {
            faction = HeaderFactions[panel];
            factionPerk.text = Perks[panel];
            Select(selectedTab);
        }

        // Related to Deck Cards
        if (transform.CompareTag("Player"))
        {
            // Start from a new empty deck of that faction (the loaded deck object is shared with the dropdown list)
            deckController.my_deck = new Deck { Faction = faction, Name = DeckRules.EmptyDeckName };

            // Change the leader to the default of each faction
            deckController.my_deck.Leader = LeaderIds.DefaultFor(faction);

            // Load the new decklists and get the first deck if not empty
            middleManager.LoadDecks();
        }
    }

    // Reference: Toggle On Value Changed
    public void ToggleNeutrals()
    {
        Select(selectedTab, false);
    }

    // DECK CARDS RELATED
    // Changes my_deck and currentDeckPath in DeckController
    public void OnDeckChange(Deck selectedDeck, string selectedPath, bool select_for_battle = true)
    {
        // Replace the loaded deck with the selected one
        deckController.my_deck = selectedDeck;
        deckController.currentDeckPath = selectedPath;
        LoadFromDeck();
        Select(selectedTab);

        // A valid selected deck becomes the battle deck
        if (select_for_battle)
            deckController.UpdateBattleDeck();
    }

    // From Double Clicking a card
    public void OnDeckEdit()
    {
        LoadFromDeck();
        Select(selectedTab);
        middleManager.InitializeDeck();

        // Save to the current deck
        deckController.WriteDeckToFile(deckController.my_deck, deckController.currentDeckPath);

        // Becomes (or stops being, if now invalid) the battle deck
        deckController.UpdateBattleDeck();
    }

}

using System.Collections;
using System.Collections.Generic;
using Gwent.Battle;
using Gwent.Core;
using Gwent.Rules;
using Gwent.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Gwent.DeckBuilding
{
    public class CardGrid : MonoBehaviour
    {
        // The factions of the header's panels, in order, and their perks
        public static readonly Faction[] HeaderFactions = { Faction.NorthernRealms, Faction.Nilfgaard, Faction.Scoiatael, Faction.Monsters };
        private static readonly string[] Perks =
        {
            "Draw a card from your deck whenever you win a round",
            "Automatically win the round that ends in a draw",
            "Choose who goes first at the start of the battle",
            "Keep one random unit on the board after each round",
        };

        public DeckBuilder deckBuilder;
        public FactionHeader header;
        public Toggle toggleBtn;

        public TextMeshProUGUI subtitle;
        public GameObject CCardPrefab;
        public GameObject cardsArea;
        public GameObject cardDetails;

        public int selectedTab;
        public Faction faction = Faction.NorthernRealms;
        public TextMeshProUGUI factionPerk;

        public List<Image> currentSprites;
        public List<Sprite> idleSprites;
        public List<Sprite> selectedSprites;

        [System.NonSerialized] public List<Card> CardsToDisplay;

        public DeckSummary deckSummary;

        // The Witcher 3's filters: all cards, close, ranged, siege, heroes, weather, special cards
        private enum Listing { All, Close, Range, Siege, Heroes, Weather, Specials, None }
        public const int WeatherTab = 5;
        public const int SpecialTab = 6;

        IEnumerator Start()
        {
            AddWeatherTab();
            ClearCardsArea();
            // DeckBuilder loads everything in Awake; this only guards against a disabled controller
            yield return new WaitUntil(() => deckBuilder.IsReady);

            // Collection Cards or player deck
            if (transform.CompareTag("Player"))
                LoadFromDeck();
            else
                CardsToDisplay = deckBuilder.allCards;

            SetCardsArea(UnitMergerHelper(faction, null));
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
            Listing listing;

            // UI: Text and Icon
            currentSprites[index].sprite = selectedSprites[index];
            switch (index)
            {
                case 0:
                    subtitle.text = "All Cards";
                    listing = Listing.All;
                    break;
                case 1:
                    subtitle.text = "Close Units";
                    listing = Listing.Close;
                    break;
                case 2:
                    subtitle.text = "Ranged Units";
                    listing = Listing.Range;
                    break;
                case 3:
                    subtitle.text = "Siege Units";
                    listing = Listing.Siege;
                    break;
                case 4:
                    subtitle.text = "Heroes";
                    listing = Listing.Heroes;
                    break;
                case WeatherTab:
                    subtitle.text = "Weather";
                    listing = Listing.Weather;
                    break;
                case SpecialTab:
                    subtitle.text = "Specials";
                    listing = Listing.Specials;
                    break;
                default:
                    listing = Listing.None;
                    break;
            }

            // GAME: Instantiated Cards
            ManageCardsArea(listing, resetScroll);
        }

        //------------------------------------------------------------Functions-----------------------------------------------//
        // Sets Deck faction and Cards from my_deck
        private void LoadFromDeck()
        {
            faction = deckBuilder.my_deck.Faction;
            CardsToDisplay = deckBuilder.GetCardsFromDeck(deckBuilder.my_deck);
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

        // The Witcher 3 shows each card once, with its count: the copies in the deck, or in the collection the copies
        // left to add (a card whose copies are all in the deck leaves the collection)
        private void SetCardsArea(List<Card> cards_list)
        {
            ClearCardsArea();
            bool deck = transform.CompareTag("Player");
            Dictionary<int, int> in_deck = new Dictionary<int, int>();
            foreach (int id in deckBuilder.my_deck.Cards)
                in_deck[id] = in_deck.TryGetValue(id, out int copies) ? copies + 1 : 1;
            HashSet<int> shown = new HashSet<int>();
            foreach (Card card in cards_list)
            {
                // Bovine defense force cannot be added to a deck (it is summoned by the Cow)
                if (card._id == CardIds.BovineDefenseForce || !shown.Add(card._id))
                    continue;
                in_deck.TryGetValue(card._id, out int used);
                int count = deck ? used : DeckRules.MaxCopies(card) - used;
                if (count <= 0)
                    continue;

                GameObject instantiatedCard = Instantiate(CCardPrefab);
                instantiatedCard.name = card._id.ToString();
                CardRef stats = instantiatedCard.GetComponent<CardRef>();
                stats.Card = card;

                instantiatedCard.GetComponent<Image>().sprite = SpriteCache.Load("Cards/List/591x380/" + card._id);
                instantiatedCard.transform.SetParent(cardsArea.transform, false);
                AddCountBadge(instantiatedCard.transform, count);
            }
        }

        // "x2" near the bottom of the card, as in The Witcher 3: at the right of the art, just above the name strip
        // (which it would cover on the left)
        public const string CountBadge = "Count";

        private static void AddCountBadge(Transform card, int count)
        {
            UITheme theme = UITheme.Current;
            RectTransform badge = UIKit.Rect(CountBadge, card).Place(new Vector2(1, 0), new Vector2(-8, 70), new Vector2(50, 28), new Vector2(1, 0));
            Image back = badge.gameObject.AddComponent<Image>();
            back.sprite = theme.pill;
            back.type = Image.Type.Sliced;
            back.color = new Color(0.07f, 0.05f, 0.03f, 0.88f);
            back.raycastTarget = false;
            TextMeshProUGUI text = UIKit.Text(badge, "x" + count, UIKit.TextStyle.Caption, TextAlignmentOptions.Center);
            text.rectTransform.Stretch(0, 0, 0, 1);
            text.fontSize = 21;
            text.color = theme.goldBright;
            text.raycastTarget = false;
        }

        // The scene has six tabs, the specials last; The Witcher 3 gives the weather cards a tab of their own, before
        // the specials. The specials' tab is copied for it, and the seven tabs share the menu's width.
        private void AddWeatherTab()
        {
            if (currentSprites.Count > SpecialTab)
                return;
            Image special = currentSprites[WeatherTab];
            Image weather = Instantiate(special.gameObject, special.transform.parent).GetComponent<Image>();
            weather.name = "weatherBtn";
            weather.transform.SetSiblingIndex(special.transform.GetSiblingIndex());
            currentSprites.Insert(WeatherTab, weather);
            idleSprites.Insert(WeatherTab, SpriteCache.Load("DeckEdit/CardsView/Menu/idle/weather"));
            selectedSprites.Insert(WeatherTab, SpriteCache.Load("DeckEdit/CardsView/Menu/selected/weather"));
            weather.sprite = idleSprites[WeatherTab];
            OnClickSelects(weather.GetComponent<Button>(), WeatherTab);
            OnClickSelects(special.GetComponent<Button>(), SpecialTab);

            RectTransform first = (RectTransform)currentSprites[0].transform;
            float left = first.anchoredPosition.x, right = ((RectTransform)special.transform).anchoredPosition.x;
            for (int i = 0; i < currentSprites.Count; i++)
            {
                RectTransform tab = (RectTransform)currentSprites[i].transform;
                tab.anchoredPosition = new Vector2(Mathf.Lerp(left, right, i / (float)(currentSprites.Count - 1)), tab.anchoredPosition.y);
                tab.sizeDelta = new Vector2(90, 90);
            }
        }

        private void OnClickSelects(Button button, int tab)
        {
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                button.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => HandleClick(tab));
        }

        private void ResetScrolling()
        {
            ScrollRect scroll = cardsArea.transform.parent.GetComponent<ScrollRect>();
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1; // back to the top, at any resolution
        }

        private void ManageCardsArea(Listing listing, bool resetScroll = true)
        {
            if (resetScroll)
                ResetScrolling();

            switch (listing)
            {
                case Listing.Weather:
                case Listing.Specials:
                    List<Card> specials_list = deckBuilder.GetSpecialCards(CardsToDisplay).FindAll(card => (card.row == Row.Weather) == (listing == Listing.Weather));
                    specials_list.Sort((a, b) => a._id.CompareTo(b._id));
                    SetCardsArea(specials_list);
                    break;
                case Listing.Heroes:
                    SetCardsArea(UnitMergerHelper(faction, null, true));
                    break;
                case Listing.All:
                    SetCardsArea(UnitMergerHelper(faction, null));
                    break;
                case Listing.Close:
                    SetCardsArea(UnitMergerHelper(faction, Row.Close));
                    break;
                case Listing.Range:
                    SetCardsArea(UnitMergerHelper(faction, Row.Range));
                    break;
                case Listing.Siege:
                    SetCardsArea(UnitMergerHelper(faction, Row.Siege));
                    break;
                default:
                    Debug.LogError("ManageCardsArea: Unexpected value: " + listing);
                    break;
            }
        }

        // The units of a faction (and the neutral ones, if shown) for a row (null: all, with the special cards after)
        private List<Card> UnitMergerHelper(Faction fc, Row? rw, bool ishero = false)
        {
            List<Card> returned_list;
            if (toggleBtn.isOn)
            {
                returned_list = deckBuilder.MergedList(
                deckBuilder.GetUnitCards(CardsToDisplay, fc, rw, ishero),
                deckBuilder.GetUnitCards(CardsToDisplay, Faction.Neutral, rw, ishero)
                );
            }
            else
            {
                returned_list = deckBuilder.MergedList(
                deckBuilder.GetUnitCards(CardsToDisplay, fc, rw, ishero)
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

            if (rw == null && !ishero) // Add special Cards to all cards
            {
                List<Card> specials_list = deckBuilder.GetSpecialCards(CardsToDisplay);
                specials_list.Sort((a, b) => a._id.CompareTo(b._id));
                returned_list = deckBuilder.MergedList(returned_list, specials_list);
            }

            return returned_list;
        }

        //----------------------------------------------------Event Handlers---------------------------------------------------//
        // Opens on a faction (the deck's, before the header places itself): its cards and its perk
        public void OpenOn(Faction deck_faction)
        {
            int panel = System.Array.IndexOf(HeaderFactions, deck_faction);
            if (panel < 0)
                return;
            faction = deck_faction;
            if (factionPerk != null)
                factionPerk.text = Perks[panel];
            if (header != null)
                header.startingPanel = panel;
        }

        // Reference: FactionHeader On Panel Changed
        public void UpdateSelectedFaction()
        {
            int panel = header.CurrentPanel;
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
                deckBuilder.my_deck = new Deck { Faction = faction, Name = DeckRules.EmptyDeckName };

                // Change the leader to the default of each faction
                deckBuilder.my_deck.Leader = LeaderIds.DefaultFor(faction);

                // Load the new decklists and get the first deck if not empty
                deckSummary.LoadDecks();
            }
        }

        // The collection counts the copies left to add: it follows the deck (where it is scrolled to stays)
        private void RefreshCollection()
        {
            foreach (CardGrid collection in FindObjectsByType<CardGrid>())
                if (collection != this && collection.CardsToDisplay != null)
                    collection.Select(collection.selectedTab, false);
        }

        // Reference: Toggle On Value Changed
        public void ToggleNeutrals()
        {
            Select(selectedTab, false);
        }

        // DECK CARDS RELATED
        // Changes my_deck and currentDeckPath in DeckBuilder
        public void OnDeckChange(Deck selectedDeck, string selectedPath, bool select_for_battle = true)
        {
            // Replace the loaded deck with the selected one
            deckBuilder.my_deck = selectedDeck;
            deckBuilder.currentDeckPath = selectedPath;
            LoadFromDeck();
            Select(selectedTab);
            RefreshCollection();

            // A valid selected deck becomes the battle deck (the status line follows the deck shown either way)
            if (select_for_battle)
                deckBuilder.UpdateBattleDeck();
            else
                deckBuilder.UpdateBattleDeckStatus();
        }

        // From Double Clicking a card
        public void OnDeckEdit()
        {
            // A preset is never changed: the edit goes to the player's own copy
            if (deckBuilder.ShowsPreset)
            {
                deckBuilder.KeepPresetEdit();
                return;
            }
            LoadFromDeck();
            Select(selectedTab, false);
            RefreshCollection();
            deckSummary.InitializeDeck();

            // Save to the current deck
            deckBuilder.WriteDeckToFile(deckBuilder.my_deck, deckBuilder.currentDeckPath);

            // Becomes (or stops being, if now invalid) the battle deck
            deckBuilder.UpdateBattleDeck();
        }

    }
}

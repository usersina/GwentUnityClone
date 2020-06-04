using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.UI;
using System.IO;
using UnityEngine.SceneManagement;

public enum BattleState { START, PLAYERTURN, ENEMYTURN, ROUNDOVER, WON, LOST, DRAW }

public class SceneController : MonoBehaviour
{
    public BattleState battleState;

    public GameObject PostGame;
    public GameObject CardPrefab;
    public GameObject WeatherField;

    public TextAsset CardsDatabase;
    //public TextAsset PlayerDeck;
    public string PlayerDeck;
    public TextAsset EnemyDeck;

    public GameObject PlayerField;
    public GameObject EnemyField;

    [HideInInspector] // List of all cards (each card has unique stats)
    public List<Card> allCards;

    public MainInfo PlayerInfo;
    public MainInfo EnemyInfo;

    // In-play related
    public GameObject TurnPicker;
    public GameObject Panel;
    public GameObject PassBtn;
    // Medic related
    public GameObject hiderObject;
    public GameObject selectedCard;
    public List<int> weatherList;
    public bool doubleSpies;
    public bool randomMedic;
    public bool disableLeader;
    public bool swapActivated;

    private AIManager AIManager;

    private void Start()
    {
        PlayerDeck = Path.Combine(Application.streamingAssetsPath, "player_default.json");

        // Get Player Deck
        if (!string.IsNullOrEmpty(ApplicationModel.playerDeckPath))
            PlayerDeck = ApplicationModel.playerDeckPath;

        Debug.Log("Deck path from scene controller: " + PlayerDeck);
        PlayerInfo.Name = "Player";
        EnemyInfo.Name = "Opponent";

        // Initialize AI
        AIManager = GetComponent<AIManager>();

        // Setup the battle
        Debug.Log("Starting battle...");
        battleState = BattleState.START;
        SetupBattle();

        Debug.Log("Waiting for all players de redraw cards: ");

        // TODO: Replace with a proper FirstTurnPicker() method which takes Scoiatel faction in mind
        StartCoroutine(FirstTurnPicker());
    }

    void Update()
    {
        if (battleState == BattleState.START)
        {
            if (Input.GetMouseButtonDown(1))
            {
                Debug.Log("Player has skipped cards exchanging !");
                PlayerInfo.CanExchange = false;
            }
        }

        // Disabled the pass button of not the player turn
        if (battleState == BattleState.PLAYERTURN)
        {
            if (PassBtn.GetComponent<PassManager>().isActiveAndEnabled)
                return;
            else
                PassBtn.GetComponent<PassManager>().gameObject.SetActive(true);
        }
        else
        {
            if (!PassBtn.GetComponent<PassManager>().isActiveAndEnabled)
                return;
            else
                PassBtn.GetComponent<PassManager>().gameObject.SetActive(false);
        }


        // TODO: Change image, a bit lame (grass pass crown)
        if (PlayerInfo.hasPassed)
        {
            if (!PlayerField.transform.Find("Stats").Find("Total").Find("Pass").gameObject.activeSelf)
                PlayerField.transform.Find("Stats").Find("Total").Find("Pass").gameObject.SetActive(true);
        }
        if (!PlayerInfo.hasPassed)
        {
            if (PlayerField.transform.Find("Stats").Find("Total").Find("Pass").gameObject.activeSelf)
                PlayerField.transform.Find("Stats").Find("Total").Find("Pass").gameObject.SetActive(false);
        }

        if (EnemyInfo.hasPassed)
        {
            if (!EnemyField.transform.Find("Stats").Find("Total").Find("Pass").gameObject.activeSelf)
                EnemyField.transform.Find("Stats").Find("Total").Find("Pass").gameObject.SetActive(true);
        }
        if (!EnemyInfo.hasPassed)
        {
            if (EnemyField.transform.Find("Stats").Find("Total").Find("Pass").gameObject.activeSelf)
                EnemyField.transform.Find("Stats").Find("Total").Find("Pass").gameObject.SetActive(false);
        }
        //Detected everywhere in the game(even if there is an onclick attached to a gameobject)
        //if (Input.GetMouseButtonDown(0))
        //    Debug.Log("Pressed primary button
    }

    //----------------------------------------------BATTLE STATES HANDLING---------------------------------------------------//
    // On Battle Start: Draw 10 cards then change two cards or not at all
    private void SetupBattle()
    {
        // Initialize all of the card infos
        CardsDB cards_db = JsonUtility.FromJson<CardsDB>(CardsDatabase.text);
        allCards = cards_db.Cards;

        // Setup Player Info
        Deck myDeck = JsonUtility.FromJson<Deck>(File.ReadAllText(PlayerDeck));
        // TODO: Name
        // TODO: Avatar

        // Setup Enemy Info
        Deck oppDeck = JsonUtility.FromJson<Deck>(EnemyDeck.text);
        // TODO: Name
        // TODO: Avatar

        PrepareField(myDeck, PlayerInfo.HandList, true);
        PrepareField(oppDeck, EnemyInfo.HandList, false);
    }

    // Prepares the field for the first time
    private void PrepareField(Deck deck, List<int> hand_list, bool is_player_deck)
    {
        List<int> deck_list;
        Sprite deck_back;
        MainInfo info;

        deck_back = Resources.Load<Sprite>("Cards/Back/" + deck.Faction);
        deck_list = GenerateDecklist(deck);
        deck_list = ShuffleList(deck_list);

        // Set the global variables accordingly for future uses
        if (is_player_deck)
            info = PlayerInfo;
        else
            info = EnemyInfo;

        info.Faction = deck.Faction;
        info.DeckBSprite = deck_back;
        info.DeckList = deck_list;
        info.HandList = hand_list; // Unneeded

        // Sets the stats
        SetInitialStats(info, is_player_deck);

        // Instantiate a card back for each list item and set as child to Player/Deck/DeckBase
        SetDeckObject(deck_list, deck_back, is_player_deck);

        // Sets the leader object at the start of the game
        info.Leader = deck.Leader;
        SetLeaderCard(deck.Leader, is_player_deck);

        if (deck.Leader == 57)
            DrawCards(1, deck_list, hand_list, deck_back, is_player_deck);

        // Add the top 10 Cards to the Player hand
        DrawCards(10, deck_list, hand_list, deck_back, is_player_deck);
        Debug.Log("Double click on a card to redraw or right click to skip");
        // Called from any card in hand on double click

        // DEVONLY:
        //EnemyInfo.CloseList = new List<int> { 18, 19, 20, 94, 94, 94, 126, 128, 129, 127, 125, 77, 83 };
        //EnemyInfo.RangeList = new List<int> { 71, 96, 95, 71};
        //EnemyInfo.SiegeList = new List<int> { 11, 11, 4, 28 };
        //PlayerInfo.CloseList = new List<int> { 138, 29, 138 };
        //PlayerInfo.RangeList = new List<int> { 31, 5, 13 };
    }

    private void ManageOutcome()
    {
        battleState = BattleState.ROUNDOVER;
        int player_total = GetTotalScore(true, "all");
        int enemy_total = GetTotalScore(false, "all");
        string round_winner;

        PlayerInfo.RoundsScore.Add(player_total);
        EnemyInfo.RoundsScore.Add(enemy_total);

        if (player_total > enemy_total)
        {
            EnemyInfo.Lives--;
            round_winner = "player";
        }
        else if (enemy_total > player_total)
        {
            PlayerInfo.Lives--;
            round_winner = "enemy";
        }
        else if (enemy_total == player_total)
        {
            // Draw //-------------Nilfgaard: Automatically win the round that ends in a draw--------------//
            if (PlayerInfo.Faction == "NF" && EnemyInfo.Faction != "NF")
            {
                EnemyInfo.Lives--;
                round_winner = "player";
            }
            else if (EnemyInfo.Faction == "NF" && PlayerInfo.Faction != "NF")
            {
                PlayerInfo.Lives--;
                round_winner = "enemy";
            }
            else // Both Nilfgaard or both are not Nilfgaard
            {
                PlayerInfo.Lives--;
                EnemyInfo.Lives--;
                round_winner = "none";
            }
        }
        else
        {
            Debug.LogError("ManageOutcome(): Unexpected Error !");
            round_winner = "error";
        }

        // Update UI Gems;
        PlayerField.transform.Find("Stats").GetComponent<StatsManager>().UIUpdateGemLives(PlayerInfo.Lives);
        EnemyField.transform.Find("Stats").GetComponent<StatsManager>().UIUpdateGemLives(EnemyInfo.Lives);

        if (PlayerInfo.Lives == 0 && EnemyInfo.Lives == 0)
        {
            Debug.Log("Battle Ended in a draw"); // NF already handled above, unreachable if one NF is playing
            ConcludeBattle("DRAW");
        }
        else if (PlayerInfo.Lives > 0 && EnemyInfo.Lives == 0)
        {
            Debug.Log("Player has won the battle !");
            ConcludeBattle("WON");
        }
        else if (PlayerInfo.Lives == 0 && EnemyInfo.Lives > 0)
        {
            Debug.Log("Enemy has won the battle !");
            ConcludeBattle("LOST");
        }
        else
        {
            PrepareNextRound(round_winner);
        }
    }

    private void PrepareNextRound(string round_winner)
    {
        Debug.Log("Preparing next round");

        List<MainInfo> infos = new List<MainInfo> { PlayerInfo, EnemyInfo };
        // Related to Monsters Faction
        int random_unit = -1;
        bool recovered = false;
        string add_to = "none";

        foreach (MainInfo info in infos)
        {
            bool there_is_cow = false;

            if (info.Faction == "M")
            {
                random_unit = -1;
                List<int> all_monsters = new List<int>(info.CloseList.Count + info.RangeList.Count + info.SiegeList.Count);
                System.Random rnd = new System.Random();
                all_monsters.AddRange(info.CloseList);
                all_monsters.AddRange(info.RangeList);
                all_monsters.AddRange(info.SiegeList);
                if (all_monsters.Count > 0)
                    random_unit = all_monsters[rnd.Next(all_monsters.Count)];
                Debug.Log("Chosen Random: " + random_unit);
            }

            // Clear unit fields
            if (info.CloseList.Count > 0)
                for (int i = 0; i < info.CloseList.Count; i++)
                {
                    int card_id = info.CloseList[i];
                    if (info.Faction == "M" && !recovered)
                    {
                        if (random_unit == card_id)
                        {
                            recovered = true;
                            add_to = "close";
                        }
                    }
                    info.CloseList.Remove(card_id);
                    info.DiscardList.Add(card_id);
                    i--;
                }

            if (info.RangeList.Count > 0)
                for (int i = 0; i < info.RangeList.Count; i++)
                {
                    int card_id = info.RangeList[i];
                    if (card_id == 16)
                        there_is_cow = true;
                    if (info.Faction == "M" && !recovered)
                    {
                        if (random_unit == card_id)
                        {
                            recovered = true;
                            add_to = "range";
                        }
                    }
                    info.RangeList.Remove(card_id);
                    info.DiscardList.Add(card_id);
                    i--;
                }

            if (info.SiegeList.Count > 0)
                for (int i = 0; i < info.SiegeList.Count; i++)
                {
                    int card_id = info.SiegeList[i];
                    if (info.Faction == "M" && !recovered)
                    {
                        if (random_unit == card_id)
                        {
                            recovered = true;
                            add_to = "siege";
                        }
                    }
                    info.SiegeList.Remove(card_id);
                    info.DiscardList.Add(card_id);
                    i--;
                }

            // Clear special fields
            if (info.SpCloseList.Count > 0)
                for (int i = 0; i < info.SpCloseList.Count; i++)
                {
                    int card_id = info.SpCloseList[i];
                    info.SpCloseList.Remove(card_id);
                    info.DiscardList.Add(card_id);
                    i--;
                }
            if (info.SpRangeList.Count > 0)
                for (int i = 0; i < info.SpRangeList.Count; i++)
                {
                    int card_id = info.SpRangeList[i];
                    info.SpRangeList.Remove(card_id);
                    info.DiscardList.Add(card_id);
                    i--;
                }
            if (info.SpSiegeList.Count > 0)
                for (int i = 0; i < info.SpSiegeList.Count; i++)
                {
                    int card_id = info.SpSiegeList[i];
                    info.SpSiegeList.Remove(card_id);
                    info.DiscardList.Add(card_id);
                    i--;
                }

            // Clear weather
            if (weatherList.Count > 0)
            {
                for (int i = 0; i < weatherList.Count; i++)
                {
                    int card_id = weatherList[i];
                    weatherList.Remove(card_id);
                    PlayerInfo.DiscardList.Add(card_id); // Won' be used anyway so can be omited
                }
            }

            //------Monsters: Players with this deck will keep one random unit on the board after each round-------//
            if (random_unit != -1)
            {
                info.DiscardList.Remove(random_unit);
                switch (add_to)
                {
                    case "none":
                        break;
                    case "close":
                        info.CloseList.Add(random_unit);
                        break;
                    case "range":
                        info.RangeList.Add(random_unit);
                        break;
                    case "siege":
                        info.SiegeList.Add(random_unit);
                        break;
                }
            }

            if (there_is_cow)
                info.CloseList.Add(9);
        }

        LogList(PlayerInfo.CloseList, "Player Close");
        LogList(EnemyInfo.CloseList, "Enemy Close");

        LogList(PlayerInfo.DiscardList, "Player Discard");
        LogList(EnemyInfo.DiscardList, "Enemy Discard");

        //------Northern Realms: Get one additional card from your deck every time you win a round-------------//
        switch (round_winner)
        {
            case "none":
                Debug.Log("Draw");
                break;
            case "player":
                if (PlayerInfo.Faction == "NR")
                    DrawCards(1, PlayerInfo.DeckList, PlayerInfo.HandList, PlayerInfo.DeckBSprite, true);
                break;
            case "enemy":
                if (EnemyInfo.Faction == "NR")
                    DrawCards(1, EnemyInfo.DeckList, EnemyInfo.HandList, EnemyInfo.DeckBSprite, true);
                break;
            default:
                Debug.LogError("PrepareNextRound(): Unexpected Error: " + round_winner);
                break;
        }

        UpdateLists();

        RefreshFields(true);
        RefreshFields(false);

        UpdateUIScores(true);
        UpdateUIScores(false);

        // Play next round
        PlayerInfo.hasPassed = false;
        EnemyInfo.hasPassed = false;
        StartCoroutine(FirstTurnPicker());
    }

    private void ConcludeBattle(string outcome)
    {
        Debug.Log("Drawing a conclusion !");
        PostGame.GetComponent<PostGameManager>().ShowScores(PlayerInfo.RoundsScore, EnemyInfo.RoundsScore);
        switch (outcome)
        {
            case "DRAW":
                battleState = BattleState.DRAW;
                PostGame.SetActive(true);
                PostGame.GetComponent<PostGameManager>().ShowDraw();
                break;
            case "WON":
                battleState = BattleState.WON;
                PostGame.SetActive(true);
                PostGame.GetComponent<PostGameManager>().ShowWin();
                break;
            case "LOST":
                battleState = BattleState.LOST;
                PostGame.SetActive(true);
                PostGame.GetComponent<PostGameManager>().ShowLose();
                break;
        }
    }

    // Creates card back prefabs from list -also updates number of cards in deck count, as well as card backs-
    private void SetDeckObject(List<int> deck_list, Sprite deck_back, bool is_player_deck)
    {
        GameObject my_object;
        if (is_player_deck)
            my_object = PlayerField;
        else
            my_object = EnemyField;
        // Updates the UI number as well as cards on top of each other
        my_object.transform.Find("Deck").gameObject.GetComponent<DeckManager>().UIUpdateDeckBase(deck_back, deck_list.Count);
    }

    // Sets the appropirate left stats (PlayerName / DeckName / Avatar)
    private void SetInitialStats(MainInfo info, bool is_player_deck)
    {
        GameObject my_object;
        if (is_player_deck)
            my_object = PlayerField;
        else
            my_object = EnemyField;

        // UI
        my_object.transform.Find("Stats").GetComponent<StatsManager>().UISetGeneral(info.Faction);
        my_object.transform.Find("Stats").GetComponent<StatsManager>().UIUpdateHandCount(info.HandList.Count);
        // TODO: PlayerName, avatar and shield
    }

    // Initial 10 Card Draw
    private void DrawCards(int num, List<int> deck_list, List<int> hand_list, Sprite deck_back, bool is_player_deck)
    {
        if (deck_list.Count < num)
        {
            Debug.LogError("Cannot draw, not enough cards left in hand: " + deck_list.Count);
        }
        else
        {
            for (int i = 0; i < num; i++)
            {
                hand_list.Add(deck_list[i]);
                // Debug.Log("Player hand list length: " + PlayerInfo.HandList.Count);
                // Does this update PlayerInfo.HandList ? Yes, but how
            }
            deck_list.RemoveRange(0, num);
            SetDeckObject(deck_list, deck_back, is_player_deck);
            SetHandObject(hand_list, is_player_deck);
        }
    }

    // Create Hand Cards from myHandList and updates card count (in Stats/HandCount)
    public void SetHandObject(List<int> hand_list, bool is_player_hand)
    {
        GameObject my_object;
        if (is_player_hand)
            my_object = PlayerField;
        else
            my_object = EnemyField;

        // Clear Hand Object First
        my_object.transform.Find("Hand").gameObject.GetComponent<HandManager>().ClearHandObject();
        // Create cards and place them in Field.Hand Gameobject as children
        CreateCardObjects(my_object.transform.Find("Hand").gameObject, hand_list, true, is_player_hand);
        my_object.transform.Find("Hand").gameObject.GetComponent<HandManager>().ResizeHand();
        // Updates the UI
        my_object.transform.Find("Stats").gameObject.GetComponent<StatsManager>().UIUpdateHandCount(hand_list.Count);
    }

    // Sets the discard Object
    private void SetDiscardObject(List<int> discard_list, bool is_player_hand)
    {
        GameObject my_object;
        if (is_player_hand)
            my_object = PlayerField;
        else
            my_object = EnemyField;

        my_object.transform.Find("Discard").GetComponent<PileManager>().ClearDiscardPile();
        CreateCardObjects(my_object.transform.Find("Discard").Find("Pile").gameObject, discard_list, false, is_player_hand);
    }

    // Picks the first turn
    IEnumerator FirstTurnPicker()
    {
        if (EnemyInfo.CanExchange)
        {
            // Initialize AI (first turn only)
            AIManager.AIInitialize();
            AIManager.AIExchangeCards();
        }

        // Wait for all players to either redraw or skip
        while (EnemyInfo.CanExchange || PlayerInfo.CanExchange)
        {
            yield return null;
        }

        //------------Scoia'tel: Player with this deck chooses who begins each round-------------//
        if (PlayerInfo.Faction == "SC" && EnemyInfo.Faction != "SC")
        {
            // Show the turn picker
            TurnPicker.SetActive(true);
        }
        else if (PlayerInfo.Faction != "SC" && EnemyInfo.Faction == "SC")
        {
            // Enemy chooses who goes first
            StartCoroutine(PlayerTurn());
        }
        else
        {
            // TODO: Rock paper scissors maybe ?
            int picker = new System.Random().Next(0, 2);
            if (picker == 0)
                StartCoroutine(EnemyTurn());
            else
                StartCoroutine(PlayerTurn());
        }
    }

    public IEnumerator PlayerTurn()
    {
        if (PlayerInfo.hasPassed)
        {
            CheckRoundEnd();
            yield break;
        }
        yield return new WaitForSeconds(1f);
        Debug.Log("Player Turn !");
        battleState = BattleState.PLAYERTURN;
    }

    public IEnumerator EnemyTurn()
    {
        if (EnemyInfo.hasPassed)
        {
            CheckRoundEnd();
            yield break;
        }
        battleState = BattleState.ENEMYTURN;
        Debug.Log("Enemey Turn !");
        yield return new WaitForSeconds(2f); // Order of this is essential
        AIManager.AIStartTurn();
        //StartCoroutine(PlayerTurn()); Turn is started from directly place card (enemy)
    }

    public void CheckRoundEnd()
    {
        if (PlayerInfo.hasPassed && EnemyInfo.hasPassed)
        {
            // Both passed, round has ended
            battleState = BattleState.ROUNDOVER;
            Debug.Log("Round has ended");

            // Either PrepareNextRound() or ConcludeBattle(); // Depending on Lives
            PlayerInfo.hasPassed = false;
            EnemyInfo.hasPassed = false;
            ManageOutcome();
        }
        else if (PlayerInfo.hasPassed && !EnemyInfo.hasPassed)
        {
            StartCoroutine(EnemyTurn());
        }
        else if (!PlayerInfo.hasPassed && EnemyInfo.hasPassed)
        {
            StartCoroutine(PlayerTurn());
        }
    }

    // Highlights the field based on the selected card's stats
    // And stores the highlighted field if any in a gameobject list
    public void HighlightField(CardStats card_stats, bool is_player_turn)
    {
        //Debug.Log("Highlighting field of type: " + card_stats.faction + " with a row: " + card_stats.row);
        GameObject my_object;
        GameObject opp_object;
        // Playerturn: Hightlight player field
        if (is_player_turn)
        {
            my_object = PlayerField;
            opp_object = EnemyField;
        }
        else
        {
            my_object = EnemyField;
            opp_object = PlayerField;
        }


        // Reset any previously stored fields
        my_object.GetComponent<RowPicker>().ResetStored();
        WeatherField.GetComponent<WeatherManager>().isWeatherCard = false;

        switch (card_stats.faction)
        {
            case "Special":
                // Further check if weather card or special card
                if (card_stats.row == "special")
                {
                    // Highlights all of the three specials
                    Sprite targetSprite = my_object.GetComponent<RowPicker>().specialSelected;
                    my_object.transform.Find("Close").Find("Special").GetComponent<Image>().sprite = targetSprite;
                    my_object.transform.Find("Range").Find("Special").GetComponent<Image>().sprite = targetSprite;
                    my_object.transform.Find("Siege").Find("Special").GetComponent<Image>().sprite = targetSprite;

                    // Store all highlighted in the GO list from RowPicker
                    my_object.GetComponent<RowPicker>().StoreField(my_object.transform.Find("Close").Find("Special").gameObject);
                    my_object.GetComponent<RowPicker>().StoreField(my_object.transform.Find("Range").Find("Special").gameObject);
                    my_object.GetComponent<RowPicker>().StoreField(my_object.transform.Find("Siege").Find("Special").gameObject);
                }
                else // A "weather" card or a "one_time" card_row effect
                {
                    // Highlights the weather board
                    WeatherField.GetComponent<Image>().sprite = WeatherField.GetComponent<WeatherManager>().weatherSelected;
                    WeatherField.GetComponent<WeatherManager>().isWeatherCard = true;
                }
                break;
            default: // other than Special, "NR", "NF"... meaning a unit card
                if (card_stats.ability == "spy")
                {
                    // Spy, highlight opponent object instead !
                    //Debug.Log("SPY DETECTED: " + card_stats.name);
                    my_object = opp_object;
                }
                switch (card_stats.row)
                {
                    case "close":
                        Sprite targetSprite1 = my_object.GetComponent<RowPicker>().closeSelected;
                        my_object.transform.Find("Close").Find("Unit").GetComponent<Image>().sprite = targetSprite1;
                        my_object.GetComponent<RowPicker>().StoreField(my_object.transform.Find("Close").Find("Unit").gameObject);
                        break;
                    case "range":
                        Sprite targetSprite2 = my_object.GetComponent<RowPicker>().rangeSelected;
                        my_object.transform.Find("Range").Find("Unit").GetComponent<Image>().sprite = targetSprite2;
                        my_object.GetComponent<RowPicker>().StoreField(my_object.transform.Find("Range").Find("Unit").gameObject);
                        break;
                    case "siege":
                        Sprite targetSprite3 = my_object.GetComponent<RowPicker>().siegeSelected;
                        my_object.transform.Find("Siege").Find("Unit").GetComponent<Image>().sprite = targetSprite3;
                        my_object.GetComponent<RowPicker>().StoreField(my_object.transform.Find("Siege").Find("Unit").gameObject);
                        break;
                    case "close_range":
                        Sprite targetSprite4 = my_object.GetComponent<RowPicker>().closeSelected;
                        Sprite targetSprite5 = my_object.GetComponent<RowPicker>().rangeSelected;
                        my_object.transform.Find("Close").Find("Unit").GetComponent<Image>().sprite = targetSprite4;
                        my_object.transform.Find("Range").Find("Unit").GetComponent<Image>().sprite = targetSprite5;
                        my_object.GetComponent<RowPicker>().StoreField(my_object.transform.Find("Close").Find("Unit").gameObject);
                        my_object.GetComponent<RowPicker>().StoreField(my_object.transform.Find("Range").Find("Unit").gameObject);
                        break;
                }

                break;
        }
    }

    // Lights off any highlighted field (close_range)
    public void LightoffField(bool is_player_turn) // Playdown (!Highligh) is misleading
    {
        GameObject my_object;
        if (is_player_turn)
            my_object = PlayerField;
        else
            my_object = EnemyField;

        // Reset any previously stored fields
        my_object.GetComponent<RowPicker>().ResetStored();

        // Restores special sprite
        Sprite specialSprite = my_object.GetComponent<RowPicker>().special;
        my_object.transform.Find("Close").Find("Special").GetComponent<Image>().sprite = specialSprite;
        my_object.transform.Find("Range").Find("Special").GetComponent<Image>().sprite = specialSprite;
        my_object.transform.Find("Siege").Find("Special").GetComponent<Image>().sprite = specialSprite;

        // Restore unit sprite
        Sprite unitSprite = my_object.GetComponent<RowPicker>().close;
        my_object.transform.Find("Close").Find("Unit").GetComponent<Image>().sprite = unitSprite;
        unitSprite = my_object.GetComponent<RowPicker>().range;
        my_object.transform.Find("Range").Find("Unit").GetComponent<Image>().sprite = unitSprite;
        unitSprite = my_object.GetComponent<RowPicker>().siege;
        my_object.transform.Find("Siege").Find("Unit").GetComponent<Image>().sprite = unitSprite;

        // Light off the weather board and deselect weather card
        WeatherField.GetComponent<Image>().sprite = WeatherField.GetComponent<WeatherManager>().weather;
        WeatherField.GetComponent<WeatherManager>().isWeatherCard = false;
    }

    // Directly places a specific card_go on a field_go
    public void DirectlyPlaceCard(GameObject card_go, GameObject field_go, bool is_player_turn)
    {
        // The tests are to make sure the appropriate lists gets updated
        // as well as the correct list is being passed to CreateCardObject()
        // which instantiates all cards of a card_id list after clearing
        // the previous children of the gameobject

        MainInfo my_info;
        MainInfo opp_info;

        if (is_player_turn)
        {
            my_info = PlayerInfo;
            opp_info = EnemyInfo;
        }
        else
        {
            my_info = EnemyInfo;
            opp_info = PlayerInfo;
        }

        CardStats card_stats = card_go.GetComponent<CardDisplay>().cardStats.GetComponent<CardStats>();
        bool waitMedic = false;
        bool isScorch = false;
        bool isScorchAbility = false;

        string card_type;
        if (card_stats.faction == "Special") card_type = "special"; else card_type = "unit";
        string card_row = card_stats.row;
        string field_row = field_go.transform.parent.name.ToLower();
        //Debug.Log("Card type is: " + card_type);
        //Debug.Log("Card row is: " + card_row);
        //Debug.Log("Field row is: " + field_row);
        if (card_row == "close_range")
            card_row = field_row;

        // Remove From Hand
        my_info.HandList.Remove(card_stats._id);

        // Play Card
        if (card_type == "unit")
        {
            // Abilities related to adding to list
            if (card_stats.ability == "spy")
            {
                // my_object(spy user) draws two cards
                DrawCards(2, my_info.DeckList, my_info.HandList, my_info.DeckBSprite, is_player_turn);

                // Swap the objects
                my_info = opp_info;
            }
            else if (card_stats.ability == "muster")
            {
                Muster(card_stats, card_row, is_player_turn);
            }
            else if (card_stats.ability == "medic")
            {
                if (randomMedic && !card_stats.unique)
                    RndMedic(is_player_turn);
                else
                    waitMedic = Medic(is_player_turn);
            }
            else if (card_stats.ability.Contains("scorch"))
            {
                isScorchAbility = true;
            }
            switch (card_row)
            {
                case "close":
                    // Updates the list
                    my_info.CloseList.Add(card_stats._id);
                    break;
                case "range":
                    my_info.RangeList.Add(card_stats._id);
                    break;
                case "siege":
                    my_info.SiegeList.Add(card_stats._id);
                    break;
            }
        }
        else // card_type == "special"
        {
            switch (card_row)
            {
                case "special":
                    switch (field_row)
                    {
                        case "close":
                            if (!my_info.SpCloseList.Any(item => item == card_stats._id))
                                my_info.SpCloseList.Add(card_stats._id); // If not exists on the list, add it
                            else
                                my_info.DiscardList.Add(card_stats._id); // Else add to discard
                            break;
                        case "range":
                            if (!my_info.SpRangeList.Any(item => item == card_stats._id))
                                my_info.SpRangeList.Add(card_stats._id);
                            else
                                my_info.DiscardList.Add(card_stats._id);
                            break;
                        case "siege":
                            if (!my_info.SpSiegeList.Any(item => item == card_stats._id))
                                my_info.SpSiegeList.Add(card_stats._id);
                            else
                                my_info.DiscardList.Add(card_stats._id);
                            break;
                    }
                    break;
                case "weather":
                    // Weather card is selected and and weather field is clicked
                    if (!weatherList.Any(item => item == card_stats._id))
                        weatherList.Add(card_stats._id);
                    else
                        my_info.DiscardList.Add(card_stats._id);
                    // Clear Weather Effect
                    if (card_stats.ability == "clear_weather")
                    {
                        weatherList.Clear(); // EZ
                        my_info.DiscardList.Add(card_stats._id);
                    }
                    break;
                case "one_time":
                    // "Not placing any cards";
                    if (card_stats._idstr == "scorch")
                        isScorch = true;
                    else if (card_stats._idstr == "decoy")
                        swapActivated = true;
                    else
                        Debug.LogError("Unexpected error placing card_id: " + card_stats._id);
                    my_info.DiscardList.Add(card_stats._id);
                    break;
            }
        }

        // Updates the lists for both enemy and player
        // Also apply any modifiers (morale boost / commander horn / weather )
        UpdateLists();

        // Scorch AFTER modifiers
        if (isScorch)
            Scorch();
        if (isScorchAbility)
            ScorchAbility(card_row, is_player_turn);

        // Just to be sure
        UpdateLists();

        // Refresh the enemy and the player fields from the updated lists
        RefreshFields(false);
        RefreshFields(true);

        // Lights off the enemy and the player fields
        LightoffField(true);
        LightoffField(false);

        // Updates the UI scores of both player and enemy
        UpdateUIScores(true);
        UpdateUIScores(false);

        // Hider the hider to allow user to use his hand
        // This is used incase a medic ability is activated
        // to disable user from picking another card
        SetHider(false);

        // FIXME: WILL BREAK WITH AI USING MEDIC CARD (add a break to skip)
        // If medic is activated, for another card's placing
        if (waitMedic)
        {
            SetHider(true);
            // TODO: AI FIX
            return;
        }

        // FIXME: WILL BREAK WITH AI USING DECOY (add a break to skip)
        // If decoy is activated, for card swapping
        if (swapActivated && CheckDecoyable(is_player_turn))
        {
            // Continue swap
            SetHider(true);
            // TODO: AI FIX
            return;
        }
        else
            swapActivated = false;

        //Debug.Log("Card placed successfully! ");
        if (is_player_turn)
            StartCoroutine(EnemyTurn());
        else
            StartCoroutine(PlayerTurn());
    }

    // Reference: PassManager event (hold to pass button)
    public void SkipTurn(bool is_player_turn)
    {
        MainInfo info;
        if (is_player_turn)
            info = PlayerInfo;
        else
            info = EnemyInfo;

        info.hasPassed = true;
        TurnCallBack(is_player_turn);
    }

    public void TurnCallBack(bool is_player_turn)
    {
        if (is_player_turn)
            StartCoroutine(EnemyTurn());
        else
            StartCoroutine(PlayerTurn());
    }

    //-----------------------------------------------Classes and Json Related--------------------------------------------//
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

    public class CardsDB
    {
        public List<Card> Cards = new List<Card>();
    }

    private class Deck
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

    [Serializable]
    public class MainInfo
    {
        public string Name;
        public Image Avatar;

        public Sprite DeckBSprite;
        public string Faction;
        public int Leader;
        public bool canLeader = true;
        public List<int> DeckList = new List<int>();     // List of card_ids in deck
        public List<int> HandList = new List<int>();     // List of card_ids in hand
        public List<int> DiscardList = new List<int>();  // List of card_ids in discard

        public List<int> SpCloseList = new List<int>();   // List of card_ids in Special Close Field
        public List<int> SpRangeList = new List<int>();
        public List<int> SpSiegeList = new List<int>();

        // List of Unit Cards on the field (Length L)
        public List<int> CloseList = new List<int>();
        public List<int> RangeList = new List<int>();
        public List<int> SiegeList = new List<int>();

        // List of Original Strengths respectively (Length L)
        public List<int> CloseStrengthL = new List<int>();
        public List<int> RangeStrengthL = new List<int>();
        public List<int> SiegeStrengthL = new List<int>();

        // List of Modified Strengths respectively (Length L)
        public List<int> MCloseStrength = new List<int>();
        public List<int> MRangeStrength = new List<int>();
        public List<int> MSiegeStrength = new List<int>();

        public bool CanExchange = true;
        public int CardsExchanged = 0;

        public bool CanDiscard = false;
        public int CardsDiscarded = 0;

        public bool hasPassed = false;
        public int Lives = 2;
        public List<int> RoundsScore = new List<int>(3);
    }

    //-----------------------------------------Generate Deck, Exchange / Discard---------------------------------//
    // Returns a List of integers containing the card ids
    private List<int> GenerateDecklist(Deck deck)
    {
        List<int> newDeck = new List<int>();
        foreach (int s in deck.Cards)
        {
            newDeck.Add(s);
        }
        return newDeck;
    }

    //Shuffles a List of a T type
    private List<int> ShuffleList(List<int> my_list)
    {
        return my_list.OrderBy(x => Guid.NewGuid()).ToList();
    }

    // Exchange a card with another from deck (shuffle card back into the deck then draw)
    public void ExchangeCard(int card_id, bool is_player_card)
    {
        MainInfo info;
        if (is_player_card)
            info = PlayerInfo;
        else
            info = EnemyInfo;

        //Debug.Log("Player exchanging: " + card_id);
        info.DeckList.Add(card_id);
        info.HandList.Remove(card_id);

        info.DeckList = ShuffleList(info.DeckList);

        info.HandList.Add(info.DeckList[0]);
        info.DeckList.RemoveAt(0);

        SetDeckObject(info.DeckList, info.DeckBSprite, is_player_card);
        SetHandObject(info.HandList, is_player_card);
    }

    // Discard 
    public void DiscardCard(int card_id, bool is_player_card)
    {
        MainInfo info;
        if (is_player_card)
            info = PlayerInfo;
        else
            info = EnemyInfo;

        info.HandList.Remove(card_id);
        info.DiscardList.Add(card_id);
        SetHandObject(info.HandList, is_player_card);
        SetDiscardObject(info.DiscardList, is_player_card);

        Debug.Log("Discarding from CardDiscard");
    }

    //---------------------------------------------Helper Methods------------------------------------------------//
    // Hides the player's hand interactability
    public void SetHider(bool hidden)
    {
        if (hidden)
            hiderObject.transform.localPosition = new Vector2(0, -461);
        else
            hiderObject.transform.localPosition = new Vector2(0, -618);
    }

    // Checks if the player's field permits a decoy
    private bool CheckDecoyable(bool is_player_field)
    {
        bool decoyable = true;
        MainInfo info;

        if (is_player_field)
            info = PlayerInfo;
        else
            info = EnemyInfo;

        // They use the same list
        List<int> close_list = GetMedicList(info.CloseList);
        List<int> range_list = GetMedicList(info.RangeList);
        List<int> siege_list = GetMedicList(info.SiegeList);

        if (close_list.Count == 0 && range_list.Count == 0 && siege_list.Count == 0)
            decoyable = false;

        return decoyable;
    }

    // Gets unit cards from a list (no hero or special cards)
    private List<int> GetMedicList(List<int> my_list)
    {
        List<int> returned_list = new List<int>();
        List<Card> card_list = GetCardsList(my_list);
        foreach (Card card in card_list)
        {
            if (card.faction != "Special" && !card.unique)
                returned_list.Add(card._id);
        }
        return returned_list;
    }

    // Gets weather cards from a list
    private List<int> GetWeatherList(List<int> my_list, string row)
    {
        List<int> returned_list = new List<int>();
        List<Card> card_list = GetCardsList(my_list);
        foreach (Card card in card_list)
        {
            switch (row)
            {
                case "all":
                    if (card.row == "weather" && card.ability != "clear_weather")
                        returned_list.Add(card._id);
                    break;
                case "close":
                    if (card.row == "weather" && card.ability == "reduce_close")
                        returned_list.Add(card._id);
                    break;
                case "range":
                    if (card.row == "weather" && card.ability == "reduce_range")
                        returned_list.Add(card._id);
                    break;
                case "siege":
                    if (card.row == "weather" && card.ability == "reduce_siege")
                        returned_list.Add(card._id);
                    break;
            }
        }
        return returned_list;
    }

    // Get Card Stats from a card id
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

    // Returns a list of cards from a list of card ids
    public List<Card> GetCardsList(List<int> card_list)
    {
        List<Card> returned_list = new List<Card>();
        foreach (int card_id in card_list)
            returned_list.Add(GetCardStats(card_id));
        return returned_list;
    }

    // "vampire_something","_" => "vampire"
    public string TrimToChar(string my_string, char ch)
    {
        int index = my_string.IndexOf(ch);
        if (index > 0)
            my_string = my_string.Substring(0, index);
        return my_string;
    }

    // Number of occurences of an int in a list
    private int OccurenceNum(List<int> my_list, int num)
    {
        int occ = 0;
        for (int i = 0; i < my_list.Count; i++)
            if (my_list[i] == num)
                occ++;
        return occ;
    }

    // Generate Cards GameObject from cards prefab and set their parent to the passed gameobject
    private void CreateCardObjects(GameObject my_object, List<int> card_list, bool isSelectable, bool is_player_card)
    {
        List<Card> my_cards = GetCardsList(card_list);
        foreach (Card card in my_cards)
        {
            GameObject instantiatedCard = Instantiate(CardPrefab);
            instantiatedCard.name = card._id.ToString();
            instantiatedCard.transform.Find("Stats").GetComponent<CardStats>().name = card.name;
            instantiatedCard.transform.Find("Stats").GetComponent<CardStats>()._id = card._id;
            instantiatedCard.transform.Find("Stats").GetComponent<CardStats>()._idstr = card._idstr;
            instantiatedCard.transform.Find("Stats").GetComponent<CardStats>().faction = card.faction;
            instantiatedCard.transform.Find("Stats").GetComponent<CardStats>().unique = card.unique;
            instantiatedCard.transform.Find("Stats").GetComponent<CardStats>().strength = card.strength;
            instantiatedCard.transform.Find("Stats").GetComponent<CardStats>().row = card.row;
            instantiatedCard.transform.Find("Stats").GetComponent<CardStats>().ability = card.ability;
            if (is_player_card)
                instantiatedCard.tag = "Player";
            else
                instantiatedCard.tag = "Enemy";

            instantiatedCard.GetComponent<CardSelect>().isSelectable = isSelectable;
            instantiatedCard.GetComponent<CardHover>().isHoverable = isSelectable;

            if (card.ability == "leader")
                instantiatedCard.GetComponent<CardExchange>().isExchangeable = false;
            else
                instantiatedCard.GetComponent<CardExchange>().isExchangeable = true;

            // Set the parent to the gameObject my_object
            instantiatedCard.transform.SetParent(my_object.transform, false);
        }
    }

    // Updates the gameplay lists for both player and enemy
    private void UpdateLists()
    {
        List<MainInfo> infos = new List<MainInfo>() { PlayerInfo, EnemyInfo };
        foreach (MainInfo info in infos)
        {
            info.CloseStrengthL = GetUnitsStrength(info.CloseList);
            info.RangeStrengthL = GetUnitsStrength(info.RangeList);
            info.SiegeStrengthL = GetUnitsStrength(info.SiegeList);

            // Update Strengths List for close, range and siege if necessary
            // Weather Mods
            info.MCloseStrength = ApplyWeatherMod(info, "close");
            info.MRangeStrength = ApplyWeatherMod(info, "range");
            info.MSiegeStrength = ApplyWeatherMod(info, "siege");

            // Apply Tight Bond for any cards
            info.MCloseStrength = ApplyTightBond(info, "close");
            info.MRangeStrength = ApplyTightBond(info, "range");
            info.MSiegeStrength = ApplyTightBond(info, "siege");

            // Morale Boost Goes Here
            info.MCloseStrength = ApplyMoraleBoost(info, "close");
            info.MRangeStrength = ApplyMoraleBoost(info, "range");
            info.MSiegeStrength = ApplyMoraleBoost(info, "siege");

            // Monster Double Strength Goes here
            info.MCloseStrength = ApplyUnitDouble(info, "close");
            info.MRangeStrength = ApplyUnitDouble(info, "range");
            info.MSiegeStrength = ApplyUnitDouble(info, "siege");

            // Commander horn mods
            info.MCloseStrength = ApplyCommanderHorn(info, "close");
            info.MRangeStrength = ApplyCommanderHorn(info, "range");
            info.MSiegeStrength = ApplyCommanderHorn(info, "siege");

            // Apply Eredin Double Spy (leader)
            if (doubleSpies)
            {
                info.MCloseStrength = ApplyDoubleSpy(info, "close");
                info.MRangeStrength = ApplyDoubleSpy(info, "range");
                info.MSiegeStrength = ApplyDoubleSpy(info, "siege");
            }
        }
    }

    // Regenerate objects from the lists
    // TODO: TEST THOUROUGHLY
    private void RefreshFields(bool is_player_field)
    {
        MainInfo my_info;
        GameObject my_object;
        if (is_player_field)
        {
            my_info = PlayerInfo;
            my_object = PlayerField;
        }

        else
        {
            my_info = EnemyInfo;
            my_object = EnemyField;
        }

        // Refresh Hand Object (or simply SetHandObject(my_info.HandList, is_player_field);)
        my_object.transform.Find("Hand").gameObject.GetComponent<HandManager>().ClearHandObject();
        CreateCardObjects(my_object.transform.Find("Hand").gameObject, my_info.HandList, true, is_player_field);
        my_object.transform.Find("Hand").gameObject.GetComponent<HandManager>().ResizeHand();

        //------------------------Refresh Units Objects
        // Refresh Close
        my_object.transform.Find("Close").GetComponent<RowManager>().ClearUnitObject();
        CreateCardObjects(my_object.transform.Find("Close").Find("Unit").gameObject, my_info.CloseList, false, is_player_field);
        my_object.transform.Find("Close").GetComponent<RowManager>().UpdateRowScore(my_info.CloseStrengthL);
        my_object.transform.Find("Close").GetComponent<RowManager>().ResizeUnitObject(my_info.CloseList.Count);
        // Refresh Range
        my_object.transform.Find("Range").GetComponent<RowManager>().ClearUnitObject();
        CreateCardObjects(my_object.transform.Find("Range").Find("Unit").gameObject, my_info.RangeList, false, is_player_field);
        my_object.transform.Find("Range").GetComponent<RowManager>().UpdateRowScore(my_info.RangeStrengthL);
        my_object.transform.Find("Range").GetComponent<RowManager>().ResizeUnitObject(my_info.RangeList.Count);
        // Refresh Siege
        my_object.transform.Find("Siege").GetComponent<RowManager>().ClearUnitObject();
        CreateCardObjects(my_object.transform.Find("Siege").Find("Unit").gameObject, my_info.SiegeList, false, is_player_field);
        my_object.transform.Find("Siege").GetComponent<RowManager>().UpdateRowScore(my_info.SiegeStrengthL);
        my_object.transform.Find("Siege").GetComponent<RowManager>().ResizeUnitObject(my_info.SiegeList.Count);

        //-----------------------Refresh Special Objects
        // Refresh Close
        my_object.transform.Find("Close").GetComponent<RowManager>().ClearSpecialObject();
        CreateCardObjects(my_object.transform.Find("Close").Find("Special").gameObject, my_info.SpCloseList, false, is_player_field);
        // Refresh Close
        my_object.transform.Find("Range").GetComponent<RowManager>().ClearSpecialObject();
        CreateCardObjects(my_object.transform.Find("Range").Find("Special").gameObject, my_info.SpRangeList, false, is_player_field);
        // Refresh Close
        my_object.transform.Find("Siege").GetComponent<RowManager>().ClearSpecialObject();
        CreateCardObjects(my_object.transform.Find("Siege").Find("Special").gameObject, my_info.SpSiegeList, false, is_player_field);

        //-----------------------Refresh Discard Object
        my_object.transform.Find("Discard").GetComponent<PileManager>().ClearDiscardPile();
        CreateCardObjects(my_object.transform.Find("Discard").Find("Pile").gameObject, my_info.DiscardList, false, is_player_field);

        //-----------------------Refresh Weather Field
        WeatherField.GetComponent<WeatherManager>().ClearWeatherObject();
        CreateCardObjects(WeatherField, weatherList, false, is_player_field);

    }

    // Updates the UI scores
    private void UpdateUIScores(bool is_player_field)
    {
        MainInfo my_info;
        GameObject my_object;
        if (is_player_field)
        {
            my_info = PlayerInfo;
            my_object = PlayerField;
        }

        else
        {
            my_info = EnemyInfo;
            my_object = EnemyField;
        }

        // Updates the individual score of each card item under the unit
        // IMPORTANT: Others UI elements don't need a coroutine because
        // their respective gameobjects are not being destroyed
        StartCoroutine(my_object.transform.Find("Close").GetComponent<RowManager>().UIUpdateUnitsStrength(my_info.CloseStrengthL, my_info.MCloseStrength));
        StartCoroutine(my_object.transform.Find("Range").GetComponent<RowManager>().UIUpdateUnitsStrength(my_info.RangeStrengthL, my_info.MRangeStrength));
        StartCoroutine(my_object.transform.Find("Siege").GetComponent<RowManager>().UIUpdateUnitsStrength(my_info.SiegeStrengthL, my_info.MSiegeStrength));

        // Updates the unit row score
        my_object.transform.Find("Close").GetComponent<RowManager>().UpdateRowScore(my_info.MCloseStrength);
        my_object.transform.Find("Range").GetComponent<RowManager>().UpdateRowScore(my_info.MRangeStrength);
        my_object.transform.Find("Siege").GetComponent<RowManager>().UpdateRowScore(my_info.MSiegeStrength);

        // Updates the total score
        my_object.transform.Find("Stats").GetComponent<StatsManager>().UIUpdateTotalScore(my_info.MCloseStrength, my_info.MRangeStrength, my_info.MSiegeStrength);

        // Updates the deck count
        SetDeckObject(my_info.DeckList, my_info.DeckBSprite, is_player_field);

        // Update the left stats
        // Hand Counter
        my_object.transform.Find("Stats").gameObject.GetComponent<StatsManager>().UIUpdateHandCount(my_info.HandList.Count);
    }

    // Gets the row total or the field total of a player
    public int GetTotalScore(bool is_player, string row)
    {
        MainInfo info;
        int total = 0;

        if (is_player)
            info = PlayerInfo;
        else
            info = EnemyInfo;

        switch (row)
        {
            case "all":
                foreach (int score in info.MCloseStrength)
                    total += score;
                foreach (int score in info.MRangeStrength)
                    total += score;
                foreach (int score in info.MSiegeStrength)
                    total += score;
                break;
            case "close":
                foreach (int score in info.MCloseStrength)
                    total += score;
                break;
            case "range":
                foreach (int score in info.MRangeStrength)
                    total += score;
                break;
            case "siege":
                foreach (int score in info.MSiegeStrength)
                    total += score;
                break;
        }
        return total;
    }

    //---------------------------------------------------Modifiers and card effects handlers---------------------------------------------//
    //--------------------Strength Modifiers--------------------------//
    // Returns a list of unit strengths from a list of unit
    // FOR PLACED CARDS ONLY (unit_row is where a card is placed "close","range","siege")
    private List<int> GetUnitsStrength(List<int> units_list)
    {// For unit cards exclusively
        List<int> strength_list = new List<int>();
        List<Card> cards_list = GetCardsList(units_list);
        foreach (Card card in cards_list)
        {
            strength_list.Add(card.strength);
        }
        return strength_list;
    }

    // Applies any weather modifications if any
    private List<int> ApplyWeatherMod(MainInfo info, string units_row)
    {
        List<int> modified_strengths = new List<int>();
        List<Card> card_list;
        int i;
        switch (units_row)
        {
            case "close":
                i = 0;
                card_list = GetCardsList(info.CloseList);
                foreach (Card card in card_list)
                {
                    if (weatherList.Contains(200)) // Biting Frost
                    {
                        if (card.unique | info.CloseStrengthL[i] <= 1)
                            modified_strengths.Add(info.CloseStrengthL[i]);
                        else
                            modified_strengths.Add(1);
                    }
                    else
                        modified_strengths.Add(info.CloseStrengthL[i]);
                    i++;
                }
                break;
            case "range":
                i = 0;
                card_list = GetCardsList(info.RangeList);
                foreach (Card card in card_list)
                {
                    if (weatherList.Contains(204)) // Impenetrable Fog
                    {
                        if (card.unique | info.RangeStrengthL[i] <= 1)
                            modified_strengths.Add(info.RangeStrengthL[i]);
                        else
                            modified_strengths.Add(1);
                    }
                    else
                        modified_strengths.Add(info.RangeStrengthL[i]);
                    i++;
                }
                break;
            case "siege":
                i = 0;
                card_list = GetCardsList(info.SiegeList);
                foreach (Card card in card_list)
                {
                    if (weatherList.Contains(206)) // Torrential Rain
                    {
                        if (card.unique | info.SiegeStrengthL[i] <= 1)
                            modified_strengths.Add(info.SiegeStrengthL[i]);
                        else
                            modified_strengths.Add(1);
                    }
                    else
                        modified_strengths.Add(info.SiegeStrengthL[i]);
                    i++;
                }
                break;
        }
        return modified_strengths;
    }

    // Doubles the strength of rows if not unique
    private List<int> ApplyCommanderHorn(MainInfo my_info, string units_row)
    {
        List<int> modified_strengths = new List<int>();
        List<Card> card_list;
        int i;
        switch (units_row)
        {
            case "close":
                i = 0;
                card_list = GetCardsList(my_info.CloseList);
                foreach (Card card in card_list)
                {
                    if (my_info.SpCloseList.Contains(202)) // Close list contains Commander Horn
                    {
                        if (card.unique)
                            modified_strengths.Add(my_info.MCloseStrength[i]);
                        else
                            modified_strengths.Add(my_info.MCloseStrength[i] * 2);
                    }
                    else
                        modified_strengths.Add(my_info.MCloseStrength[i]);
                    i++;
                }
                break;
            case "range":
                i = 0;
                card_list = GetCardsList(my_info.RangeList);
                foreach (Card card in card_list)
                {
                    if (my_info.SpRangeList.Contains(202)) // Range list contains Commander Horn
                    {
                        if (card.unique)
                            modified_strengths.Add(my_info.MRangeStrength[i]);
                        else
                            modified_strengths.Add(my_info.MRangeStrength[i] * 2);
                    }
                    else
                        modified_strengths.Add(my_info.MRangeStrength[i]);
                    i++;
                }
                break;
            case "siege":
                i = 0;
                card_list = GetCardsList(my_info.SiegeList);
                foreach (Card card in card_list)
                {
                    if (my_info.SpSiegeList.Contains(202)) // Siege list contains Commander Horn
                    {
                        if (card.unique)
                            modified_strengths.Add(my_info.MSiegeStrength[i]);
                        else
                            modified_strengths.Add(my_info.MSiegeStrength[i] * 2);
                    }
                    else
                        modified_strengths.Add(my_info.MSiegeStrength[i]);
                    i++;
                }
                break;
        }
        return modified_strengths;
    }

    // Doens't need a return method due to list assignment:
    // modified_strengths = info.MCloseStrength;
    private List<int> ApplyMoraleBoost(MainInfo info, string units_row)
    {
        List<int> modified_strengths = new List<int>();
        List<int> morales = new List<int>();
        List<Card> card_list;
        int i;
        switch (units_row)
        {
            case "close":
                // 1: Look for morale boost cards an put their indexes in a list
                card_list = GetCardsList(info.CloseList);
                morales.Clear();
                i = 0;
                foreach (Card card in card_list)
                {
                    if (card.ability == "morale_boost")
                    {
                        morales.Add(i);
                    }
                    i++;
                }

                // 2: Set the strengths appropriately
                modified_strengths = info.MCloseStrength;
                if (morales.Count != 0)
                {
                    foreach (int index in morales)
                    {
                        i = 0;
                        foreach (Card card in card_list)
                        {
                            // Only change the indexes where conditions are met
                            if ((i != index) & !(card.unique))
                                modified_strengths[i] = info.MCloseStrength[i] + 1;
                            i++;
                        }
                    }
                }
                break;
            case "range":
                // 1: Look for morale boost cards an put their indexes in a list
                card_list = GetCardsList(info.RangeList);
                morales.Clear();
                i = 0;
                foreach (Card card in card_list)
                {
                    if (card.ability == "morale_boost")
                    {
                        morales.Add(i);
                    }
                    i++;
                }

                // 2: Set the strengths appropriately
                modified_strengths = info.MRangeStrength;
                if (morales.Count != 0)
                {
                    foreach (int index in morales)
                    {
                        i = 0;
                        foreach (Card card in card_list)
                        {
                            // Only change the indexes where conditions are met
                            if ((i != index) & !(card.unique))
                                modified_strengths[i] = info.MRangeStrength[i] + 1;
                            i++;
                        }
                    }
                }
                break;
            case "siege":
                // 1: Look for morale boost cards an put their indexes in a list
                card_list = GetCardsList(info.SiegeList);
                morales.Clear();
                i = 0;
                foreach (Card card in card_list)
                {
                    if (card.ability == "morale_boost")
                    {
                        morales.Add(i);
                    }
                    i++;
                }

                // 2: Set the strengths appropriately
                modified_strengths = info.MSiegeStrength;
                if (morales.Count != 0)
                {
                    foreach (int index in morales)
                    {
                        i = 0;
                        foreach (Card card in card_list)
                        {
                            // Only change the indexes where conditions are met
                            if ((i != index) & !(card.unique))
                                modified_strengths[i] = info.MSiegeStrength[i] + 1;
                            i++;
                        }
                    }
                }
                break;
        }
        return modified_strengths;
    }

    // TODO: Tight Bond (Double Strength of both cards)
    private List<int> ApplyTightBond(MainInfo info, string units_row)
    {//private List<int>
        List<int> modified_strengths = new List<int>();
        List<int> bonds = new List<int>();
        List<Card> card_list;
        switch (units_row)
        {
            case "close":
                // 1: Look for tight bond cards an put their indexes in a list
                card_list = GetCardsList(info.CloseList);
                bonds.Clear();
                foreach (Card card in card_list)
                {
                    if (card.ability == "tight_bond")
                    {
                        bonds.Add(card._id);
                    }
                }
                // 2: Set the strengths appropriately
                for (int i = 0; i < info.CloseList.Count; i++)
                {
                    if (card_list[i].ability == "tight_bond")
                        modified_strengths.Add(info.MCloseStrength[i] * OccurenceNum(bonds, card_list[i]._id));
                    else
                        modified_strengths.Add(info.MCloseStrength[i]);
                }
                break;
            case "range":
                // 1: Look for tight bond cards an put their indexes in a list
                card_list = GetCardsList(info.RangeList);
                bonds.Clear();
                foreach (Card card in card_list)
                {
                    if (card.ability == "tight_bond")
                    {
                        bonds.Add(card._id);
                    }
                }
                // 2: Set the strengths appropriately
                for (int i = 0; i < info.RangeList.Count; i++)
                {
                    if (card_list[i].ability == "tight_bond")
                        modified_strengths.Add(info.MRangeStrength[i] * OccurenceNum(bonds, card_list[i]._id));
                    else
                        modified_strengths.Add(info.MRangeStrength[i]);
                }
                break;
            case "siege":
                // 1: Look for tight bond cards an put their indexes in a list
                card_list = GetCardsList(info.SiegeList);
                bonds.Clear();
                foreach (Card card in card_list)
                {
                    if (card.ability == "tight_bond")
                    {
                        bonds.Add(card._id);
                    }
                }
                // 2: Set the strengths appropriately
                for (int i = 0; i < info.SiegeList.Count; i++)
                {
                    if (card_list[i].ability == "tight_bond")
                        modified_strengths.Add(info.MSiegeStrength[i] * OccurenceNum(bonds, card_list[i]._id));
                    else
                        modified_strengths.Add(info.MSiegeStrength[i]);
                }
                break;
        }
        return modified_strengths;
    }

    // Monster ability's commander horn 
    // Work only once for each monster
    // Even for doublers themselves
    private List<int> ApplyUnitDouble(MainInfo info, string units_row)
    {
        List<int> modified_strengths = new List<int>();
        List<int> doublers = new List<int>();
        List<int> doublers_id = new List<int>();
        List<Card> card_list;
        int i;
        bool isDoubled; // Checks if the unit's strength has been doubled
        bool isUnitDb;
        switch (units_row)
        {
            case "close":
                isDoubled = false;
                if (info.SpCloseList.Contains(202))
                    isDoubled = true;
                // 1: Look for double strength cards an put their indexes in a list
                card_list = GetCardsList(info.CloseList);
                doublers.Clear();
                doublers_id.Clear();
                i = 0;
                foreach (Card card in card_list)
                {
                    // Used indexes instead of ids in case 2 of the same card were on the field
                    if (card.ability == "commander_horn")
                    {
                        doublers.Add(i);
                        doublers_id.Add(card._id);
                    }
                    i++;
                }

                // 2: Set the strengths appropriately
                isUnitDb = false;
                modified_strengths = info.MCloseStrength;
                if (doublers.Count != 0)
                {
                    foreach (int index in doublers)
                    {
                        i = 0;
                        foreach (Card card in card_list)
                        {
                            // Doubles Different Units (only once)
                            if ((i != index) && !(card.unique) && !isDoubled && !isUnitDb && !doublers_id.Contains(card._id))
                                modified_strengths[i] = info.MCloseStrength[i] * 2;

                            // Double Other Doublers (only once)
                            if (doublers_id.Contains(card._id) && !isDoubled && !isUnitDb)
                                if (doublers.Count == 1)
                                    modified_strengths[i] = info.MCloseStrength[i];
                                else
                                    modified_strengths[i] = info.MCloseStrength[i] * 2;
                            i++;
                        }
                        isUnitDb = true;
                    }
                }
                break;
            case "range":
                isDoubled = false;
                if (info.SpRangeList.Contains(202))
                    isDoubled = true;
                // 1: Look for double strength cards an put their indexes in a list
                card_list = GetCardsList(info.RangeList);
                doublers.Clear();
                doublers_id.Clear();
                i = 0;
                foreach (Card card in card_list)
                {
                    // Used indexes instead of ids in case 2 of the same card were on the field
                    if (card.ability == "commander_horn")
                    {
                        doublers.Add(i);
                        doublers_id.Add(card._id);
                    }
                    i++;
                }

                // 2: Set the strengths appropriately
                isUnitDb = false;
                modified_strengths = info.MRangeStrength;
                if (doublers.Count != 0)
                {
                    foreach (int index in doublers)
                    {
                        i = 0;
                        foreach (Card card in card_list)
                        {
                            // Doubles Different Units (only once)
                            if ((i != index) && !(card.unique) && !isDoubled && !isUnitDb && !doublers_id.Contains(card._id))
                                modified_strengths[i] = info.MRangeStrength[i] * 2;

                            // Double Other Doublers (only once)
                            if (doublers_id.Contains(card._id) && !isDoubled && !isUnitDb)
                                if (doublers.Count == 1)
                                    modified_strengths[i] = info.MRangeStrength[i];
                                else
                                    modified_strengths[i] = info.MRangeStrength[i] * 2;
                            i++;
                        }
                        isUnitDb = true;
                    }
                }
                break;
            case "siege":
                isDoubled = false;
                if (info.SpSiegeList.Contains(202))
                    isDoubled = true;
                // 1: Look for double strength cards an put their indexes in a list
                card_list = GetCardsList(info.SiegeList);
                doublers.Clear();
                doublers_id.Clear();
                i = 0;
                foreach (Card card in card_list)
                {
                    // Used indexes instead of ids in case 2 of the same card were on the field
                    if (card.ability == "commander_horn")
                    {
                        doublers.Add(i);
                        doublers_id.Add(card._id);
                    }
                    i++;
                }

                // 2: Set the strengths appropriately
                isUnitDb = false;
                modified_strengths = info.MSiegeStrength;
                if (doublers.Count != 0)
                {
                    foreach (int index in doublers)
                    {
                        i = 0;
                        foreach (Card card in card_list)
                        {
                            // Doubles Different Units (only once)
                            if ((i != index) && !(card.unique) && !isDoubled && !isUnitDb && !doublers_id.Contains(card._id))
                                modified_strengths[i] = info.MSiegeStrength[i] * 2;

                            // Double Other Doublers (only once)
                            if (doublers_id.Contains(card._id) && !isDoubled && !isUnitDb)
                                if (doublers.Count == 1)
                                    modified_strengths[i] = info.MSiegeStrength[i];
                                else
                                    modified_strengths[i] = info.MSiegeStrength[i] * 2;
                            i++;
                        }
                        isUnitDb = true;
                    }
                }
                break;
        }
        return modified_strengths;
    }

    private List<int> ApplyDoubleSpy(MainInfo info, string units_row)
    {
        List<int> modified_strengths = new List<int>();
        List<Card> card_list;
        switch (units_row)
        {
            case "close":
                card_list = GetCardsList(info.CloseList);
                for (int i = 0; i < info.CloseList.Count; i++)
                {
                    if (card_list[i].ability == "spy" && !card_list[i].unique)
                        modified_strengths.Add(info.MCloseStrength[i] * 2);
                    else
                        modified_strengths.Add(info.MCloseStrength[i]);
                }
                break;
            case "range":
                card_list = GetCardsList(info.RangeList);
                for (int i = 0; i < info.RangeList.Count; i++)
                {
                    if (card_list[i].ability == "spy" && !card_list[i].unique)
                        modified_strengths.Add(info.MRangeStrength[i] * 2);
                    else
                        modified_strengths.Add(info.MRangeStrength[i]);
                }
                break;
            case "siege":
                card_list = GetCardsList(info.SiegeList);
                for (int i = 0; i < info.SiegeList.Count; i++)
                {
                    if (card_list[i].ability == "spy" && !card_list[i].unique)
                        modified_strengths.Add(info.MSiegeStrength[i] * 2);
                    else
                        modified_strengths.Add(info.MSiegeStrength[i]);
                }
                break;
        }
        return modified_strengths;
    }

    //-------------List manipulation effetc---( scorch / decoy )-------------------//
    // Destroy strongest if not unique
    private void Scorch() //"close", "range", "siege" or "all" for global scorch
    {
        List<MainInfo> infos = new List<MainInfo>() { PlayerInfo, EnemyInfo };
        int top_strength = -1;
        List<Card> card_list;

        // Get the top strength on the field
        foreach (MainInfo info in infos)
        {
            card_list = GetCardsList(info.CloseList);
            for (int i = 0; i < info.CloseList.Count; i++)
            {
                if (info.MCloseStrength[i] >= top_strength & !card_list[i].unique)
                {
                    top_strength = info.MCloseStrength[i];
                }
            }
            card_list = GetCardsList(info.RangeList);
            for (int i = 0; i < info.RangeList.Count; i++)
            {
                if (info.MRangeStrength[i] >= top_strength & !card_list[i].unique)
                {
                    top_strength = info.MRangeStrength[i];
                }
            }
            card_list = GetCardsList(info.SiegeList);
            for (int i = 0; i < info.SiegeList.Count; i++)
            {
                if (info.MSiegeStrength[i] >= top_strength & !card_list[i].unique)
                {
                    top_strength = info.MSiegeStrength[i];
                }
            }
        }
        Debug.Log("Strongest number is: " + top_strength);

        int initial_count;
        // TODO: TEST THOUROUGHLY
        // Remove the card with the strongest strength from the list
        foreach (MainInfo info in infos)
        {
            // Close
            card_list = GetCardsList(info.CloseList);
            initial_count = info.CloseList.Count;
            for (int i = 0; i < info.CloseList.Count; i++)
            {
                Debug.Log("i: " + i + " | Close_Count: " + info.CloseList.Count);
                if (info.MCloseStrength[i] == top_strength & !card_list[i].unique)
                {
                    Debug.Log("Scorching: " + card_list[i].name + " | Strength: " + info.MCloseStrength[i]);
                    info.DiscardList.Add(card_list[i]._id);
                    card_list.RemoveAt(i); // Joker 1
                    info.CloseList.RemoveAt(i);
                    info.CloseStrengthL.RemoveAt(i);
                    info.MCloseStrength.RemoveAt(i);
                    if (initial_count > 0) // Joker 2
                        i--;
                }
            }

            // Range
            card_list = GetCardsList(info.RangeList);
            initial_count = info.RangeList.Count;
            for (int i = 0; i < info.RangeList.Count; i++)
            {
                Debug.Log("i: " + i + " | Range_Count: " + info.RangeList.Count);
                if (info.MRangeStrength[i] == top_strength & !card_list[i].unique)
                {
                    Debug.Log("Scorching: " + card_list[i].name + " | Strength: " + info.MRangeStrength[i]);
                    info.DiscardList.Add(card_list[i]._id);
                    card_list.RemoveAt(i);
                    info.RangeList.RemoveAt(i);
                    info.RangeStrengthL.RemoveAt(i);
                    info.MRangeStrength.RemoveAt(i);
                    if (initial_count > 0)
                        i--;
                }
            }

            // Siege
            card_list = GetCardsList(info.SiegeList);
            initial_count = info.SiegeList.Count;
            for (int i = 0; i < info.SiegeList.Count; i++)
            {
                Debug.Log("i: " + i + " | Siege_Count: " + info.SiegeList.Count);
                if (info.MSiegeStrength[i] == top_strength & !card_list[i].unique)
                {
                    Debug.Log("Scorching: " + card_list[i].name + " | Strength: " + info.MSiegeStrength[i]);
                    info.DiscardList.Add(card_list[i]._id);
                    card_list.RemoveAt(i);
                    info.SiegeList.RemoveAt(i);
                    info.SiegeStrengthL.RemoveAt(i);
                    info.MSiegeStrength.RemoveAt(i);
                    if (initial_count > 0)
                        i--;
                }
            }
        }
    }

    // Destroy strongest if combined strengths is 10 or more
    private void ScorchAbility(string units_row, bool is_player_played)
    {
        int combined;
        int initial_count;
        int top_strength = -1;
        List<Card> card_list;
        MainInfo info;

        if (is_player_played) // Destroy from the enemy's field
            info = EnemyInfo;
        else
            info = PlayerInfo;

        switch (units_row)
        {
            case "close":
                combined = 0;
                foreach (int strength in info.MCloseStrength)
                    combined += strength;
                if (combined >= 10)
                {
                    // Look for strongest
                    card_list = GetCardsList(info.CloseList);
                    for (int i = 0; i < info.CloseList.Count; i++)
                    {
                        if (info.MCloseStrength[i] >= top_strength & !card_list[i].unique)
                        {
                            top_strength = info.MCloseStrength[i];
                        }
                    }
                    // Destroy strongest(s)
                    card_list = GetCardsList(info.CloseList);
                    initial_count = info.CloseList.Count;
                    for (int i = 0; i < info.CloseList.Count; i++)
                    {
                        Debug.Log("i: " + i + " | Close_Count: " + info.CloseList.Count);
                        if (info.MCloseStrength[i] == top_strength & !card_list[i].unique)
                        {
                            Debug.Log("Scorching: " + card_list[i].name + " | Strength: " + info.MCloseStrength[i]);
                            info.DiscardList.Add(card_list[i]._id);
                            card_list.RemoveAt(i); // Joker 1
                            info.CloseList.RemoveAt(i);
                            info.CloseStrengthL.RemoveAt(i);
                            info.MCloseStrength.RemoveAt(i);
                            if (initial_count > 0) // Joker 2
                                i--;
                        }
                    }
                }
                break;
            case "range":
                combined = 0;
                foreach (int strength in info.MRangeStrength)
                    combined += strength;
                if (combined >= 10)
                {
                    // Look for strongest
                    card_list = GetCardsList(info.RangeList);
                    for (int i = 0; i < info.RangeList.Count; i++)
                    {
                        if (info.MRangeStrength[i] >= top_strength & !card_list[i].unique)
                        {
                            top_strength = info.MRangeStrength[i];
                        }
                    }
                    // Destroy strongest(s)
                    card_list = GetCardsList(info.RangeList);
                    initial_count = info.RangeList.Count;
                    for (int i = 0; i < info.RangeList.Count; i++)
                    {
                        Debug.Log("i: " + i + " | Range_Count: " + info.RangeList.Count);
                        if (info.MRangeStrength[i] == top_strength & !card_list[i].unique)
                        {
                            Debug.Log("Scorching: " + card_list[i].name + " | Strength: " + info.MRangeStrength[i]);
                            info.DiscardList.Add(card_list[i]._id);
                            card_list.RemoveAt(i); // Joker 1
                            info.RangeList.RemoveAt(i);
                            info.RangeStrengthL.RemoveAt(i);
                            info.MRangeStrength.RemoveAt(i);
                            if (initial_count > 0) // Joker 2
                                i--;
                        }
                    }
                }
                break;
            case "siege":
                combined = 0;
                foreach (int strength in info.MSiegeStrength)
                    combined += strength;
                if (combined >= 10)
                {
                    // Look for strongest
                    card_list = GetCardsList(info.SiegeList);
                    for (int i = 0; i < info.SiegeList.Count; i++)
                    {
                        if (info.MSiegeStrength[i] >= top_strength & !card_list[i].unique)
                        {
                            top_strength = info.MSiegeStrength[i];
                        }
                    }
                    // Destroy strongest(s)
                    card_list = GetCardsList(info.SiegeList);
                    initial_count = info.SiegeList.Count;
                    for (int i = 0; i < info.SiegeList.Count; i++)
                    {
                        Debug.Log("i: " + i + " | Siege_Count: " + info.SiegeList.Count);
                        if (info.MSiegeStrength[i] == top_strength & !card_list[i].unique)
                        {
                            Debug.Log("Scorching: " + card_list[i].name + " | Strength: " + info.MSiegeStrength[i]);
                            info.DiscardList.Add(card_list[i]._id);
                            card_list.RemoveAt(i); // Joker 1
                            info.SiegeList.RemoveAt(i);
                            info.SiegeStrengthL.RemoveAt(i);
                            info.MSiegeStrength.RemoveAt(i);
                            if (initial_count > 0) // Joker 2
                                i--;
                        }
                    }
                }
                break;
        }
    }

    // Swap with a non unique card from your field
    // Called from double clicking a card while swapActivated == true
    public void Decoy(int card_id, string tag, string row)
    {
        Debug.Log("Initiation decoy");
        if (battleState == BattleState.PLAYERTURN && (tag.ToLower() == "player"))
            ActuallyDecoy(card_id, row.ToLower(), true);
        else if (battleState == BattleState.ENEMYTURN && (tag.ToLower() == "enemy"))
            ActuallyDecoy(card_id, row.ToLower(), false);
        else
            Debug.Log("Invalid Move");
    }

    private void ActuallyDecoy(int card_id, string row, bool is_player_card)
    {
        MainInfo info;
        if (is_player_card)
            info = PlayerInfo;
        else
            info = EnemyInfo;

        Debug.Log("Decoying: " + card_id);
        switch (row)
        {
            case "close":
                info.CloseList.Remove(card_id);
                info.HandList.Add(card_id);
                break;
            case "range":
                info.RangeList.Remove(card_id);
                info.HandList.Add(card_id);
                break;
            case "siege":
                info.SiegeList.Remove(card_id);
                info.HandList.Add(card_id);
                break;
        }

        UpdateLists();
        RefreshFields(is_player_card);
        UpdateUIScores(is_player_card);

        swapActivated = false;
        SetHider(false);

        if (is_player_card)
            StartCoroutine(EnemyTurn());
        else
            StartCoroutine(PlayerTurn());
    }

    //------------------------------List manipulation abilities---( muster / medic )------------------------------//
    // Add all cards with the same name to the field
    // close_range not INCLUDED ! (no muster close range in game)
    private void Muster(CardStats card_stats, string row, bool is_player_turn)
    {
        Debug.Log("Mustering: " + card_stats.name);
        int amounts;

        MainInfo my_info;
        if (is_player_turn)
            my_info = PlayerInfo;
        else
            my_info = EnemyInfo;

        switch (row)
        {
            case "close":
                // 1: Add all of the same ID from deck
                amounts = 0;
                foreach (int id in my_info.DeckList)
                {
                    if (card_stats._id == id)
                    {
                        my_info.CloseList.Add(id);
                        amounts++;
                    }
                }
                for (int i = 0; i < amounts; i++)
                    my_info.DeckList.Remove(card_stats._id);

                // 2: Add all of the same ID from hand
                amounts = 0;
                foreach (int id in my_info.HandList)
                {
                    if (card_stats._id == id)
                    {
                        my_info.CloseList.Add(id);
                        amounts++;
                    }
                }
                for (int i = 0; i < amounts; i++)
                    my_info.HandList.Remove(card_stats._id);
                break;
            case "range":
                // 1: Add all of the same ID from deck
                amounts = 0;
                foreach (int id in my_info.DeckList)
                {
                    if (card_stats._id == id)
                    {
                        my_info.RangeList.Add(id);
                        amounts++;
                    }
                }
                for (int i = 0; i < amounts; i++)
                    my_info.DeckList.Remove(card_stats._id);

                // 2: Add all of the same ID from hand
                amounts = 0;
                foreach (int id in my_info.HandList)
                {
                    if (card_stats._id == id)
                    {
                        my_info.RangeList.Add(id);
                        amounts++;
                    }
                }
                for (int i = 0; i < amounts; i++)
                    my_info.HandList.Remove(card_stats._id);
                break;
            case "siege":
                // 1: Add all of the same ID from deck
                amounts = 0;
                foreach (int id in my_info.DeckList)
                {
                    if (card_stats._id == id)
                    {
                        my_info.SiegeList.Add(id);
                        amounts++;
                    }
                }
                for (int i = 0; i < amounts; i++)
                    my_info.DeckList.Remove(card_stats._id);

                // 2: Add all of the same ID from hand
                amounts = 0;
                foreach (int id in my_info.HandList)
                {
                    if (card_stats._id == id)
                    {
                        my_info.SiegeList.Add(id);
                        amounts++;
                    }
                }
                for (int i = 0; i < amounts; i++)
                    my_info.HandList.Remove(card_stats._id);
                break;
        }

        // 3: Add all with a similar name from deck
        string name = TrimToChar(card_stats._idstr, '_');
        Debug.Log("Name: " + name);
        List<Card> cards_list = GetCardsList(my_info.DeckList);
        List<int> to_remove = new List<int>();

        foreach (Card card in cards_list)
        {
            if (card._idstr.Contains(name))
            {
                switch (card.row)
                {
                    case "close":
                        my_info.CloseList.Add(card._id);
                        break;
                    case "range":
                        my_info.RangeList.Add(card._id);
                        break;
                    case "siege":
                        my_info.SiegeList.Add(card._id);
                        break;
                }
                to_remove.Add(card._id);
            }
        }
        for (int i = 0; i < to_remove.Count; i++)
            my_info.DeckList.Remove(to_remove[i]);

        // 4: Add all with a similar name from hand
        cards_list = GetCardsList(my_info.HandList);
        to_remove.Clear();

        foreach (Card card in cards_list)
        {
            if (card._idstr.Contains(name))
            {
                switch (card.row)
                {
                    case "close":
                        my_info.CloseList.Add(card._id);
                        break;
                    case "range":
                        my_info.RangeList.Add(card._id);
                        break;
                    case "siege":
                        my_info.SiegeList.Add(card._id);
                        break;
                }
                to_remove.Add(card._id);
            }
        }
        for (int i = 0; i < to_remove.Count; i++)
            my_info.HandList.Remove(to_remove[i]);
    }

    // Add a chosen card from discard pile to field
    private bool Medic(bool is_player_turn)
    {
        MainInfo info;
        if (is_player_turn)
            info = PlayerInfo;
        else
            info = EnemyInfo;

        // Crop info.DiscardList: Only leave unit cards (no hero or special)
        List<int> units = GetMedicList(info.DiscardList);

        if (units.Count > 0)
        {
            Panel.SetActive(true);
            Panel.GetComponent<PanelManager>().ShowToPlay("discard_list", units, is_player_turn);
            return true; // Medic ability activated
        }
        return false; // Medic ability not activated
    }

    // Restore random unit from discard
    private void RndMedic(bool is_player_turn)
    {
        Debug.Log("Restoring randomely");
        MainInfo info;
        if (is_player_turn)
            info = PlayerInfo;
        else
            info = EnemyInfo;

        List<int> units = GetMedicList(info.DiscardList);

        if (units.Count > 0)
        {
            System.Random rnd = new System.Random();
            int r = rnd.Next(units.Count);
            //Debug.Log("Reviving: " + units[r]);
            Card card = GetCardStats(units[r]);
            info.DiscardList.RemoveAt(r);

            switch (card.row)
            {
                case "close":
                    info.CloseList.Add(units[r]);
                    break;
                case "range":
                    info.RangeList.Add(units[r]);
                    break;
                case "siege":
                    info.SiegeList.Add(units[r]);
                    break;
                case "close_range": // Restore randomly to close or range
                    if (rnd.Next(0, 2) == 0)
                        info.CloseList.Add(units[r]);
                    else
                        info.RangeList.Add(units[r]);
                    break;
            }
        }
    }

    public void PlayBtnCleanUp(string field, int card_id, bool is_player_turn)
    {
        MainInfo info;
        if (is_player_turn)
            info = PlayerInfo;
        else
            info = EnemyInfo;

        Debug.Log("Playing from: " + field);
        switch (field)
        {
            case "deck_list":
                info.DeckList.Remove(card_id);
                SetDeckObject(info.DeckList, info.DeckBSprite, is_player_turn);
                break;
            case "discard_list":
                info.DiscardList.Remove(card_id);
                SetDiscardObject(info.DiscardList, is_player_turn);
                break;
        }

        // To be removed later from directly place
        info.HandList.Add(card_id);
    }

    //-------------------------------------------Leader Related------------------------------------------------//
    // Sets the leader in the UI 
    public void SetLeaderCard(int leader_id, bool is_player_deck)
    {
        GameObject my_object;
        List<int> one_card = new List<int>() { leader_id };

        if (is_player_deck)
            my_object = PlayerField;
        else
            my_object = EnemyField;

        Card my_card = GetCardStats(leader_id);
        my_object.transform.Find("Leader").GetComponent<LeaderManager>().leaderId = leader_id;
        my_object.transform.Find("Leader").GetComponent<LeaderManager>().type = my_card.row;
        CreateCardObjects(my_object.transform.Find("Leader").Find("Holder").gameObject, one_card, false, is_player_deck);

        // Set leader card ability here if passive
        my_object.transform.Find("Leader").GetComponent<LeaderManager>().UISetPassive();

        // Sets passive bools to active
        if (leader_id == 35)
            randomMedic = true;
        if (leader_id == 37)
            disableLeader = true;
        // if (leader_id == 57): Activated in the calling function
    }

    // Activate the leader's ACTIVE ability
    public void ActivateLeader(int leader_id, bool is_player_turn)
    {
        MainInfo info;
        if (is_player_turn)
            info = PlayerInfo;
        else
            info = EnemyInfo;

        //Debug.Log("Leader ability activated! ID: " + leader_id);
        switch (leader_id)
        {
            //-------------Monsters: Eredin--------------//
            case 40:
                doubleSpies = true;
                if (is_player_turn)
                    StartCoroutine(EnemyTurn());
                break;
            case 41:
                if (info.HandList.Count >= 2)
                    info.CanDiscard = true;
                break;
            case 42:
                PickWeatherCard("all", is_player_turn);
                break;
            case 43:
                PickDiscardCard(is_player_turn);
                break;
            case 44:
                PlaceCommanderHorn("close", is_player_turn);
                break;
            //------------Nilfgaard: Emhyr-------------//
            case 33:
                LookHandRandom(3, is_player_turn);
                break;
            case 34:
                PickWeatherCard("siege", is_player_turn);
                break;
            // 35 is randomMedic
            case 36:
                PickOppDiscard(is_player_turn);
                break;
            // 37 is disabling opponent leader
            //----------Northern Realms: Foltest---------//
            case 51:
                PickWeatherCard("range", is_player_turn);
                break;
            case 52: // TODO: Update for skellige
                weatherList.Clear();
                break;
            case 53:
                ScorchAbility("range", is_player_turn);
                break;
            case 54:
                PlaceCommanderHorn("siege", is_player_turn);
                break;
            case 55:
                ScorchAbility("siege", is_player_turn);
                break;
            //---------Scoia'tel: Francesca--------------//
            // 57 is passive (draw extra card at battle start)
            case 58:
                OptimizeAgile(is_player_turn);
                break;
            case 59:
                PickWeatherCard("close", is_player_turn);
                break;
            case 60:
                ScorchAbility("close", is_player_turn);
                break;
            case 61:
                PlaceCommanderHorn("range", is_player_turn);
                break;
        }

        info.canLeader = false;

        // Update gameplay lists and ui
        UpdateLists();
        RefreshFields(true);
        RefreshFields(false);
        UpdateUIScores(false);
        UpdateUIScores(true);
    }

    // Initialize the Deck List on screen
    public void DeckCardPicker(bool is_player_turn)
    {
        MainInfo info;
        if (is_player_turn)
            info = PlayerInfo;
        else
            info = EnemyInfo;

        if (info.DeckList.Count > 0)
        {
            Panel.SetActive(true);
            Panel.GetComponent<PanelManager>().ShowToDraw("deck_list", info.DeckList, is_player_turn);
        }
        else
        {
            TurnCallBack(is_player_turn);
        }
    }

    // Actually pick a card (gameplay)
    public void ActuallyPickCard(string field, int card_id, bool is_player_turn)
    {
        MainInfo info, opp_info;
        if (is_player_turn)
        {
            info = PlayerInfo;
            opp_info = EnemyInfo;
        }
        else
        {
            info = EnemyInfo;
            opp_info = PlayerInfo;
        }

        Debug.Log("Picking from: " + field);
        switch (field)
        {
            case "deck_list":
                info.DeckList.Remove(card_id);
                SetDeckObject(info.DeckList, info.DeckBSprite, is_player_turn);
                break;
            case "discard_list":
                info.DiscardList.Remove(card_id);
                SetDiscardObject(info.DiscardList, is_player_turn);
                break;
            case "opp_discard_list":
                opp_info.DiscardList.Remove(card_id);
                SetDiscardObject(opp_info.DiscardList, !is_player_turn);
                break;
        }

        info.HandList.Add(card_id);
        info.CanDiscard = false;
        SetHandObject(info.HandList, true);

        TurnCallBack(is_player_turn);
    }

    // Plays any weather card from deck (except clear weather)
    public void PickWeatherCard(string row, bool is_player_turn)
    {
        MainInfo info;
        if (is_player_turn)
            info = PlayerInfo;
        else
            info = EnemyInfo;

        // Crop info.DeckList: Only leave weather cards
        List<int> weather_cards = GetWeatherList(info.DeckList, row);

        if (weather_cards.Count > 0)
        {
            Panel.SetActive(true);
            Panel.GetComponent<PanelManager>().ShowToPlay("deck_list", weather_cards, is_player_turn);
        }
    }

    // Draw card from own discard pile (no heroes or special cards)
    public void PickDiscardCard(bool is_player_card)
    {
        MainInfo info;
        if (is_player_card)
            info = PlayerInfo;
        else
            info = EnemyInfo;

        List<int> units_list = GetMedicList(info.DiscardList);

        if (info.DiscardList.Count > 0)
        {
            Panel.SetActive(true);
            Panel.GetComponent<PanelManager>().ShowToDraw("discard_list", units_list, is_player_card);
        }
    }

    // Draw card from opponent's discard (no heroes or special cards)
    public void PickOppDiscard(bool is_player_turn)
    {
        MainInfo opp_info;
        if (is_player_turn)
            opp_info = EnemyInfo;
        else
            opp_info = PlayerInfo;

        List<int> units_list = GetMedicList(opp_info.DiscardList);

        if (opp_info.DiscardList.Count > 0)
        {
            Panel.SetActive(true);
            Panel.GetComponent<PanelManager>().ShowToDraw("opp_discard_list", units_list, is_player_turn);
        }
    }

    // View cards from opponent's hand
    public void LookHandRandom(int cards_num, bool is_player_turn)
    {
        MainInfo opp_info;
        if (is_player_turn)
            opp_info = EnemyInfo;
        else
            opp_info = PlayerInfo;

        if (opp_info.HandList.Count < 1) // Empty hand
        {
            TurnCallBack(is_player_turn);
            return;
        }
        else // Full hand
        {
            List<int> random_list;
            if (opp_info.HandList.Count <= cards_num)
                random_list = opp_info.HandList; // List count is 3, show all
            else
            {
                // Random List if count is 4 or more
                System.Random rnd = new System.Random();
                random_list = opp_info.HandList.OrderBy(r => rnd.Next()).Take(cards_num).ToList();
            }
            Panel.SetActive(true);
            Panel.GetComponent<PanelManager>().ShowToView(random_list, is_player_turn);
        }
    }

    // Place Commander Horn Leader ability
    public void PlaceCommanderHorn(string row, bool is_player_turn)
    {
        MainInfo info;
        if (is_player_turn)
            info = PlayerInfo;
        else
            info = EnemyInfo;

        switch (row)
        {
            case "close":
                if (!info.SpCloseList.Any(item => item == 202))
                    info.SpCloseList.Add(202);
                break;
            case "range":
                if (!info.SpRangeList.Any(item => item == 202))
                    info.SpRangeList.Add(202);
                break;
            case "siege":
                if (!info.SpSiegeList.Any(item => item == 202))
                    info.SpSiegeList.Add(202);
                break;
        }

        if (is_player_turn)
            StartCoroutine(EnemyTurn());
        else
            StartCoroutine(PlayerTurn());
    }

    // Moves agiles to the row that maximizes their strengths
    public void OptimizeAgile(bool is_player_turn)
    {
        Debug.Log("Optimizing agile !");

        MainInfo info;
        if (is_player_turn)
            info = PlayerInfo;
        else
            info = EnemyInfo;

        //------------Optimize "close_range" cards in Close-----------//
        // 1: Get strengths in close row
        List<int> cr_list = new List<int>();        // Length L  close_list
        List<int> orig_strength = new List<int>();  // Length L
        List<int> mod_strength = new List<int>();   // Length L

        List<Card> close_cards = GetCardsList(info.CloseList);
        for (int i = 0; i < close_cards.Count; i++)
        {
            if (close_cards[i].row == "close_range" && !close_cards[i].unique)
            {
                cr_list.Add(info.CloseList[i]);
                orig_strength.Add(info.CloseStrengthL[i]);
                mod_strength.Add(info.MCloseStrength[i]);
            }
        }

        // 2: Get virtual strengths in range row (from original list after updating)
        List<int> added_indexes = new List<int>();
        for (int i = 0; i < cr_list.Count; i++)
        {
            added_indexes.Add(info.RangeList.Count);
            info.RangeList.Add(cr_list[i]);
        }
        UpdateLists();
        List<int> vr_mod_strength = new List<int>();
        foreach (int index in added_indexes)
        {
            for (int i = 0; i < info.MRangeStrength.Count; i++)
            {
                if (i == index)
                    vr_mod_strength.Add(info.MRangeStrength[i]);
            }
        }

        // 3: Choose, either remove from close list or from range list
        for (int j = 0; j < cr_list.Count; j++)
        {
            if (vr_mod_strength[j] > mod_strength[j])
            {
                // Remove from close
                foreach (int card_id in cr_list)
                    info.CloseList.Remove(card_id);
            }
            else
            {
                // Remove from range
                foreach (int card_id in cr_list)
                    info.RangeList.Remove(card_id);
            }
        }
        UpdateLists();

        //------------Optimize "close_range" cards in Range-----------//
        // 1: Get strengths in range row
        cr_list.Clear();
        orig_strength.Clear();
        mod_strength.Clear();

        List<Card> range_cards = GetCardsList(info.RangeList);
        for (int i = 0; i < range_cards.Count; i++)
        {
            if (range_cards[i].row == "close_range" && !range_cards[i].unique)
            {
                cr_list.Add(info.RangeList[i]);
                orig_strength.Add(info.RangeStrengthL[i]);
                mod_strength.Add(info.MRangeStrength[i]);
            }
        }

        // 2: Get virtual strengths in close row (from original list after updating)
        added_indexes.Clear();
        for (int i = 0; i < cr_list.Count; i++)
        {
            added_indexes.Add(info.CloseList.Count);
            info.CloseList.Add(cr_list[i]);
        }
        UpdateLists();
        vr_mod_strength.Clear();
        foreach (int index in added_indexes)
        {
            for (int i = 0; i < info.MCloseStrength.Count; i++)
            {
                if (i == index)
                    vr_mod_strength.Add(info.MCloseStrength[i]);
            }
        }

        // 3: Choose, either remove from close list or from range list
        for (int j = 0; j < cr_list.Count; j++)
        {
            if (vr_mod_strength[j] > mod_strength[j])
            {
                // Remove from range
                foreach (int card_id in cr_list)
                    info.RangeList.Remove(card_id);
            }
            else
            {
                // Remove from close
                foreach (int card_id in cr_list)
                    info.CloseList.Remove(card_id);
            }
        }
        //UpdateLists(); not necessary, being called from parent function
        TurnCallBack(is_player_turn);
    }

    //-------------Debugging Methods
    public void LogList(List<int> my_list, string title = "List")
    {
        Debug.Log(title + ": " + string.Join(",", my_list.ConvertAll(i => i.ToString()).ToArray()));
    }


    //--------------ToMainMenu
    public void ToMainMenu()
    {
        SceneManager.LoadScene(0);
    }
}

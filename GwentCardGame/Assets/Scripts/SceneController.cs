using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gwent.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum BattleState { START, PLAYERTURN, ENEMYTURN, ROUNDOVER, WON, LOST, DRAW }

// A multi-step player action in progress. While one is pending the turn cannot end (pass / leader are blocked).
public enum PendingAction { None, PanelChoice, PlacePickedCard, DiscardCards }

// Runs the battle: setup, turns, rounds and playing cards (leaders and pickers: SceneController.Leaders.cs).
// The rules themselves live in Gwent.Rules; BoardView draws the board from the model.
public partial class SceneController : MonoBehaviour
{
    private const string DefaultDeckResource = "Decks/player_default";

    public static SceneController Instance { get; private set; }

    public BattleState battleState;
    public PendingAction pendingAction;

    public GameObject PostGame;
    public GameObject CardPrefab;
    public GameObject WeatherField;

    public TextAsset CardsDatabase;
    public string PlayerDeck;
    public TextAsset EnemyDeck;

    public GameObject PlayerField;
    public GameObject EnemyField;

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

    public CardDatabase Db { get; private set; }
    public GameObject TempObject { get; private set; }

    private AIManager AIManager;
    private BoardView board;
    private GameObject mulliganPrompt;
    private Coroutine turnRoutine;
    private int turnGeneration;

    [Serializable]
    public class MainInfo : PlayerState
    {
        public string Name;
        public Image Avatar;
        public Sprite DeckBSprite;
    }

    private void Awake()
    {
        Instance = this;
        Db = CardLibrary.Get(CardsDatabase);
        TempObject = GameObject.Find("Temp");
        board = new BoardView(this);
        AIManager = GetComponent<AIManager>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        GameAudio.PlayGameMusic();
        PlayerInfo.Name = "Player";
        EnemyInfo.Name = "Opponent";

        GameLog.Info("Starting battle...");
        GameAudio.PlaySfx("game_start");
        UIOverlay.SetEscapeAction(PauseMenu.Open);
        battleState = BattleState.START;
        SetupBattle();
        if (PlayerInfo.CanExchange)
            mulliganPrompt = RedrawPicker.ShowPhase(this);

        StartCoroutine(FirstTurnPicker());
    }

    private void Update()
    {
        if (battleState == BattleState.START && PlayerInfo.CanExchange && ShouldSkipCardExchange())
        {
            GameLog.Info("Player has skipped cards exchanging !");
            FinishCardExchange();
        }

        // The pass button only exists during the player's turn, and not in the middle of a multi-step action
        bool can_pass = battleState == BattleState.PLAYERTURN && pendingAction == PendingAction.None;
        if (PassBtn.activeSelf != can_pass)
            PassBtn.SetActive(can_pass);

        board.UpdateMarkers();
    }

    // Right click (or a second finger) keeps the hand; Escape opens the pause menu instead
    private bool ShouldSkipCardExchange()
    {
        return !UIOverlay.BlocksCancel && (GameInput.SecondaryPressed() || GameInput.SecondFingerTapped());
    }

    //----------------------------------------------Info helpers---------------------------------------------------//
    private MainInfo Info(bool is_player) { return is_player ? PlayerInfo : EnemyInfo; }
    private MainInfo Opponent(bool is_player) { return is_player ? EnemyInfo : PlayerInfo; }
    private BoardView.FieldView View(bool is_player) { return board.View(is_player); }

    public bool IsPlayerActionAllowed
    {
        get { return battleState == BattleState.PLAYERTURN && pendingAction == PendingAction.None; }
    }

    //----------------------------------------------BATTLE SETUP---------------------------------------------------//
    // On Battle Start: Draw 10 cards then change two cards or not at all
    private void SetupBattle()
    {
        Deck myDeck = LoadPlayerDeck();
        Deck oppDeck = DeckRules.Sanitize(CardLibrary.ParseDeck(EnemyDeck.text, EnemyDeck.name), Db);

        PrepareField(myDeck, PlayerInfo, true);
        PrepareField(oppDeck, EnemyInfo, false);

        // Emhyr, The White Flame: cancels the opponent's leader ability
        foreach (bool is_player in new[] { true, false })
            if (Info(is_player).Leader == LeaderIds.EmhyrTheWhiteFlame)
            {
                Opponent(is_player).leaderDisabled = true;
                View(!is_player).Leader.MarkCancelled();
            }
    }

    // The selected deck if it exists and is valid, else the built-in default deck
    private Deck LoadPlayerDeck()
    {
        string path = ApplicationModel.playerDeckPath;
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            Deck deck = DeckStorage.Load(path);
            List<string> errors = DeckRules.Validate(deck, Db);
            if (errors.Count == 0)
            {
                PlayerDeck = path;
                return deck;
            }
            UIOverlay.Toast("Your selected deck can't be used (" + errors[0] + "). Playing with the default deck.", 4f);
        }

        PlayerDeck = DefaultDeckResource;
        TextAsset deckAsset = Resources.Load<TextAsset>(DefaultDeckResource);
        return DeckRules.Sanitize(CardLibrary.ParseDeck(deckAsset.text, DefaultDeckResource), Db);
    }

    // Prepares the field for the first time
    private void PrepareField(Deck deck, MainInfo info, bool is_player_deck)
    {
        info.Faction = deck.Faction;
        info.DeckBSprite = Resources.Load<Sprite>("Cards/Back/" + deck.Faction);
        info.DeckList = Rng.Shuffle(deck.Cards);
        info.Leader = deck.Leader;

        View(is_player_deck).Stats.UISetGeneral(info.Faction);
        View(is_player_deck).Stats.UISetPlayer(info.Name, SpriteCache.Load("Cards/List/591x380/" + deck.Leader));
        SetLeaderCard(deck.Leader, is_player_deck);

        // Francesca, Daisy of the Valley: one extra card at the beginning of the battle
        int cards_to_draw = deck.Leader == LeaderIds.FrancescaDaisyOfTheValley ? 11 : 10;
        DrawCards(cards_to_draw, info, is_player_deck);
    }

    // Sets the leader in the UI
    public void SetLeaderCard(int leader_id, bool is_player_deck)
    {
        BoardView.FieldView view = View(is_player_deck);
        view.Leader.leaderId = leader_id;
        view.Leader.type = Db.Get(leader_id).row;
        CreateCardObjects(view.LeaderHolder, new List<int> { leader_id }, false, is_player_deck);
        view.Leader.UISetPassive();

        // Emhyr, Invader of the North affects both players
        if (leader_id == LeaderIds.EmhyrInvaderOfTheNorth)
            randomMedic = true;
    }

    //----------------------------------------------Redraw---------------------------------------------------//
    public void FinishCardExchange()
    {
        PlayerInfo.CanExchange = false;
    }

    // One redraw of the player's (double-click in the hand, or the redraw picker); the second one ends the phase
    public void RedrawPlayerCard(int card_id)
    {
        if (!PlayerInfo.CanExchange || PlayerInfo.CardsExchanged >= 2 || !PlayerInfo.HandList.Contains(card_id))
            return;
        ExchangeCard(card_id, true);
        PlayerInfo.CardsExchanged++;
        if (PlayerInfo.CardsExchanged >= 2)
            FinishCardExchange();
    }

    // The menu button at the top right
    public void OpenPauseMenu()
    {
        PauseMenu.Open();
    }

    // Exchange a card with another from deck (shuffle card back into the deck then draw)
    public void ExchangeCard(int card_id, bool is_player_card)
    {
        MainInfo info = Info(is_player_card);
        if (!info.HandList.Remove(card_id))
            return;

        GameAudio.PlaySfx("card_exchange");
        info.DeckList.Add(card_id);
        info.DeckList = Rng.Shuffle(info.DeckList);
        RoundRules.Draw(info, 1);

        SetDeckObject(info, is_player_card);
        SetHandObject(info.HandList, is_player_card);
    }

    //----------------------------------------------TURNS---------------------------------------------------//
    // Picks the first turn
    private IEnumerator FirstTurnPicker()
    {
        if (EnemyInfo.CanExchange)
        {
            // Initialize AI (first turn only)
            AIManager.AIInitialize();
            AIManager.AIExchangeCards();
        }

        // Wait for all players to either redraw or skip
        while (EnemyInfo.CanExchange || PlayerInfo.CanExchange)
            yield return null;

        if (mulliganPrompt != null)
            Destroy(mulliganPrompt);

        //------------Scoia'tel: Player with this deck chooses who begins each round-------------//
        if (PlayerInfo.Faction == Factions.Scoiatael && EnemyInfo.Faction != Factions.Scoiatael)
            TurnPicker.SetActive(true);
        else if (PlayerInfo.Faction != Factions.Scoiatael && EnemyInfo.Faction == Factions.Scoiatael)
            StartTurn(true); // The AI always lets the player begin
        else
            StartTurn(Rng.Shared.Next(0, 2) != 0);
    }

    // Only one turn coroutine at a time: a stale "player turn in 1s" must not fire during a later turn
    // Each new turn bumps the generation: a coroutine from an older turn gives up after its wait.
    // (A turn can start another one synchronously, e.g. when the side to play has already passed.)
    private void StartTurn(bool is_player)
    {
        StopTurn();
        int generation = turnGeneration;
        Coroutine routine = StartCoroutine(is_player ? PlayerTurn(generation) : EnemyTurn(generation));
        if (generation == turnGeneration)
            turnRoutine = routine;
    }

    private void StopTurn()
    {
        turnGeneration++;
        if (turnRoutine != null)
            StopCoroutine(turnRoutine);
        turnRoutine = null;
    }

    private IEnumerator PlayerTurn(int generation)
    {
        if (PlayerInfo.hasPassed)
        {
            CheckRoundEnd();
            yield break;
        }
        yield return new WaitForSeconds(1f);
        if (generation != turnGeneration)
            yield break;
        GameLog.Info("Player Turn !");
        GameAudio.PlaySfx("turn_player");
        battleState = BattleState.PLAYERTURN;
    }

    private IEnumerator EnemyTurn(int generation)
    {
        if (EnemyInfo.hasPassed)
        {
            CheckRoundEnd();
            yield break;
        }
        battleState = BattleState.ENEMYTURN;
        GameLog.Info("Enemy Turn !");
        GameAudio.PlaySfx("turn_enemy");
        yield return new WaitForSeconds(2f);
        if (generation != turnGeneration)
            yield break;
        AIManager.AIStartTurn();
    }

    public void CheckRoundEnd()
    {
        if (PlayerInfo.hasPassed && EnemyInfo.hasPassed)
        {
            battleState = BattleState.ROUNDOVER;
            GameLog.Info("Round has ended");
            ManageOutcome();
        }
        else if (PlayerInfo.hasPassed)
            StartTurn(false);
        else if (EnemyInfo.hasPassed)
            StartTurn(true);
    }

    // Ends whatever the given side was doing and hands the turn to the other side
    public void TurnCallBack(bool is_player_turn)
    {
        pendingAction = PendingAction.None;
        PlayerInfo.CanDiscard = false;
        SetHider(false);
        StartTurn(!is_player_turn);
    }

    // Reference: PassManager event (hold to pass button)
    public void SkipTurn(bool is_player_turn)
    {
        if (is_player_turn && !IsPlayerActionAllowed)
        {
            GameAudio.PlaySfx("invalid");
            return;
        }

        GameAudio.PlaySfx("pass");
        Info(is_player_turn).hasPassed = true;
        if (is_player_turn)
            DeselectCard();
        TurnCallBack(is_player_turn);
    }

    //----------------------------------------------ROUNDS---------------------------------------------------//
    private void ManageOutcome()
    {
        StopTurn();
        battleState = BattleState.ROUNDOVER;
        RecalculateStrengths();
        RoundWinner winner = RoundRules.DecideRound(PlayerInfo.TotalScore(), EnemyInfo.TotalScore(), PlayerInfo.Faction, EnemyInfo.Faction);
        RoundRules.ApplyRoundResult(PlayerInfo, EnemyInfo, winner);
        PlayerInfo.hasPassed = false;
        EnemyInfo.hasPassed = false;

        // Update UI Gems
        View(true).Stats.UIUpdateGemLives(PlayerInfo.Lives);
        View(false).Stats.UIUpdateGemLives(EnemyInfo.Lives);

        if (PlayerInfo.Lives == 0 && EnemyInfo.Lives == 0)
            ConcludeBattle(BattleState.DRAW);
        else if (EnemyInfo.Lives == 0)
            ConcludeBattle(BattleState.WON);
        else if (PlayerInfo.Lives == 0)
            ConcludeBattle(BattleState.LOST);
        else
        {
            PlayRoundOutcomeAudio(winner);
            PrepareNextRound(winner);
        }
    }

    private void PlayRoundOutcomeAudio(RoundWinner winner)
    {
        switch (winner)
        {
            case RoundWinner.Player:
                GameAudio.PlaySfx("round_win");
                break;
            case RoundWinner.Enemy:
                GameAudio.PlaySfx("round_lose");
                break;
            default:
                GameAudio.PlaySfx("round_end");
                break;
        }
    }

    private void PrepareNextRound(RoundWinner winner)
    {
        GameLog.Info("Preparing next round");
        DeselectCard();
        pendingAction = PendingAction.None;
        PlayerInfo.CanDiscard = false;

        // Monsters keep a random unit; leader horns vanish; weather goes back to whoever played it
        RoundRules.CleanupBoard(PlayerInfo, Rng.Shared, Db);
        RoundRules.CleanupBoard(EnemyInfo, Rng.Shared, Db);
        weatherList.Clear();

        //------Northern Realms: Get one additional card from your deck every time you win a round-------------//
        if (winner == RoundWinner.Player && PlayerInfo.Faction == Factions.NorthernRealms)
            DrawCards(1, PlayerInfo, true);
        else if (winner == RoundWinner.Enemy && EnemyInfo.Faction == Factions.NorthernRealms)
            DrawCards(1, EnemyInfo, false);

        RefreshBoard();
        StartCoroutine(FirstTurnPicker());
    }

    private void ConcludeBattle(BattleState outcome)
    {
        GameLog.Info("Battle ended: " + outcome);
        battleState = outcome;
        PostGame.SetActive(true);
        PostGame.GetComponent<PostGameManager>().Show(outcome, PlayerInfo, EnemyInfo);
        if (outcome == BattleState.WON)
            GameAudio.PlaySfx("victory");
        else if (outcome == BattleState.LOST)
            GameAudio.PlaySfx("defeat");
    }

    // Draws up to num cards (fewer if the deck runs out)
    private void DrawCards(int num, MainInfo info, bool is_player_deck)
    {
        if (RoundRules.Draw(info, num) == 0)
            return;
        SetDeckObject(info, is_player_deck);
        SetHandObject(info.HandList, is_player_deck);
        GameAudio.PlaySfx("card_draw");
    }

    //----------------------------------------------PLAYING CARDS---------------------------------------------------//
    // Directly places a specific card_go (from hand or picked from a panel) on a field_go
    public void DirectlyPlaceCard(GameObject card_go, GameObject field_go, bool is_player_turn)
    {
        CardStats card_stats = card_go.GetComponent<CardDisplay>().cardStats.GetComponent<CardStats>();
        card_go.GetComponent<CardHover>().DestroyEffect();
        string field_row = field_go == WeatherField ? Rows.Weather : field_go.transform.parent.name.ToLower();

        PlayCard(card_stats._id, field_row, is_player_turn);

        // Hand objects are rebuilt by the refresh; picked/AI cards would otherwise pile up under Temp
        if (card_go != null)
            Destroy(card_go);
    }

    // Plays a card from the hand: resolves it, refreshes the board and passes the turn (unless a choice is pending)
    public void PlayCard(int card_id, string field_row, bool is_player_turn)
    {
        MainInfo me = Info(is_player_turn);
        me.HandList.Remove(card_id);
        selectedCard = null;
        pendingAction = PendingAction.None;

        Card card = Db.Get(card_id);
        PlayCardAudio(card);
        int hand_before = me.HandList.Count;
        PlayOutcome outcome = PlayRules.Resolve(CurrentBoard(), card, field_row, is_player_turn, is_player_turn);
        if (me.HandList.Count > hand_before)
            GameAudio.PlaySfx("card_draw");

        RefreshBoard();
        SetHider(false);

        // Medic: the player picks a unit to restore from the discard panel
        if (outcome == PlayOutcome.WaitingForMedicChoice && ShowMedicChoice())
        {
            SetHider(true);
            return;
        }
        TurnCallBack(is_player_turn);
    }

    // The rules operate directly on the battle's lists
    private Board CurrentBoard()
    {
        return new Board
        {
            Player = PlayerInfo,
            Enemy = EnemyInfo,
            Weather = weatherList,
            DoubleSpies = doubleSpies,
            RandomMedic = randomMedic,
            Db = Db,
            Rnd = Rng.Shared,
        };
    }

    private bool ShowMedicChoice()
    {
        List<int> units = AbilityRules.MedicCandidates(PlayerInfo.DiscardList, Db);
        if (units.Count == 0)
            return false;
        OpenPanel().ShowToPlay("discard_list", units, true);
        return true;
    }

    public bool TryPlaySelectedCardOnBoard()
    {
        if (!IsSelectedCardPlayable())
            return false;

        CardStats selectedStats = SelectedStats();
        if (selectedStats.faction != Factions.Special)
            return false;

        if (selectedStats.row != Rows.Weather && selectedStats.ability != Abilities.Scorch)
            return false;

        DirectlyPlaceCard(selectedCard, WeatherField, true);
        return true;
    }

    public bool TryPlaySelectedDecoyOnCard(GameObject targetCard)
    {
        if (!IsSelectedCardPlayable())
            return false;

        CardStats selectedStats = SelectedStats();
        if (selectedStats.ability != Abilities.Decoy)
            return false;

        CardStats targetStats = targetCard.GetComponent<CardDisplay>().cardStats.GetComponent<CardStats>();
        string row = targetCard.transform.parent.parent.name.ToLower();
        bool valid_target = targetCard.CompareTag("Player") && targetStats.faction != Factions.Special
            && !targetStats.unique && targetStats.ability != Abilities.Leader && Rows.IsUnitRow(row);
        if (!valid_target)
        {
            GameAudio.PlaySfx("invalid");
            UIOverlay.Toast("Decoy can only swap a non-hero unit on your side");
            return true;
        }

        PlayCardAudio(Db.Get(selectedStats._id));
        selectedCard.GetComponent<CardHover>().DestroyEffect();
        selectedCard = null;
        PlayDecoy(targetStats._id, row, true, selectedStats._id);
        return true;
    }

    // Decoy: the target goes back to the hand, the decoy takes its place on the row
    public void PlayDecoy(int target_id, string row, bool is_player_card, int decoy_id)
    {
        Info(is_player_card).HandList.Remove(decoy_id);
        GameLog.Info("Decoying: " + target_id);
        PlayRules.Decoy(CurrentBoard(), target_id, row, is_player_card, decoy_id);
        RefreshBoard();
        TurnCallBack(is_player_card);
    }

    private bool IsSelectedCardPlayable()
    {
        return battleState == BattleState.PLAYERTURN && selectedCard != null
            && (pendingAction == PendingAction.None || pendingAction == PendingAction.PlacePickedCard);
    }

    private CardStats SelectedStats()
    {
        return selectedCard.GetComponent<CardDisplay>().cardStats.GetComponent<CardStats>();
    }

    private void DeselectCard()
    {
        if (selectedCard != null)
        {
            CardHover hover = selectedCard.GetComponent<CardHover>();
            hover.isHoverable = true;
            hover.TranslateDown();
            hover.DestroyEffect();
        }
        selectedCard = null;
        LightoffField(true);
        LightoffField(false);
    }

    private void PlayCardAudio(Card card)
    {
        if (card == null)
            return;

        if (card.IsSpecial)
        {
            if (card.row == Rows.Weather)
            {
                switch (card._idstr)
                {
                    case "biting_frost": GameAudio.PlaySfx("ability_weather_frost"); break;
                    case "impenetrable_fog": GameAudio.PlaySfx("ability_weather_fog"); break;
                    case "torrential_rain": GameAudio.PlaySfx("ability_weather_rain"); break;
                    case "clear_weather": GameAudio.PlaySfx("ability_weather_clear"); break;
                    default: GameAudio.PlaySfx("ability_weather"); break;
                }
            }
            else if (card.ability == Abilities.Scorch)
                GameAudio.PlaySfx("ability_scorch");
            else if (card.ability == Abilities.Decoy)
                GameAudio.PlaySfx("ability_decoy");
            else if (card.ability == Abilities.CommanderHorn)
                GameAudio.PlaySfx("ability_horn");
            else
                GameAudio.PlaySfx("card_play_special");
            return;
        }

        switch (card.ability)
        {
            case Abilities.Spy: GameAudio.PlaySfx("ability_spy"); return;
            case Abilities.Medic: GameAudio.PlaySfx("ability_medic"); return;
            case Abilities.Muster: GameAudio.PlaySfx("ability_muster"); return;
            case Abilities.UnitHorn: GameAudio.PlaySfx("ability_horn"); return;
            case Abilities.MoraleBoost: GameAudio.PlaySfx("ability_morale"); return;
            case Abilities.TightBond: GameAudio.PlaySfx("ability_tight_bond"); return;
        }
        if (Abilities.IsRowScorch(card.ability))
            GameAudio.PlaySfx("ability_scorch");
        else
            GameAudio.PlaySfx(card.unique ? "card_play_hero" : "card_play_unit");
    }

    //----------------------------------------------Board (drawn by BoardView)---------------------------------------------------//
    private void RecalculateStrengths()
    {
        StrengthCalculator.Recalculate(PlayerInfo, weatherList, doubleSpies, Db);
        StrengthCalculator.Recalculate(EnemyInfo, weatherList, doubleSpies, Db);
    }

    // Recalculates the strengths, then shows both sides from the lists
    private void RefreshBoard()
    {
        RecalculateStrengths();
        board.Refresh();
    }

    private void SetDeckObject(MainInfo info, bool is_player_deck) { board.SetDeck(info, is_player_deck); }
    public void SetHandObject(List<int> hand_list, bool is_player_hand) { board.SetHand(hand_list, is_player_hand); }
    private void SetDiscardObject(List<int> discard_list, bool is_player) { board.SetDiscard(discard_list, is_player); }
    private void CreateCardObjects(GameObject my_object, List<int> card_list, bool isSelectable, bool is_player_card) { board.CreateCardObjects(my_object, card_list, isSelectable, is_player_card); }
    public GameObject CreateCardObject(Card card, bool is_player_card) { return board.CreateCardObject(card, is_player_card); }
    public void HighlightField(CardStats card_stats, bool is_player_turn) { board.Highlight(card_stats, is_player_turn); }
    public void LightoffField(bool is_player_turn) { board.Lightoff(is_player_turn); }
    public void SetHider(bool hidden) { board.SetHider(hidden); }

    //----------------------------------------------Queries---------------------------------------------------//
    public Card GetCardStats(int card_id)
    {
        return Db.Get(card_id);
    }

    public List<Card> GetCardsList(List<int> card_list)
    {
        return Db.GetCards(card_list);
    }

    // Gets the row total or the field total of a player
    public int GetTotalScore(bool is_player, string row)
    {
        MainInfo info = Info(is_player);
        return row == "all" ? info.TotalScore() : info.RowScore(row);
    }

    //--------------ToMainMenu
    public void ToMainMenu()
    {
        GameAudio.PlaySfx("ui_click");
        SceneManager.LoadScene(SceneNames.Menu);
    }
}

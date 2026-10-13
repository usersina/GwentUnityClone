using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gwent.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum BattleState { START, PLAYERTURN, ENEMYTURN, ROUNDOVER, WON, LOST, DRAW }

// A multi-step player action in progress. While one is pending the turn cannot end (pass / leader are blocked).
public enum PendingAction { None, PanelChoice, PlacePickedCard, DiscardCards }

// Runs the battle: turns, rounds and the board UI. The rules themselves live in Gwent.Rules.
public class SceneController : MonoBehaviour
{
    private const string WeatherOverlayName = "WeatherOverlay";
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
    private FieldView playerView;
    private FieldView enemyView;
    private WeatherManager weatherManager;
    private Image weatherImage;
    private GameObject mulliganPrompt;
    private TextMeshProUGUI mulliganCount;
    private string mulliganHint;
    private int mulliganShown = -1;
    private Coroutine turnRoutine;
    private int turnGeneration;

    [Serializable]
    public class MainInfo : PlayerState
    {
        public string Name;
        public Image Avatar;
        public Sprite DeckBSprite;
    }

    //----------------------------------------Cached scene references----------------------------------------//
    // Looked up once instead of transform.Find chains on every refresh / every frame
    private class RowView
    {
        public RowManager Manager;
        public GameObject Unit;
        public GameObject Special;
        public Image UnitImage;
        public Image SpecialImage;
    }

    private class FieldView
    {
        public GameObject Root;
        public RowPicker Picker;
        public HandManager Hand;
        public DeckManager Deck;
        public PileManager Discard;
        public GameObject DiscardPile;
        public StatsManager Stats;
        public LeaderManager Leader;
        public GameObject LeaderHolder;
        public GameObject PassMarker;
        public readonly Dictionary<string, RowView> Rows = new Dictionary<string, RowView>();

        public FieldView(GameObject root)
        {
            Root = root;
            Transform t = root.transform;
            Picker = root.GetComponent<RowPicker>();
            Hand = t.Find("Hand").GetComponent<HandManager>();
            Deck = t.Find("Deck").GetComponent<DeckManager>();
            Discard = t.Find("Discard").GetComponent<PileManager>();
            DiscardPile = t.Find("Discard").Find("Pile").gameObject;
            Stats = t.Find("Stats").GetComponent<StatsManager>();
            Leader = t.Find("Leader").GetComponent<LeaderManager>();
            LeaderHolder = t.Find("Leader").Find("Holder").gameObject;
            PassMarker = t.Find("Stats").Find("Total").Find("Pass").gameObject;
            foreach (string row in Gwent.Rules.Rows.Units)
            {
                Transform row_t = t.Find(char.ToUpper(row[0]) + row.Substring(1));
                Rows[row] = new RowView
                {
                    Manager = row_t.GetComponent<RowManager>(),
                    Unit = row_t.Find("Unit").gameObject,
                    Special = row_t.Find("Special").gameObject,
                    UnitImage = row_t.Find("Unit").GetComponent<Image>(),
                    SpecialImage = row_t.Find("Special").GetComponent<Image>(),
                };
            }
        }
    }

    private void Awake()
    {
        Instance = this;
        Db = CardLibrary.Get(CardsDatabase);
        TempObject = GameObject.Find("Temp");
        playerView = new FieldView(PlayerField);
        enemyView = new FieldView(EnemyField);
        weatherManager = WeatherField.GetComponent<WeatherManager>();
        weatherImage = WeatherField.GetComponent<Image>();
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
        ShowMulliganPrompt();

        StartCoroutine(FirstTurnPicker());
    }

    private void Update()
    {
        UpdateMulliganCount();
        if (battleState == BattleState.START && PlayerInfo.CanExchange && ShouldSkipCardExchange())
        {
            GameLog.Info("Player has skipped cards exchanging !");
            FinishCardExchange();
        }

        // The pass button only exists during the player's turn, and not in the middle of a multi-step action
        bool can_pass = battleState == BattleState.PLAYERTURN && pendingAction == PendingAction.None;
        if (PassBtn.activeSelf != can_pass)
            PassBtn.SetActive(can_pass);

        SetActiveIfChanged(playerView.PassMarker, PlayerInfo.hasPassed);
        SetActiveIfChanged(enemyView.PassMarker, EnemyInfo.hasPassed);
    }

    private static void SetActiveIfChanged(GameObject go, bool active)
    {
        if (go.activeSelf != active)
            go.SetActive(active);
    }

    // Right click (or a second finger) keeps the hand; Escape opens the pause menu instead
    private bool ShouldSkipCardExchange()
    {
        return !UIOverlay.BlocksCancel && (GameInput.SecondaryPressed() || GameInput.SecondFingerTapped());
    }

    //----------------------------------------------Info helpers---------------------------------------------------//
    private MainInfo Info(bool is_player) { return is_player ? PlayerInfo : EnemyInfo; }
    private MainInfo Opponent(bool is_player) { return is_player ? EnemyInfo : PlayerInfo; }
    private FieldView View(bool is_player) { return is_player ? playerView : enemyView; }

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
                View(!is_player).Leader.DisableButtonRC();
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
        FieldView view = View(is_player_deck);
        view.Leader.leaderId = leader_id;
        view.Leader.type = Db.Get(leader_id).row;
        CreateCardObjects(view.LeaderHolder, new List<int> { leader_id }, false, is_player_deck);
        view.Leader.UISetPassive();

        // Emhyr, Invader of the North affects both players
        if (leader_id == LeaderIds.EmhyrInvaderOfTheNorth)
            randomMedic = true;
    }

    //----------------------------------------------Mulligan---------------------------------------------------//
    private void ShowMulliganPrompt()
    {
        if (!PlayerInfo.CanExchange)
            return;
        RectTransform prompt = UIKit.Panel(UIOverlay.Root, "MulliganPrompt", true).Place(new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(920, 250));
        TextMeshProUGUI title = UIKit.Text(prompt, "Redraw cards", UIKit.TextStyle.Heading, TextAlignmentOptions.Center);
        title.fontSize = 36;
        title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -44), new Vector2(860, 50));
        string how = Application.isMobilePlatform ? "Double-tap" : "Double-click";
        mulliganCount = UIKit.Text(prompt, "", UIKit.TextStyle.Body, TextAlignmentOptions.Center);
        mulliganCount.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -102), new Vector2(860, 44));
        mulliganHint = how + " up to 2 cards in your hand to redraw them";
        Button done = UIKit.Button(prompt, "Done", UIKit.ButtonStyle.Primary, FinishCardExchange, new Vector2(240, 64));
        ((RectTransform)done.transform).Place(new Vector2(0.5f, 0f), new Vector2(0, 58), new Vector2(240, 64));
        mulliganPrompt = prompt.gameObject;
        UpdateMulliganCount();
    }

    private void UpdateMulliganCount()
    {
        if (mulliganCount == null || mulliganShown == PlayerInfo.CardsExchanged)
            return;
        mulliganShown = PlayerInfo.CardsExchanged;
        mulliganCount.text = mulliganHint + "  (" + mulliganShown + "/2)";
    }

    public void FinishCardExchange()
    {
        PlayerInfo.CanExchange = false;
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
        playerView.Stats.UIUpdateGemLives(PlayerInfo.Lives);
        enemyView.Stats.UIUpdateGemLives(EnemyInfo.Lives);

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
        PostGameManager postGame = PostGame.GetComponent<PostGameManager>();
        postGame.ShowScores(PlayerInfo.RoundsScore, EnemyInfo.RoundsScore);
        PostGame.SetActive(true);
        switch (outcome)
        {
            case BattleState.DRAW:
                postGame.ShowDraw();
                break;
            case BattleState.WON:
                GameAudio.PlaySfx("victory");
                postGame.ShowWin();
                break;
            case BattleState.LOST:
                GameAudio.PlaySfx("defeat");
                postGame.ShowLose();
                break;
        }
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

    //----------------------------------------------HIGHLIGHTING---------------------------------------------------//
    // Highlights the rows where the selected card can go, and stores them in the RowPicker
    public void HighlightField(CardStats card_stats, bool is_player_turn)
    {
        FieldView mine = View(is_player_turn);
        FieldView opp = View(!is_player_turn);

        mine.Picker.ResetStored();
        weatherManager.isWeatherCard = false;

        if (card_stats.faction == Factions.Special)
        {
            if (card_stats.row == Rows.Special)
            {
                // Commander's Horn: any of the three special slots
                foreach (RowView row in mine.Rows.Values)
                {
                    row.SpecialImage.sprite = mine.Picker.specialSelected;
                    mine.Picker.StoreField(row.Special);
                }
            }
            else
            {
                // Weather / scorch go on the weather board; decoy targets a card on the rows
                HighlightUnitRows(mine, Rows.Units, card_stats.ability != Abilities.Decoy);
                if (card_stats.ability != Abilities.Decoy)
                    weatherManager.isWeatherCard = true;
            }
            return;
        }

        // Spies are played on the opponent's side
        FieldView target = card_stats.ability == Abilities.Spy ? opp : mine;
        target.Picker.ResetStored();
        string[] rows = card_stats.row == Rows.Agile ? new[] { Rows.Close, Rows.Range } : new[] { card_stats.row };
        HighlightUnitRows(target, rows.Where(Rows.IsUnitRow).ToArray(), true);
    }

    private static void HighlightUnitRows(FieldView view, string[] rows, bool store)
    {
        foreach (string row in rows)
        {
            view.Rows[row].UnitImage.sprite = SelectedSprite(view.Picker, row);
            if (store)
                view.Picker.StoreField(view.Rows[row].Unit);
        }
    }

    private static Sprite SelectedSprite(RowPicker picker, string row)
    {
        switch (row)
        {
            case Rows.Close: return picker.closeSelected;
            case Rows.Range: return picker.rangeSelected;
            default: return picker.siegeSelected;
        }
    }

    private static Sprite IdleSprite(RowPicker picker, string row)
    {
        switch (row)
        {
            case Rows.Close: return picker.close;
            case Rows.Range: return picker.range;
            default: return picker.siege;
        }
    }

    // Lights off any highlighted field
    public void LightoffField(bool is_player_turn)
    {
        FieldView view = View(is_player_turn);
        view.Picker.ResetStored();
        foreach (KeyValuePair<string, RowView> row in view.Rows)
        {
            row.Value.SpecialImage.sprite = view.Picker.special;
            row.Value.UnitImage.sprite = IdleSprite(view.Picker, row.Key);
        }

        // Light off the weather board and deselect weather card
        weatherImage.sprite = weatherManager.weather;
        weatherManager.isWeatherCard = false;
    }

    //----------------------------------------------PANELS (pickers)---------------------------------------------------//
    private PanelManager OpenPanel()
    {
        pendingAction = PendingAction.PanelChoice;
        Panel.SetActive(true);
        return Panel.GetComponent<PanelManager>();
    }

    // A card was chosen in a "play" panel (medic / weather from deck): it is moved to the hand, then placed on the board
    public void PlayBtnCleanUp(string field, int card_id, bool is_player_turn)
    {
        MainInfo info = Info(is_player_turn);
        GameLog.Info("Playing from: " + field);
        switch (field)
        {
            case "deck_list":
                info.DeckList.Remove(card_id);
                SetDeckObject(info, is_player_turn);
                break;
            case "discard_list":
                info.DiscardList.Remove(card_id);
                SetDiscardObject(info.DiscardList, is_player_turn);
                break;
        }

        // Removed from the hand again when placed
        info.HandList.Add(card_id);
        pendingAction = PendingAction.PlacePickedCard;
    }

    // A card was chosen in a "draw" panel (leader abilities): it goes to the hand and the turn ends
    public void ActuallyPickCard(string field, int card_id, bool is_player_turn)
    {
        MainInfo info = Info(is_player_turn);
        MainInfo opp_info = Opponent(is_player_turn);

        GameLog.Info("Picking from: " + field);
        switch (field)
        {
            case "deck_list":
                info.DeckList.Remove(card_id);
                SetDeckObject(info, is_player_turn);
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
        SetHandObject(info.HandList, is_player_turn);
        TurnCallBack(is_player_turn);
    }

    // Panel "Skip" button: give up the choice and end the turn (nothing is ever left half done)
    public void CancelPanel()
    {
        Panel.SetActive(false);
        GameAudio.PlaySfx("ui_click");
        DeselectCard();
        RefreshBoard();
        if (battleState == BattleState.PLAYERTURN)
            TurnCallBack(true);
        else
            pendingAction = PendingAction.None;
    }

    // Discard (Eredin, Bringer of Death: discard 2 then pick 1 from the deck)
    public void DiscardCard(int card_id, bool is_player_card)
    {
        MainInfo info = Info(is_player_card);
        if (!info.HandList.Remove(card_id))
            return;

        GameAudio.PlaySfx("card_discard");
        info.DiscardList.Add(card_id);
        SetHandObject(info.HandList, is_player_card);
        SetDiscardObject(info.DiscardList, is_player_card);
    }

    // Initialize the Deck List on screen
    public void DeckCardPicker(bool is_player_turn)
    {
        MainInfo info = Info(is_player_turn);
        info.CanDiscard = false;
        if (info.DeckList.Count > 0)
            OpenPanel().ShowToDraw("deck_list", info.DeckList, is_player_turn);
        else
            TurnCallBack(is_player_turn);
    }

    //----------------------------------------------LEADERS---------------------------------------------------//
    private enum LeaderOutcome { Unavailable, Instant, Pending }

    public bool CanUseLeader(bool is_player)
    {
        MainInfo info = Info(is_player);
        if (!info.canLeader || info.leaderDisabled || Db.Get(info.Leader).row != Rows.LeaderActive)
            return false;
        return is_player ? IsPlayerActionAllowed : battleState == BattleState.ENEMYTURN;
    }

    // Activate the leader's ACTIVE ability. Does nothing (and keeps the leader) when it would have no effect.
    public void ActivateLeader(int leader_id, bool is_player_turn)
    {
        if (!CanUseLeader(is_player_turn))
        {
            GameAudio.PlaySfx("invalid");
            if (is_player_turn && Info(true).leaderDisabled)
                UIOverlay.Toast("Your leader ability was cancelled by the opponent's leader");
            return;
        }

        string reason;
        LeaderOutcome outcome = ApplyLeader(leader_id, is_player_turn, out reason);
        if (outcome == LeaderOutcome.Unavailable)
        {
            GameAudio.PlaySfx("invalid");
            if (is_player_turn)
                UIOverlay.Toast(reason);
            return;
        }

        GameAudio.PlaySfx("leader");
        Info(is_player_turn).canLeader = false;
        View(is_player_turn).Leader.DisableButtonRC();

        RecalculateStrengths();
        RefreshBoard();

        // Pending abilities end the turn once their choice is made
        if (outcome == LeaderOutcome.Instant)
            TurnCallBack(is_player_turn);
    }

    private LeaderOutcome ApplyLeader(int leader_id, bool is_player_turn, out string reason)
    {
        MainInfo info = Info(is_player_turn);
        reason = null;

        string weather_row = LeaderIds.WeatherPickRow(leader_id);
        if (weather_row != null)
        {
            reason = "No matching weather card left in your deck";
            return PickWeatherCard(weather_row, is_player_turn) ? LeaderOutcome.Pending : LeaderOutcome.Unavailable;
        }

        // Horns, scorches, clear weather, double spies, agile: resolved by the rules right away
        if (LeaderRules.IsInstant(leader_id))
        {
            Board board = CurrentBoard();
            bool applied = LeaderRules.ApplyInstant(board, leader_id, is_player_turn, out reason);
            doubleSpies = board.DoubleSpies;
            return applied ? LeaderOutcome.Instant : LeaderOutcome.Unavailable;
        }

        switch (leader_id)
        {
            case LeaderIds.EredinBringerOfDeath:
                if (!is_player_turn || info.HandList.Count < 2)
                {
                    reason = "You need at least 2 cards in hand";
                    return LeaderOutcome.Unavailable;
                }
                info.CanDiscard = true;
                info.CardsDiscarded = 0;
                pendingAction = PendingAction.DiscardCards;
                UIOverlay.Toast((Application.isMobilePlatform ? "Double-tap" : "Double-click") + " 2 cards to discard them", 4f);
                return LeaderOutcome.Pending;
            case LeaderIds.EredinDestroyerOfWorlds:
                reason = "No unit in your discard pile to restore";
                return PickDiscardCard(is_player_turn) ? LeaderOutcome.Pending : LeaderOutcome.Unavailable;
            case LeaderIds.EmhyrTheRelentless:
                reason = "No unit in your opponent's discard pile";
                return PickOppDiscard(is_player_turn) ? LeaderOutcome.Pending : LeaderOutcome.Unavailable;
            case LeaderIds.EmhyrEmperorOfNilfgaard:
                reason = "Your opponent has no cards in hand";
                return LookHandRandom(3, is_player_turn) ? LeaderOutcome.Pending : LeaderOutcome.Unavailable;
        }
        reason = "This leader ability is passive";
        return LeaderOutcome.Unavailable;
    }

    // Plays a weather card from deck (except clear weather); false when there is none
    public bool PickWeatherCard(string row, bool is_player_turn)
    {
        if (!is_player_turn)
            return false;
        List<int> weather_cards = new List<int>();
        foreach (int card_id in PlayerInfo.DeckList)
        {
            Card card = Db.Get(card_id);
            if (card.row != Rows.Weather || card.ability == Abilities.ClearWeather)
                continue;
            if (row == "all" || card_id == CardIds.WeatherFor(row))
                weather_cards.Add(card_id);
        }
        if (weather_cards.Count == 0)
            return false;
        OpenPanel().ShowToPlay("deck_list", weather_cards, true);
        return true;
    }

    // Draw a unit from your own discard pile (no heroes or special cards); false when there is none
    public bool PickDiscardCard(bool is_player_card)
    {
        List<int> units_list = AbilityRules.MedicCandidates(Info(is_player_card).DiscardList, Db);
        if (!is_player_card || units_list.Count == 0)
            return false;
        OpenPanel().ShowToDraw("discard_list", units_list, true);
        return true;
    }

    // Draw a unit from the opponent's discard pile (no heroes or special cards); false when there is none
    public bool PickOppDiscard(bool is_player_turn)
    {
        List<int> units_list = AbilityRules.MedicCandidates(Opponent(is_player_turn).DiscardList, Db);
        if (!is_player_turn || units_list.Count == 0)
            return false;
        OpenPanel().ShowToDraw("opp_discard_list", units_list, true);
        return true;
    }

    // View random cards from the opponent's hand; false when the hand is empty
    public bool LookHandRandom(int cards_num, bool is_player_turn)
    {
        MainInfo opp_info = Opponent(is_player_turn);
        if (!is_player_turn || opp_info.HandList.Count < 1)
            return false;
        List<int> random_list = Rng.Shuffle(opp_info.HandList).Take(cards_num).ToList();
        OpenPanel().ShowToView(random_list, true);
        return true;
    }

    // AI leader that plays a weather card from its deck (chosen by the AI)
    public bool AiUseWeatherLeader(int weather_card_id)
    {
        if (!CanUseLeader(false) || LeaderIds.WeatherPickRow(EnemyInfo.Leader) == null || !EnemyInfo.DeckList.Remove(weather_card_id))
            return false;
        EnemyInfo.canLeader = false;
        enemyView.Leader.DisableButtonRC();
        GameAudio.PlaySfx("leader");
        EnemyInfo.HandList.Add(weather_card_id);
        PlayCard(weather_card_id, Rows.Weather, false);
        return true;
    }

    //----------------------------------------------HIDER---------------------------------------------------//
    // Hides the player's hand interactability
    public void SetHider(bool hidden)
    {
        hiderObject.transform.localPosition = hidden ? new Vector2(0, -461) : new Vector2(0, -618);
    }

    //----------------------------------------------BOARD REFRESH---------------------------------------------------//
    private void RecalculateStrengths()
    {
        StrengthCalculator.Recalculate(PlayerInfo, weatherList, doubleSpies, Db);
        StrengthCalculator.Recalculate(EnemyInfo, weatherList, doubleSpies, Db);
    }

    // Rebuilds both sides from the lists, then scores and highlights
    private void RefreshBoard()
    {
        RecalculateStrengths();
        RefreshFields(true);
        RefreshFields(false);
        RefreshWeather();
        LightoffField(true);
        LightoffField(false);
        UpdateUIScores(true);
        UpdateUIScores(false);
    }

    // Updates the deck count and the stack of card backs
    private void SetDeckObject(MainInfo info, bool is_player_deck)
    {
        View(is_player_deck).Deck.UIUpdateDeckBase(info.DeckBSprite, info.DeckList.Count);
    }

    // Create Hand Cards from hand_list and updates card count
    public void SetHandObject(List<int> hand_list, bool is_player_hand)
    {
        FieldView view = View(is_player_hand);
        view.Hand.ClearHandObject();
        CreateCardObjects(view.Hand.gameObject, hand_list, true, is_player_hand);
        view.Hand.ResizeHand();
        view.Stats.UIUpdateHandCount(hand_list.Count);
    }

    private void SetDiscardObject(List<int> discard_list, bool is_player_hand)
    {
        FieldView view = View(is_player_hand);
        view.Discard.ClearDiscardPile();
        CreateCardObjects(view.DiscardPile, discard_list, false, is_player_hand);
    }

    private void RefreshFields(bool is_player_field)
    {
        MainInfo info = Info(is_player_field);
        FieldView view = View(is_player_field);

        SetHandObject(info.HandList, is_player_field);

        foreach (string row in Rows.Units)
        {
            RowView row_view = view.Rows[row];
            List<int> units = info.UnitRow(row);
            row_view.Manager.ClearUnitObject();
            CreateCardObjects(row_view.Unit, units, false, is_player_field);
            row_view.Manager.ResizeUnitObject(units.Count);

            row_view.Manager.ClearSpecialObject();
            CreateCardObjects(row_view.Special, info.SpecialRow(row), false, is_player_field);
        }

        SetDiscardObject(info.DiscardList, is_player_field);
        RefreshWeatherOverlays(view);
    }

    private void RefreshWeather()
    {
        weatherManager.ClearWeatherObject();
        CreateCardObjects(WeatherField, weatherList, false, true);
    }

    private void RefreshWeatherOverlays(FieldView view)
    {
        SetWeatherOverlay(view.Rows[Rows.Close].Unit.transform, weatherList.Contains(CardIds.BitingFrost), "Cards/EffectBox/overlay_frost", new Color32(108, 198, 255, 78));
        SetWeatherOverlay(view.Rows[Rows.Range].Unit.transform, weatherList.Contains(CardIds.ImpenetrableFog), "Cards/EffectBox/overlay_fog", new Color32(185, 190, 188, 86));
        SetWeatherOverlay(view.Rows[Rows.Siege].Unit.transform, weatherList.Contains(CardIds.TorrentialRain), "Cards/EffectBox/overlay_rain", new Color32(61, 111, 184, 86));
    }

    private void SetWeatherOverlay(Transform unitRow, bool isActive, string spriteResourcePath, Color fallbackColor)
    {
        Transform existingOverlay = unitRow.Find(WeatherOverlayName);
        if (!isActive)
        {
            if (existingOverlay != null)
                Destroy(existingOverlay.gameObject);
            return;
        }

        GameObject overlayObject;
        if (existingOverlay == null)
        {
            overlayObject = new GameObject(WeatherOverlayName, typeof(RectTransform));
            overlayObject.transform.SetParent(unitRow, false);

            LayoutElement layoutElement = overlayObject.AddComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;

            Image image = overlayObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = false;

            RectTransform rectTransform = overlayObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
        else
            overlayObject = existingOverlay.gameObject;

        overlayObject.transform.SetAsLastSibling();

        Image overlayImage = overlayObject.GetComponent<Image>();
        Sprite overlaySprite = SpriteCache.Load(spriteResourcePath);
        overlayImage.sprite = overlaySprite;
        overlayImage.color = overlaySprite != null ? new Color(1f, 1f, 1f, 0.82f) : fallbackColor;
    }

    // Updates the UI scores
    private void UpdateUIScores(bool is_player_field)
    {
        MainInfo info = Info(is_player_field);
        FieldView view = View(is_player_field);

        // The card objects were just re-created: their strength labels update next frame (coroutine)
        foreach (string row in Rows.Units)
        {
            RowManager manager = view.Rows[row].Manager;
            StartCoroutine(manager.UIUpdateUnitsStrength(info.BaseStrengths(row), info.ModifiedStrengths(row)));
            manager.UpdateRowScore(info.ModifiedStrengths(row));
        }

        view.Stats.UIUpdateTotalScore(info.MCloseStrength, info.MRangeStrength, info.MSiegeStrength);
        SetDeckObject(info, is_player_field);
        view.Stats.UIUpdateHandCount(info.HandList.Count);
    }

    // Generate Cards GameObject from cards prefab and set their parent to the passed gameobject
    private void CreateCardObjects(GameObject my_object, List<int> card_list, bool isSelectable, bool is_player_card)
    {
        foreach (int card_id in card_list)
        {
            GameObject instantiatedCard = CreateCardObject(Db.Get(card_id), is_player_card);
            instantiatedCard.GetComponent<CardSelect>().isSelectable = isSelectable;
            instantiatedCard.GetComponent<CardHover>().isHoverable = isSelectable;
            instantiatedCard.GetComponent<CardExchange>().isExchangeable = Db.Get(card_id).ability != Abilities.Leader;
            instantiatedCard.transform.SetParent(my_object.transform, false);
        }
    }

    // A card object with its stats filled in (not parented)
    public GameObject CreateCardObject(Card card, bool is_player_card)
    {
        GameObject instantiatedCard = Instantiate(CardPrefab);
        instantiatedCard.name = card._id.ToString();
        CardStats stats = instantiatedCard.transform.Find("Stats").GetComponent<CardStats>();
        stats.name = card.name;
        stats._id = card._id;
        stats._idstr = card._idstr;
        stats.faction = card.faction;
        stats.unique = card.unique;
        stats.strength = card.strength;
        stats.row = card.row;
        stats.ability = card.ability;
        instantiatedCard.tag = is_player_card ? "Player" : "Enemy";
        return instantiatedCard;
    }

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

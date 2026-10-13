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
    private bool roundOpening;      // the next turn opens a round (announced as "Round N")
    private bool? lastTurnWasPlayer; // whose turn was announced last in this round
    private bool lastPasserWasPlayer; // after a drawn round, whoever passed last starts the next one
    private string whiteFlameNotice;  // shown once the redraw is over

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
        // The opponent the player chose before the battle, or a random one
        TextAsset enemy_deck = OpponentDecks.ForBattle() ?? EnemyDeck;
        GameLog.Info("Opponent deck: " + enemy_deck.name);
        Deck oppDeck = DeckRules.Sanitize(CardLibrary.ParseDeck(enemy_deck.text, enemy_deck.name), Db);

        // Emhyr, The White Flame cancels the other leader's ability, passive ones included (Daisy's extra card,
        // Invader's random medics, Treacherous' doubled spies), unless both sides play him
        bool white_flame = myDeck.Leader != oppDeck.Leader
            && (myDeck.Leader == LeaderIds.EmhyrTheWhiteFlame || oppDeck.Leader == LeaderIds.EmhyrTheWhiteFlame);
        PrepareField(myDeck, PlayerInfo, true, white_flame);
        PrepareField(oppDeck, EnemyInfo, false, white_flame);

        foreach (bool is_player in new[] { true, false })
            if (white_flame && Info(is_player).Leader != LeaderIds.EmhyrTheWhiteFlame)
            {
                Info(is_player).leaderDisabled = true;
                View(is_player).Leader.MarkCancelled();
                whiteFlameNotice = is_player ? "Your opponent's leader cancels your leader ability" : "Your leader cancels your opponent's leader ability";
            }

        // Passive leaders that affect both players
        bool either(int leader) { return !white_flame && (myDeck.Leader == leader || oppDeck.Leader == leader); }
        doubleSpies = either(LeaderIds.EredinTheTreacherous);
        randomMedic = either(LeaderIds.EmhyrInvaderOfTheNorth);
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
    private void PrepareField(Deck deck, MainInfo info, bool is_player_deck, bool white_flame)
    {
        info.Faction = deck.Faction;
        info.DeckBSprite = Resources.Load<Sprite>("Cards/Back/" + deck.Faction);
        info.DeckList = Rng.Shuffle(deck.Cards);
        info.Leader = deck.Leader;

        View(is_player_deck).Stats.UISetGeneral(info.Faction);
        View(is_player_deck).Stats.UISetPlayer(info.Name, SpriteCache.Load("Cards/List/591x380/" + deck.Leader));
        SetLeaderCard(deck.Leader, is_player_deck);

        // Francesca, Daisy of the Valley: one extra card at the beginning of the battle
        int cards_to_draw = deck.Leader == LeaderIds.FrancescaDaisyOfTheValley && !white_flame ? 11 : 10;
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

    // Redraw: the deck's top card takes the card's place in the hand, the card goes to the bottom of the deck
    public void ExchangeCard(int card_id, bool is_player_card)
    {
        MainInfo info = Info(is_player_card);
        if (RoundRules.Redraw(info, card_id) == -1)
            return;

        GameAudio.PlaySfx("card_exchange");

        SetDeckObject(info, is_player_card);
        SetHandObject(info.HandList, is_player_card);
    }

    //----------------------------------------------TURNS---------------------------------------------------//
    // Picks who takes the first turn of a round: the given side, or for the battle's first round the Scoia'tael perk
    // or a coin toss
    private IEnumerator FirstTurnPicker(bool? player_starts = null)
    {
        if (EnemyInfo.CanExchange)
        {
            // Initialize AI (first turn only)
            AIManager.AIInitialize();
            AIManager.AIExchangeCards();
        }

        // Wait for all players to either redraw or skip, and for the end of the last round's notices
        while (EnemyInfo.CanExchange || PlayerInfo.CanExchange || BattleBanner.Busy)
            yield return null;

        if (mulliganPrompt != null)
            Destroy(mulliganPrompt);

        // The White Flame's notice, once the redraw is over (The Witcher 3 announces it at the start)
        if (whiteFlameNotice != null)
        {
            BattleBanner.Show("Emhyr: The White Flame", whiteFlameNotice, null, BattleBanner.Long);
            whiteFlameNotice = null;
            while (BattleBanner.Busy)
                yield return null;
        }
        roundOpening = true;
        lastTurnWasPlayer = null;

        if (player_starts.HasValue)
            StartTurn(player_starts.Value);
        //------------Scoia'tael: decides who takes the first turn of the battle-------------//
        else if (PlayerInfo.Faction == Factions.Scoiatael && EnemyInfo.Faction != Factions.Scoiatael)
            TurnPicker.SetActive(true);
        else if (PlayerInfo.Faction != Factions.Scoiatael && EnemyInfo.Faction == Factions.Scoiatael)
            StartTurn(false); // The AI's Scoia'tael goes first, as in The Witcher 3
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
        if (!PlayerInfo.hasPassed && OutOfPlays(true))
            MarkPassed(true, "No cards left to play");
        if (PlayerInfo.hasPassed)
        {
            CheckRoundEnd();
            yield break;
        }
        AnnounceTurn(true);
        yield return new WaitForSeconds(1f);
        if (generation != turnGeneration)
            yield break;
        GameLog.Info("Player Turn !");
        GameAudio.PlaySfx("turn_player");
        battleState = BattleState.PLAYERTURN;
    }

    private IEnumerator EnemyTurn(int generation)
    {
        if (!EnemyInfo.hasPassed && OutOfPlays(false))
            MarkPassed(false, "No cards left to play");
        if (EnemyInfo.hasPassed)
        {
            CheckRoundEnd();
            yield break;
        }
        battleState = BattleState.ENEMYTURN;
        AnnounceTurn(false);
        GameLog.Info("Enemy Turn !");
        GameAudio.PlaySfx("turn_enemy");
        yield return new WaitForSeconds(2f);
        if (generation != turnGeneration)
            yield break;
        AIManager.AIStartTurn();
    }

    // The Witcher 3's turn notices: who opens the round, then each change of side. Not while the other side has
    // passed: its pass was announced already and the turns no longer alternate.
    private void AnnounceTurn(bool is_player)
    {
        if (roundOpening)
        {
            roundOpening = false;
            BattleBanner.Show("Round " + (PlayerInfo.RoundsScore.Count + 1), is_player ? "You will go first" : "Your opponent will go first");
        }
        else if (lastTurnWasPlayer != is_player && !Opponent(is_player).hasPassed)
            BattleBanner.Show(is_player ? "Your turn!" : "Opponent's turn");
        lastTurnWasPlayer = is_player;
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

        MarkPassed(is_player_turn, null);
        TurnCallBack(is_player_turn);
    }

    // The Witcher 3: a side with no card in hand and no leader ability left passes automatically
    private bool OutOfPlays(bool is_player)
    {
        MainInfo info = Info(is_player);
        return info.HandList.Count == 0 && (!info.canLeader || info.leaderDisabled || Db.Get(info.Leader).row != Rows.LeaderActive);
    }

    private void MarkPassed(bool is_player, string why)
    {
        GameAudio.PlaySfx("pass");
        // A pass that ends the round is followed by the round's result instead; at the opening of a round the
        // other side's "Round N" notice says it all
        if (!Opponent(is_player).hasPassed && !roundOpening)
            BattleBanner.Show(is_player ? "You passed" : "Your opponent has passed", why);
        Info(is_player).hasPassed = true;
        lastPasserWasPlayer = is_player;
        if (is_player)
            DeselectCard();
    }

    //----------------------------------------------ROUNDS---------------------------------------------------//
    private void ManageOutcome()
    {
        StopTurn();
        battleState = BattleState.ROUNDOVER;
        RecalculateStrengths();
        int player_total = PlayerInfo.TotalScore(), enemy_total = EnemyInfo.TotalScore();
        RoundWinner winner = RoundRules.DecideRound(player_total, enemy_total, PlayerInfo.Faction, EnemyInfo.Faction);
        RoundRules.ApplyRoundResult(PlayerInfo, EnemyInfo, winner);
        PlayerInfo.hasPassed = false;
        EnemyInfo.hasPassed = false;

        // Update UI Gems
        View(true).Stats.UIUpdateGemLives(PlayerInfo.Lives);
        View(false).Stats.UIUpdateGemLives(EnemyInfo.Lives);

        // The result over the board as it ended, then the board clears
        UITheme theme = UITheme.Current;
        string result = winner == RoundWinner.Player ? "You won the round!" : winner == RoundWinner.Enemy ? "Your opponent won the round" : "The round ended in a draw";
        string score = player_total + "  –  " + enemy_total;
        if (player_total == enemy_total && winner != RoundWinner.Draw)
            score += "   ·   Nilfgaard wins ties";
        BattleBanner.Show(result, score, winner == RoundWinner.Player ? theme.goldBright : winner == RoundWinner.Enemy ? theme.danger : theme.textSecondary, BattleBanner.Long);
        PlayRoundOutcomeAudio(winner);
        StartCoroutine(FinishRound(winner));
    }

    private IEnumerator FinishRound(RoundWinner winner)
    {
        while (BattleBanner.Busy)
            yield return null;

        if (PlayerInfo.Lives == 0 && EnemyInfo.Lives == 0)
            ConcludeBattle(BattleState.DRAW);
        else if (EnemyInfo.Lives == 0)
            ConcludeBattle(BattleState.WON);
        else if (PlayerInfo.Lives == 0)
            ConcludeBattle(BattleState.LOST);
        else
            PrepareNextRound(winner);
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
        foreach (bool is_player in new[] { true, false })
        {
            int kept = RoundRules.CleanupBoard(Info(is_player), Rng.Shared, Db);
            if (kept != -1)
                BattleBanner.Show("Monsters perk", (is_player ? "Your " : "Your opponent's ") + Db.Get(kept).DisplayName + " stays on the battlefield");
        }
        weatherList.Clear();

        //------Northern Realms: Get one additional card from your deck every time you win a round-------------//
        if (winner != RoundWinner.Draw && Info(winner == RoundWinner.Player).Faction == Factions.NorthernRealms)
        {
            bool is_player = winner == RoundWinner.Player;
            if (DrawCards(1, Info(is_player), is_player) > 0)
                BattleBanner.Show("Northern Realms perk", is_player ? "You draw a card for winning the round" : "Your opponent draws a card for winning the round");
        }

        RefreshBoard();

        // The Witcher 3: the round's winner starts the next one; after a draw, whoever passed last
        StartCoroutine(FirstTurnPicker(winner == RoundWinner.Player || (winner == RoundWinner.Draw && lastPasserWasPlayer)));
    }

    public bool IsBattleOver
    {
        get { return battleState == BattleState.WON || battleState == BattleState.LOST || battleState == BattleState.DRAW; }
    }

    // Giving up (pause menu): everything in progress stops and the battle is lost
    public void GiveUp()
    {
        if (IsBattleOver)
            return;
        GameLog.Info("Player gave up");
        StopTurn();
        StopAllCoroutines();
        BattleBanner.Clear();
        if (mulliganPrompt != null)
            Destroy(mulliganPrompt);
        TurnPicker.SetActive(false);
        Panel.SetActive(false);
        pendingAction = PendingAction.None;
        DeselectCard();
        SetHider(false);
        ConcludeBattle(BattleState.LOST, true);
    }

    private void ConcludeBattle(BattleState outcome, bool gave_up = false)
    {
        GameLog.Info("Battle ended: " + outcome);
        battleState = outcome;
        PostGame.SetActive(true);
        PostGame.GetComponent<PostGameManager>().Show(outcome, PlayerInfo, EnemyInfo, gave_up);
        if (outcome == BattleState.WON)
            GameAudio.PlaySfx("victory");
        else if (outcome == BattleState.LOST)
            GameAudio.PlaySfx("defeat");
    }

    // Draws up to num cards (fewer if the deck runs out), returns how many were drawn
    private int DrawCards(int num, MainInfo info, bool is_player_deck)
    {
        int drawn = RoundRules.Draw(info, num);
        if (drawn == 0)
            return 0;
        SetDeckObject(info, is_player_deck);
        SetHandObject(info.HandList, is_player_deck);
        GameAudio.PlaySfx("card_draw");
        return drawn;
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

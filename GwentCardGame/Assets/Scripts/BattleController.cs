using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gwent.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum BattleState { Start, PlayerTurn, EnemyTurn, RoundOver, Won, Lost, Draw }

// A multi-step player action in progress. While one is pending the turn cannot end (pass / leader are blocked).
public enum PendingAction { None, PanelChoice, PlacePickedCard, DiscardCards }

// Runs the battle: setup, turns, rounds and playing cards (leaders and pickers: BattleController.Leaders.cs).
// The rules themselves live in Gwent.Rules; BoardView draws the board from the model.
public partial class BattleController : MonoBehaviour
{
    private const string DefaultDeckResource = "Decks/player_default";

    public static BattleController Instance { get; private set; }

    public BattleState battleState;
    public PendingAction pendingAction;

    public GameObject PostGame;
    public GameObject CardPrefab;
    public GameObject WeatherField;

    public TextAsset CardsDatabase;
    public TextAsset EnemyDeck;

    public GameObject PlayerField;
    public GameObject EnemyField;

    public BattleSide PlayerInfo;
    public BattleSide EnemyInfo;

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

    private AiPlayer ai;
    private BoardView board;
    private BoardPresenter presenter;
    private int presenting;           // plays being shown (the rules resolved them already)
    private readonly List<BoardChange> presented = new List<BoardChange>();
    private GameObject mulliganPrompt;
    private Coroutine turnRoutine;
    private int turnGeneration;
    private bool roundOpening;      // the next turn opens a round (after "Round Start")
    private bool? firstPlayer;      // who takes the battle's first turn
    private bool? lastTurnWasPlayer; // whose turn was announced last in this round
    private bool lastPasserWasPlayer; // after a drawn round, whoever passed last starts the next one
    private string whiteFlameNotice;  // shown once the redraw is over

    [Serializable]
    public class BattleSide : PlayerState
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
        presenter = new BoardPresenter(this, board, Db);
        ai = GetComponent<AiPlayer>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            Time.timeScale = 1f; // the game speed is the battle's
        }
    }

    public LeaderSlot PlayerLeader { get { return View(true).Leader; } }

    private void Start()
    {
        if (!GameInput.IsTouch)
            gameObject.AddComponent<BattleKeys>();
        GameAudio.PlayGameMusic();
        Time.timeScale = GameSettings.GameTimeScale;
        PlayerInfo.Name = "Player";
        EnemyInfo.Name = "Opponent";

        GameLog.Info("Starting battle...");
        GameAudio.PlaySfx("game_start");
        UIOverlay.SetEscapeAction(OnEscape);
        battleState = BattleState.Start;
        SetupBattle();
        StartCoroutine(BattleOpening());
    }

    // The Witcher 3's opening: the leaders' notice (The White Flame), who goes first (a coin toss, or the Scoia'tael
    // perk), then the redraw; the first round starts once both sides are done
    private IEnumerator BattleOpening()
    {
        if (whiteFlameNotice != null)
        {
            BattleBanner.Show(BattleBanner.FactionEmblem(Factions.Nilfgaard), whiteFlameNotice, BattleBanner.Long);
            whiteFlameNotice = null;
        }

        if (PlayerInfo.Faction == Factions.Scoiatael && EnemyInfo.Faction != Factions.Scoiatael)
        {
            while (BattleBanner.Busy)
                yield return null;
            firstPlayer = null;
            TurnPicker.SetActive(true); // the player's choice comes back through ChooseFirstPlayer
            while (!firstPlayer.HasValue)
                yield return null;
            BattleBanner.Show(firstPlayer.Value ? BattleBanner.YouFirst : BattleBanner.OpponentFirst, firstPlayer.Value ? "You will go first" : "Your opponent will go first");
        }
        else if (PlayerInfo.Faction != Factions.Scoiatael && EnemyInfo.Faction == Factions.Scoiatael)
        {
            firstPlayer = false; // the AI's Scoia'tael goes first
            BattleBanner.Show(BattleBanner.FactionEmblem(Factions.Scoiatael), "Opponent used the Scoia'tael faction perk to go first.");
        }
        else
        {
            firstPlayer = Rng.Shared.Next(0, 2) != 0;
            BattleBanner.Show(firstPlayer.Value ? BattleBanner.YouFirst : BattleBanner.OpponentFirst, firstPlayer.Value ? "You will go first" : "Your opponent will go first");
        }
        while (BattleBanner.Busy)
            yield return null;

        if (PlayerInfo.CanExchange)
            mulliganPrompt = RedrawPicker.ShowPhase(this);
        yield return RoundOpening(firstPlayer.Value);
    }

    public Rect DeckScreenRect(bool is_player) { return board.DeckRect(is_player); }

    // The Scoia'tael dialog's answer
    public void ChooseFirstPlayer(bool player_first)
    {
        firstPlayer = player_first;
    }

    private void Update()
    {
        if (battleState == BattleState.Start && PlayerInfo.CanExchange && mulliganPrompt != null && ShouldSkipCardExchange())
        {
            GameLog.Info("Player has skipped cards exchanging !");
            FinishCardExchange();
        }

        // The pass button only exists during the player's turn, and not in the middle of a multi-step action
        bool can_pass = battleState == BattleState.PlayerTurn && pendingAction == PendingAction.None && presenting == 0;
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
    private BattleSide Info(bool is_player) { return is_player ? PlayerInfo : EnemyInfo; }
    private BattleSide Opponent(bool is_player) { return is_player ? EnemyInfo : PlayerInfo; }
    private BoardView.FieldView View(bool is_player) { return board.View(is_player); }

    public bool IsPlayerActionAllowed
    {
        get { return battleState == BattleState.PlayerTurn && pendingAction == PendingAction.None && presenting == 0; }
    }

    // A play is being shown: nothing else can be done until it ends
    public bool IsPresenting { get { return presenting > 0; } }

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
        string path = DeckStorage.BattleDeckPath;
        if (DeckStorage.Exists(path)) // a deck file, or a preset (an AI deck)
        {
            Deck deck = DeckRules.Sanitize(DeckStorage.Load(path), Db); // copies beyond the W3 limits are dropped
            List<string> errors = DeckRules.Validate(deck, Db);
            if (errors.Count == 0)
            {
                return deck;
            }
            UIOverlay.Toast("Your selected deck can't be used (" + errors[0] + "). Playing with the default deck.", 4f);
        }

        TextAsset deckAsset = Resources.Load<TextAsset>(DefaultDeckResource);
        return DeckRules.Sanitize(CardLibrary.ParseDeck(deckAsset.text, DefaultDeckResource), Db);
    }

    // Prepares the field for the first time
    private void PrepareField(Deck deck, BattleSide info, bool is_player_deck, bool white_flame)
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
        BattleSide info = Info(is_player_card);
        if (RoundRules.Redraw(info, card_id) == -1)
            return;

        GameAudio.PlaySfx("card_exchange");

        SetDeckObject(info, is_player_card);
        SetHandObject(info.HandList, is_player_card);
    }

    //----------------------------------------------TURNS---------------------------------------------------//
    // Picks who takes the first turn of a round: the given side, or for the battle's first round the Scoia'tael perk
    // or a coin toss
    private IEnumerator RoundOpening(bool player_starts)
    {
        if (EnemyInfo.CanExchange)
        {
            // Initialize AI (first turn only)
            ai.Initialize();
            ai.ExchangeCards();
        }

        // Wait for all players to either redraw or skip (and the last redrawn card to land), and for the end of the last
        // round's notices
        while (EnemyInfo.CanExchange || PlayerInfo.CanExchange || RedrawPicker.IsAnimating || BattleBanner.Busy)
            yield return null;

        if (mulliganPrompt != null)
            Destroy(mulliganPrompt);

        BattleBanner.Show(BattleBanner.RoundStart, "Round Start");
        while (BattleBanner.Busy)
            yield return null;
        roundOpening = true;
        lastTurnWasPlayer = null;
        // The Witcher 3: a side that starts a round with no card in hand sits it out, even with its leader unused
        foreach (bool is_player in new[] { true, false })
            if (Info(is_player).HandList.Count == 0 && !Info(is_player).hasPassed)
                MarkPassed(is_player);
        StartTurn(player_starts);
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
            MarkPassed(true);
        if (PlayerInfo.hasPassed)
        {
            CheckRoundEnd();
            yield break;
        }
        AnnounceTurn(true);
        yield return new WaitForSeconds(0.3f);
        while (BattleBanner.Busy)
            yield return null;
        if (generation != turnGeneration)
            yield break;
        GameLog.Info("Player Turn !");
        GameAudio.PlaySfx("turn_player");
        battleState = BattleState.PlayerTurn;
    }

    private IEnumerator EnemyTurn(int generation)
    {
        if (!EnemyInfo.hasPassed && OutOfPlays(false))
            MarkPassed(false);
        if (EnemyInfo.hasPassed)
        {
            CheckRoundEnd();
            yield break;
        }
        battleState = BattleState.EnemyTurn;
        AnnounceTurn(false);
        GameLog.Info("Enemy Turn !");
        GameAudio.PlaySfx("turn_enemy");
        AiPlayer.Decision decision = ai.StartDecision(); // Hard thinks while the turn's notice shows
        yield return new WaitForSeconds(0.3f);
        while (!decision.Done || BattleBanner.Busy)
            yield return null;
        yield return new WaitForSeconds(0.15f);
        if (generation != turnGeneration)
            yield break;

        // The card the opponent plays (or its leader) comes in from above the board to the showcase and stays a
        // moment there, then glides to its place
        AiAction action = decision.Action;
        int shown = action.Type == AiActionType.PlayCard ? action.CardId : action.Type == AiActionType.UseLeader ? EnemyInfo.Leader : -1;
        if (shown != -1)
        {
            yield return CardShowcase.Opponent(Db.Get(shown), board.EntryRect(CardShowcase.ScreenSize()));
            if (generation != turnGeneration)
            {
                CardShowcase.Clear();
                yield break;
            }
        }
        ai.Play(action);
    }

    // The Witcher 3's turn notices: each change of side. Not while the other side has passed: its pass was
    // announced already and the turns no longer alternate.
    private void AnnounceTurn(bool is_player)
    {
        roundOpening = false;
        if (lastTurnWasPlayer != is_player && !Opponent(is_player).hasPassed)
            BattleBanner.Show(is_player ? BattleBanner.YourTurn : BattleBanner.OpponentsTurn, is_player ? "Your turn!" : "Opponent's turn");
        lastTurnWasPlayer = is_player;
    }

    public void CheckRoundEnd()
    {
        if (PlayerInfo.hasPassed && EnemyInfo.hasPassed)
        {
            GameLog.Info("Round has ended");
            ManageOutcome();
        }
        else if (PlayerInfo.hasPassed)
            StartTurn(false);
        else if (EnemyInfo.hasPassed)
            StartTurn(true);
    }

    // Ends whatever the given side was doing and hands the turn to the other side
    public void EndTurn(bool is_player_turn)
    {
        CardShowcase.Clear();
        pendingAction = PendingAction.None;
        PlayerInfo.CanDiscard = false;
        SetHider(false);
        StartTurn(!is_player_turn);
    }

    // Reference: PassButton event (hold to pass button)
    public void SkipTurn(bool is_player_turn)
    {
        if (is_player_turn && !IsPlayerActionAllowed)
        {
            GameAudio.PlaySfx("invalid");
            return;
        }

        MarkPassed(is_player_turn);
        EndTurn(is_player_turn);
    }

    // The Witcher 3: a side with no card in hand and no leader ability it could use right now passes automatically
    private bool OutOfPlays(bool is_player)
    {
        return RoundRules.OutOfPlays(CurrentBoard(), is_player);
    }

    private void MarkPassed(bool is_player)
    {
        GameAudio.PlaySfx("pass");
        // A pass that ends the round is followed by the round's result instead; at the opening of a round the
        // other side's "Round N" notice says it all
        if (!Opponent(is_player).hasPassed && !roundOpening)
            BattleBanner.Show(BattleBanner.Passed, is_player ? "Turn passed" : "Your opponent has passed");
        Info(is_player).hasPassed = true;
        lastPasserWasPlayer = is_player;
        if (is_player)
            DeselectCard();
    }

    //----------------------------------------------ROUNDS---------------------------------------------------//
    private void ManageOutcome()
    {
        StopTurn();
        battleState = BattleState.RoundOver;
        RecalculateStrengths();
        int player_total = PlayerInfo.TotalScore(), enemy_total = EnemyInfo.TotalScore();
        RoundWinner winner = RoundRules.DecideRound(player_total, enemy_total, PlayerInfo.Faction, EnemyInfo.Faction);
        int player_lives = PlayerInfo.Lives, enemy_lives = EnemyInfo.Lives;
        RoundRules.ApplyRoundResult(PlayerInfo, EnemyInfo, winner);
        PlayerInfo.hasPassed = false;
        EnemyInfo.hasPassed = false;
        bool[] lost = { PlayerInfo.Lives < player_lives, EnemyInfo.Lives < enemy_lives }; // the player's, the opponent's

        // The result over the board as it ended, then the board clears; a tie Nilfgaard wins is announced first
        if (player_total == enemy_total && winner != RoundWinner.Draw)
            BattleBanner.Show(BattleBanner.FactionEmblem(Factions.Nilfgaard), "Nilfgaardian faction ability triggered: Nilfgaard wins the tie.");
        if (winner == RoundWinner.Player)
            BattleBanner.Show(BattleBanner.RoundWon, "You won the round!", BattleBanner.Long);
        else if (winner == RoundWinner.Enemy)
            BattleBanner.Show(BattleBanner.RoundLost, "Your opponent won the round", BattleBanner.Long);
        else
            BattleBanner.Show(BattleBanner.RoundDrawn, "The round ended in a draw", BattleBanner.Long);
        PlayRoundOutcomeAudio(winner);
        StartCoroutine(FinishRound(winner, lost));
    }

    // After the result, the gem of each side that lost a life breaks (The Witcher 3), then the battle ends or the
    // board clears for the next round
    private IEnumerator FinishRound(RoundWinner winner, bool[] lost)
    {
        while (BattleBanner.Busy)
            yield return null;

        List<Coroutine> shatters = new List<Coroutine>();
        foreach (bool is_player in new[] { true, false })
            if (lost[is_player ? 0 : 1])
                shatters.Add(StartCoroutine(CardEffects.Shatter(View(is_player).Stats.LostGem(Info(is_player).Lives), View(is_player).Stats.greyRuby)));
        foreach (Coroutine shatter in shatters)
            yield return shatter;
        View(true).Stats.UIUpdateGemLives(PlayerInfo.Lives);
        View(false).Stats.UIUpdateGemLives(EnemyInfo.Lives);

        if (PlayerInfo.Lives == 0 && EnemyInfo.Lives == 0)
            ConcludeBattle(BattleState.Draw);
        else if (EnemyInfo.Lives == 0)
            ConcludeBattle(BattleState.Won);
        else if (PlayerInfo.Lives == 0)
            ConcludeBattle(BattleState.Lost);
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

        // The board clears as in The Witcher 3: every card flies to its discard pile. Monsters keep a random unit;
        // leader horns vanish; weather goes back to whoever played it
        board.BeginChange();
        foreach (bool is_player in new[] { true, false })
        {
            int kept = RoundRules.CleanupBoard(Info(is_player), Rng.Shared, Db);
            if (kept != -1)
                BattleBanner.Show(BattleBanner.FactionEmblem(Factions.Monsters), "Monsters faction ability triggered: one randomly chosen Monster Unit Card stays on the battlefield.");
        }
        weatherList.Clear();

        //------Northern Realms: Get one additional card from your deck every time you win a round-------------//
        if (winner != RoundWinner.Draw && Info(winner == RoundWinner.Player).Faction == Factions.NorthernRealms)
        {
            bool is_player = winner == RoundWinner.Player;
            if (DrawCards(1, Info(is_player), is_player) > 0)
                BattleBanner.Show(BattleBanner.FactionEmblem(Factions.NorthernRealms), "Northern Realms faction ability triggered: North draws an additional card.");
        }

        RefreshBoard();
        BoardChange change = board.EndChange();

        // The Witcher 3: the round's winner starts the next one; after a draw, whoever passed last
        bool player_starts = winner == RoundWinner.Player || (winner == RoundWinner.Draw && lastPasserWasPlayer);
        Present(change, presenter.Clear(change), () => StartCoroutine(RoundOpening(player_starts)));
    }

    public bool IsBattleOver
    {
        get
        {
            return battleState == BattleState.Won || battleState == BattleState.Lost || battleState == BattleState.Draw
                || PlayerInfo.Lives == 0 || EnemyInfo.Lives == 0; // decided: the last round's result is showing
        }
    }

    // Giving up (pause menu): everything in progress stops and the battle is lost
    public void GiveUp()
    {
        if (IsBattleOver)
            return;
        GameLog.Info("Player gave up");
        StopTurn();
        StopAllCoroutines();
        EndPresentations();
        BattleBanner.Clear();
        DiscardViewer.Close();
        if (mulliganPrompt != null)
            Destroy(mulliganPrompt);
        TurnPicker.SetActive(false);
        Panel.SetActive(false);
        pendingAction = PendingAction.None;
        DeselectCard();
        SetHider(false);
        ConcludeBattle(BattleState.Lost, true);
    }

    private void ConcludeBattle(BattleState outcome, bool gave_up = false)
    {
        GameLog.Info("Battle ended: " + outcome);
        battleState = outcome;
        CardShowcase.Clear();
        CardMotion.ClearLayer();
        PostGame.SetActive(true);
        PostGame.GetComponent<EndScreen>().Show(outcome, PlayerInfo, EnemyInfo, gave_up);
        if (outcome == BattleState.Won)
            GameAudio.PlaySfx("victory");
        else if (outcome == BattleState.Lost)
            GameAudio.PlaySfx("defeat");
    }

    // Draws up to num cards (fewer if the deck runs out), returns how many were drawn
    private int DrawCards(int num, BattleSide info, bool is_player_deck)
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
        Card card_stats = card_go.GetComponent<CardFace>().Card;
        card_go.GetComponent<CardHover>().DestroyEffect();
        string field_row = field_go == WeatherField ? Rows.Weather : field_go.transform.parent.name.ToLower();

        // Its object leaves the hand first (the showcase shows it): the refresh reuses hand objects, and with two
        // copies in hand it could otherwise keep this very one for the copy that stays
        card_go.transform.SetParent(null, false);
        Destroy(card_go);
        PlayCard(card_stats._id, field_row, is_player_turn);
    }

    // Plays a card from the hand: resolves it, shows it on the board, then passes the turn (unless a choice is
    // pending)
    public void PlayCard(int card_id, string field_row, bool is_player_turn)
    {
        BattleSide me = Info(is_player_turn);
        me.HandList.Remove(card_id);
        selectedCard = null;
        pendingAction = PendingAction.None;
        CardShowcase.Clear();

        Card card = Db.Get(card_id);
        PlayCardAudio(card);
        int hand_before = me.HandList.Count;
        board.BeginChange();
        PlayOutcome outcome = PlayRules.Resolve(CurrentBoard(), card, field_row, is_player_turn, is_player_turn);
        if (me.HandList.Count > hand_before)
            GameAudio.PlaySfx("card_draw");
        RefreshBoard();
        SetHider(false);

        bool medic_choice = outcome == PlayOutcome.WaitingForMedicChoice && AbilityRules.MedicCandidates(PlayerInfo.DiscardList, Db).Count > 0;
        BoardChange change = board.EndChange();
        Present(change, presenter.Play(change, card_id, is_player_turn, medic_choice), () =>
        {
            // Medic: the player picks a unit to restore from the discard panel
            if (outcome == PlayOutcome.WaitingForMedicChoice && ShowMedicChoice())
            {
                SetHider(true);
                return;
            }
            EndTurn(is_player_turn);
        });
    }

    // Shows a change of the board (BoardPresenter), then goes on; meanwhile the player cannot act
    private void Present(BoardChange change, IEnumerator show, Action then)
    {
        StartCoroutine(Presenting(change, show, then));
    }

    private IEnumerator Presenting(BoardChange change, IEnumerator show, Action then)
    {
        presenting++;
        presented.Add(change);
        // Never left waiting: a presentation that fails or runs too long is cut short (game time, the pause stops it)
        bool finished = false;
        Coroutine running = StartCoroutine(Then(show, () => finished = true));
        float limit = Time.time + PresentationLimit;
        while (!finished && Time.time < limit)
            yield return null;
        if (!finished)
        {
            StopCoroutine(running);
            Debug.LogError("A play's presentation did not end, the board shows the result at once");
        }
        change.Flush();
        presented.Remove(change);
        presenting--;
        then();
    }

    private const float PresentationLimit = 12f;

    private static IEnumerator Then(IEnumerator routine, Action done)
    {
        yield return routine;
        done();
    }

    // Plays cut short (giving up): every card shows in its place at once
    private void EndPresentations()
    {
        foreach (BoardChange change in presented)
            change.Flush();
        presented.Clear();
        presenting = 0;
        CardShowcase.Clear();
        CardMotion.ClearLayer();
    }

    // A card chosen from a pile (medic, weather from the deck) comes from that pile to the showcase
    public void ShowcasePicked(int card_id, string field, bool is_player)
    {
        CardShowcase.Pick(Db.Get(card_id), field == "deck_list" ? board.DeckRect(is_player) : board.PileRect(is_player));
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

        Card selectedStats = SelectedStats();
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

        Card selectedStats = SelectedStats();
        if (selectedStats.ability != Abilities.Decoy)
            return false;

        Card targetStats = targetCard.GetComponent<CardFace>().Card;
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
        // Out of the hand first, as in DirectlyPlaceCard (a second decoy in hand keeps its own object)
        selectedCard.transform.SetParent(null, false);
        Destroy(selectedCard);
        selectedCard = null;
        PlayDecoy(targetStats._id, row, true, selectedStats._id);
        return true;
    }

    // Decoy: the target goes back to the hand, the decoy takes its place on the row
    public void PlayDecoy(int target_id, string row, bool is_player_card, int decoy_id)
    {
        Info(is_player_card).HandList.Remove(decoy_id);
        CardShowcase.Clear();
        GameLog.Info("Decoying: " + target_id);
        board.BeginChange();
        PlayRules.Decoy(CurrentBoard(), target_id, row, is_player_card, decoy_id);
        RefreshBoard();
        BoardChange change = board.EndChange();
        Present(change, presenter.Play(change, decoy_id, is_player_card, false), () => EndTurn(is_player_card));
    }

    // Escape (the back button on Android) puts a selected card (or the chosen leader) back first, as in The Witcher 3;
    // otherwise it pauses
    private void OnEscape()
    {
        if ((selectedCard != null || LeaderSelected) && IsPlayerActionAllowed)
            PutBackSelectedCard();
        else
            PauseMenu.Open();
    }

    public void PutBackSelectedCard(bool sound = true)
    {
        if (sound)
            GameAudio.PlaySfx("card_select");
        DeselectCard();
    }

    // A card is selected and can be placed now (from the hand, or picked from a pile by a medic or a leader)
    public bool IsSelectedCardPlayable()
    {
        return battleState == BattleState.PlayerTurn && selectedCard != null && presenting == 0
            && (pendingAction == PendingAction.None || pendingAction == PendingAction.PlacePickedCard);
    }

    private Card SelectedStats()
    {
        return selectedCard.GetComponent<CardFace>().Card;
    }

    // The selected card (or the chosen leader) goes back to its place, gliding there from the showcase (or leaving it
    // at once: its play takes over)
    private void DeselectCard(bool glide = true)
    {
        ReleaseLeader();
        if (selectedCard != null)
        {
            CardHover hover = selectedCard.GetComponent<CardHover>();
            hover.isHoverable = true;
            hover.TranslateDown();
            hover.DestroyEffect();
        }
        if (glide)
            CardShowcase.Deselect();
        else
            CardShowcase.Clear();
        selectedCard = null;
        LightOffField(true);
        LightOffField(false);
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

    private void SetDeckObject(BattleSide info, bool is_player_deck) { board.SetDeck(info, is_player_deck); }
    public void SetHandObject(List<int> hand_list, bool is_player_hand) { board.SetHand(hand_list, is_player_hand); }
    private void SetDiscardObject(List<int> discard_list, bool is_player) { board.SetDiscard(discard_list, is_player); }
    private void CreateCardObjects(GameObject my_object, List<int> card_list, bool isSelectable, bool is_player_card) { board.CreateCardObjects(my_object, card_list, isSelectable, is_player_card); }
    public GameObject CreateCardObject(Card card, bool is_player_card) { return board.CreateCardObject(card, is_player_card); }
    public void HighlightField(Card card_stats, bool is_player_turn) { board.Highlight(card_stats, is_player_turn); }
    public void LightOffField(bool is_player_turn) { board.Lightoff(is_player_turn); }
    public void SetHider(bool hidden) { board.SetHider(hidden); }

    //----------------------------------------------Queries---------------------------------------------------//
    public Card GetCard(int card_id)
    {
        return Db.Get(card_id);
    }



    //--------------ToMainMenu
    public void ToMainMenu()
    {
        GameAudio.PlaySfx("ui_click");
        SceneManager.LoadScene(SceneNames.Menu);
    }
}

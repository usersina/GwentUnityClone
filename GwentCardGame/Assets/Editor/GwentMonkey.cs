using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gwent.Battle;
using Gwent.Core;
using Gwent.Menus;
using Gwent.Rules;
using Gwent.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gwent.EditorTools
{
    // Monkey test: battles played under a storm of random input. A virtual mouse and keyboard click, double-click,
    // right-click, hold, drag and scroll on whatever the screen offers (cards, rows, piles, the leader, menus, pickers,
    // dialogs, the end screen) and mash the game's keys and others, at any moment: during plays, banners, the redraw,
    // the pause menu, a round's end. Touch controls and the game speed change now and then. When nothing moves for a
    // while, a random legal move keeps the battle going (and a stuck dialog gets an Escape), so every phase is reached.
    // Fails on errors and exceptions in the log, cards appearing or disappearing, a board that shows other cards than
    // the game once it is idle, and a game that stops progressing. Restores every setting and deck file it may change.
    // Menu: Gwent > Run Monkey Test (10 minutes). Report: Logs/monkey-report.txt.
    public static class GwentMonkey
    {
        private const string ActiveKey = "GwentMonkey.Active", MinutesKey = "GwentMonkey.Minutes", SeedKey = "GwentMonkey.Seed", ReportKey = "GwentMonkey.Report";
        private static Runner runner;

        [MenuItem("Gwent/Run Monkey Test")]
        public static void RunFromMenu()
        {
            Begin(10f, Environment.TickCount, "Logs/monkey-report.txt");
        }

        // The run starts once Play mode has (entering it reloads the scripts) and stays in it until the end
        public static void Begin(float minutes, int seed, string report)
        {
            if (EditorApplication.isPlaying || Running)
            {
                Debug.LogWarning("Stop Play mode (and any run) before starting the monkey test.");
                return;
            }
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetFloat(MinutesKey, minutes);
            SessionState.SetInt(SeedKey, seed);
            SessionState.SetString(ReportKey, report);
            EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
            EditorApplication.isPlaying = true;
        }

        public static bool Running { get { return runner != null && runner.Running; } }

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode && Running)
            {
                runner.Abort("Play mode was stopped before the run ended");
                return;
            }
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(ActiveKey, false))
                return;
            SessionState.SetBool(ActiveKey, false);
            runner = new Runner(SessionState.GetFloat(MinutesKey, 10f), SessionState.GetInt(SeedKey, 1), SessionState.GetString(ReportKey, "Logs/monkey-report.txt"));
            runner.Start();
        }

        private class Runner
        {
            private const float StuckSeconds = 6f;     // no progress for this long: a random legal move (or Escape)
            private const float SoftlockSeconds = 45f; // still nothing after the rescues: a softlock
            private const double PressDuration = 0.08;

            private readonly System.Random rnd;
            private readonly double endTime;
            private readonly double startTime;
            private readonly string reportPath;
            private readonly int seed;
            private readonly Dictionary<string, int> failures = new Dictionary<string, int>();
            private readonly Dictionary<string, string> failureDetails = new Dictionary<string, string>();
            private readonly Dictionary<string, int> stats = new Dictionary<string, int>();
            private readonly List<KeyValuePair<double, Action>> steps = new List<KeyValuePair<double, Action>>();

            private Mouse mouse;
            private Keyboard keyboard;
            private Vector2 mousePosition;
            private InputSettings.EditorInputBehaviorInPlayMode savedEditorBehavior;
            private InputSettings.BackgroundBehavior savedBackgroundBehavior;
            private bool inputSetUp;
            private readonly Dictionary<string, object> savedPrefs = new Dictionary<string, object>();
            private bool savedTouch;
            private Dictionary<string, byte[]> savedDecks;

            private BattleController controller;
            private int expectedCards = -1;
            private string lastSignature;
            private double lastProgress;
            private double lastRescue;
            private double nextAction;
            private double nextBoardCheck;
            private double sceneSince;
            private string sceneName;
            private int battles;

            private static readonly string[] PrefKeys = { "masterVolume", "musicVolume", "sfxVolume", "muteInBackground", "fullscreen",
                "resolutionWidth", "resolutionHeight", "frameRate", "menuScale", "gameSpeed", "aiDifficulty", "playerDeckPath" };

            public bool Running { get; private set; }

            public Runner(float minutes, int seed, string report)
            {
                this.seed = seed;
                rnd = new System.Random(seed);
                startTime = EditorApplication.timeSinceStartup;
                endTime = startTime + minutes * 60;
                reportPath = report;
            }

            public void Start()
            {
                Running = true;
                foreach (string key in PrefKeys)
                    if (PlayerPrefs.HasKey(key))
                        savedPrefs[key] = key == "playerDeckPath" ? (object)PlayerPrefs.GetString(key) : key.StartsWith("master") || key.EndsWith("Volume") || key == "menuScale" ? (object)PlayerPrefs.GetFloat(key) : PlayerPrefs.GetInt(key);
                savedPrefs["opponent"] = OpponentDecks.Chosen;
                savedTouch = GameInput.SimulateTouch;
                savedDecks = Directory.Exists(DeckStorage.Root)
                    ? Directory.GetFiles(DeckStorage.Root, "*", SearchOption.AllDirectories).ToDictionary(f => f, File.ReadAllBytes)
                    : new Dictionary<string, byte[]>();
                Application.logMessageReceived += OnLog;
                EditorApplication.update += Tick;
                Debug.Log("Monkey test: seed " + seed);
                // The first battle with random decks too
                PickDecks();
                SceneManager.LoadScene(SceneNames.Game);
            }

            public void Abort(string why)
            {
                Fail("ABORTED: " + why, "");
                Finish();
            }

            //----------------------------------------The loop----------------------------------------//
            private void Tick()
            {
                if (!Running)
                    return;
                double now = EditorApplication.timeSinceStartup;
                if (now > endTime)
                {
                    Finish();
                    return;
                }
                if (!EditorApplication.isPlaying)
                    return;
                if (!inputSetUp)
                    SetUpInput();

                string scene = SceneManager.GetActiveScene().name;
                if (scene != sceneName)
                {
                    sceneName = scene;
                    sceneSince = now;
                    lastProgress = now;
                    Count("scene " + scene);
                }

                RunSteps(now);
                if (scene == SceneNames.Game)
                    WatchBattle(now);
                else if (now - sceneSince > 25)
                {
                    // Out of a menu after a while: back to a battle
                    Count("back to a battle from " + scene);
                    PickDecks();
                    SceneManager.LoadScene(SceneNames.Game);
                    sceneSince = now;
                }

                if (now >= nextAction)
                {
                    RandomAction(now);
                    nextAction = now + 0.03 + rnd.NextDouble() * (rnd.Next(10) == 0 ? 1.2 : 0.3);
                }
            }

            // A random preset for each side (any faction, any leader they carry)
            private void PickDecks()
            {
                List<TextAsset> presets = OpponentDecks.All();
                if (presets.Count == 0)
                    return;
                DeckStorage.BattleDeckPath = OpponentDecks.PresetPath(presets[rnd.Next(presets.Count)]);
                OpponentDecks.Override = presets[rnd.Next(presets.Count)].name;
            }

            //----------------------------------------The battle's watch----------------------------------------//
            private void WatchBattle(double now)
            {
                BattleController c = BattleController.Instance;
                if (c == null)
                    return;
                if (c != controller)
                {
                    controller = c;
                    expectedCards = -1;
                    battles++;
                    lastProgress = now;
                    Count("battles");
                }
                if (expectedCards < 0 && c.PlayerInfo.HandList.Count > 0 && c.battleState != BattleState.Start)
                    expectedCards = CountCards(c);

                string signature = Signature(c);
                if (signature != lastSignature)
                {
                    lastSignature = signature;
                    lastProgress = now;
                    if (expectedCards >= 0 && !IsOver(c))
                    {
                        int cards = CountCards(c);
                        if (cards != expectedCards)
                        {
                            Fail("CARD COUNT changed " + expectedCards + " -> " + cards, signature);
                            expectedCards = cards;
                        }
                    }
                }

                // The board shows exactly the game's cards whenever it is idle
                if (now >= nextBoardCheck && c.IsPlayerActionAllowed && !UIOverlay.HasModal && !c.Panel.activeSelf && !RedrawPicker.IsOpen && !DiscardViewer.IsOpen)
                {
                    nextBoardCheck = now + 0.5;
                    CheckBoardShowsModel(c);
                }

                if (now - lastProgress > SoftlockSeconds)
                {
                    Fail("SOFTLOCK: no progress for " + SoftlockSeconds + " s", signature + Describe(c));
                    Count("restarts after a softlock");
                    PickDecks();
                    SceneManager.LoadScene(SceneNames.Game);
                    lastProgress = now;
                    return;
                }
                if (now - lastProgress > StuckSeconds && now - lastRescue > StuckSeconds)
                {
                    lastRescue = now;
                    Rescue(c);
                }
            }

            private static bool IsOver(BattleController c)
            {
                return c.IsBattleOver;
            }

            // Moves the game on like a player would: closes a dialog, finishes the redraw, makes a choice, plays or passes
            private void Rescue(BattleController c)
            {
                Count("rescues");
                if (UIOverlay.HasModal || PauseMenu.IsOpen || DiscardViewer.IsOpen)
                {
                    PressKey(Key.Escape);
                    Count("rescue: Escape");
                    return;
                }
                if (c.TurnPicker.activeInHierarchy)
                {
                    c.TurnPicker.GetComponentInChildren<FirstTurnPicker>(true).GoFirst();
                    return;
                }
                if (c.PostGame.activeInHierarchy)
                {
                    EndScreen post = c.PostGame.GetComponent<EndScreen>();
                    PickDecks();
                    if (rnd.Next(4) == 0)
                        post.ToMainMenu();
                    else
                        post.PlayAgain();
                    Count("rescue: leave the end screen");
                    return;
                }
                if (RedrawPicker.IsOpen || c.PlayerInfo.CanExchange)
                {
                    c.FinishCardExchange();
                    return;
                }
                if (c.Panel.activeSelf)
                {
                    BattlePickers panel = c.Panel.GetComponent<BattlePickers>();
                    if (panel.Cards.Count > 0 && rnd.Next(4) > 0)
                        panel.Choose(panel.Cards[rnd.Next(panel.Cards.Count)]);
                    else
                        c.CancelPanel();
                    Count("rescue: picker");
                    return;
                }
                if (c.battleState != BattleState.PlayerTurn)
                    return;
                switch (c.pendingAction)
                {
                    case PendingAction.DiscardCards:
                        List<int> hand = c.PlayerInfo.HandList;
                        if (hand.Count > 0)
                            c.DiscardCard(hand[rnd.Next(hand.Count)], true);
                        c.PlayerInfo.CardsDiscarded++;
                        if (c.PlayerInfo.CardsDiscarded >= 2 || hand.Count == 0)
                            c.OpenDeckPicker(true);
                        return;
                    case PendingAction.PlacePickedCard:
                        PlaceSelected(c);
                        return;
                }
                if (!c.IsPlayerActionAllowed)
                    return;
                Transform hand_t = c.PlayerField.transform.Find("Hand");
                if (hand_t.childCount == 0 || rnd.Next(8) == 0)
                {
                    c.SkipTurn(true);
                    Count("rescue: pass");
                    return;
                }
                GameObject card = hand_t.GetChild(rnd.Next(hand_t.childCount)).gameObject;
                card.GetComponent<CardSelect>().OnCardSelect();
                if (c.selectedCard == card)
                    PlaceSelected(c);
                Count("rescue: play");
            }

            private void PlaceSelected(BattleController c)
            {
                if (c.selectedCard == null)
                    return;
                Card stats = c.selectedCard.GetComponent<CardFace>().Card;
                if (stats.ability == Ability.Decoy)
                {
                    // What a decoy can swap (PlayRules.IsDecoyTarget): a unit, not a hero, not a token, not a special
                    // card such as another decoy
                    List<GameObject> targets = new List<GameObject>();
                    foreach (Row row in Rows.Units)
                        foreach (Transform t in c.PlayerField.transform.Find(row.ToString()).Find("Unit"))
                        {
                            CardFace face = t.GetComponent<CardFace>();
                            if (t.GetComponent<CardSelect>() != null && face != null && PlayRules.IsDecoyTarget(face.Card))
                                targets.Add(t.gameObject);
                        }
                    if (targets.Count == 0)
                    {
                        c.PutBackSelectedCard(false);
                        c.SkipTurn(true);
                        return;
                    }
                    targets[rnd.Next(targets.Count)].GetComponent<CardSelect>().OnCardSelect();
                    return;
                }
                if (stats.row == Row.Weather || stats.IsSpecial && stats.ability == Ability.Scorch)
                {
                    c.WeatherField.GetComponent<WeatherSlot>().OnWeatherClick();
                    return;
                }
                List<GameObject> rows = new List<GameObject>();
                rows.AddRange(c.PlayerField.GetComponent<RowPicker>().highLightedRows);
                rows.AddRange(c.EnemyField.GetComponent<RowPicker>().highLightedRows);
                if (rows.Count == 0)
                {
                    c.PutBackSelectedCard(false);
                    c.SkipTurn(true);
                    return;
                }
                GameObject target = rows[rnd.Next(rows.Count)];
                RowClick click = target.GetComponent<RowClick>();
                if (click != null)
                    click.OnRowClick();
                else
                    c.DirectlyPlaceCard(c.selectedCard, target, true);
            }

            //----------------------------------------Random input----------------------------------------//
            private static readonly Key[] GameKeys = { Key.Space, Key.Enter, Key.Escape, Key.X, Key.LeftArrow, Key.RightArrow, Key.UpArrow, Key.DownArrow,
                Key.A, Key.D, Key.W, Key.S, Key.Tab, Key.NumpadEnter, Key.Backspace };
            private static readonly Key[] OtherKeys = { Key.Q, Key.E, Key.F, Key.Digit1, Key.Digit2, Key.Delete, Key.Home, Key.PageUp, Key.F1, Key.Z, Key.LeftShift, Key.LeftCtrl };

            private void RandomAction(double now)
            {
                int r = rnd.Next(1000);
                if (r < 420)
                {
                    Vector2 at = RandomTarget();
                    int kind = rnd.Next(100);
                    if (kind < 55) Click(now, at, MouseButton.Left);
                    else if (kind < 70) Click(now, at, MouseButton.Right);
                    else if (kind < 85) DoubleClick(now, at);
                    else if (kind < 93) Hold(now, at, 0.3 + rnd.NextDouble() * 2.2);
                    else Drag(now, at, RandomTarget());
                    Count("input: mouse");
                }
                else if (r < 520)
                {
                    MoveMouse(RandomTarget());
                    Count("input: hover");
                }
                else if (r < 560)
                {
                    Scroll(rnd.Next(2) == 0 ? 120 : -120);
                    Count("input: wheel");
                }
                else if (r < 840)
                {
                    PressKey(GameKeys[rnd.Next(GameKeys.Length)]);
                    Count("input: key");
                }
                else if (r < 880)
                {
                    // Space held (passes), Enter held, an arrow held
                    Key key = new[] { Key.Space, Key.Space, Key.Enter, Key.RightArrow, Key.X }[rnd.Next(5)];
                    HoldKey(now, key, 0.4 + rnd.NextDouble() * 2.5);
                    Count("input: key held");
                }
                else if (r < 930)
                {
                    // A mash: several keys at once, some unrelated
                    List<Key> keys = new List<Key>();
                    for (int i = 0, n = 2 + rnd.Next(4); i < n; i++)
                        keys.Add(rnd.Next(3) == 0 ? OtherKeys[rnd.Next(OtherKeys.Length)] : GameKeys[rnd.Next(GameKeys.Length)]);
                    At(now, () => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys.Distinct().ToArray())));
                    At(now + PressDuration, () => InputSystem.QueueStateEvent(keyboard, new KeyboardState()));
                    Count("input: key mash");
                }
                else if (r < 960)
                {
                    // A burst of clicks in the same place (an impatient player)
                    Vector2 at = RandomTarget();
                    for (int i = 0, n = 3 + rnd.Next(6); i < n; i++)
                        Click(now + i * 0.06, at, MouseButton.Left);
                    Count("input: click burst");
                }
                else if (r < 975)
                {
                    GameInput.SimulateTouch = !GameInput.SimulateTouch;
                    Count("touch controls " + (GameInput.SimulateTouch ? "on" : "off"));
                }
                else if (r < 984)
                {
                    GameSettings.GameSpeed = rnd.Next(3);
                    Count("game speed " + GameSettings.GameSpeed);
                }
                else if (r < 990)
                {
                    GameSettings.AiDifficulty = (AiDifficulty)rnd.Next(3);
                    Count("AI " + GameSettings.AiDifficulty);
                }
                else
                {
                    PressKey(OtherKeys[rnd.Next(OtherKeys.Length)]);
                    Count("input: other key");
                }
            }

            // Somewhere clickable on the screen (any active raycast target of any canvas), or anywhere at all
            private Vector2 RandomTarget()
            {
                if (rnd.Next(5) == 0)
                    return new Vector2((float)rnd.NextDouble() * Screen.width, (float)rnd.NextDouble() * Screen.height);
                List<Graphic> targets = new List<Graphic>();
                foreach (Canvas canvas in UnityEngine.Object.FindObjectsByType<Canvas>())
                {
                    if (!canvas.isActiveAndEnabled || !canvas.isRootCanvas)
                        continue;
                    foreach (Graphic g in canvas.GetComponentsInChildren<Graphic>())
                        if (g.raycastTarget && g.isActiveAndEnabled && g.color.a > 0.01f)
                            targets.Add(g);
                }
                if (targets.Count == 0)
                    return new Vector2(Screen.width / 2f, Screen.height / 2f);
                Graphic pick = targets[rnd.Next(targets.Count)];
                Vector2 point = ScreenPoint(pick.rectTransform);
                return new Vector2(Mathf.Clamp(point.x, 1, Screen.width - 1), Mathf.Clamp(point.y, 1, Screen.height - 1));
            }

            private static Vector2 ScreenPoint(RectTransform rect)
            {
                Vector3[] corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                Canvas canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
                Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                return RectTransformUtility.WorldToScreenPoint(camera, (corners[0] + corners[2]) / 2);
            }

            private void At(double time, Action action)
            {
                int index = steps.FindIndex(step => step.Key > time);
                steps.Insert(index < 0 ? steps.Count : index, new KeyValuePair<double, Action>(time, action));
            }

            private void RunSteps(double now)
            {
                while (steps.Count > 0 && steps[0].Key <= now)
                {
                    Action action = steps[0].Value;
                    steps.RemoveAt(0);
                    try { action(); }
                    catch (Exception e) { Debug.LogWarning("Monkey step failed: " + e.Message); }
                }
            }

            private void Send(Vector2 position, MouseButton button, bool pressed, Vector2 scroll = default(Vector2))
            {
                mousePosition = position;
                if (mouse != null)
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = position, scroll = scroll }.WithButton(button, pressed));
            }

            private void MoveMouse(Vector2 at) { Send(at, MouseButton.Left, false); }

            private void Click(double time, Vector2 at, MouseButton button)
            {
                At(time, () => Send(at, button, false));
                At(time + 0.02, () => Send(at, button, true));
                At(time + 0.02 + PressDuration, () => Send(at, button, false));
            }

            private void DoubleClick(double time, Vector2 at)
            {
                Click(time, at, MouseButton.Left);
                Click(time + 0.15, at, MouseButton.Left);
            }

            private void Hold(double time, Vector2 at, double duration)
            {
                At(time, () => Send(at, MouseButton.Left, true));
                At(time + duration, () => Send(at, MouseButton.Left, false));
            }

            private void Drag(double time, Vector2 from, Vector2 to)
            {
                At(time, () => Send(from, MouseButton.Left, true));
                for (int i = 1; i <= 6; i++)
                {
                    Vector2 p = Vector2.Lerp(from, to, i / 6f);
                    At(time + i * 0.04, () => Send(p, MouseButton.Left, true));
                }
                At(time + 0.3, () => Send(to, MouseButton.Left, false));
            }

            private void Scroll(float amount)
            {
                Vector2 at = mousePosition;
                At(EditorApplication.timeSinceStartup, () => Send(at, MouseButton.Left, false, new Vector2(0, amount)));
                At(EditorApplication.timeSinceStartup + 0.02, () => Send(at, MouseButton.Left, false));
            }

            private void PressKey(Key key)
            {
                double now = EditorApplication.timeSinceStartup;
                At(now, () => InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)));
                At(now + PressDuration, () => InputSystem.QueueStateEvent(keyboard, new KeyboardState()));
            }

            private void HoldKey(double time, Key key, double duration)
            {
                At(time, () => InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)));
                At(time + duration, () => InputSystem.QueueStateEvent(keyboard, new KeyboardState()));
            }

            private void SetUpInput()
            {
                inputSetUp = true;
                InputSettings settings = InputSystem.settings;
                savedEditorBehavior = settings.editorInputBehaviorInPlayMode;
                savedBackgroundBehavior = settings.backgroundBehavior;
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                mouse = InputSystem.AddDevice<Mouse>("MonkeyMouse");
                keyboard = InputSystem.AddDevice<Keyboard>("MonkeyKeyboard");
            }

            //----------------------------------------Checks----------------------------------------//
            private void OnLog(string message, string stack, LogType type)
            {
                if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert)
                    return;
                if (stack.Contains("UnityEditor.Search.SearchInit") || message.StartsWith("Monkey"))
                    return;
                string where = string.Join(" | ", stack.Split('\n').Where(l => l.Trim().Length > 0).Take(4).Select(l => l.Trim()));
                Fail(type + ": " + message.Split('\n')[0], where + (controller != null ? "\n      state " + Signature(controller) : ""));
            }

            private void Fail(string what, string detail)
            {
                GameLog.Event(LogCategory.Game, "monkey_failure", "what", what, "detail", detail);
                string key = what.Length > 200 ? what.Substring(0, 200) : what;
                int count;
                failures.TryGetValue(key, out count);
                failures[key] = count + 1;
                if (!failureDetails.ContainsKey(key))
                    failureDetails[key] = detail + "\n      scene " + SceneManager.GetActiveScene().name + ", battle " + battles + ", at " + (EditorApplication.timeSinceStartup - startTime).ToString("0") + " s";
            }

            private void Count(string key)
            {
                int value;
                stats.TryGetValue(key, out value);
                stats[key] = value + 1;
            }

            private static int CountCards(BattleController c)
            {
                int total = 0;
                foreach (PlayerState info in new PlayerState[] { c.PlayerInfo, c.EnemyInfo })
                    total += info.AllCards().Count(id => !CardIds.IsSummoned(id)) - info.LeaderHornRows.Count;
                return total;
            }

            private void CheckBoardShowsModel(BattleController c)
            {
                List<string> problems = new List<string>();
                foreach (bool player in new[] { true, false })
                {
                    PlayerState info = player ? c.PlayerInfo : c.EnemyInfo;
                    Transform field = (player ? c.PlayerField : c.EnemyField).transform;
                    string side = player ? "player " : "opponent ";
                    if (player)
                        Compare(field.Find("Hand"), info.HandList, side + "hand", problems);
                    foreach (Row row in Rows.Units)
                    {
                        Transform row_t = field.Find(row.ToString());
                        Compare(row_t.Find("Unit"), CardOrder.Sorted(info.UnitRow(row), c.Db), side + row, problems);
                        Compare(row_t.Find("Special"), info.SpecialRow(row), side + row + " horn", problems);
                    }
                    Compare(field.Find("Discard/Pile"), info.DiscardList, side + "discard pile", problems);
                }
                Compare(c.WeatherField.transform, c.weatherList, "weather", problems);
                Count(problems.Count == 0 ? "board shows the model" : "board mismatch");
                if (problems.Count > 0)
                    Fail("BOARD shows other cards than the game: " + problems[0].Split('[')[0], string.Join("; ", problems));
            }

            private static void Compare(Transform container, List<int> ids, string what, List<string> problems)
            {
                List<string> shown = new List<string>();
                foreach (Transform child in container)
                    if (child.GetComponent<CardFace>() != null)
                        shown.Add(child.name);
                string expected = string.Join(",", ids), actual = string.Join(",", shown);
                if (expected != actual)
                    problems.Add(what + " shows [" + actual + "] instead of [" + expected + "]");
            }

            private static string Signature(BattleController c)
            {
                PlayerState p = c.PlayerInfo, e = c.EnemyInfo;
                return string.Format("{0}/{1} P[h{2} d{3} g{4} b{5} pass{6} lives{7}] E[h{8} d{9} g{10} b{11} pass{12} lives{13}] w{14} panel{15}",
                    c.battleState, c.pendingAction, p.HandList.Count, p.DeckList.Count, p.DiscardList.Count, p.UnitsOnBoard(), p.hasPassed, p.Lives,
                    e.HandList.Count, e.DeckList.Count, e.DiscardList.Count, e.UnitsOnBoard(), e.hasPassed, e.Lives, c.weatherList.Count, c.Panel.activeSelf);
            }

            private static string Describe(BattleController c)
            {
                return string.Format("\n      selected={0} leaderSelected={1} presenting={2} modal={3} pause={4} redraw={5} viewer={6} banner={7} timeScale={8}",
                    c.selectedCard != null ? c.selectedCard.name : "-", c.LeaderSelected, c.IsPresenting, UIOverlay.HasModal, PauseMenu.IsOpen,
                    RedrawPicker.IsOpen, DiscardViewer.IsOpen, BattleBanner.Busy, Time.timeScale);
            }

            //----------------------------------------The end----------------------------------------//
            private void Finish()
            {
                Running = false;
                EditorApplication.update -= Tick;
                Application.logMessageReceived -= OnLog;
                if (mouse != null) InputSystem.RemoveDevice(mouse);
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                if (inputSetUp)
                {
                    InputSystem.settings.editorInputBehaviorInPlayMode = savedEditorBehavior;
                    InputSystem.settings.backgroundBehavior = savedBackgroundBehavior;
                }
                // Settings, the battle deck, the opponent, the touch controls switch, the deck files: as they were
                foreach (string key in PrefKeys)
                {
                    object value;
                    if (!savedPrefs.TryGetValue(key, out value))
                        PlayerPrefs.DeleteKey(key);
                    else if (value is string) PlayerPrefs.SetString(key, (string)value);
                    else if (value is float) PlayerPrefs.SetFloat(key, (float)value);
                    else PlayerPrefs.SetInt(key, (int)value);
                }
                OpponentDecks.Chosen = (string)savedPrefs["opponent"];
                OpponentDecks.Override = null;
                DeckStorage.BattleDeckPath = null;
                GameInput.SimulateTouch = savedTouch;
                PlayerPrefs.Save();
                if (Directory.Exists(DeckStorage.Root))
                    foreach (string file in Directory.GetFiles(DeckStorage.Root, "*", SearchOption.AllDirectories))
                        if (!savedDecks.ContainsKey(file))
                            File.Delete(file);
                foreach (KeyValuePair<string, byte[]> deck in savedDecks)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(deck.Key));
                    if (!File.Exists(deck.Key) || !File.ReadAllBytes(deck.Key).SequenceEqual(deck.Value))
                        File.WriteAllBytes(deck.Key, deck.Value);
                }
                Time.timeScale = 1f;

                List<string> lines = new List<string>();
                lines.Add("Gwent monkey test: " + (failures.Count == 0 ? "PASSED" : "FAILED") + " - " + battles + " battles, seed " + seed);
                lines.Add("Coverage:");
                lines.AddRange(stats.OrderBy(kv => kv.Key).Select(kv => "  " + kv.Key + ": " + kv.Value));
                lines.Add("Failures: " + failures.Count + " kinds");
                foreach (KeyValuePair<string, int> f in failures.OrderByDescending(kv => kv.Value))
                {
                    lines.Add("  [" + f.Value + "x] " + f.Key);
                    lines.Add("      " + failureDetails[f.Key]);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath)));
                File.WriteAllLines(reportPath, lines);
                Debug.Log(string.Join("\n", lines.Take(3)));
                if (EditorApplication.isPlaying)
                    EditorApplication.isPlaying = false;
            }
        }
    }
}

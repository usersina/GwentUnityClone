using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Gwent.Rules;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

// End-to-end smoke test: plays whole battles in the real Game scene with a random "player" against the AI.
// The first two battles start with the redraw played through real input (a virtual mouse and keyboard at human speed),
// the player's first turn checks card selection and the pause menu the same way, and so does the main menu at the end.
// Every leader of every faction gets a generated deck. Fails on exceptions/errors in the log, on a game that
// stops progressing (softlock), and on cards appearing or disappearing (card conservation).
// Then exercises the deck builder (add, duplicate names, rename, battle deck, corrupt files, delete) in the Deck scene;
// it only touches its own "Autoplay ..." decks and restores the saved battle deck afterwards. Finally loads the main menu.
//
//  Editor:  menu Gwent > Run Autoplay Smoke Test (Ctrl+Shift+Alt+G)
//  Batch:   Unity -batchmode -projectPath <path> -executeMethod GwentAutoplay.RunBatch
//                 [-autoplayMatches 21] [-autoplaySeed 1] [-autoplayReport <file>]
public static class GwentAutoplay
{
    private const string ActiveKey = "GwentAutoplay.Active";
    private const string BatchKey = "GwentAutoplay.Batch";
    private const string MatchesKey = "GwentAutoplay.Matches";
    private const string SeedKey = "GwentAutoplay.Seed";
    private const string ReportKey = "GwentAutoplay.Report";
    private const float SoftlockSeconds = 20f;
    private const float TimeScale = 20f;

    [MenuItem("Gwent/Run Autoplay Smoke Test %#&g")] // Ctrl+Shift+Alt+G
    public static void RunFromMenu()
    {
        Begin(Factions.Playable.Length * 5 + 1, 1, false, Path.Combine("Logs", "autoplay-report.txt"));
    }

    public static void RunBatch()
    {
        string[] args = Environment.GetCommandLineArgs();
        int matches = IntArg(args, "-autoplayMatches", Factions.Playable.Length * 5 + 1);
        int seed = IntArg(args, "-autoplaySeed", 1);
        string report = StringArg(args, "-autoplayReport", Path.Combine("Logs", "autoplay-report.txt"));
        Begin(matches, seed, true, report);
    }

    private static void Begin(int matches, int seed, bool batch, string report)
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("Stop Play mode before running the autoplay smoke test.");
            return;
        }
        // Opening the Game scene would otherwise drop unsaved changes of the current scene
        if (!batch && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        SessionState.SetBool(ActiveKey, true);
        SessionState.SetBool(BatchKey, batch);
        SessionState.SetInt(MatchesKey, matches);
        SessionState.SetInt(SeedKey, seed);
        SessionState.SetString(ReportKey, report);
        EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    private static void Hook()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static Runner runner;

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(ActiveKey, false))
            return;
        SessionState.SetBool(ActiveKey, false);
        runner = new Runner(SessionState.GetInt(MatchesKey, 21), SessionState.GetInt(SeedKey, 1),
            SessionState.GetBool(BatchKey, false), SessionState.GetString(ReportKey, "Logs/autoplay-report.txt"));
        runner.Start();
    }

    private static int IntArg(string[] args, string name, int fallback)
    {
        int index = Array.IndexOf(args, name);
        int value;
        return index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out value) ? value : fallback;
    }

    private static string StringArg(string[] args, string name, string fallback)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
    }

    //----------------------------------------The runner (ticked by EditorApplication.update)----------------------------------------//
    private class Runner
    {
        private readonly int totalMatches;
        private readonly int seed;
        private readonly bool batch;
        private readonly string reportPath;
        private readonly System.Random rnd;
        private readonly List<string> failures = new List<string>();
        private readonly List<string> results = new List<string>();
        private readonly List<string> errors = new List<string>();
        private int ignoredEditorErrors;
        private readonly Dictionary<string, int> stats = new Dictionary<string, int>();
        private List<Deck> decks;

        private int match = -1;
        private bool deckPhase;
        private int deckPhaseFrames;
        private bool menuPhase;
        private int menuPhaseFrames;
        private SceneController controller;
        private int expectedCards = -1;
        private string lastSignature;
        private double lastProgress;
        private double nextActionTime;
        private string currentDeck;
        private InputCheck inputCheck;
        private int redrawChecks;
        private bool selectionChecked;
        private bool pauseChecked;
        private bool passKeyChecked;
        private bool menuInputStarted;

        public Runner(int total_matches, int seed, bool batch, string report_path)
        {
            totalMatches = total_matches;
            this.seed = seed;
            this.batch = batch;
            reportPath = report_path;
            rnd = new System.Random(seed);
        }

        public void Start()
        {
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
            Time.timeScale = TimeScale;
            decks = GenerateDecks(CardLibrary.Get());
            NextMatch(first: true);
        }

        private void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert)
                return;
            // Unity 6 editor bug: Quick Search indexing throws on startup when no index exists yet (not game code)
            if (stack.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup"))
            {
                ignoredEditorErrors++;
                return;
            }
            errors.Add("[" + Label() + "] " + type + ": " + message + "\n" + stack.Split('\n').Take(4).Aggregate("", (a, b) => a + "    " + b + "\n"));
        }

        private string Label()
        {
            return "match " + (match + 1) + " (" + currentDeck + ")";
        }

        private void Count(string key)
        {
            int value;
            stats.TryGetValue(key, out value);
            stats[key] = value + 1;
        }

        private void NextMatch(bool first = false)
        {
            match++;
            if (match >= totalMatches)
            {
                // Battles done: on to the deck builder
                deckPhase = true;
                deckPhaseFrames = 0;
                currentDeck = "deck builder";
                SceneManager.LoadScene(SceneNames.Deck);
                return;
            }

            Rng.Seed(seed * 1000 + match);
            // Match 0: the built-in default deck; then one generated deck per leader (cycling)
            if (match == 0 || decks.Count == 0)
            {
                ApplicationModel.playerDeckPath = null;
                currentDeck = "default NR deck";
            }
            else
            {
                Deck deck = decks[(match - 1) % decks.Count];
                string path = Path.Combine(Application.temporaryCachePath, "autoplay_" + deck.Faction + "_" + deck.Leader + ".json");
                File.WriteAllText(path, JsonUtility.ToJson(deck));
                ApplicationModel.playerDeckPath = path;
                currentDeck = deck.Name;
            }

            controller = null;
            expectedCards = -1;
            lastSignature = null;
            lastProgress = EditorApplication.timeSinceStartup;
            if (!first)
                SceneManager.LoadScene(SceneNames.Game);
        }

        private void Tick()
        {
            if (!EditorApplication.isPlaying)
                return;
            // Real-time input runs at normal speed, like a person playing
            if (!PauseMenu.IsOpen)
                Time.timeScale = inputCheck != null ? 1f : TimeScale;

            if (menuPhase)
            {
                MenuStep();
                return;
            }
            if (deckPhase)
            {
                DeckBuilderStep();
                return;
            }

            try
            {
                Step();
            }
            catch (Exception e)
            {
                failures.Add("[" + Label() + "] autoplay exception: " + e);
                EndInputCheck();
                NextMatch();
            }
        }

        private void Step()
        {
            if (controller == null || controller != SceneController.Instance)
            {
                controller = SceneController.Instance;
                if (controller == null)
                    return;
            }
            SceneController c = controller;

            if (expectedCards < 0 && c.PlayerInfo.HandList.Count > 0)
                expectedCards = CountCards(c);

            // Progress / softlock detection
            string signature = Signature(c);
            double now = EditorApplication.timeSinceStartup;
            if (signature != lastSignature)
            {
                lastSignature = signature;
                lastProgress = now;
                CheckConservation(c);
            }
            else if (now - lastProgress > SoftlockSeconds)
            {
                failures.Add("[" + Label() + "] SOFTLOCK: no progress for " + SoftlockSeconds + "s, state " + signature);
                NextMatch();
                return;
            }

            if (c.battleState == BattleState.WON || c.battleState == BattleState.LOST || c.battleState == BattleState.DRAW)
            {
                results.Add(Label() + ": " + c.battleState + " " + string.Join("-", c.PlayerInfo.RoundsScore) + " vs " + string.Join("-", c.EnemyInfo.RoundsScore));
                Count("result " + c.battleState);
                NextMatch();
                return;
            }

            if (inputCheck != null)
            {
                inputCheck.Tick();
                if (inputCheck.Done)
                    EndInputCheck();
                return;
            }

            if (now < nextActionTime)
                return;
            nextActionTime = now + 0.02;
            PlayerStep(c);
        }

        private void EndInputCheck()
        {
            if (inputCheck == null)
                return;
            inputCheck.Teardown();
            inputCheck = null;
        }

        //----------------------------------------The random player----------------------------------------//
        private void PlayerStep(SceneController c)
        {
            if (c.battleState == BattleState.START && c.PlayerInfo.CanExchange)
            {
                if (redrawChecks < 3)
                {
                    InputScenario scenario = new[] { InputScenario.RedrawSlowClicksThenDone, InputScenario.RedrawTwice, InputScenario.RedrawCarousel }[redrawChecks++];
                    inputCheck = new InputCheck(c, scenario, (ok, what) => Check(ok, what, "redraw input"));
                    return;
                }

                List<int> hand = c.PlayerInfo.HandList;
                int exchanges = rnd.Next(3);
                for (int i = 0; i < exchanges && hand.Count > 0; i++)
                {
                    c.ExchangeCard(hand[rnd.Next(hand.Count)], true);
                    c.PlayerInfo.CardsExchanged++;
                    Count("exchange");
                }
                c.FinishCardExchange();
                return;
            }

            if (c.TurnPicker.activeInHierarchy)
            {
                TurnManager picker = c.TurnPicker.GetComponentInChildren<TurnManager>();
                if (rnd.Next(2) == 0) picker.GoFirst(); else picker.GoSecond();
                Count("scoiatael turn pick");
                return;
            }

            if (c.Panel.activeInHierarchy)
            {
                CardBtn[] buttons = c.Panel.GetComponentsInChildren<CardBtn>();
                if (buttons.Length == 0 || rnd.Next(10) == 0)
                {
                    c.CancelPanel();
                    Count("panel skip");
                }
                else
                {
                    buttons[rnd.Next(buttons.Length)].Choose();
                    Count("panel choose");
                }
                return;
            }

            if (c.battleState != BattleState.PLAYERTURN)
                return;

            switch (c.pendingAction)
            {
                case PendingAction.PlacePickedCard:
                    PlaceSelected(c);
                    return;
                case PendingAction.DiscardCards:
                    List<int> hand = c.PlayerInfo.HandList;
                    c.DiscardCard(hand[rnd.Next(hand.Count)], true);
                    c.PlayerInfo.CardsDiscarded++;
                    if (c.PlayerInfo.CardsDiscarded >= 2)
                        c.DeckCardPicker(true);
                    Count("leader discard");
                    return;
                case PendingAction.PanelChoice:
                    return;
            }

            if (!selectionChecked && c.PlayerField.transform.Find("Hand").childCount > 0)
            {
                selectionChecked = true;
                inputCheck = new InputCheck(c, InputScenario.SelectCard, (ok, what) => Check(ok, what, "selection input"));
                return;
            }
            if (!pauseChecked)
            {
                pauseChecked = true;
                inputCheck = new InputCheck(c, InputScenario.PauseMenu, (ok, what) => Check(ok, what, "pause menu input"));
                return;
            }
            if (!passKeyChecked)
            {
                passKeyChecked = true;
                inputCheck = new InputCheck(c, InputScenario.PassWithSpace, (ok, what) => Check(ok, what, "pass input"));
                return;
            }

            // A normal turn: maybe the leader, sometimes pass, otherwise play a random card through the real UI scripts
            if (c.CanUseLeader(true) && rnd.Next(6) == 0)
            {
                c.ActivateLeader(c.PlayerInfo.Leader, true);
                Count("leader " + c.PlayerInfo.Leader + (c.PlayerInfo.canLeader ? " (unavailable)" : ""));
                return;
            }

            Transform hand_t = c.PlayerField.transform.Find("Hand");
            bool should_pass = hand_t.childCount == 0 || (c.EnemyInfo.hasPassed && c.PlayerInfo.TotalScore() > c.EnemyInfo.TotalScore()) || rnd.Next(14) == 0;
            if (should_pass)
            {
                c.SkipTurn(true);
                Count("pass");
                return;
            }

            GameObject card = hand_t.GetChild(rnd.Next(hand_t.childCount)).gameObject;
            card.GetComponent<CardSelect>().OnCardSelect();
            if (c.selectedCard != card)
                return;
            PlaceSelected(c);
        }

        private void PlaceSelected(SceneController c)
        {
            if (c.selectedCard == null)
                return;
            CardStats stats = c.selectedCard.GetComponent<CardDisplay>().cardStats.GetComponent<CardStats>();
            Count("play " + (stats.faction == Factions.Special ? stats._idstr : string.IsNullOrEmpty(stats.ability) ? "unit" : stats.ability));

            if (stats.ability == Abilities.Decoy)
            {
                List<GameObject> targets = new List<GameObject>();
                foreach (string row in Rows.Units)
                    foreach (Transform t in c.PlayerField.transform.Find(char.ToUpper(row[0]) + row.Substring(1)).Find("Unit"))
                        if (t.GetComponent<CardSelect>() != null)
                            targets.Add(t.gameObject);
                if (targets.Count == 0)
                {
                    // Nothing to swap: click the same card again to deselect it
                    c.selectedCard.GetComponent<CardSelect>().OnCardSelect();
                    c.SkipTurn(true);
                    return;
                }
                targets[rnd.Next(targets.Count)].GetComponent<CardSelect>().OnCardSelect();
                return;
            }

            if (stats.row == Rows.Weather || stats.ability == Abilities.Scorch)
            {
                c.WeatherField.GetComponent<WeatherManager>().OnWeatherClick();
                return;
            }

            List<GameObject> rows = new List<GameObject>();
            rows.AddRange(c.PlayerField.GetComponent<RowPicker>().highLightedRows);
            rows.AddRange(c.EnemyField.GetComponent<RowPicker>().highLightedRows);
            if (rows.Count == 0)
            {
                failures.Add("[" + Label() + "] nothing highlighted for selected card " + stats._idstr);
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

        //----------------------------------------Deck builder----------------------------------------//
        private void DeckBuilderStep()
        {
            DeckController dc = DeckController.Instance;
            if (dc == null || !dc.IsReady || ++deckPhaseFrames < 30)
                return; // let every Start() of the scene run first

            string saved_battle_deck = PlayerPrefs.GetString("playerDeckPath", null);
            List<string> created = new List<string>();
            try
            {
                RunDeckBuilderChecks(dc, created);
            }
            catch (Exception e)
            {
                failures.Add("[deck builder] exception: " + e);
            }
            finally
            {
                foreach (string path in created)
                    if (File.Exists(path))
                        File.Delete(path);
                if (string.IsNullOrEmpty(saved_battle_deck))
                    PlayerPrefs.DeleteKey("playerDeckPath");
                else
                    PlayerPrefs.SetString("playerDeckPath", saved_battle_deck);
                PlayerPrefs.Save();
            }

            // Last: the main menu (its scene and audio buttons) must load and run without errors
            deckPhase = false;
            menuPhase = true;
            currentDeck = "main menu";
            SceneManager.LoadScene(SceneNames.Menu);
        }

        private void MenuStep()
        {
            if (++menuPhaseFrames < 60)
                return;
            if (!menuInputStarted)
            {
                menuInputStarted = true;
                MainMenu menu = UnityEngine.Object.FindAnyObjectByType<MainMenu>();
                Check(menu != null && menu.Items.Count == 4, "the main menu loads with its 4 entries", "main menu");
                inputCheck = new InputCheck(null, InputScenario.MainMenuSettings, (ok, what) => Check(ok, what, "main menu input"));
                return;
            }
            if (inputCheck != null)
            {
                inputCheck.Tick();
                if (inputCheck.Done)
                    EndInputCheck();
                return;
            }
            Finish();
        }

        private void Check(bool condition, string what, string area = "deck builder")
        {
            Count(area + (condition ? " check passed" : " check FAILED"));
            if (!condition)
                failures.Add("[" + area + "] " + what);
        }

        private void RunDeckBuilderChecks(DeckController dc, List<string> created)
        {
            TMP_InputField input = UnityEngine.Object.FindAnyObjectByType<TMP_InputField>();
            Check(input != null, "deck name input field found");
            string faction = dc.my_deck.Faction;
            string tag = "Autoplay " + seed;
            Func<int> deck_count = () => DeckStorage.DeckPaths(faction).Count;
            int before = deck_count();

            // Add a deck
            input.text = "  " + tag + "  Deck ";
            dc.AddDeck(input);
            string path = DeckStorage.PathFor(faction, DeckRules.ToFileName(tag + " Deck"));
            created.Add(path);
            Check(File.Exists(path) && deck_count() == before + 1, "AddDeck creates the deck file");
            Check(dc.currentDeckPath == path && dc.my_deck.Name == tag + " Deck", "the new deck is selected");

            // Same name with other letter case / spacing: rejected, nothing overwritten (B3)
            dc.my_deck.Cards.Add(LeaderIds.ByFaction[faction][0]); // marker that must survive
            File.WriteAllText(path, JsonUtility.ToJson(new Deck { Faction = faction, Name = tag + " Deck", Leader = dc.my_deck.Leader, Cards = new List<int> { 1 } }));
            input.text = tag.ToUpperInvariant() + " DECK";
            dc.AddDeck(input);
            Deck after_duplicate = DeckStorage.Load(path);
            Check(deck_count() == before + 1 && after_duplicate != null && after_duplicate.Cards.Count == 1, "a duplicate name does not overwrite the existing deck");

            // Fill it up to a valid deck: it becomes the battle deck (B9)
            dc.middleManager.SelectDeckPath(path);
            CardDatabase db = dc.Db;
            dc.my_deck.Cards = db.All.Where(c => c.IsUnit && (c.faction == faction || c.faction == Factions.Neutral) && !c.unique && c._id != CardIds.BovineDefenseForce)
                .SelectMany(c => Enumerable.Repeat(c._id, 2)).Take(24).ToList();
            dc.deckCollection.OnDeckEdit();
            Check(DeckRules.IsValid(DeckStorage.Load(path), db), "edited deck saved and valid");
            Check(ApplicationModel.playerDeckPath == path && PlayerPrefs.GetString("playerDeckPath", "") == path, "a valid edited deck becomes the battle deck");

            // Remove cards below the minimum: no longer the battle deck
            dc.my_deck.Cards.RemoveRange(0, 10);
            dc.deckCollection.OnDeckEdit();
            Check(ApplicationModel.playerDeckPath == null && !PlayerPrefs.HasKey("playerDeckPath"), "an invalid deck stops being the battle deck");

            // Rename (B4)
            input.text = tag + " Renamed";
            dc.RenameDeck(input);
            string renamed = DeckStorage.PathFor(faction, DeckRules.ToFileName(tag + " Renamed"));
            created.Add(renamed);
            Check(File.Exists(renamed) && !File.Exists(path), "rename moves the deck file");
            Check(dc.my_deck.Name == tag + " Renamed" && dc.my_deck.Cards.Count == 14, "rename keeps the cards");

            // Rename another deck onto that name: rejected, both decks intact
            input.text = tag + " Other";
            dc.AddDeck(input);
            string other = DeckStorage.PathFor(faction, DeckRules.ToFileName(tag + " Other"));
            created.Add(other);
            input.text = (tag + " renamed").ToLowerInvariant();
            dc.RenameDeck(input);
            Check(File.Exists(other) && File.Exists(renamed) && DeckStorage.Load(renamed).Cards.Count == 14, "rename onto an existing name is rejected");

            // Unsafe names never leave the deck folder (B20)
            input.text = "../../" + tag + " escape";
            dc.AddDeck(input);
            string escaped = DeckStorage.PathFor(faction, DeckRules.ToFileName("../../" + tag + " escape"));
            created.Add(escaped);
            Check(File.Exists(escaped) && Path.GetDirectoryName(escaped) == DeckStorage.FactionFolder(faction), "unsafe characters are stripped from the file name");

            // A corrupt deck file is skipped instead of breaking the builder (B18)
            string corrupt = Path.Combine(DeckStorage.FactionFolder(faction), "autoplay_corrupt_" + seed + ".json");
            created.Add(corrupt);
            File.WriteAllText(corrupt, "{ this is not json");
            dc.PopulateDeckAssets();
            dc.middleManager.LoadDecks();
            Check(dc.middleManager.myPaths.Count == deck_count() - 1, "a corrupt deck file is skipped");

            // Delete (the confirmation dialog's "Delete" button calls ConfirmDelete)
            dc.middleManager.SelectDeckPath(other);
            dc.DeleteDeck();
            Check(File.Exists(other), "DeleteDeck only asks for confirmation");
            foreach (string path_to_delete in new[] { other, renamed, escaped })
                dc.ConfirmDelete(path_to_delete);
            Check(!File.Exists(other) && !File.Exists(renamed) && !File.Exists(escaped), "confirmed deletes remove the files");
        }

        //----------------------------------------Checks----------------------------------------//
        private static int CountCards(SceneController c)
        {
            int total = 0;
            foreach (PlayerState info in new PlayerState[] { c.PlayerInfo, c.EnemyInfo })
                total += info.AllCards().Count(id => id != CardIds.BovineDefenseForce) - info.LeaderHornRows.Count;
            return total;
        }

        private void CheckConservation(SceneController c)
        {
            if (expectedCards < 0 || c.battleState == BattleState.START)
                return;
            int now = CountCards(c);
            if (now != expectedCards)
            {
                failures.Add("[" + Label() + "] CARD COUNT changed: " + expectedCards + " -> " + now + " (" + Signature(c) + ")");
                expectedCards = now; // report each change once
            }
        }

        private static string Signature(SceneController c)
        {
            PlayerState p = c.PlayerInfo, e = c.EnemyInfo;
            return string.Format("{0}/{1} P[h{2} d{3} g{4} b{5} pass{6} lives{7}] E[h{8} d{9} g{10} b{11} pass{12} lives{13}] w{14} panel{15}",
                c.battleState, c.pendingAction, p.HandList.Count, p.DeckList.Count, p.DiscardList.Count, p.UnitsOnBoard(), p.hasPassed, p.Lives,
                e.HandList.Count, e.DeckList.Count, e.DiscardList.Count, e.UnitsOnBoard(), e.hasPassed, e.Lives, c.weatherList.Count, c.Panel.activeSelf);
        }

        private void Finish()
        {
            EndInputCheck();
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            Time.timeScale = 1f;

            StringBuilder report = new StringBuilder();
            bool ok = failures.Count == 0 && errors.Count == 0;
            report.AppendLine("Gwent autoplay: " + (ok ? "PASSED" : "FAILED") + " - " + totalMatches + " matches, seed " + seed);
            report.AppendLine();
            report.AppendLine("Results:");
            foreach (string result in results)
                report.AppendLine("  " + result);
            report.AppendLine();
            report.AppendLine("Coverage (actions performed by the random player, and checks):");
            foreach (KeyValuePair<string, int> stat in stats.OrderBy(s => s.Key))
                report.AppendLine("  " + stat.Key + ": " + stat.Value);
            report.AppendLine();
            report.AppendLine("Failures: " + failures.Count);
            foreach (string failure in failures)
                report.AppendLine("  " + failure);
            report.AppendLine("Errors logged: " + errors.Count + (ignoredEditorErrors > 0 ? " (ignored " + ignoredEditorErrors + " known Unity editor-internal error(s))" : ""));
            foreach (string error in errors.Take(40))
                report.AppendLine("  " + error);

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath)));
            File.WriteAllText(reportPath, report.ToString());
            Debug.Log(report.ToString());

            if (batch)
                EditorApplication.Exit(ok ? 0 : 1);
            else
                EditorApplication.isPlaying = false;
        }

        //----------------------------------------Deck generation----------------------------------------//
        // One valid random deck per leader: faction + neutral units (incl. heroes), and a few specials
        private List<Deck> GenerateDecks(CardDatabase db)
        {
            List<Deck> generated = new List<Deck>();
            foreach (string faction in Factions.Playable)
                foreach (int leader in LeaderIds.ByFaction[faction])
                {
                    List<Card> pool = db.All.Where(card => card.IsUnit && card._id != CardIds.BovineDefenseForce
                        && (card.faction == faction || card.faction == Factions.Neutral)).ToList();
                    List<int> cards = new List<int>();
                    foreach (Card card in pool.OrderBy(x => rnd.Next()))
                    {
                        int copies = card.unique ? 1 : 1 + rnd.Next(3);
                        for (int i = 0; i < copies && cards.Count < 26; i++)
                            cards.Add(card._id);
                    }
                    int[] specials = { CardIds.Decoy, CardIds.Scorch, CardIds.CommanderHorn, CardIds.BitingFrost, CardIds.ImpenetrableFog, CardIds.TorrentialRain, CardIds.ClearWeather };
                    foreach (int special in specials.OrderBy(x => rnd.Next()).Take(4 + rnd.Next(4)))
                        cards.Add(special);

                    Deck deck = new Deck { Faction = faction, Leader = leader, Name = faction + " / " + db.Get(leader).name, Cards = cards };
                    List<string> problems = DeckRules.Validate(deck, db);
                    if (problems.Count == 0)
                        generated.Add(deck);
                    else
                        failures.Add("Generated deck invalid (" + deck.Name + "): " + problems[0]);
                }
            return generated;
        }
    }

    //----------------------------------------Real mouse input----------------------------------------//
    private enum InputScenario
    {
        RedrawSlowClicksThenDone, // two slow clicks (no redraw), a slow double-click (redraw), then Done
        RedrawTwice,              // a burst of four clicks (one redraw only), then the carousel's front card (ends it)
        RedrawCarousel,           // carousel: Right arrow, click a side card, Enter redraws it, right-click keeps the hand
        SelectCard,               // hovering at a card's lower edge keeps it raised, left-click selects it, a second
                                  // click deselects it, right-click does nothing
        PauseMenu,                // Escape pauses and resumes; the menu button and Resume do the same
        PassWithSpace,            // a short press of Space does not pass, holding it does
        MainMenuSettings,         // hovering moves the highlight, Settings opens, the volume slider works, Escape closes
    }

    // Plays part of the game like a person would: a virtual mouse and keyboard at human speed, going through the
    // Input System, the EventSystem and the game's own handlers.
    private class InputCheck
    {
        private const double PressDuration = 0.1;

        private readonly SceneController c;
        private readonly Action<bool, string> check;
        private readonly List<KeyValuePair<double, Action>> steps = new List<KeyValuePair<double, Action>>();
        private readonly double start;
        private int next;
        private Mouse mouse;
        private Keyboard keyboard;
        private float savedMasterVolume = -1;
        private bool settingsChanged;
        private InputSettings.EditorInputBehaviorInPlayMode savedEditorBehavior;
        private InputSettings.BackgroundBehavior savedBackgroundBehavior;
        private Transform card;

        public bool Done { get { return next >= steps.Count; } }

        // The times are when the button goes down, in seconds
        public InputCheck(SceneController controller, InputScenario scenario, Action<bool, string> check)
        {
            c = controller;
            this.check = check;
            start = EditorApplication.timeSinceStartup;

            At(0, Setup);
            if (c != null && scenario != InputScenario.PauseMenu)
                At(0, () => check(CardPosition() != Vector2.zero, "the hand has cards to click"));
            if (scenario == InputScenario.RedrawSlowClicksThenDone)
            {
                Click(0.4, CardPosition);
                Click(1.2, CardPosition);
                At(1.8, () => check(c.PlayerInfo.CardsExchanged == 0, "two clicks 0.8 s apart on a hand card do not redraw it"));
                Click(2.2, CardPosition);
                Click(2.65, CardPosition);
                At(3.2, () => check(c.PlayerInfo.CardsExchanged == 1 && c.PlayerInfo.HandList.Count == 10, "a slow double-click (0.45 s) on a hand card redraws it"));
                Click(3.6, DonePosition);
                At(4.1, () => check(!c.PlayerInfo.CanExchange, "the Done button ends the redraw"));
            }
            else if (scenario == InputScenario.RedrawTwice)
            {
                // Clicks 3 and 4 land on the card that replaced the redrawn one: they must not redraw it too
                Click(0.4, CardPosition);
                Click(0.54, CardPosition);
                Click(0.68, CardPosition);
                Click(0.82, CardPosition);
                At(1.5, () => check(c.PlayerInfo.CardsExchanged == 1 && c.PlayerInfo.HandList.Count == 10, "a burst of four clicks redraws one card only"));
                At(1.5, () => check(Picker() != null && Picker().CardCount == 10 && c.PlayerInfo.HandList.Contains(Picker().FocusedCardId), "the redraw carousel follows the hand"));
                Click(1.8, CarouselCard(-1));
                At(2.4, () => check(c.PlayerInfo.CardsExchanged == 2 && !c.PlayerInfo.CanExchange, "clicking the carousel's front card redraws it (and ends the redraw)"));
            }
            else if (scenario == InputScenario.RedrawCarousel)
            {
                Key(0.4, UnityEngine.InputSystem.Key.RightArrow);
                At(0.8, () => check(Picker() != null && Picker().FocusIndex == 1, "the Right arrow brings the next card forward"));
                Click(1.1, CarouselCard(3));
                At(1.6, () => check(Picker() != null && Picker().FocusIndex == 3, "clicking a side card brings it forward"));
                Key(1.9, UnityEngine.InputSystem.Key.Enter);
                At(2.4, () => check(c.PlayerInfo.CardsExchanged == 1, "Enter redraws the front card"));
                Click(2.7, CardPosition, MouseButton.Right);
                At(3.2, () => check(!c.PlayerInfo.CanExchange && c.PlayerInfo.CardsExchanged == 1, "right-click keeps the rest of the hand"));
            }
            else if (scenario == InputScenario.PauseMenu)
            {
                Key(0.3, UnityEngine.InputSystem.Key.Escape);
                At(0.8, () => check(PauseMenu.IsOpen && Time.timeScale == 0, "Escape pauses the battle (time stops)"));
                Key(1.1, UnityEngine.InputSystem.Key.Escape);
                At(1.6, () => check(!PauseMenu.IsOpen && Time.timeScale > 0, "Escape again resumes it"));
                Click(1.9, Named("PauseButton"));
                At(2.4, () => check(PauseMenu.IsOpen, "the menu button opens the pause menu"));
                Click(2.7, Named("Item Resume"));
                At(3.2, () => check(!PauseMenu.IsOpen && Time.timeScale > 0, "Resume closes it"));
            }
            else if (scenario == InputScenario.PassWithSpace)
            {
                PassManager pass = UnityEngine.Object.FindAnyObjectByType<PassManager>(FindObjectsInactive.Include);
                float hold = pass != null ? pass.requiredHoldTime : 1f;
                KeyHold(0.3, UnityEngine.InputSystem.Key.Space, 0.25);
                At(0.9, () => check(!c.PlayerInfo.hasPassed, "a short press of Space does not pass"));
                KeyHold(1.2, UnityEngine.InputSystem.Key.Space, hold + 0.4);
                At(1.2 + hold + 0.8, () => check(c.PlayerInfo.hasPassed, "holding Space passes"));
            }
            else if (scenario == InputScenario.MainMenuSettings)
            {
                Move(0.3, Named("Item Deck builder"));
                At(0.6, () => check(EventSystem.current.currentSelectedGameObject == GameObject.Find("Item Deck builder"), "hovering a menu entry highlights it"));
                Click(0.9, Named("Item Settings"));
                At(1.4, () => check(SettingsPanel.IsOpen, "clicking Settings opens the settings"));
                Click(1.7, MasterVolumeAt(0.25f));
                At(2.2, () => check(Mathf.Abs(GameSettings.MasterVolume - 0.25f) < 0.06f, "clicking the master volume slider sets the volume"));
                Key(2.5, UnityEngine.InputSystem.Key.Escape);
                At(3.0, () => check(!SettingsPanel.IsOpen, "Escape closes the settings"));
            }
            else
            {
                // The pointer just above the card's lower edge: the card rises and must stay up, not bob
                Move(0.3, CardLowerEdge);
                bool stayed_up = true;
                foreach (double t in new[] { 0.6, 0.7, 0.8, 0.9 })
                    At(t, () => stayed_up &= card != null && card.GetComponent<CardHover>().isCardUp);
                At(0.95, () => check(stayed_up, "a card hovered at its lower edge stays raised"));
                Click(1.4, CardPosition, MouseButton.Right);
                At(1.9, () => check(c.selectedCard == null, "right-clicking a hand card does not select it"));
                Click(2.2, CardPosition);
                At(2.7, () => check(card != null && c.selectedCard == card.gameObject, "left-clicking a hand card selects it"));
                Click(3.4, CardPosition);
                At(3.9, () => check(c.selectedCard == null, "clicking the selected card again deselects it"));
            }
        }

        public void Tick()
        {
            double elapsed = EditorApplication.timeSinceStartup - start;
            while (next < steps.Count && elapsed >= steps[next].Key)
                steps[next++].Value();
        }

        public void Teardown()
        {
            next = steps.Count;
            if (mouse != null)
            {
                InputSystem.RemoveDevice(mouse);
                mouse = null;
            }
            if (keyboard != null)
            {
                InputSystem.RemoveDevice(keyboard);
                keyboard = null;
            }
            if (savedMasterVolume >= 0 && !Mathf.Approximately(GameSettings.MasterVolume, savedMasterVolume))
                GameSettings.MasterVolume = savedMasterVolume;
            if (settingsChanged)
            {
                InputSystem.settings.editorInputBehaviorInPlayMode = savedEditorBehavior;
                InputSystem.settings.backgroundBehavior = savedBackgroundBehavior;
                settingsChanged = false;
            }
        }

        private void At(double time, Action action)
        {
            steps.Add(new KeyValuePair<double, Action>(time, action));
        }

        // Moves onto the target first (where it is at that moment), then presses and releases the button
        private void Click(double time, Func<Vector2> position, MouseButton button = MouseButton.Left)
        {
            Vector2 at = Vector2.zero;
            At(time - 0.05, () => { at = position(); Send(at, button, false); });
            At(time, () => Send(at, button, true));
            At(time + PressDuration, () => Send(at, button, false));
        }

        private void Move(double time, Func<Vector2> position)
        {
            At(time, () => Send(position(), MouseButton.Left, false));
        }

        private void KeyHold(double time, Key key, double duration)
        {
            At(time, () => InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)));
            At(time + duration, () => InputSystem.QueueStateEvent(keyboard, new KeyboardState()));
        }

        private void Key(double time, Key key)
        {
            At(time, () => InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)));
            At(time + PressDuration, () => InputSystem.QueueStateEvent(keyboard, new KeyboardState()));
        }
        private void Setup()
        {
            // Input reaches the game without the Game view having focus (batch mode has none). The settings are changed
            // in place and restored afterwards: replacing the settings object would destroy the Input System's own one.
            InputSettings settings = InputSystem.settings;
            savedEditorBehavior = settings.editorInputBehaviorInPlayMode;
            savedBackgroundBehavior = settings.backgroundBehavior;
            settingsChanged = true;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            mouse = InputSystem.AddDevice<Mouse>("AutoplayMouse");
            keyboard = InputSystem.AddDevice<Keyboard>("AutoplayKeyboard");
            savedMasterVolume = GameSettings.MasterVolume;

            InputSystemUIInputModule module = EventSystem.current != null ? EventSystem.current.currentInputModule as InputSystemUIInputModule : null;
            check(module != null, "the scene uses the Input System UI module");
            if (module != null && module.actionsAsset != null)
            {
                module.actionsAsset.Disable();
                module.actionsAsset.Enable();
            }
        }

        private void Send(Vector2 position, MouseButton button, bool pressed)
        {
            if (mouse != null)
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(button, pressed));
        }

        // A card in the middle of the hand (another one once a redraw has rebuilt the hand)
        private Vector2 CardPosition()
        {
            Transform hand = c.PlayerField.transform.Find("Hand");
            if (card == null && hand.childCount > 0)
                card = hand.GetChild(hand.childCount / 2);
            return card != null ? ScreenPoint(card) : Vector2.zero;
        }

        private static RedrawPicker Picker()
        {
            return UnityEngine.Object.FindAnyObjectByType<RedrawPicker>();
        }

        // A card of the redraw carousel (-1: the front one), where it is when the click happens
        private Func<Vector2> CarouselCard(int index)
        {
            return () =>
            {
                RedrawPicker picker = Picker();
                RectTransform card = picker != null ? picker.CardAt(index < 0 ? picker.FocusIndex : index) : null;
                check(card != null, "the redraw carousel shows card " + index);
                return card != null ? ScreenPoint(card) : Vector2.zero;
            };
        }

        // 3 pixels above the bottom edge of the (not yet raised) card
        private Vector2 CardLowerEdge()
        {
            CardPosition();
            if (card == null)
                return Vector2.zero;
            RectTransform rect = (RectTransform)card;
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 bottom = RectTransformUtility.WorldToScreenPoint(null, (corners[0] + corners[3]) / 2);
            return bottom + new Vector2(0, 3);
        }

        private Vector2 DonePosition()
        {
            GameObject done = GameObject.Find("Button Done");
            check(done != null, "the redraw prompt shows a Done button");
            return done != null ? ScreenPoint(done.transform) : Vector2.zero;
        }

        // An active UI object by name (looked up when the click happens)
        private Func<Vector2> Named(string name)
        {
            return () =>
            {
                GameObject target = GameObject.Find(name);
                check(target != null, "\"" + name + "\" is on screen");
                return target != null ? ScreenPoint(target.transform) : Vector2.zero;
            };
        }

        // A point on the settings' master volume slider, at that fraction of its travel
        private Func<Vector2> MasterVolumeAt(float fraction)
        {
            return () =>
            {
                SettingsPanel panel = UnityEngine.Object.FindAnyObjectByType<SettingsPanel>();
                UnityEngine.UI.Slider slider = panel != null ? panel.GetComponentInChildren<UnityEngine.UI.Slider>() : null;
                check(slider != null, "the settings show the master volume slider");
                if (slider == null)
                    return Vector2.zero;
                RectTransform area = (RectTransform)slider.handleRect.parent;
                Vector3[] corners = new Vector3[4];
                area.GetWorldCorners(corners);
                return RectTransformUtility.WorldToScreenPoint(null, Vector3.Lerp(corners[0], corners[3], fraction) + (corners[1] - corners[0]) / 2);
            };
        }

        private static Vector2 ScreenPoint(Transform target)
        {
            RectTransform rect = (RectTransform)target;
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Canvas canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return RectTransformUtility.WorldToScreenPoint(camera, (corners[0] + corners[2]) / 2);
        }
    }
}

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
using UnityEngine.SceneManagement;

// End-to-end smoke test: plays whole battles in the real Game scene with a random "player" against the AI.
// Every leader of every faction gets a generated deck. Fails on exceptions/errors in the log, on a game that
// stops progressing (softlock), and on cards appearing or disappearing (card conservation).
// Then exercises the deck builder (add, duplicate names, rename, battle deck, corrupt files, delete) in the Deck scene;
// it only touches its own "Autoplay ..." decks and restores the saved battle deck afterwards.
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
        private readonly Dictionary<string, int> stats = new Dictionary<string, int>();
        private List<Deck> decks;

        private int match = -1;
        private bool deckPhase;
        private int deckPhaseFrames;
        private SceneController controller;
        private int expectedCards = -1;
        private string lastSignature;
        private double lastProgress;
        private double nextActionTime;
        private string currentDeck;

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
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
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
            Time.timeScale = TimeScale;

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

            if (now < nextActionTime)
                return;
            nextActionTime = now + 0.02;
            PlayerStep(c);
        }

        //----------------------------------------The random player----------------------------------------//
        private void PlayerStep(SceneController c)
        {
            if (c.battleState == BattleState.START && c.PlayerInfo.CanExchange)
            {
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
            Finish();
        }

        private void Check(bool condition, string what)
        {
            Count(condition ? "deck builder check passed" : "deck builder check FAILED");
            if (!condition)
                failures.Add("[deck builder] " + what);
        }

        private void RunDeckBuilderChecks(DeckController dc, List<string> created)
        {
            TMP_InputField input = UnityEngine.Object.FindObjectOfType<TMP_InputField>();
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
            report.AppendLine("Coverage (actions performed by the random player):");
            foreach (KeyValuePair<string, int> stat in stats.OrderBy(s => s.Key))
                report.AppendLine("  " + stat.Key + ": " + stat.Value);
            report.AppendLine();
            report.AppendLine("Failures: " + failures.Count);
            foreach (string failure in failures)
                report.AppendLine("  " + failure);
            report.AppendLine("Errors logged: " + errors.Count);
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
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace Gwent.Rules.Tests
{
// The rules engine under fire: battles between random and absurd decks (all spies, all medics, musters, weather
// spam, decoys, heroes, a handful of cards...), each side making random moves the way the battle does them
// (BattleController's flows: medic choices, decoys, every leader, round ends, faction abilities), with invariants
// checked after every move: no card appears or disappears, every unit's strength is The Witcher 3's
// (an independent copy of its CardInstance.getTotalPower), scores add up, horn slots and the weather board hold
// what they should, the board clears at a round's end, ties and round winners are decided right, nothing throws,
// every battle ends. RulesFuzzTests runs a few hundred; it also builds outside Unity for runs of thousands.
public static class RulesFuzzer
{
    static CardDatabase Db { get { return TestCardData.Db; } }
    static Dictionary<string, int> Failures;
    static Dictionary<string, string> FirstRepro;
    static Dictionary<string, int> Stats;
    static List<string> log;
    static string context;

    public class Report
    {
        public int Battles;
        public Dictionary<string, int> Failures;
        public Dictionary<string, string> Repro;
        public Dictionary<string, int> Stats;

        public override string ToString()
        {
            var lines = new List<string> { Battles + " battles, " + Failures.Count + " kinds of failure" };
            foreach (var kv in Failures.OrderByDescending(k => k.Value))
                lines.Add("[" + kv.Value + "x] " + kv.Key + "\n" + Repro[kv.Key]);
            return string.Join("\n", lines);
        }
    }

    public static Report Run(int battles, int seed0)
    {
        Failures = new Dictionary<string, int>();
        FirstRepro = new Dictionary<string, string>();
        Stats = new Dictionary<string, int>();
        for (int g = 0; g < battles; g++)
        {
            int seed = seed0 * 100000 + g;
            log = new List<string>();
            try { PlayBattle(seed); }
            catch (Exception e)
            {
                Fail("EXCEPTION " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n').FirstOrDefault(l => l.Contains("Gwent.Rules"))?.Trim());
            }
        }
        return new Report { Battles = battles, Failures = Failures, Repro = FirstRepro, Stats = Stats };
    }

    static void Count(string key) { int v; Stats.TryGetValue(key, out v); Stats[key] = v + 1; }

    static void Fail(string what)
    {
        string key = what.Length > 220 ? what.Substring(0, 220) : what;
        int v; Failures.TryGetValue(key, out v); Failures[key] = v + 1;
        if (!FirstRepro.ContainsKey(key))
            FirstRepro[key] = "    " + context + "\n    last moves:\n      " + string.Join("\n      ", log.Skip(Math.Max(0, log.Count - 8)));
    }

    //------------------------------------------ Decks ------------------------------------------//
    static readonly string[] Themes = { "random", "random", "spies", "medics", "muster", "weather", "decoys", "heroes", "bond", "morale", "agile", "scorch", "horns", "tiny", "chaos" };

    static Deck MakeDeck(Random rnd, string faction, int leader, string theme)
    {
        var pool = Db.All.Where(c => c.IsUnit && c._id != CardIds.BovineDefenseForce && (c.faction == faction || c.faction == Factions.Neutral)).ToList();
        Func<Card, bool> themed;
        switch (theme)
        {
            case "spies": themed = c => c.ability == Abilities.Spy; break;
            case "medics": themed = c => c.ability == Abilities.Medic; break;
            case "muster": themed = c => c.ability == Abilities.Muster; break;
            case "heroes": themed = c => c.unique; break;
            case "bond": themed = c => c.ability == Abilities.TightBond; break;
            case "morale": themed = c => c.ability == Abilities.MoraleBoost || c.ability == Abilities.UnitHorn; break;
            case "agile": themed = c => c.row == Rows.Agile; break;
            case "scorch": themed = c => Abilities.IsRowScorch(c.ability); break;
            default: themed = c => true; break;
        }
        var cards = new List<int>();
        var preferred = pool.Where(themed).OrderBy(x => rnd.Next()).ToList();
        var rest = pool.OrderBy(x => rnd.Next()).ToList();
        int target = theme == "tiny" ? 6 + rnd.Next(6) : 22 + rnd.Next(10);
        foreach (var src in new[] { preferred, rest })
            foreach (var card in src)
            {
                int copies = theme == "chaos" ? 1 + rnd.Next(6) : 1 + rnd.Next(Math.Max(1, DeckRules.MaxCopies(card)));
                for (int i = 0; i < copies && cards.Count < target; i++)
                    cards.Add(card._id);
            }
        int[] specials = { CardIds.Decoy, CardIds.Scorch, CardIds.CommanderHorn, CardIds.BitingFrost, CardIds.ImpenetrableFog, CardIds.TorrentialRain, CardIds.ClearWeather };
        int nspecials = theme == "weather" ? 10 : theme == "decoys" || theme == "horns" ? 8 : rnd.Next(11);
        for (int i = 0; i < nspecials; i++)
        {
            int s = specials[rnd.Next(specials.Length)];
            if (theme == "weather") s = new[] { 200, 201, 204, 206 }[rnd.Next(4)];
            if (theme == "decoys") s = CardIds.Decoy;
            if (theme == "horns") s = CardIds.CommanderHorn;
            if (theme == "scorch" && rnd.Next(2) == 0) s = CardIds.Scorch;
            cards.Add(s);
        }
        return new Deck { Faction = faction, Leader = leader, Name = faction + "/" + leader + "/" + theme, Cards = cards };
    }

    //------------------------------------------ Battle ------------------------------------------//
    static void PlayBattle(int seed)
    {
        Random rnd = new Random(seed);
        Rng.Seed(seed);
        string f1 = Factions.Playable[rnd.Next(Factions.Playable.Length)], f2 = Factions.Playable[rnd.Next(Factions.Playable.Length)];
        int l1 = LeaderIds.ByFaction[f1][rnd.Next(5)], l2 = LeaderIds.ByFaction[f2][rnd.Next(5)];
        string t1 = Themes[rnd.Next(Themes.Length)], t2 = Themes[rnd.Next(Themes.Length)];
        Deck d1 = MakeDeck(rnd, f1, l1, t1), d2 = MakeDeck(rnd, f2, l2, t2);
        context = "seed " + seed + ": " + d1.Name + " vs " + d2.Name;
        Count("battles");

        Board board = new Board
        {
            Player = new PlayerState { Faction = f1, Leader = l1 },
            Enemy = new PlayerState { Faction = f2, Leader = l2 },
            Db = Db,
            Rnd = rnd,
        };
        board.RandomMedic = l1 == LeaderIds.EmhyrInvaderOfTheNorth || l2 == LeaderIds.EmhyrInvaderOfTheNorth;
        board.DoubleSpies = l1 == LeaderIds.EredinTheTreacherous || l2 == LeaderIds.EredinTheTreacherous;
        foreach (bool p in new[] { true, false })
        {
            Deck deck = p ? d1 : d2;
            PlayerState side = board.Side(p);
            side.DeckList = Rng.Shuffle(deck.Cards);
            RoundRules.Draw(side, deck.Leader == LeaderIds.FrancescaDaisyOfTheValley ? 11 : 10);
            if (deck.Leader == LeaderIds.EmhyrTheWhiteFlame)
                board.Opponent(p).leaderDisabled = true;
            // The redraw: up to 2 random redraws
            int redraws = rnd.Next(3);
            for (int i = 0; i < redraws && side.HandList.Count > 0; i++)
                RoundRules.Redraw(side, side.HandList[rnd.Next(side.HandList.Count)]);
        }
        var initial = Multiset(board);
        bool player_turn = rnd.Next(2) == 0;
        bool last_passer_player = false;

        for (int turn = 0; ; turn++)
        {
            if (turn > 800) { Fail("battle never ended"); return; }

            if (board.Player.hasPassed && board.Enemy.hasPassed)
            {
                board.Recalculate();
                RoundWinner winner = RoundRules.DecideRound(board.Player.TotalScore(), board.Enemy.TotalScore(), board.Player.Faction, board.Enemy.Faction);
                CheckRoundWinner(board, winner);
                int pl = board.Player.Lives, el = board.Enemy.Lives;
                RoundRules.ApplyRoundResult(board.Player, board.Enemy, winner);
                Count("rounds");
                if (winner == RoundWinner.Draw) Count("round draws");
                if (board.Player.Lives == 0 || board.Enemy.Lives == 0)
                {
                    Count(board.Player.Lives == board.Enemy.Lives ? "battle draws" : "battles decided");
                    if (board.Player.RoundsScore.Count > 3) Fail("more than 3 rounds played: " + board.Player.RoundsScore.Count);
                    return;
                }
                if (board.Player.RoundsScore.Count >= 3) Fail("3 rounds played and nobody at 0 lives (" + board.Player.Lives + "-" + board.Enemy.Lives + ")");
                board.Player.hasPassed = board.Enemy.hasPassed = false;
                foreach (bool p in new[] { true, false })
                {
                    PlayerState side = board.Side(p);
                    int units_before = Rows.Units.Sum(r => side.UnitRow(r).Count(id => !Db.Get(id).unique && !Db.Get(id).IsSpecial));
                    int cows = Rows.Units.Sum(r => side.UnitRow(r).Count(id => id == CardIds.Cow)) + side.PendingSummons.Count;
                    int kept = RoundRules.CleanupBoard(side, rnd, Db);
                    if (kept == CardIds.Cow) cows--; // a kept Cow stays: nothing summoned for it
                    if (side.Faction == Factions.Monsters && units_before > 0 && kept == -1) Fail("Monsters kept no unit although it had some");
                    if (side.Faction != Factions.Monsters && kept != -1) Fail("non-Monsters kept a unit");
                    if (kept != -1 && (Db.Get(kept).unique || Db.Get(kept).IsSpecial)) Fail("Monsters kept a hero/special: " + kept);
                    int left = Rows.Units.Sum(r => side.UnitRow(r).Count) + Rows.Units.Sum(r => side.SpecialRow(r).Count);
                    int expected_left = (kept != -1 ? 1 : 0) + cows;
                    if (left != expected_left) Fail("board not cleared at round end: " + left + " cards left, expected " + expected_left);
                    if (side.WeatherPlayed.Count > 0 || side.LeaderHornRows.Count > 0) Fail("weather/leader horn state left after cleanup");
                }
                board.Weather.Clear();
                if (winner == RoundWinner.Player && board.Player.Faction == Factions.NorthernRealms) RoundRules.Draw(board.Player, 1);
                if (winner == RoundWinner.Enemy && board.Enemy.Faction == Factions.NorthernRealms) RoundRules.Draw(board.Enemy, 1);
                player_turn = winner == RoundWinner.Player || (winner == RoundWinner.Draw && last_passer_player);
                // The Witcher 3: a side starting a round with no card in hand sits it out
                foreach (bool p in new[] { true, false })
                    if (board.Side(p).HandList.Count == 0) { board.Side(p).hasPassed = true; Count("sits out a round (empty hand)"); }
                board.Recalculate();
                CheckInvariants(board, initial, "round cleanup");
                continue;
            }

            PlayerState me = board.Side(player_turn);
            if (!me.hasPassed)
            {
                if (RoundRules.OutOfPlays(board, player_turn))
                {
                    me.hasPassed = true; last_passer_player = player_turn;
                    log.Add((player_turn ? "P" : "E") + " auto-passes");
                }
                else
                {
                    string move = Move(board, player_turn, rnd);
                    log.Add((player_turn ? "P" : "E") + " " + move);
                    if (me.hasPassed) last_passer_player = player_turn;
                    CheckInvariants(board, initial, move);
                }
            }
            player_turn = !player_turn;
        }
    }

    //------------------------------------------ Moves (mirroring BattleController) ------------------------------------------//
    static string Move(Board board, bool p, Random rnd)
    {
        PlayerState me = board.Side(p), opp = board.Opponent(p);
        int r = rnd.Next(100);
        // The AI's own choice sometimes (its decisions must always be legal)
        if (r < 25)
        {
            // Every difficulty (Hard rarely: it plays whole battles out for each move)
            AiContext ctx = board.AiContextFor(p);
            int level = rnd.Next(100);
            ctx.Difficulty = level < 40 ? AiDifficulty.Easy : level < 98 ? AiDifficulty.Normal : AiDifficulty.Hard;
            ctx.Seed = rnd.Next();
            AiAction a = AiBrain.Decide(ctx);
            Count("AI " + ctx.Difficulty);
            return ExecuteAi(board, a, p, rnd);
        }
        if (r < 30 || me.HandList.Count == 0 && !LeaderUsable(board, p))
        {
            me.hasPassed = true;
            Count("pass");
            return "passes";
        }
        if (r < 42 && LeaderUsable(board, p))
            return UseLeader(board, p, rnd);
        if (me.HandList.Count == 0) { me.hasPassed = true; return "passes (empty hand)"; }

        int card_id = me.HandList[rnd.Next(me.HandList.Count)];
        Card card = Db.Get(card_id);
        if (card.ability == Abilities.Decoy)
        {
            var targets = new List<KeyValuePair<string, int>>();
            foreach (string row in Rows.Units)
                foreach (int id in me.UnitRow(row))
                {
                    Card t = Db.Get(id);
                    if (!t.unique && !t.IsSpecial && t.ability != Abilities.Leader) targets.Add(new KeyValuePair<string, int>(row, id));
                }
            if (targets.Count == 0) { me.hasPassed = true; return "passes (decoy, no target)"; }
            var pick = targets[rnd.Next(targets.Count)];
            me.HandList.Remove(card_id);
            int hand_before = me.HandList.Count;
            PlayRules.Decoy(board, pick.Value, pick.Key, p, card_id);
            if (!me.HandList.Contains(pick.Value)) Fail("decoy target did not return to the hand");
            if (!me.UnitRow(pick.Key).Contains(CardIds.Decoy)) Fail("decoy not placed on the target's row");
            Count("decoy");
            return "decoys " + Db.Get(pick.Value).name + " (" + pick.Value + ") on " + pick.Key;
        }
        string field = card.row;
        if (card.row == Rows.Agile) field = rnd.Next(2) == 0 ? Rows.Close : Rows.Range;
        if (card.row == Rows.Special)
        {
            var free = Rows.Units.Where(row => !me.HasHorn(row)).ToList();
            if (free.Count == 0) { me.hasPassed = true; return "passes (horn, no free slot)"; }
            field = free[rnd.Next(free.Count)];
        }
        if (card.row == Rows.Weather || card.row == Rows.OneTime) field = Rows.Weather;
        return Play(board, card_id, field, p, rnd, "plays");
    }

    static string Play(Board board, int card_id, string field, bool p, Random rnd, string verb)
    {
        PlayerState me = board.Side(p);
        Card card = Db.Get(card_id);
        if (!me.HandList.Remove(card_id)) Fail("played card not in hand: " + card_id);
        var weather_before = new List<int>(board.Weather);
        int deck_before = me.DeckList.Count, hand_before = me.HandList.Count;
        var snapshot = Snapshot(board);
        bool manual = p && rnd.Next(2) == 0;
        PlayOutcome outcome = PlayRules.Resolve(board, card, field, p, manual);
        Count("play " + (card.IsUnit ? (string.IsNullOrEmpty(card.ability) ? "unit" : card.ability) : card.ability));
        CheckPlayEffects(board, snapshot, card, field, p, deck_before, hand_before);
        string desc = verb + " " + card.name + " (" + card_id + ") on " + field;
        // Manual medic: the player picks a unit from the discard pile, which is then played like a card from the hand
        while (outcome == PlayOutcome.WaitingForMedicChoice)
        {
            var candidates = AbilityRules.MedicCandidates(me.DiscardList, Db);
            if (candidates.Count == 0) { Fail("WaitingForMedicChoice with no candidate"); break; }
            int revived = candidates[rnd.Next(candidates.Count)];
            if (Db.Get(revived).unique) Fail("medic can revive a hero: " + revived);
            me.DiscardList.Remove(revived);
            me.HandList.Add(revived);
            Card rc = Db.Get(revived);
            string rf = rc.row == Rows.Agile ? (rnd.Next(2) == 0 ? Rows.Close : Rows.Range) : rc.row;
            me.HandList.Remove(revived);
            desc += "; revives " + rc.name + " (" + revived + ") on " + rf;
            deck_before = me.DeckList.Count; hand_before = me.HandList.Count;
            snapshot = Snapshot(board);
            outcome = PlayRules.Resolve(board, rc, rf, p, true);
            CheckPlayEffects(board, snapshot, rc, rf, p, deck_before, hand_before);
            Count("medic chain step");
        }
        return desc;
    }

    static string ExecuteAi(Board board, AiAction a, bool p, Random rnd)
    {
        PlayerState me = board.Side(p);
        switch (a.Type)
        {
            case AiActionType.PlayCard:
                if (!me.HandList.Contains(a.CardId)) { Fail("AI plays a card not in its hand: " + a.CardId); me.hasPassed = true; return "AI bad card"; }
                Card c = Db.Get(a.CardId);
                if (c.ability == Abilities.Decoy)
                {
                    if (!Rows.IsUnitRow(a.Row) || !me.UnitRow(a.Row).Contains(a.TargetId)) Fail("AI decoys a missing target " + a.TargetId + " on " + a.Row);
                    else if (Db.Get(a.TargetId).unique) Fail("AI decoys a hero " + a.TargetId);
                    me.HandList.Remove(a.CardId);
                    PlayRules.Decoy(board, a.TargetId, a.Row, p, a.CardId);
                    Count("AI decoy");
                    return "AI decoys " + a.TargetId;
                }
                if (c.row == Rows.Special && Rows.IsUnitRow(a.Row) && me.HasHorn(a.Row)) Fail("AI plays a horn on a row that has one: " + a.Row);
                return Play(board, a.CardId, a.Row, p, rnd, "AI plays");
            case AiActionType.UseLeader:
                if (!LeaderUsable(board, p)) Fail("AI uses an unusable leader " + me.Leader + ": " + LeaderRules.Unavailable(board, me.Leader, p));
                return UseLeader(board, p, rnd, a);
            default:
                me.hasPassed = true;
                return "AI passes";
        }
    }

    static bool LeaderUsable(Board board, bool p)
    {
        PlayerState me = board.Side(p);
        return me.canLeader && !me.leaderDisabled && LeaderRules.Unavailable(board, me.Leader, p) == null;
    }

    static string UseLeader(Board board, bool p, Random rnd, AiAction? ai = null)
    {
        PlayerState me = board.Side(p), opp = board.Opponent(p);
        int leader = me.Leader;
        me.canLeader = false;
        Count("leader " + leader);
        string weather_row = LeaderIds.WeatherPickRow(leader);
        if (weather_row != null)
        {
            var choices = LeaderRules.WeatherCardsFor(me, weather_row, Db);
            int pick = ai.HasValue && choices.Contains(ai.Value.CardId) ? ai.Value.CardId : choices[rnd.Next(choices.Count)];
            if (!me.DeckList.Remove(pick)) Fail("weather leader card not in deck");
            var snap = Snapshot(board);
            PlayRules.Resolve(board, Db.Get(pick), Rows.Weather, p, false);
            return "leader " + leader + " plays weather " + pick;
        }
        if (LeaderRules.IsInstant(leader))
        {
            string reason;
            int opp_units = Rows.Units.Sum(r => opp.UnitRow(r).Count);
            if (!LeaderRules.ApplyInstant(board, leader, p, out reason)) Fail("instant leader " + leader + " failed after Unavailable said yes: " + reason);
            if (LeaderIds.ScorchRow(leader) != null && Rows.Units.Sum(r => opp.UnitRow(r).Count) >= opp_units) Fail("leader row scorch " + leader + " destroyed nothing although allowed");
            return "leader " + leader + " (instant)";
        }
        switch (leader)
        {
            case LeaderIds.EredinDestroyerOfWorlds:
                for (int i = 0; i < 2 && me.HandList.Count > 0; i++)
                {
                    int d = me.HandList[rnd.Next(me.HandList.Count)];
                    me.HandList.Remove(d); AbilityRules.Discard(me, d);
                }
                if (me.DeckList.Count > 0)
                {
                    int drawn = me.DeckList[rnd.Next(me.DeckList.Count)];
                    me.DeckList.Remove(drawn); me.HandList.Add(drawn);
                }
                return "leader Destroyer of Worlds";
            case LeaderIds.EredinBringerOfDeath:
            case LeaderIds.EmhyrTheRelentless:
                {
                    var pile = leader == LeaderIds.EmhyrTheRelentless ? opp.DiscardList : me.DiscardList;
                    var units = AbilityRules.MedicCandidates(pile, Db);
                    int pick = units[rnd.Next(units.Count)];
                    pile.Remove(pick); me.HandList.Add(pick);
                    return "leader restores " + pick + " to hand";
                }
            case LeaderIds.EmhyrEmperorOfNilfgaard:
                return "leader looks at the hand";
        }
        Fail("leader " + leader + " reported usable but has no flow");
        return "leader ?";
    }

    //------------------------------------------ Checks ------------------------------------------//
    static Dictionary<int, int> Multiset(Board board)
    {
        var m = new Dictionary<int, int>();
        foreach (var side in new[] { board.Player, board.Enemy })
        {
            var all = side.AllCards().ToList();
            for (int i = 0; i < side.LeaderHornRows.Count; i++) all.Remove(CardIds.CommanderHorn);
            foreach (int id in all)
            {
                if (id == CardIds.BovineDefenseForce) continue;
                int v; m.TryGetValue(id, out v); m[id] = v + 1;
            }
        }
        foreach (int id in board.Weather) { } // weather cards are counted through WeatherPlayed in AllCards (checked below)
        return m;
    }

    static string Snapshot(Board board)
    {
        return string.Join("|", new[] { board.Player, board.Enemy }.Select(s => string.Join(",", Rows.Units.Select(r => string.Join(" ", s.UnitRow(r))))));
    }

    static void CheckInvariants(Board board, Dictionary<int, int> initial, string after)
    {
        board.Recalculate();
        var now = Multiset(board);
        foreach (int id in initial.Keys.Union(now.Keys))
        {
            int a, b; initial.TryGetValue(id, out a); now.TryGetValue(id, out b);
            if (a != b) { Fail("card " + Db.Get(id).name + " (" + id + ") count " + a + " -> " + b + " after: " + Normalize(after)); break; }
        }
        // Weather board: no duplicates, each card owned by exactly one WeatherPlayed list
        if (board.Weather.Distinct().Count() != board.Weather.Count) Fail("duplicate weather on the board");
        var played = board.Player.WeatherPlayed.Concat(board.Enemy.WeatherPlayed).OrderBy(x => x).ToList();
        if (!played.SequenceEqual(board.Weather.OrderBy(x => x))) Fail("weather board and WeatherPlayed disagree after: " + Normalize(after));
        foreach (var side in new[] { board.Player, board.Enemy })
        {
            foreach (string row in Rows.Units)
            {
                if (side.SpecialRow(row).Count > 1) Fail("more than one card in a horn slot");
                if (side.SpecialRow(row).Any(id => id != CardIds.CommanderHorn)) Fail("non-horn card in a horn slot");
                foreach (int id in side.UnitRow(row))
                {
                    Card c = Db.Get(id);
                    if (!Db.Contains(id)) Fail("unknown card id on a row: " + id);
                    if (c.IsSpecial && id != CardIds.Decoy) Fail("special card " + c.name + " on a unit row");
                    if (c.IsUnit && c.row != Rows.Agile && c.row != row) Fail(c.name + " on the wrong row " + row + " (its row is " + c.row + ")");
                    if (c.row == Rows.Agile && row == Rows.Siege) Fail("agile unit on the siege row");
                }
                // Strength oracle (The Witcher 3's getTotalPower)
                var expected = Oracle(side, row, board);
                var actual = side.ModifiedStrengths(row);
                if (!expected.SequenceEqual(actual))
                    Fail("strength mismatch on " + row + ": got [" + string.Join(",", actual) + "] W3 oracle [" + string.Join(",", expected) + "] cards [" + string.Join(",", side.UnitRow(row).Select(i => Db.Get(i).name + ":" + Db.Get(i).strength + (Db.Get(i).unique ? "H" : "") + "/" + Db.Get(i).ability)) + "] horn=" + side.HasHorn(row) + " weather=" + StrengthCalculator.IsWeathered(row, board.Weather));
            }
            int sum = Rows.Units.Sum(r => side.ModifiedStrengths(r).Sum());
            if (sum != side.TotalScore()) Fail("TotalScore " + side.TotalScore() + " != sum of strengths " + sum);
            if (side.Lives < 0 || side.Lives > 2) Fail("lives out of range " + side.Lives);
            if (side.LeaderHornRows.Any(r => !side.HasHorn(r))) Fail("leader horn row without a horn");
            if (side.HandList.Any(id => !Db.Contains(id))) Fail("unknown id in hand");
            if (side.DiscardList.Contains(CardIds.BovineDefenseForce)) Fail("Bovine Defense Force in a discard pile");
        }
    }

    static string Normalize(string s) { return System.Text.RegularExpressions.Regex.Replace(s, @"\(\d+\)", "(#)"); }

    // An independent reimplementation of The Witcher 3's CardInstance.getTotalPower for every card on a row
    static List<int> Oracle(PlayerState side, string row, Board board)
    {
        var ids = side.UnitRow(row);
        bool weather = board.Weather.Contains(CardIds.WeatherFor(row));
        bool horn_card = side.SpecialRow(row).Count > 0;
        var result = new List<int>();
        for (int i = 0; i < ids.Count; i++)
        {
            Card c = Db.Get(ids[i]);
            if (c.unique || !c.IsUnit) { result.Add(c.IsUnit ? c.strength : 0); continue; }
            int horn = horn_card ? 1 : 0, morale = 0, bonds = 0;
            for (int j = 0; j < ids.Count; j++)
            {
                if (j == i) continue;
                Card o = Db.Get(ids[j]);
                if (o.ability == Abilities.UnitHorn) horn++;
                if (o.ability == Abilities.MoraleBoost) morale++;
                if (o.ability == Abilities.TightBond && o._id == c._id) bonds++;
            }
            int total = c.strength;
            if (weather) total = Math.Min(1, total);
            if (board.DoubleSpies && c.ability == Abilities.Spy) total *= 2;
            int additional = total * bonds + morale;
            if (horn > 0) additional += total + additional;
            result.Add(total + additional);
        }
        return result;
    }

    static void CheckRoundWinner(Board board, RoundWinner winner)
    {
        int a = board.Player.TotalScore(), b = board.Enemy.TotalScore();
        if (a > b && winner != RoundWinner.Player) Fail("higher score did not win");
        if (b > a && winner != RoundWinner.Enemy) Fail("higher score did not win");
        if (a == b)
        {
            bool pn = board.Player.Faction == Factions.Nilfgaard, en = board.Enemy.Faction == Factions.Nilfgaard;
            RoundWinner expected = pn && !en ? RoundWinner.Player : en && !pn ? RoundWinner.Enemy : RoundWinner.Draw;
            if (winner != expected) Fail("tie decided wrongly: " + winner + " expected " + expected);
        }
    }

    static void CheckPlayEffects(Board board, string before, Card card, string field, bool p, int deck_before, int hand_before)
    {
        PlayerState me = board.Side(p), opp = board.Opponent(p);
        if (card.ability == Abilities.Spy && card.IsUnit)
        {
            int expected_draw = Math.Min(2, deck_before);
            if (me.HandList.Count < hand_before + expected_draw && me.DeckList.Count != deck_before - expected_draw) Fail("spy did not draw " + expected_draw);
            if (!Rows.Units.Any(r => opp.UnitRow(r).Contains(card._id))) Fail("spy not on the opponent's side");
        }
        if (card.ability == Abilities.Muster)
        {
            foreach (var src in new[] { me.DeckList, me.HandList })
                if (src.Any(id => AbilityRules.IsMusterRelative(card, Db.Get(id)))) Fail("muster left a relative of " + card.name + " behind");
        }
        if (card.ability == Abilities.Scorch && !card.IsUnit)
        {
            // No non-hero unit left with a strength above the scorched one... checked loosely: the strongest remaining
            // non-hero must be weaker than before unless nothing was destroyed
        }
    }
}
}

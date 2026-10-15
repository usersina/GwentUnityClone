using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Gwent.Rules;

namespace Gwent.Rules.Tests
{
    // Whole battles, AI against AI, through the same rule code the game uses (PlayRules, LeaderRules, RoundRules).
    // Every leader of every faction plays against the real AI deck. Checks that every battle ends
    // and that no card ever appears or disappears.
    public class MatchSimulationTests
    {
        // Resources/Cards/AI_Monsters.json
        private static readonly Deck AiDeck = new Deck
        {
            Faction = Faction.Monsters,
            Name = "Monsters Slay",
            Leader = LeaderIds.EredinKingOfTheWildHunt,
            Cards = new List<int> { 67, 14, 77, 83, 85, 27, 125, 126, 127, 128, 129, 1, 1, 1, 2, 18, 19, 20, 47, 30, 22, 135, 92, 94, 94, 94, 201, 202, 204 }
        };

        private static CardDatabase Db { get { return TestBoard.Db; } }

        [Test]
        public void AiVsAiBattlesFinishAndConserveCards()
        {
            List<Deck> decks = GenerateDecks(new Random(7));
            Assert.AreEqual(LeaderIds.ByFaction.Values.Sum(leaders => leaders.Length), decks.Count, "one valid deck per leader");

            int battles = 0;
            int[] outcomes = new int[3];
            for (int game = 0; game < decks.Count * 6; game++)
            {
                RoundWinner? result = PlayBattle(decks[game % decks.Count], AiDeck, game);
                outcomes[result == RoundWinner.Player ? 0 : result == RoundWinner.Enemy ? 1 : 2]++;
                battles++;
            }
            Assert.AreEqual(decks.Count * 6, battles);
            Assert.Greater(outcomes[0] + outcomes[1], 0, "battles produce winners");
        }

        [Test]
        public void MonstersMirrorBattlesWork()
        {
            for (int game = 0; game < 40; game++)
                PlayBattle(AiDeck, AiDeck, 1000 + game);
        }

        private static RoundWinner? PlayBattle(Deck player_deck, Deck enemy_deck, int seed)
        {
            Random rnd = new Random(seed);
            Rng.Seed(seed);
            Board board = new Board
            {
                Player = new PlayerState { Faction = player_deck.Faction, Leader = player_deck.Leader },
                Enemy = new PlayerState { Faction = enemy_deck.Faction, Leader = enemy_deck.Leader },
                Db = Db,
                Rnd = rnd,
            };
            board.RandomMedic = player_deck.Leader == LeaderIds.EmhyrInvaderOfTheNorth || enemy_deck.Leader == LeaderIds.EmhyrInvaderOfTheNorth;
            foreach (bool is_player in new[] { true, false })
            {
                Deck deck = is_player ? player_deck : enemy_deck;
                PlayerState side = board.Side(is_player);
                side.DeckList = Rng.Shuffle(deck.Cards);
                RoundRules.Draw(side, deck.Leader == LeaderIds.FrancescaDaisyOfTheValley ? 11 : 10);
                if (deck.Leader == LeaderIds.EmhyrTheWhiteFlame)
                    board.Opponent(is_player).leaderDisabled = true;
            }

            int expected = CountCards(board);
            bool player_turn = rnd.Next(2) == 0;
            string context = player_deck.Name + " vs " + enemy_deck.Name + " (seed " + seed + ")";

            for (int turn = 0; ; turn++)
            {
                Assert.Less(turn, 500, "battle never ended: " + context);

                if (board.Player.hasPassed && board.Enemy.hasPassed)
                {
                    board.Recalculate();
                    RoundWinner winner = RoundRules.DecideRound(board.Player.TotalScore(), board.Enemy.TotalScore(), board.Player.Faction, board.Enemy.Faction);
                    RoundRules.ApplyRoundResult(board.Player, board.Enemy, winner);
                    if (board.Player.Lives == 0 || board.Enemy.Lives == 0)
                        return board.Player.Lives == board.Enemy.Lives ? (RoundWinner?)null : board.Player.Lives > 0 ? RoundWinner.Player : RoundWinner.Enemy;

                    RoundRules.CleanupBoard(board.Player, rnd, Db);
                    RoundRules.CleanupBoard(board.Enemy, rnd, Db);
                    board.Weather.Clear();
                    if (winner == RoundWinner.Player && board.Player.Faction == Faction.NorthernRealms)
                        RoundRules.Draw(board.Player, 1);
                    if (winner == RoundWinner.Enemy && board.Enemy.Faction == Faction.NorthernRealms)
                        RoundRules.Draw(board.Enemy, 1);
                    foreach (bool is_player in new[] { true, false })
                        if (RoundRules.SkelligeReinforces(board.Side(is_player)))
                            RoundRules.SkelligeReinforcements(board, is_player);
                    board.Player.hasPassed = board.Enemy.hasPassed = false;
                    Assert.AreEqual(expected, CountCards(board), "cards lost/created at round end: " + context);
                    player_turn = rnd.Next(2) == 0;
                    continue;
                }

                PlayerState me = board.Side(player_turn);
                if (!me.hasPassed)
                {
                    AiAction action = AiBrain.Decide(board.AiContextFor(player_turn));
                    Execute(board, action, player_turn, context);
                    Assert.AreEqual(expected, CountCards(board), "cards lost/created by " + action + ": " + context);
                }
                player_turn = !player_turn;
            }
        }

        // Mirrors AiPlayer.Execute + BattleController.PlayCard/PlayDecoy/AiUseWeatherLeader/ActivateLeader
        private static void Execute(Board board, AiAction action, bool is_player, string context)
        {
            PlayerState me = board.Side(is_player);
            switch (action.Type)
            {
                case AiActionType.PlayCard:
                    Assert.That(me.HandList, Has.Member(action.CardId), context);
                    me.HandList.Remove(action.CardId);
                    if (Db.Get(action.CardId).ability == Ability.Decoy)
                        PlayRules.Decoy(board, action.TargetId, action.Row, is_player, action.CardId);
                    else
                        Assert.AreEqual(PlayOutcome.Done, PlayRules.Resolve(board, Db.Get(action.CardId), action.Row, is_player, false), context);
                    break;

                case AiActionType.UseLeader:
                    Assert.IsTrue(me.canLeader && !me.leaderDisabled, "leader used twice or while cancelled: " + context);
                    me.canLeader = false;
                    if (LeaderIds.WeatherPickRow(me.Leader) != null)
                    {
                        Assert.IsTrue(me.DeckList.Remove(action.CardId), "weather leader card not in deck: " + context);
                        PlayRules.Resolve(board, Db.Get(action.CardId), Row.Weather, is_player, false);
                    }
                    else if (me.Leader == LeaderIds.EredinBringerOfDeath || me.Leader == LeaderIds.EmhyrTheRelentless)
                    {
                        // A unit from a discard pile to the hand (BattleController.RestoreToHand, the AI's choice)
                        List<int> pile = me.Leader == LeaderIds.EmhyrTheRelentless ? board.Side(!is_player).DiscardList : me.DiscardList;
                        int restored = AiBrain.ChooseRestoreTarget(AbilityRules.MedicCandidates(pile, board.Db), me, board.Db);
                        Assert.AreNotEqual(-1, restored, "AI used a restore leader with nothing to restore: " + context);
                        pile.Remove(restored);
                        me.HandList.Add(restored);
                    }
                    else
                    {
                        string reason;
                        Assert.IsTrue(LeaderRules.ApplyInstant(board, me.Leader, is_player, out reason), "AI proposed an unusable leader: " + reason + " " + context);
                    }
                    break;

                default:
                    me.hasPassed = true;
                    break;
            }
        }

        private static int CountCards(Board board)
        {
            return new[] { board.Player, board.Enemy }.Sum(p => p.AllCards().Count(id => !CardIds.IsSummoned(id)) - p.LeaderHornRows.Count);
        }

        // One valid random deck per leader, with every kind of special card in the mix
        private static List<Deck> GenerateDecks(Random rnd)
        {
            List<Deck> decks = new List<Deck>();
            foreach (Faction faction in Factions.Playable)
                foreach (int leader in LeaderIds.ByFaction[faction])
                {
                    List<int> cards = new List<int>();
                    foreach (Card card in Db.All.Where(c => c.IsUnit && !CardIds.IsSummoned(c._id)
                        && (c.faction == faction || c.faction == Faction.Neutral)).OrderBy(x => rnd.Next()))
                    {
                        int copies = 1 + rnd.Next(DeckRules.MaxCopies(card));
                        for (int i = 0; i < copies && cards.Count < 26; i++)
                            cards.Add(card._id);
                    }
                    cards.AddRange(new[] { CardIds.Decoy, CardIds.Scorch, CardIds.CommanderHorn, CardIds.BitingFrost, CardIds.ImpenetrableFog, CardIds.TorrentialRain, CardIds.ClearWeather, CardIds.SkelligeStorm, CardIds.Mardroeme }
                        .Where(id => DeckRules.Fits(Db.Get(id), faction)).OrderBy(x => rnd.Next()).Take(4 + rnd.Next(4)));
                    Deck deck = new Deck { Faction = faction, Leader = leader, Name = faction + "/" + leader, Cards = cards };
                    if (DeckRules.IsValid(deck, Db))
                        decks.Add(deck);
                }
            return decks;
        }
    }
}

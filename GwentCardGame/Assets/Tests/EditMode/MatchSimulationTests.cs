using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Gwent.Rules;

namespace Gwent.Rules.Tests
{
    // Whole battles, AI against AI, through the match the game plays (Match). Every leader of every faction plays
    // against the real AI deck. Checks that every battle ends and that no card ever appears or disappears.
    public class MatchSimulationTests
    {
        private static readonly Deck AiDeck = MatchTests.Monsters();

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

        // A battle through the match (Match), the AI deciding for both sides; no card may appear or disappear
        private static RoundWinner? PlayBattle(Deck player_deck, Deck enemy_deck, int seed)
        {
            Match match = new Match(new MatchSetup { Player = player_deck, Enemy = enemy_deck, Seed = seed }, Db);
            string context = player_deck.Name + " vs " + enemy_deck.Name + " (seed " + seed + ")";
            int expected = -1;
            MatchTests.PlayOut(match, seed, command =>
            {
                if (expected < 0)
                    expected = MatchTests.CountCards(match.Board);
                Assert.AreEqual(expected, MatchTests.CountCards(match.Board), "cards lost/created by " + command + ": " + context);
            });
            return match.Result == RoundWinner.Draw ? (RoundWinner?)null : match.Result;
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

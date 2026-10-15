using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Gwent.Rules;

namespace Gwent.Rules.Tests
{
    public class RoundRulesTests
    {
        private const int Fiend = 47;           // M close 6
        private const int EarthElemental = 30;  // M siege 6
        private const int Draug = 27;           // M hero close 10

        [Test]
        public void NilfgaardWinsTies_UnlessBothAreNilfgaard()
        {
            Assert.AreEqual(RoundWinner.Player, RoundRules.DecideRound(11, 10, Faction.Monsters, Faction.Monsters));
            Assert.AreEqual(RoundWinner.Player, RoundRules.DecideRound(10, 10, Faction.Nilfgaard, Faction.Monsters));
            Assert.AreEqual(RoundWinner.Enemy, RoundRules.DecideRound(10, 10, Faction.Scoiatael, Faction.Nilfgaard));
            Assert.AreEqual(RoundWinner.Draw, RoundRules.DecideRound(10, 10, Faction.Nilfgaard, Faction.Nilfgaard));
            Assert.AreEqual(RoundWinner.Draw, RoundRules.DecideRound(0, 0, Faction.NorthernRealms, Faction.Monsters));
        }

        // The Witcher 3 passes a side automatically when its hand is empty and its leader could do nothing right now
        [Test]
        public void AnEmptyHandWithALeaderThatWouldDoNothingIsDone()
        {
            PlayerState p = TestBoard.Player(Faction.NorthernRealms, LeaderIds.FoltestLordCommander);
            Board board = new Board { Player = p, Enemy = TestBoard.Player(Faction.Monsters), Db = TestBoard.Db };
            Assert.IsTrue(RoundRules.OutOfPlays(board, true), "no weather to clear: done");
            board.Weather.Add(CardIds.BitingFrost);
            Assert.IsFalse(RoundRules.OutOfPlays(board, true), "weather to clear: the leader is a play");
            p.canLeader = false;
            Assert.IsTrue(RoundRules.OutOfPlays(board, true), "leader used: done");
            p.HandList.Add(Fiend);
            Assert.IsFalse(RoundRules.OutOfPlays(board, true), "a card in hand: not done");
        }

        [Test]
        public void DrawCostsBothALife()
        {
            PlayerState p = TestBoard.Player();
            PlayerState e = TestBoard.Player(Faction.Monsters);
            RoundRules.ApplyRoundResult(p, e, RoundWinner.Draw);
            Assert.AreEqual(1, p.Lives);
            Assert.AreEqual(1, e.Lives);
            Assert.AreEqual(1, p.RoundsScore.Count);
        }

        [Test]
        public void MonstersMirrorMatchKeepsEachUnitOnItsOwnRow()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                PlayerState p = TestBoard.Player(Faction.Monsters, LeaderIds.EredinTheTreacherous);
                PlayerState e = TestBoard.Player(Faction.Monsters, LeaderIds.EredinCommanderOfTheRedRiders);
                p.CloseList.Add(Fiend);
                e.SiegeList.Add(EarthElemental);
                Random rnd = new Random(seed);

                RoundRules.CleanupBoard(p, rnd, TestBoard.Db);
                RoundRules.CleanupBoard(e, rnd, TestBoard.Db);

                CollectionAssert.AreEqual(new[] { Fiend }, p.CloseList);
                CollectionAssert.AreEqual(new[] { EarthElemental }, e.SiegeList);
                Assert.IsEmpty(e.CloseList);
                Assert.IsEmpty(p.DiscardList);
                Assert.IsEmpty(e.DiscardList);
            }
        }

        [Test]
        public void CowLeavingTheBoardSummonsTheBovineDefenseForce()
        {
            PlayerState p = TestBoard.Player();
            p.RangeList.Add(CardIds.Cow);
            RoundRules.CleanupBoard(p, new Random(0), TestBoard.Db);
            CollectionAssert.AreEqual(new[] { CardIds.BovineDefenseForce }, p.CloseList);
            CollectionAssert.AreEqual(new[] { CardIds.Cow }, p.DiscardList);
        }

        [Test]
        public void EachCowSummonsItsOwnDefenseForce()
        {
            PlayerState p = TestBoard.Player();
            p.RangeList.Add(CardIds.Cow);
            p.RangeList.Add(CardIds.Cow);
            RoundRules.CleanupBoard(p, new Random(0), TestBoard.Db);
            CollectionAssert.AreEqual(new[] { CardIds.BovineDefenseForce, CardIds.BovineDefenseForce }, p.CloseList);
        }

        [Test]
        public void ACowThatMonstersKeepSummonsNothing()
        {
            PlayerState p = TestBoard.Player(Faction.Monsters, LeaderIds.EredinTheTreacherous);
            p.RangeList.Add(CardIds.Cow);
            RoundRules.CleanupBoard(p, new Random(0), TestBoard.Db);
            CollectionAssert.AreEqual(new[] { CardIds.Cow }, p.RangeList);
            Assert.IsEmpty(p.CloseList);
            Assert.IsEmpty(p.DiscardList);
        }

        [Test]
        public void TheDefenseForceVanishesInsteadOfGoingToTheDiscardPile()
        {
            PlayerState p = TestBoard.Player();
            p.CloseList.Add(CardIds.BovineDefenseForce);
            p.CloseList.Add(Fiend);
            RoundRules.CleanupBoard(p, new Random(0), TestBoard.Db);
            Assert.IsEmpty(p.CloseList);
            CollectionAssert.AreEqual(new[] { Fiend }, p.DiscardList);
        }

        [Test]
        public void MonstersNeverKeepAHeroOrADecoy()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                PlayerState p = TestBoard.Player(Faction.Monsters, LeaderIds.EredinTheTreacherous);
                p.CloseList.AddRange(new[] { Draug, CardIds.Decoy, Fiend });
                RoundRules.CleanupBoard(p, new Random(seed), TestBoard.Db);
                CollectionAssert.AreEqual(new[] { Fiend }, p.CloseList);
            }
        }

        [Test]
        public void MonstersCanKeepTheDefenseForce()
        {
            PlayerState p = TestBoard.Player(Faction.Monsters, LeaderIds.EredinTheTreacherous);
            p.CloseList.Add(CardIds.BovineDefenseForce);
            RoundRules.CleanupBoard(p, new Random(0), TestBoard.Db);
            CollectionAssert.AreEqual(new[] { CardIds.BovineDefenseForce }, p.CloseList);
            Assert.IsEmpty(p.DiscardList);
        }

        [Test]
        public void DrawingFromAShortDeckDrawsWhatIsLeft()
        {
            PlayerState p = TestBoard.Player();
            p.DeckList.Add(Fiend);
            Assert.AreEqual(1, RoundRules.Draw(p, 2));
            CollectionAssert.AreEqual(new[] { Fiend }, p.HandList);
            Assert.IsEmpty(p.DeckList);
            Assert.AreEqual(0, RoundRules.Draw(p, 2));
        }

        [Test]
        public void ARedrawTakesTheTopCardAndSendsTheCardToTheBottom()
        {
            PlayerState p = TestBoard.Player();
            p.HandList.AddRange(new[] { 1, 2, 3 });
            p.DeckList.AddRange(new[] { 7, 8 });
            Assert.AreEqual(7, RoundRules.Redraw(p, 2));
            CollectionAssert.AreEqual(new[] { 1, 7, 3 }, p.HandList, "the new card takes the old one's place");
            CollectionAssert.AreEqual(new[] { 8, 2 }, p.DeckList, "the old card goes to the bottom");

            p.DeckList.Clear();
            Assert.AreEqual(-1, RoundRules.Redraw(p, 1), "an empty deck keeps the hand");
            CollectionAssert.AreEqual(new[] { 1, 7, 3 }, p.HandList);
        }

        [Test]
        public void EachRoundsWinnerIsRecorded()
        {
            PlayerState p = TestBoard.Player(Faction.Nilfgaard);
            PlayerState e = TestBoard.Player(Faction.Monsters);
            RoundRules.ApplyRoundResult(p, e, RoundWinner.Player);
            RoundRules.ApplyRoundResult(p, e, RoundWinner.Draw);
            CollectionAssert.AreEqual(new[] { true, false }, p.RoundsWon);
            CollectionAssert.AreEqual(new[] { false, false }, e.RoundsWon);
        }
    }

    public class DeckRulesTests
    {
        // Resources/Decks/player_default.json
        private static Deck DefaultDeck()
        {
            return new Deck
            {
                Faction = Faction.NorthernRealms,
                Name = "NR Default",
                Leader = LeaderIds.FoltestTheSiegemaster,
                Cards = new List<int> { 203, 203, 202, 202, 205, 200, 14, 143, 135, 92, 132, 45, 96, 11, 11, 123, 123, 4, 4, 99, 17, 17, 28, 115, 7, 7, 7, 119 }
            };
        }

        [Test]
        public void NamesThatOnlyDifferByCaseOrSpacingCollide()
        {
            Assert.IsTrue(DeckRules.NamesCollide("My Deck", "my deck"));
            Assert.IsTrue(DeckRules.NamesCollide("My  Deck ", "MY DECK"));
            Assert.IsTrue(DeckRules.NamesCollide("my deck!", "My_Deck"));
            Assert.IsFalse(DeckRules.NamesCollide("Deck A", "Deck B"));
        }

        [Test]
        public void FileNamesAreSanitized()
        {
            Assert.AreEqual("hello_world", DeckRules.ToFileName("  Hello   World! "));
            Assert.AreEqual("etcpasswd", DeckRules.ToFileName("../../etc/passwd"));
            Assert.AreEqual("a_b", DeckRules.ToFileName("a/ b"));
            Assert.IsNull(DeckRules.ToFileName("   "));
            Assert.IsNull(DeckRules.ToFileName("???"));
            Assert.IsNull(DeckRules.ToFileName(DeckRules.EmptyDeckName));
        }

        [Test]
        public void DisplayNamesAreTrimmedAndCapped()
        {
            Assert.AreEqual("My Deck", DeckRules.NormalizeDisplayName("  My \t Deck  "));
            Assert.AreEqual(DeckRules.MaxNameLength, DeckRules.NormalizeDisplayName(new string('x', 80)).Length);
        }

        [Test]
        public void DefaultDeckIsValid()
        {
            CollectionAssert.IsEmpty(DeckRules.Validate(DefaultDeck(), TestBoard.Db));
        }

        [Test]
        public void InvalidDecksAreRejected()
        {
            Deck small = DefaultDeck();
            small.Cards.RemoveRange(0, 20);
            Assert.IsFalse(DeckRules.IsValid(small, TestBoard.Db), "too few units");

            Deck wrong_leader = DefaultDeck();
            wrong_leader.Leader = LeaderIds.EredinTheTreacherous;
            Assert.IsFalse(DeckRules.IsValid(wrong_leader, TestBoard.Db), "leader from another faction");

            Deck many_specials = DefaultDeck();
            many_specials.Cards.AddRange(new[] { 200, 200, 201, 201, 204 });
            Assert.IsFalse(DeckRules.IsValid(many_specials, TestBoard.Db), "11 specials");

            Deck two_heroes = DefaultDeck();
            two_heroes.Cards.Add(14);
            Assert.IsFalse(DeckRules.IsValid(two_heroes, TestBoard.Db), "same hero twice");

            Deck unknown = DefaultDeck();
            unknown.Cards.Add(9999);
            Assert.IsFalse(DeckRules.IsValid(unknown, TestBoard.Db), "unknown card");
        }

        [Test]
        public void DecksHoldOnlyTheCopiesTheWitcher3Has()
        {
            Card dandelion = TestBoard.Db.Get(CardIds.Dandelion);
            Card infantry = TestBoard.Db.Get(98);  // Poor Fucking Infantry: 4 in The Witcher 3
            Card defender = TestBoard.Db.Get(87);  // Mahakaman Defender: 5
            Assert.AreEqual(1, DeckRules.MaxCopies(dandelion));
            Assert.AreEqual(4, DeckRules.MaxCopies(infantry));
            Assert.AreEqual(5, DeckRules.MaxCopies(defender));
            Assert.AreEqual(1, DeckRules.MaxCopies(TestBoard.Db.Get(143)), "a hero");
            Assert.AreEqual(0, DeckRules.MaxCopies(TestBoard.Db.Get(CardIds.BovineDefenseForce)), "summoned by the Cow only");

            Deck four_infantry = DefaultDeck();
            four_infantry.Cards.AddRange(new[] { 98, 98, 98, 98 });
            CollectionAssert.IsEmpty(DeckRules.Validate(four_infantry, TestBoard.Db));
            four_infantry.Cards.Add(98);
            Assert.That(DeckRules.Validate(four_infantry, TestBoard.Db), Has.Member("There are only 4 copies of Poor Fucking Infantry"));

            Deck two_dandelions = DefaultDeck();
            two_dandelions.Cards.AddRange(new[] { CardIds.Dandelion, CardIds.Dandelion });
            Assert.That(DeckRules.Validate(two_dandelions, TestBoard.Db), Has.Member("There is only one Dandelion"));

            Deck cow_force = DefaultDeck();
            cow_force.Cards.Add(CardIds.BovineDefenseForce);
            Assert.IsFalse(DeckRules.IsValid(cow_force, TestBoard.Db), "the Bovine Defense Force");
        }

        [Test]
        public void SanitizeKeepsOnlyTheCopiesThatExist()
        {
            // A deck saved before the copy limits still loads, without the extra copies
            Deck deck = DefaultDeck();
            deck.Cards.AddRange(new[] { CardIds.Dandelion, CardIds.Dandelion, CardIds.Dandelion, CardIds.BovineDefenseForce, 135 });
            DeckRules.Sanitize(deck, TestBoard.Db);
            Assert.AreEqual(1, deck.Cards.Count(id => id == CardIds.Dandelion));
            Assert.AreEqual(1, deck.Cards.Count(id => id == 135), "Villentretenmerth");
            Assert.That(deck.Cards, Has.No.Member(CardIds.BovineDefenseForce));
            CollectionAssert.IsEmpty(DeckRules.Validate(deck, TestBoard.Db));
        }

        [Test]
        public void SanitizeDropsUnknownCardsAndFixesTheLeader()
        {
            Deck deck = DefaultDeck();
            deck.Cards.Add(9999);
            deck.Leader = LeaderIds.EredinTheTreacherous;
            DeckRules.Sanitize(deck, TestBoard.Db);
            Assert.That(deck.Cards, Has.No.Member(9999));
            Assert.AreEqual(LeaderIds.FoltestKingOfTemeria, deck.Leader);
        }

        [Test]
        public void HandsAndRowsAreInTheWitcher3Order()
        {
            // Special cards first (by id), then units by printed strength, then by id
            int[] cards = { 47, CardIds.Scorch, 1, 30, CardIds.Decoy, 7, 27 };
            CollectionAssert.AreEqual(new[] { CardIds.Decoy, CardIds.Scorch, 1, 7, 30, 47, 27 }, CardOrder.Sorted(cards, TestBoard.Db));
        }

        [Test]
        public void DisplayNamesDropOnlyTheCopyNumber()
        {
            Assert.AreEqual("Nekker", Card.DisplayNameOf("Nekker 3"));
            Assert.AreEqual("Foltest the Steel-Forged", Card.DisplayNameOf("Foltest the Steel-Forged"));
            Assert.AreEqual("Geralt of Rivia", Card.DisplayNameOf("Geralt of Rivia"));
            Assert.AreEqual("Catapult", Card.DisplayNameOf("Catapult 1"));
        }
    }
}

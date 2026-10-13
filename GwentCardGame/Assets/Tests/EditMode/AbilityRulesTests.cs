using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Gwent.Rules;

namespace Gwent.Rules.Tests
{
    public class AbilityRulesTests
    {
        private const int Fiend = 47;               // M close 6
        private const int Draug = 27;               // M hero close 10
        private const int EarthElemental = 30;      // M siege 6
        private const int DunBannerMedic = 28;      // NR siege medic 5
        private const int BlueStripes = 7;
        private const int Yennefer = 143;           // hero medic
        private const int BarclayEls = 5;           // SC agile 6
        private const int Ciaran = 13;              // SC agile 3
        private const int HavekarHealer = 72;       // SC range medic
        private const int HavekarSmuggler = 73;     // SC close muster
        private const int Bruxa = 125, Ekimmara = 126, Fleder = 127, Garkain = 128;
        private const int Nekker = 94;
        private const int Arachas = 1, ArachasBehemoth = 2;

        private CardDatabase Db { get { return TestBoard.Db; } }

        //----------------------------------------Medic (B5)----------------------------------------//
        [Test]
        public void RandomMedicRemovesTheRevivedCardOnly()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                PlayerState p = TestBoard.Player();
                p.DiscardList.AddRange(new[] { CardIds.Scorch, Yennefer, DunBannerMedic, BlueStripes });

                int revived = AbilityRules.TakeRandomMedicTarget(p, new Random(seed), Db);

                Assert.That(revived, Is.EqualTo(DunBannerMedic).Or.EqualTo(BlueStripes));
                Assert.That(p.DiscardList, Has.No.Member(revived));
                Assert.That(p.DiscardList, Has.Member(CardIds.Scorch));
                Assert.That(p.DiscardList, Has.Member(Yennefer));
                Assert.AreEqual(3, p.DiscardList.Count);
            }
        }

        [Test]
        public void RandomMedicWithNothingToRestoreReturnsMinusOne()
        {
            PlayerState p = TestBoard.Player();
            p.DiscardList.AddRange(new[] { CardIds.Scorch, Yennefer });
            Assert.AreEqual(-1, AbilityRules.TakeRandomMedicTarget(p, new Random(1), Db));
            Assert.AreEqual(2, p.DiscardList.Count);
        }

        //----------------------------------------Agile (B6)----------------------------------------//
        [Test]
        public void OptimizeAgileMovesUnitsOutOfFogWithoutLosingAny()
        {
            PlayerState p = TestBoard.Player(Factions.Scoiatael, LeaderIds.FrancescaHopeOfTheAenSeidhe);
            p.RangeList.AddRange(new[] { BarclayEls, Ciaran });
            List<int> weather = new List<int> { CardIds.ImpenetrableFog };

            AbilityRules.OptimizeAgile(p, weather, false, Db);

            CollectionAssert.AreEquivalent(new[] { BarclayEls, Ciaran }, p.CloseList);
            Assert.IsEmpty(p.RangeList);
        }

        [Test]
        public void OptimizeAgileHandlesUnitsInBothRows()
        {
            PlayerState p = TestBoard.Player(Factions.Scoiatael, LeaderIds.FrancescaHopeOfTheAenSeidhe);
            p.CloseList.Add(BarclayEls);
            p.RangeList.Add(Ciaran);
            p.SpRangeList.Add(CardIds.CommanderHorn);

            AbilityRules.OptimizeAgile(p, new List<int>(), false, Db);

            Assert.IsEmpty(p.CloseList);
            CollectionAssert.AreEquivalent(new[] { BarclayEls, Ciaran }, p.RangeList);
        }

        [Test]
        public void OptimizeAgileKeepsUnitsThatAreAlreadyBest()
        {
            PlayerState p = TestBoard.Player(Factions.Scoiatael, LeaderIds.FrancescaHopeOfTheAenSeidhe);
            p.CloseList.Add(BarclayEls);
            Assert.AreEqual(0, AbilityRules.OptimizeAgile(p, new List<int>(), false, Db));
            CollectionAssert.AreEqual(new[] { BarclayEls }, p.CloseList);
        }

        //----------------------------------------Muster (B10)----------------------------------------//
        [Test]
        public void HavekarSmugglerDoesNotMusterTheHealer()
        {
            PlayerState p = TestBoard.Player(Factions.Scoiatael, LeaderIds.FrancescaDaisyOfTheValley);
            p.DeckList.AddRange(new[] { HavekarHealer, HavekarSmuggler });

            AbilityRules.Muster(p, Db.Get(HavekarSmuggler), Rows.Close, Db);

            CollectionAssert.AreEqual(new[] { HavekarSmuggler }, p.CloseList);
            CollectionAssert.AreEqual(new[] { HavekarHealer }, p.DeckList);
            Assert.IsEmpty(p.RangeList);
        }

        [Test]
        public void VampiresMusterEachOtherFromDeckAndHand()
        {
            PlayerState p = TestBoard.Player(Factions.Monsters, LeaderIds.EredinTheTreacherous);
            p.DeckList.AddRange(new[] { Ekimmara, Fleder, Nekker });
            p.HandList.Add(Garkain);

            AbilityRules.Muster(p, Db.Get(Bruxa), Rows.Close, Db);

            CollectionAssert.AreEquivalent(new[] { Ekimmara, Fleder, Garkain }, p.CloseList);
            CollectionAssert.AreEqual(new[] { Nekker }, p.DeckList);
            Assert.IsEmpty(p.HandList);
        }

        [Test]
        public void ArachasMustersTheBehemothOnItsOwnRow()
        {
            PlayerState p = TestBoard.Player(Factions.Monsters, LeaderIds.EredinTheTreacherous);
            p.DeckList.AddRange(new[] { Arachas, ArachasBehemoth });

            AbilityRules.Muster(p, Db.Get(Arachas), Rows.Close, Db);

            CollectionAssert.AreEqual(new[] { Arachas }, p.CloseList);
            CollectionAssert.AreEqual(new[] { ArachasBehemoth }, p.SiegeList);
        }

        //----------------------------------------Scorch----------------------------------------//
        [Test]
        public void ScorchDestroysTheStrongestNonHeroesOnBothSides()
        {
            PlayerState p = TestBoard.Player(Factions.Monsters);
            PlayerState e = TestBoard.Player(Factions.Monsters);
            p.CloseList.Add(Fiend);
            e.CloseList.Add(Draug);
            e.SiegeList.Add(EarthElemental);
            TestBoard.Recalc(null, p, e);

            Assert.AreEqual(2, AbilityRules.Scorch(p, e, Db));

            Assert.IsEmpty(p.CloseList);
            CollectionAssert.AreEqual(new[] { Fiend }, p.DiscardList);
            CollectionAssert.AreEqual(new[] { Draug }, e.CloseList);
            CollectionAssert.AreEqual(new[] { EarthElemental }, e.DiscardList);
        }

        [Test]
        public void RowScorchNeedsTenStrength()
        {
            PlayerState e = TestBoard.Player(Factions.Monsters);
            e.CloseList.Add(Fiend);
            TestBoard.Recalc(null, e);
            Assert.AreEqual(0, AbilityRules.ScorchRow(e, Rows.Close, Db));

            e.CloseList.Add(Fiend);
            TestBoard.Recalc(null, e);
            Assert.AreEqual(2, AbilityRules.ScorchRow(e, Rows.Close, Db));
            Assert.IsEmpty(e.CloseList);
        }

        //----------------------------------------Weather (B17)----------------------------------------//
        [Test]
        public void WeatherCardsReturnToTheirOwnersDiscard()
        {
            PlayerState p = TestBoard.Player();
            PlayerState e = TestBoard.Player(Factions.Monsters);
            List<int> board = new List<int>();

            AbilityRules.PlayWeather(p, e, board, Db.Get(CardIds.ImpenetrableFog));
            AbilityRules.PlayWeather(e, p, board, Db.Get(CardIds.ImpenetrableFog));  // duplicate
            AbilityRules.PlayWeather(e, p, board, Db.Get(CardIds.BitingFrost));
            CollectionAssert.AreEquivalent(new[] { CardIds.ImpenetrableFog, CardIds.BitingFrost }, board);

            AbilityRules.PlayWeather(p, e, board, Db.Get(CardIds.ClearWeather));

            Assert.IsEmpty(board);
            CollectionAssert.AreEquivalent(new[] { CardIds.ImpenetrableFog, CardIds.ClearWeather }, p.DiscardList);
            CollectionAssert.AreEquivalent(new[] { CardIds.ImpenetrableFog, CardIds.BitingFrost }, e.DiscardList);
        }

        //----------------------------------------Leader horn (B19)----------------------------------------//
        [Test]
        public void LeaderHornLeavesNoCardBehind()
        {
            PlayerState p = TestBoard.Player(Factions.NorthernRealms, LeaderIds.FoltestTheSiegemaster);
            Assert.IsTrue(AbilityRules.PlaceLeaderHorn(p, Rows.Siege));
            Assert.IsFalse(AbilityRules.PlaceLeaderHorn(p, Rows.Siege), "horns do not stack");

            RoundRules.CleanupBoard(p, new Random(0), TestBoard.Db);

            Assert.IsEmpty(p.SpSiegeList);
            Assert.That(p.DiscardList, Has.No.Member(CardIds.CommanderHorn));
        }
    }
}

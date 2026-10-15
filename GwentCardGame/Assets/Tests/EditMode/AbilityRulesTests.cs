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
        private const int Darkness = 66;           // Gaunter O'Dimm: Darkness, range 4 muster
        private const int Olgierd = 95;             // N agile 6 morale boost
        private const int DolBlathannaArcher = 25, VrihheddRecruit = 137, Toruviel = 122; // SC range 4, 4, 2
        private const int MahakamanDefender = 87;   // SC close 5

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
            PlayerState p = TestBoard.Player(Faction.Scoiatael, LeaderIds.FrancescaHopeOfTheAenSeidhe);
            p.RangeList.AddRange(new[] { BarclayEls, Ciaran });
            List<int> weather = new List<int> { CardIds.ImpenetrableFog };

            AbilityRules.OptimizeAgile(p, weather, false, Db);

            CollectionAssert.AreEquivalent(new[] { BarclayEls, Ciaran }, p.CloseList);
            Assert.IsEmpty(p.RangeList);
        }

        [Test]
        public void OptimizeAgileHandlesUnitsInBothRows()
        {
            PlayerState p = TestBoard.Player(Faction.Scoiatael, LeaderIds.FrancescaHopeOfTheAenSeidhe);
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
            PlayerState p = TestBoard.Player(Faction.Scoiatael, LeaderIds.FrancescaHopeOfTheAenSeidhe);
            p.CloseList.Add(BarclayEls);
            Assert.AreEqual(0, AbilityRules.OptimizeAgile(p, new List<int>(), false, Db));
            CollectionAssert.AreEqual(new[] { BarclayEls }, p.CloseList);
        }

        // The Witcher 3 moves a unit when its own strength rises, not the side's total: Olgierd (agile, morale boost)
        // gains nothing by joining three range units (they gain 3), so he stays, and the leader can't be used
        [Test]
        public void OptimizeAgileJudgesEachUnitByItsOwnStrength()
        {
            PlayerState p = TestBoard.Player(Faction.Scoiatael, LeaderIds.FrancescaHopeOfTheAenSeidhe);
            p.CloseList.Add(Olgierd);
            p.RangeList.AddRange(new[] { DolBlathannaArcher, VrihheddRecruit, Toruviel });
            Board board = NewBoard(p, TestBoard.Player());
            Assert.IsNotNull(LeaderRules.Unavailable(board, LeaderIds.FrancescaHopeOfTheAenSeidhe, true), "no unit of its own would gain");
            Assert.AreEqual(0, AbilityRules.OptimizeAgile(p, new List<int>(), false, Db));
            CollectionAssert.AreEqual(new[] { Olgierd }, p.CloseList);
        }

        // ... and leaves a frozen close row for his own sake (1 -> 6) even though the six units he boosted lose 6
        [Test]
        public void OptimizeAgileMovesAUnitThatGainsEvenIfTheSideLoses()
        {
            PlayerState p = TestBoard.Player(Faction.Scoiatael, LeaderIds.FrancescaHopeOfTheAenSeidhe);
            p.CloseList.Add(Olgierd);
            for (int i = 0; i < 6; i++)
                p.CloseList.Add(MahakamanDefender);
            List<int> weather = new List<int> { CardIds.BitingFrost };
            Assert.AreEqual(1, AbilityRules.OptimizeAgile(p, weather, false, Db));
            CollectionAssert.AreEqual(new[] { Olgierd }, p.RangeList);
        }

        //----------------------------------------Muster (B10)----------------------------------------//
        [Test]
        public void HavekarSmugglerDoesNotMusterTheHealer()
        {
            PlayerState p = TestBoard.Player(Faction.Scoiatael, LeaderIds.FrancescaDaisyOfTheValley);
            p.DeckList.AddRange(new[] { HavekarHealer, HavekarSmuggler });

            AbilityRules.Muster(p, Db.Get(HavekarSmuggler), Row.Close, Db);

            CollectionAssert.AreEqual(new[] { HavekarSmuggler }, p.CloseList);
            CollectionAssert.AreEqual(new[] { HavekarHealer }, p.DeckList);
            Assert.IsEmpty(p.RangeList);
        }

        [Test]
        public void VampiresMusterEachOtherFromDeckAndHand()
        {
            PlayerState p = TestBoard.Player(Faction.Monsters, LeaderIds.EredinTheTreacherous);
            p.DeckList.AddRange(new[] { Ekimmara, Fleder, Nekker });
            p.HandList.Add(Garkain);

            AbilityRules.Muster(p, Db.Get(Bruxa), Row.Close, Db);

            CollectionAssert.AreEquivalent(new[] { Ekimmara, Fleder, Garkain }, p.CloseList);
            CollectionAssert.AreEqual(new[] { Nekker }, p.DeckList);
            Assert.IsEmpty(p.HandList);
        }

        [Test]
        public void TheBehemothMustersTheArachasButNotTheOtherWayRound()
        {
            PlayerState p = TestBoard.Player(Faction.Monsters, LeaderIds.EredinTheTreacherous);
            p.DeckList.AddRange(new[] { Arachas, ArachasBehemoth });
            AbilityRules.Muster(p, Db.Get(Arachas), Row.Close, Db);
            CollectionAssert.AreEqual(new[] { Arachas }, p.CloseList);
            Assert.IsEmpty(p.SiegeList);
            CollectionAssert.AreEqual(new[] { ArachasBehemoth }, p.DeckList);

            PlayerState q = TestBoard.Player(Faction.Monsters, LeaderIds.EredinTheTreacherous);
            q.DeckList.AddRange(new[] { Arachas, Arachas });
            AbilityRules.Muster(q, Db.Get(ArachasBehemoth), Row.Siege, Db);
            CollectionAssert.AreEqual(new[] { Arachas, Arachas }, q.CloseList);
        }

        [Test]
        public void GaunterMustersTheDarknessButNotTheOtherWayRound()
        {
            PlayerState p = TestBoard.Player();
            p.DeckList.AddRange(new[] { Darkness, CardIds.GaunterODimm, Darkness });
            AbilityRules.Muster(p, Db.Get(Darkness), Row.Range, Db);
            CollectionAssert.AreEqual(new[] { Darkness, Darkness }, p.RangeList);
            CollectionAssert.AreEqual(new[] { CardIds.GaunterODimm }, p.DeckList);

            PlayerState q = TestBoard.Player();
            q.DeckList.AddRange(new[] { Darkness, Darkness });
            AbilityRules.Muster(q, Db.Get(CardIds.GaunterODimm), Row.Siege, Db);
            CollectionAssert.AreEqual(new[] { Darkness, Darkness }, q.RangeList);
        }

        //----------------------------------------Scorch----------------------------------------//
        [Test]
        public void ScorchDestroysTheStrongestNonHeroesOnBothSides()
        {
            PlayerState p = TestBoard.Player(Faction.Monsters);
            PlayerState e = TestBoard.Player(Faction.Monsters);
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
        public void AScorchedCowSummonsTheDefenseForceForTheNextRound()
        {
            PlayerState p = TestBoard.Player();
            PlayerState e = TestBoard.Player(Faction.Monsters);
            p.RangeList.Add(CardIds.Cow);
            e.CloseList.Add(Draug); // a hero: the Cow is the strongest unit Scorch can hit
            TestBoard.Recalc(null, p, e);

            Assert.AreEqual(1, AbilityRules.Scorch(p, e, Db));
            Assert.IsEmpty(p.RangeList);
            Assert.IsEmpty(p.CloseList);
            CollectionAssert.AreEqual(new[] { CardIds.Cow }, p.DiscardList);

            RoundRules.CleanupBoard(p, new Random(0), Db);
            CollectionAssert.AreEqual(new[] { CardIds.BovineDefenseForce }, p.CloseList);
        }

        [Test]
        public void ScorchNeverTakesADecoy()
        {
            PlayerState p = TestBoard.Player();
            PlayerState e = TestBoard.Player(Faction.Monsters);
            p.CloseList.Add(CardIds.Decoy);
            e.CloseList.Add(Draug);
            TestBoard.Recalc(null, p, e);

            Assert.AreEqual(0, AbilityRules.Scorch(p, e, Db));
            CollectionAssert.AreEqual(new[] { CardIds.Decoy }, p.CloseList);
        }

        [Test]
        public void AScorchedDefenseForceVanishes()
        {
            PlayerState p = TestBoard.Player();
            PlayerState e = TestBoard.Player(Faction.Monsters);
            p.CloseList.Add(CardIds.BovineDefenseForce);
            TestBoard.Recalc(null, p, e);

            Assert.AreEqual(1, AbilityRules.Scorch(p, e, Db));

            Assert.IsEmpty(p.CloseList);
            Assert.IsEmpty(p.DiscardList);
        }

        [Test]
        public void RowScorchNeedsTenStrength()
        {
            PlayerState e = TestBoard.Player(Faction.Monsters);
            e.CloseList.Add(Fiend);
            TestBoard.Recalc(null, e);
            Assert.AreEqual(0, AbilityRules.ScorchRow(e, Row.Close, Db));

            e.CloseList.Add(Fiend);
            TestBoard.Recalc(null, e);
            Assert.AreEqual(2, AbilityRules.ScorchRow(e, Row.Close, Db));
            Assert.IsEmpty(e.CloseList);
        }

        //----------------------------------------Weather (B17)----------------------------------------//
        [Test]
        public void WeatherCardsReturnToTheirOwnersDiscard()
        {
            PlayerState p = TestBoard.Player();
            PlayerState e = TestBoard.Player(Faction.Monsters);
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
            PlayerState p = TestBoard.Player(Faction.NorthernRealms, LeaderIds.FoltestTheSiegemaster);
            Assert.IsTrue(AbilityRules.PlaceLeaderHorn(p, Row.Siege));
            Assert.IsFalse(AbilityRules.PlaceLeaderHorn(p, Row.Siege), "horns do not stack");

            RoundRules.CleanupBoard(p, new Random(0), TestBoard.Db);

            Assert.IsEmpty(p.SpSiegeList);
            Assert.That(p.DiscardList, Has.No.Member(CardIds.CommanderHorn));
        }

        //----------------------------------------Leaders----------------------------------------//
        private Board NewBoard(PlayerState player, PlayerState enemy)
        {
            return new Board { Player = player, Enemy = enemy, Weather = new List<int>(), Db = Db, Rnd = new Random(0) };
        }

        [Test]
        public void ARowScorchLeaderNeedsTenOnTheEnemyRow()
        {
            PlayerState p = TestBoard.Player(Faction.Scoiatael, LeaderIds.FrancescaQueenOfDolBlathanna);
            PlayerState e = TestBoard.Player(Faction.Monsters);
            Board board = NewBoard(p, e);
            e.CloseList.Add(Fiend);
            Assert.IsNotNull(LeaderRules.Unavailable(board, p.Leader, true), "6 on the row");
            e.CloseList.Add(Fiend);
            Assert.IsNull(LeaderRules.Unavailable(board, p.Leader, true), "12 on the row");
        }

        [Test]
        public void LeadersThatWouldDoNothingCannotBeUsed()
        {
            PlayerState p = TestBoard.Player(Faction.NorthernRealms, LeaderIds.FoltestLordCommander);
            PlayerState e = TestBoard.Player(Faction.Monsters);
            Board board = NewBoard(p, e);
            Assert.IsNotNull(LeaderRules.Unavailable(board, LeaderIds.FoltestLordCommander, true), "no weather to clear");
            board.Weather.Add(CardIds.BitingFrost);
            Assert.IsNull(LeaderRules.Unavailable(board, LeaderIds.FoltestLordCommander, true));

            Assert.IsNotNull(LeaderRules.Unavailable(board, LeaderIds.EmhyrEmperorOfNilfgaard, true), "empty opponent hand");
            Assert.IsNotNull(LeaderRules.Unavailable(board, LeaderIds.EredinDestroyerOfWorlds, true), "nothing to discard");
            Assert.IsNotNull(LeaderRules.Unavailable(board, LeaderIds.FrancescaHopeOfTheAenSeidhe, true), "no agile unit");
        }

        [Test]
        public void PassiveLeadersCannotBeUsed()
        {
            PlayerState p = TestBoard.Player(Faction.Monsters, LeaderIds.EredinTheTreacherous);
            Board board = NewBoard(p, TestBoard.Player());
            foreach (int leader in new[] { LeaderIds.EredinTheTreacherous, LeaderIds.EmhyrInvaderOfTheNorth, LeaderIds.EmhyrTheWhiteFlame, LeaderIds.FrancescaDaisyOfTheValley })
                Assert.IsNotNull(LeaderRules.Unavailable(board, leader, true), "leader " + leader);
        }

        [Test]
        public void PickAnyWeatherIncludesClearWeather()
        {
            PlayerState p = TestBoard.Player(Faction.Monsters, LeaderIds.EredinKingOfTheWildHunt);
            p.DeckList.AddRange(new[] { CardIds.ClearWeather, Fiend, CardIds.TorrentialRain, CardIds.TorrentialRain });
            CollectionAssert.AreEquivalent(new[] { CardIds.ClearWeather, CardIds.TorrentialRain }, LeaderRules.WeatherCardsFor(p, Row.Weather, Db));
            CollectionAssert.AreEqual(new[] { CardIds.TorrentialRain }, LeaderRules.WeatherCardsFor(p, Row.Siege, Db));
        }

        [Test]
        public void InvaderOfTheNorthLeavesHeroMedicsTheirChoice()
        {
            PlayerState p = TestBoard.Player();
            p.DiscardList.Add(BlueStripes);
            Board board = NewBoard(p, TestBoard.Player(Faction.Monsters));
            board.RandomMedic = true;
            Assert.AreEqual(PlayOutcome.WaitingForMedicChoice, PlayRules.Resolve(board, Db.Get(Yennefer), Row.Range, true, true));
            Assert.That(p.DiscardList, Has.Member(BlueStripes));

            Assert.AreEqual(PlayOutcome.Done, PlayRules.Resolve(board, Db.Get(DunBannerMedic), Row.Siege, true, true));
            Assert.That(p.DiscardList, Has.No.Member(BlueStripes), "a regular medic restores at random");
        }
    }
}

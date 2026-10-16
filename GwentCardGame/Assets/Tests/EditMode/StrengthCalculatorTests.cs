using System.Collections.Generic;
using NUnit.Framework;
using Gwent.Rules;

namespace Gwent.Rules.Tests
{
    public class StrengthCalculatorTests
    {
        private const int Geralt = 67;          // hero, close 15
        private const int Fiend = 47;           // close 6
        private const int BlueStripes = 7;      // close 4, tight bond
        private const int SiegeExpert = 82;     // siege 1, morale boost
        private const int EarthElemental = 30;  // siege 6
        private const int PrinceStennis = 99;   // close 5, spy
        private const int Olgierd = 95;         // agile 6, morale boost

        private static List<int> Close(PlayerState p) { return p.MCloseStrength; }

        [Test]
        public void WeatherReducesUnitsToOneButNotHeroes()
        {
            PlayerState p = TestBoard.Player();
            p.CloseList.AddRange(new[] { Geralt, Fiend });
            TestBoard.Recalc(new List<int> { CardIds.BitingFrost }, p);
            CollectionAssert.AreEqual(new[] { 15, 1 }, Close(p));
        }

        [Test]
        public void TightBondMultipliesByCopies_AfterWeather()
        {
            PlayerState p = TestBoard.Player();
            p.CloseList.AddRange(new[] { BlueStripes, BlueStripes, BlueStripes });
            TestBoard.Recalc(null, p);
            CollectionAssert.AreEqual(new[] { 12, 12, 12 }, Close(p));

            TestBoard.Recalc(new List<int> { CardIds.BitingFrost }, p);
            CollectionAssert.AreEqual(new[] { 3, 3, 3 }, Close(p));
        }

        [Test]
        public void MoraleBoostGivesOthersOnePerBooster()
        {
            PlayerState p = TestBoard.Player();
            p.SiegeList.AddRange(new[] { SiegeExpert, EarthElemental });
            TestBoard.Recalc(null, p);
            CollectionAssert.AreEqual(new[] { 1, 7 }, p.MSiegeStrength);

            p.SiegeList.Add(SiegeExpert);
            TestBoard.Recalc(null, p);
            CollectionAssert.AreEqual(new[] { 2, 8, 2 }, p.MSiegeStrength);
        }

        [Test]
        public void HornDoublesNonHeroes()
        {
            PlayerState p = TestBoard.Player();
            p.CloseList.AddRange(new[] { Fiend, Geralt });
            p.SpCloseList.Add(CardIds.CommanderHorn);
            TestBoard.Recalc(null, p);
            CollectionAssert.AreEqual(new[] { 12, 15 }, Close(p));
        }

        [Test]
        public void DandelionDoublesOthers_ButDoesNotStackWithHorn()
        {
            PlayerState p = TestBoard.Player();
            p.CloseList.AddRange(new[] { CardIds.Dandelion, Fiend });
            TestBoard.Recalc(null, p);
            CollectionAssert.AreEqual(new[] { 2, 12 }, Close(p));

            p.SpCloseList.Add(CardIds.CommanderHorn);
            TestBoard.Recalc(null, p);
            CollectionAssert.AreEqual(new[] { 4, 12 }, Close(p));
        }

        [Test]
        public void BondAndMoraleComeBeforeTheRowsOneDoubling()
        {
            // The Witcher 3: (4 x 2 bonds + 1 morale) x 2 = 18 for each Blue Stripes, one doubling only (the horn and
            // Dandelion do not stack); Dandelion gets the horn himself; the hero takes nothing
            PlayerState p = TestBoard.Player();
            p.CloseList.AddRange(new[] { BlueStripes, BlueStripes, Olgierd, CardIds.Dandelion, Geralt });
            p.SpCloseList.Add(CardIds.CommanderHorn);
            TestBoard.Recalc(null, p);
            CollectionAssert.AreEqual(new[] { 18, 18, 12, 6, 15 }, Close(p));
        }

        [Test]
        public void ALeaderHornIsTheRowsOneHorn()
        {
            // Eredin, Commander of the Red Riders: doubles the close row like a horn, never on top of one, never a hero
            PlayerState p = TestBoard.Player(Faction.Monsters, LeaderIds.EredinCommanderOfTheRedRiders);
            p.CloseList.AddRange(new[] { Fiend, CardIds.Dandelion, Geralt });
            Assert.IsTrue(AbilityRules.PlaceLeaderHorn(p, Row.Close));
            TestBoard.Recalc(null, p);
            CollectionAssert.AreEqual(new[] { 12, 4, 15 }, Close(p));

            Board board = new Board { Player = p, Enemy = TestBoard.Player(), Weather = new List<int>(), Db = TestBoard.Db, Rnd = new System.Random(0) };
            p.HandList.Add(CardIds.CommanderHorn);
            PlayRules.Resolve(board, TestBoard.Db.Get(CardIds.CommanderHorn), Row.Close, true, false);
            TestBoard.Recalc(null, p);
            CollectionAssert.AreEqual(new[] { 12, 4, 15 }, Close(p), "a horn card on that row does not double it again");
        }

        [Test]
        public void EredinDoublesSpies()
        {
            PlayerState p = TestBoard.Player();
            p.CloseList.Add(PrinceStennis);
            StrengthCalculator.Recalculate(p, new List<int>(), true, TestBoard.Db);
            CollectionAssert.AreEqual(new[] { 10 }, Close(p));
        }

        [Test]
        public void SpiesAreDoubledBeforeMoraleAndHorn()
        {
            // The Witcher 3: (5 x 2 + 1 morale) x 2 horn = 22, not (5 + 1) x 2 x 2 = 24
            PlayerState p = TestBoard.Player();
            p.CloseList.AddRange(new[] { PrinceStennis, Olgierd });
            p.SpCloseList.Add(CardIds.CommanderHorn);
            StrengthCalculator.Recalculate(p, new List<int>(), true, TestBoard.Db);
            Assert.AreEqual(22, Close(p)[0]);
        }

        [Test]
        public void ADecoyOnARowTakesNoModifier()
        {
            PlayerState p = TestBoard.Player();
            p.CloseList.AddRange(new[] { CardIds.Decoy, Olgierd, Fiend });
            p.SpCloseList.Add(CardIds.CommanderHorn);
            TestBoard.Recalc(null, p);
            CollectionAssert.AreEqual(new[] { 0, 12, 14 }, Close(p));
        }
    }
}

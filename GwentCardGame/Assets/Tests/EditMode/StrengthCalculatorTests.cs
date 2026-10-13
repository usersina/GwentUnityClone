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
        public void EredinDoublesSpies()
        {
            PlayerState p = TestBoard.Player();
            p.CloseList.Add(PrinceStennis);
            StrengthCalculator.Recalculate(p, new List<int>(), true, TestBoard.Db);
            CollectionAssert.AreEqual(new[] { 10 }, Close(p));
        }
    }
}

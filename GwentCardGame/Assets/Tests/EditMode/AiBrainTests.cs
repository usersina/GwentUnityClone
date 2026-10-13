using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Gwent.Rules;

namespace Gwent.Rules.Tests
{
    public class AiBrainTests
    {
        private const int Fiend = 47;           // M close 6
        private const int EarthElemental = 30;  // M siege 6
        private const int Leshen = 85;          // M hero range 10
        private const int Imlerith = 77;        // M hero close 10
        private const int Draug = 27;           // M hero close 10
        private const int Nekker = 94;          // M close 2, muster
        private const int Ballista = 4;         // NR siege 6
        private const int MysteriousElf = 92;   // hero spy
        private const int PrinceStennis = 99;   // NR spy 5

        private static readonly int[] AiDeck = { 67, 14, 77, 83, 85, 27, 125, 126, 127, 128, 129, 1, 1, 1, 2, 18, 19, 20, 47, 30, 22, 135, 92, 94, 94, 94, 201, 202, 204 };

        private static AiContext Context(PlayerState me, PlayerState opp, params int[] weather)
        {
            AiContext ctx = new AiContext { Me = me, Opponent = opp, Weather = weather.ToList(), Db = TestBoard.Db };
            StrengthCalculator.Recalculate(me, ctx.Weather, false, ctx.Db);
            StrengthCalculator.Recalculate(opp, ctx.Weather, false, ctx.Db);
            return ctx;
        }

        private static PlayerState Ai(params int[] hand)
        {
            PlayerState me = TestBoard.Player(Factions.Monsters, LeaderIds.EredinCommanderOfTheRedRiders);
            me.HandList.AddRange(hand);
            return me;
        }

        private static PlayerState Opponent(params int[] close_units)
        {
            PlayerState opp = TestBoard.Player();
            opp.CloseList.AddRange(close_units);
            opp.HandList.AddRange(new[] { 7, 7, 7, 11, 11 });
            return opp;
        }

        [Test]
        public void AlwaysReturnsALegalAction()
        {
            Random rnd = new Random(1234);
            CardDatabase db = TestBoard.Db;
            List<int> units = db.All.Where(c => c.IsUnit).Select(c => c._id).ToList();
            for (int i = 0; i < 400; i++)
            {
                PlayerState me = TestBoard.Player(Factions.Monsters, LeaderIds.ByFaction[Factions.Monsters][rnd.Next(5)]);
                PlayerState opp = TestBoard.Player(Factions.NorthernRealms, LeaderIds.ByFaction[Factions.NorthernRealms][rnd.Next(5)]);
                foreach (int card in AiDeck.OrderBy(x => rnd.Next()).Take(rnd.Next(0, 11)))
                    me.HandList.Add(card);
                foreach (int card in AiDeck.OrderBy(x => rnd.Next()).Take(rnd.Next(0, 10)))
                    me.DeckList.Add(card);
                foreach (string row in Rows.Units)
                {
                    for (int n = rnd.Next(0, 4); n > 0; n--)
                        me.UnitRow(row).Add(units[rnd.Next(units.Count)]);
                    for (int n = rnd.Next(0, 4); n > 0; n--)
                        opp.UnitRow(row).Add(units[rnd.Next(units.Count)]);
                }
                for (int n = rnd.Next(0, 4); n > 0; n--)
                    me.DiscardList.Add(units[rnd.Next(units.Count)]);
                for (int n = rnd.Next(0, 8); n > 0; n--)
                    opp.HandList.Add(units[rnd.Next(units.Count)]);
                opp.hasPassed = rnd.Next(3) == 0;
                me.Lives = rnd.Next(1, 3);
                me.canLeader = rnd.Next(2) == 0;
                int[] weather = new[] { 200, 204, 206 }.Where(w => rnd.Next(3) == 0).ToArray();

                AiAction action = AiBrain.Decide(Context(me, opp, weather));

                switch (action.Type)
                {
                    case AiActionType.PlayCard:
                        Assert.That(me.HandList, Has.Member(action.CardId), "plays a card from its hand");
                        Assert.IsNotNull(action.Row);
                        break;
                    case AiActionType.UseLeader:
                        Assert.IsTrue(me.canLeader, "leader still available");
                        if (action.CardId != -1)
                            Assert.That(me.DeckList, Has.Member(action.CardId), "weather comes from the deck");
                        break;
                }
            }
        }

        [Test]
        public void NeverUsesTheFogLeaderWithoutFogInTheDeck()
        {
            PlayerState me = Ai(Fiend, EarthElemental, Leshen, Imlerith, Draug, Nekker, CardIds.ImpenetrableFog);
            me.Lives = 1;
            PlayerState opp = TestBoard.Player();
            opp.RangeList.AddRange(new[] { 17, 17, 17, 31, 13 }); // big range row
            opp.HandList.AddRange(new[] { 7, 7 });
            AiAction action = AiBrain.Decide(Context(me, opp));
            Assert.AreNotEqual(AiActionType.UseLeader, action.Type);
        }

        [Test]
        public void UsesTheWeatherLeaderWhenItPaysOff_AndNeverWhenCancelled()
        {
            PlayerState me = Ai(Fiend, EarthElemental, Leshen, Imlerith, Draug, Nekker);
            me.Lives = 1;
            me.DeckList.Add(CardIds.ImpenetrableFog);
            PlayerState opp = TestBoard.Player();
            opp.RangeList.AddRange(new[] { 17, 17, 17 }); // 3 tight-bonded Crinfrid Reavers = 45
            opp.HandList.AddRange(new[] { 7, 7 });
            me.CloseList.AddRange(new[] { Imlerith, Draug }); // AI 20 vs 45: only the fog can turn this around

            AiAction action = AiBrain.Decide(Context(me, opp));
            Assert.AreEqual(AiActionType.UseLeader, action.Type);
            Assert.AreEqual(CardIds.ImpenetrableFog, action.CardId);

            me.leaderDisabled = true;
            Assert.AreNotEqual(AiActionType.UseLeader, AiBrain.Decide(Context(me, opp)).Type);
        }

        [Test]
        public void WhenBehind_CatchesUpInsteadOfPlayingItsWeakestCard()
        {
            PlayerState me = Ai(Fiend, EarthElemental, Leshen, Imlerith, Draug, 128);
            PlayerState opp = Opponent(Ballista, Ballista); // 12 vs 0... AI is behind by less than 30
            Context(me, opp);
            int deficit = opp.TotalScore() - me.TotalScore();
            Assume.That(deficit > 6 && deficit < 30);

            AiAction action = AiBrain.Decide(Context(me, opp));

            Assert.AreEqual(AiActionType.PlayCard, action.Type);
            Assert.Greater(TestBoard.Db.Get(action.CardId).strength, 6, "needs a card that overtakes, not the weakest one");
        }

        [Test]
        public void WhenAhead_KeepsHeroesAndPlaysAWeakCard()
        {
            PlayerState me = Ai(Fiend, EarthElemental, Leshen, Imlerith, Draug, Nekker);
            me.CloseList.AddRange(new[] { Fiend, Fiend });        // AI 12 vs opponent 2
            PlayerState opp = Opponent(98);

            AiAction action = AiBrain.Decide(Context(me, opp));

            Assert.AreEqual(AiActionType.PlayCard, action.Type);
            Assert.IsFalse(TestBoard.Db.Get(action.CardId).unique, "does not spend a hero while ahead");
        }

        [Test]
        public void PassesWhenTheOpponentPassedAndItIsAhead()
        {
            PlayerState me = Ai(Fiend, Leshen);
            me.CloseList.Add(Imlerith);
            PlayerState opp = Opponent(Ballista);
            opp.hasPassed = true;
            Assert.AreEqual(AiActionType.Pass, AiBrain.Decide(Context(me, opp)).Type);
        }

        [Test]
        public void WhenTheOpponentPassed_PlaysTheCheapestCardThatWins()
        {
            PlayerState me = Ai(Fiend, Leshen, Imlerith);
            PlayerState opp = Opponent(98, 98, 98); // Poor infantry x3, tight bond: 9
            opp.hasPassed = true;
            me.SiegeList.Add(EarthElemental);       // AI 6 vs 9

            AiAction action = AiBrain.Decide(Context(me, opp));
            Assert.AreEqual(Fiend, action.CardId);
        }

        [Test]
        public void DoesNotDumpSpecialsWhenAhead()
        {
            PlayerState me = Ai(CardIds.CommanderHorn, CardIds.ClearWeather);
            me.CloseList.Add(Imlerith);
            PlayerState opp = Opponent(Ballista);
            Assert.AreEqual(AiActionType.Pass, AiBrain.Decide(Context(me, opp)).Type);
        }

        [Test]
        public void PlaysSpiesEarly()
        {
            PlayerState me = Ai(Fiend, EarthElemental, Leshen, Imlerith, Draug, PrinceStennis);
            me.DeckList.AddRange(new[] { Nekker, Nekker });
            PlayerState opp = Opponent(Ballista);
            Assert.AreEqual(PrinceStennis, AiBrain.Decide(Context(me, opp)).CardId);
        }

        [Test]
        public void MedicPrefersSpies()
        {
            PlayerState me = Ai();
            me.DeckList.Add(Nekker);
            me.DiscardList.AddRange(new[] { Fiend, PrinceStennis, Leshen });
            Assert.AreEqual(PrinceStennis, AiBrain.ChooseMedicTarget(me, TestBoard.Db));
        }
    }
}

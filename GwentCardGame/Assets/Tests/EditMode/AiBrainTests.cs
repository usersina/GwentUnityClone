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
        private const int DunBannerMedic = 28;  // NR siege 5, medic
        private const int Villentretenmerth = 135; // close 7, scorches the enemy close row

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
            PlayerState me = TestBoard.Player(Faction.Monsters, LeaderIds.EredinKingOfTheWildHunt);
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
        public void KeepsPlayingWhileTheOpponentStillHasItsLeader()
        {
            // Final round, the AI leads 12-10; the opponent has no card left but an unused range horn
            PlayerState me = Ai(Nekker);
            me.CloseList.AddRange(new[] { Fiend, EarthElemental });
            me.Lives = 1;
            PlayerState opp = TestBoard.Player(Faction.Scoiatael, LeaderIds.FrancescaTheBeautiful);
            opp.RangeList.Add(85); // 10
            opp.Lives = 1;
            Assert.AreNotEqual(AiActionType.Pass, AiBrain.Decide(Context(me, opp)).Type, "the horn would win the battle for the opponent");
        }

        [Test]
        public void OnItsLastLifeItPlaysASpecialRatherThanPassBarelyAhead()
        {
            // The AI leads 12-11 with two Fiends and only a Commander's Horn in hand; the opponent still has cards
            PlayerState me = Ai(CardIds.CommanderHorn);
            me.CloseList.AddRange(new[] { Fiend, Fiend });
            me.Lives = 1;
            PlayerState opp = Opponent(PrinceStennis, Ballista); // 5 + 6
            AiAction action = AiBrain.Decide(Context(me, opp));
            Assert.AreEqual(AiActionType.PlayCard, action.Type);
            Assert.AreEqual(CardIds.CommanderHorn, action.CardId);
        }

        [Test]
        public void ASpyDoesNotCountAsTheMoveThatWinsTheRound()
        {
            // The opponent has passed 4 ahead, final round: Ballista (6) wins it, the spy only gives the opponent 5
            PlayerState me = Ai(PrinceStennis, Ballista);
            me.DeckList.Add(CardIds.ClearWeather);
            me.Lives = 1;
            PlayerState opp = TestBoard.Player();
            opp.CloseList.Add(7); // 4
            opp.hasPassed = true;
            AiAction action = AiBrain.Decide(Context(me, opp));
            Assert.AreEqual(AiActionType.PlayCard, action.Type);
            Assert.AreEqual(Ballista, action.CardId);
        }

        [Test]
        public void AlwaysReturnsALegalAction()
        {
            ReturnsLegalActions(AiDifficulty.Normal, 400);
        }

        [TestCase(AiDifficulty.Easy, 400)]
        [TestCase(AiDifficulty.Hard, 40)]
        public void EveryDifficultyReturnsALegalAction(AiDifficulty difficulty, int positions)
        {
            ReturnsLegalActions(difficulty, positions);
        }

        // Hard guesses the opponent's hand from the cards it has not seen: which of them are in the hand and which in
        // the deck makes no difference to its move
        [Test]
        public void HardNeverReadsTheOpponentsHand()
        {
            int limit = AiSearch.TimeLimitMs;
            AiSearch.TimeLimitMs = int.MaxValue;
            try
            {
                for (int seed = 0; seed < 6; seed++)
                {
                    AiAction[] moves = new AiAction[2];
                    for (int deal = 0; deal < 2; deal++)
                    {
                        PlayerState me = Ai(Fiend, EarthElemental, Nekker, PrinceStennis, MysteriousElf, Leshen);
                        me.DeckList.AddRange(new[] { 47, 30, 22, 135 });
                        PlayerState opp = TestBoard.Player();
                        opp.CloseList.AddRange(new[] { 7, 7 });
                        int[] unseen = { 4, 4, 28, 99, 11, 11, 7, 205, 200 };
                        opp.HandList.AddRange(deal == 0 ? unseen.Take(5) : unseen.Skip(4));
                        opp.DeckList.AddRange(deal == 0 ? unseen.Skip(5) : unseen.Take(4));
                        AiContext ctx = Context(me, opp);
                        ctx.Difficulty = AiDifficulty.Hard;
                        ctx.Seed = seed;
                        moves[deal] = AiBrain.Decide(ctx);
                    }
                    Assert.AreEqual(moves[0].ToString(), moves[1].ToString(), "seed " + seed);
                }
            }
            finally
            {
                AiSearch.TimeLimitMs = limit;
            }
        }

        // Where there is no other thread (a browser), Hard thinks a few milliseconds a frame: the same move as at once
        [Test]
        public void HardDecidesTheSameInSmallSteps()
        {
            int limit = AiSearch.TimeLimitMs;
            AiSearch.TimeLimitMs = int.MaxValue;
            try
            {
                for (int seed = 0; seed < 4; seed++)
                {
                    AiAction[] moves = new AiAction[2];
                    int steps = 0;
                    for (int way = 0; way < 2; way++)
                    {
                        PlayerState me = Ai(Fiend, EarthElemental, Nekker, PrinceStennis, Leshen);
                        me.DeckList.AddRange(new[] { 47, 30, 22, 135 });
                        PlayerState opp = TestBoard.Player();
                        opp.CloseList.AddRange(new[] { 7, 7 });
                        opp.HandList.AddRange(new[] { 4, 4, 28, 99, 11 });
                        opp.DeckList.AddRange(new[] { 11, 7, 205, 200 });
                        AiContext ctx = Context(me, opp);
                        ctx.Difficulty = AiDifficulty.Hard;
                        ctx.Seed = seed;
                        if (way == 0)
                            moves[way] = AiBrain.Decide(ctx);
                        else
                        {
                            AiSearch.Thinking thinking = AiSearch.Begin(ctx, int.MaxValue);
                            while (!thinking.Step(0))
                                steps++;
                            moves[way] = thinking.Result;
                        }
                    }
                    Assert.AreEqual(moves[0].ToString(), moves[1].ToString(), "seed " + seed);
                    Assert.Greater(steps, 10, "thought in many steps");
                }
            }
            finally
            {
                AiSearch.TimeLimitMs = limit;
            }
        }

        // The AI's simulations copy a side into the same object again and again (PlayerState.CopyFrom): every field
        // must come across, into lists of its own, whatever was there before
        [Test]
        public void CopyingASideInPlaceCopiesEveryField()
        {
            PlayerState source = new PlayerState(), target = new PlayerState();
            int value = 1;
            foreach (System.Reflection.FieldInfo field in typeof(PlayerState).GetFields())
            {
                foreach (PlayerState side in new[] { source, target })
                {
                    int n = side == source ? value : 1000 + value;
                    if (field.FieldType == typeof(List<int>))
                        field.SetValue(side, new List<int> { n, n + 1 });
                    else if (field.FieldType == typeof(List<Row>))
                        field.SetValue(side, new List<Row> { side == source ? Row.Close : Row.Siege, Row.Range });
                    else if (field.FieldType == typeof(List<bool>))
                        field.SetValue(side, new List<bool> { side == source, true });
                    else if (field.FieldType == typeof(int))
                        field.SetValue(side, n);
                    else if (field.FieldType == typeof(bool))
                        field.SetValue(side, side == source);
                    else if (field.FieldType == typeof(string))
                        field.SetValue(side, "text" + n);
                    else if (field.FieldType == typeof(Faction))
                        field.SetValue(side, side == source ? Faction.Monsters : Faction.Nilfgaard);
                    else
                        Assert.Fail("PlayerState." + field.Name + ": a field of a kind this test does not know (" + field.FieldType.Name + ")");
                }
                value += 10;
            }

            target.CopyFrom(source);

            foreach (System.Reflection.FieldInfo field in typeof(PlayerState).GetFields())
            {
                object copied = field.GetValue(target), original = field.GetValue(source);
                if (original is System.Collections.IEnumerable && !(original is string))
                {
                    CollectionAssert.AreEqual((System.Collections.IEnumerable)original, (System.Collections.IEnumerable)copied, field.Name);
                    Assert.AreNotSame(original, copied, field.Name + " is a list of its own");
                }
                else
                    Assert.AreEqual(original, copied, field.Name);
            }
        }

        [TestCase(AiDifficulty.Easy)]
        [TestCase(AiDifficulty.Hard)]
        public void EveryDifficultyPassesWhenTheOpponentPassedAndItIsAhead(AiDifficulty difficulty)
        {
            PlayerState me = Ai(Fiend, Nekker);
            me.CloseList.Add(Draug);
            PlayerState opp = Opponent(Fiend);
            opp.hasPassed = true;
            AiContext ctx = Context(me, opp);
            ctx.Difficulty = difficulty;
            Assert.AreEqual(AiActionType.Pass, AiBrain.Decide(ctx).Type);
        }

        private static void ReturnsLegalActions(AiDifficulty difficulty, int positions)
        {
            Random rnd = new Random(1234);
            CardDatabase db = TestBoard.Db;
            List<int> units = db.All.Where(c => c.IsUnit).Select(c => c._id).ToList();
            for (int i = 0; i < positions; i++)
            {
                PlayerState me = TestBoard.Player(Faction.Monsters, LeaderIds.ByFaction[Faction.Monsters][rnd.Next(5)]);
                PlayerState opp = TestBoard.Player(Faction.NorthernRealms, LeaderIds.ByFaction[Faction.NorthernRealms][rnd.Next(5)]);
                foreach (int card in AiDeck.OrderBy(x => rnd.Next()).Take(rnd.Next(0, 11)))
                    me.HandList.Add(card);
                foreach (int card in AiDeck.OrderBy(x => rnd.Next()).Take(rnd.Next(0, 10)))
                    me.DeckList.Add(card);
                foreach (Row row in Rows.Units)
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

                AiContext ctx = Context(me, opp, weather);
                ctx.Difficulty = difficulty;
                ctx.Seed = i;
                AiAction action = AiBrain.Decide(ctx);

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

        [Test]
        public void MedicChainsThroughAnotherMedicBeforeTheStrongestUnit()
        {
            PlayerState me = Ai();
            me.DiscardList.AddRange(new[] { Fiend, DunBannerMedic, Villentretenmerth });
            Assert.AreEqual(DunBannerMedic, AiBrain.ChooseMedicTarget(me, TestBoard.Db));
            me.DiscardList.Remove(DunBannerMedic);
            Assert.AreEqual(Villentretenmerth, AiBrain.ChooseMedicTarget(me, TestBoard.Db), "a row scorch comes before raw strength");
        }

        [Test]
        public void DecoyTakesBackARowScorchUnit()
        {
            PlayerState me = Ai();
            me.CloseList.AddRange(new[] { Fiend, Villentretenmerth });
            Row row;
            Assert.AreEqual(Villentretenmerth, AiBrain.ChooseDecoyTarget(me, TestBoard.Db, out row));
            Assert.AreEqual(Row.Close, row);
        }

        [Test]
        public void NilfgaardHoldsATiedRoundOnceTheOpponentHasPassed()
        {
            PlayerState me = TestBoard.Player(Faction.Nilfgaard, LeaderIds.EmhyrTheWhiteFlame);
            me.HandList.AddRange(new[] { Fiend, EarthElemental });
            me.CloseList.Add(Fiend);
            PlayerState opp = Opponent(Fiend);
            opp.hasPassed = true;
            Assert.AreEqual(AiActionType.Pass, AiBrain.Decide(Context(me, opp)).Type, "Nilfgaard wins the tie");

            me.Faction = Faction.Monsters;
            Assert.AreNotEqual(AiActionType.Pass, AiBrain.Decide(Context(me, opp)).Type, "anyone else must break it");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Gwent.Rules;

namespace Gwent.Rules.Tests
{
    // Skellige (Blood and Wine): its cards' abilities, its leaders and its perk
    public class SkelligeRulesTests
    {
        private const int Berserker = CardIds.Berserker;           // close 4, turns into the Transformed Vildkaarl (14, morale)
        private const int YoungBerserker = CardIds.YoungBerserker; // range 2, turns into the Transformed Young Vildkaarl (8, bond)
        private const int Ermion = 160;                            // hero range 8, Mardroeme
        private const int ClanAnCraiteWarrior = 151;               // close 6, tight bond
        private const int BrokvarArcher = 152;                     // range 6
        private const int DimunPirate = 153;                       // range 6, scorch as he lands
        private const int ShieldMaiden = CardIds.ClanDrummondShieldMaiden; // close 4, tight bond
        private const int Madman = 167;                            // close 6
        private const int WarLongship = 173;                       // siege 6, tight bond
        private const int Olaf = 168;                              // agile 12, morale
        private const int Hjalmar = 162;                           // hero range 10
        private const int Birna = 148;                             // close 2, medic
        private const int Fiend = 47;                              // M close 6
        private const int Draug = 27;                              // M hero close 10
        private const int Prince = 99;                             // NR close spy (Prince Stennis)

        private CardDatabase Db { get { return TestBoard.Db; } }

        private Board NewBoard(int leader = LeaderIds.CrachAnCraite)
        {
            return new Board { Player = TestBoard.Player(Faction.Skellige, leader), Enemy = TestBoard.Player(Faction.Monsters, 40), Db = Db, Rnd = new Random(1) };
        }

        private static void Play(Board board, int card_id, Row row)
        {
            PlayRules.Resolve(board, TestBoard.Db.Get(card_id), row, true, false);
        }

        //----------------------------------------The cards----------------------------------------//
        [Test]
        public void SkelligesCardsAreInTheDatabase()
        {
            List<Card> cards = Db.All.Where(c => c.faction == Faction.Skellige && c.IsUnit).ToList();
            Assert.AreEqual(26, cards.Count, "23 cards for decks, Hemdall and the two bears");
            Assert.AreEqual(35, cards.Sum(c => DeckRules.MaxCopies(c)), "35 Skellige units can go in a deck");
            CollectionAssert.AreEquivalent(new[] { LeaderIds.CrachAnCraite, LeaderIds.KingBran }, LeaderIds.ByFaction[Faction.Skellige]);
            Assert.AreEqual("SK", Faction.Skellige.Code());
            Assert.AreEqual("Skellige", Faction.Skellige.DisplayName());
            CollectionAssert.Contains(Factions.Playable, Faction.Skellige);
            Assert.AreEqual(Ability.Mardroeme, Db.Get(CardIds.Mardroeme).ability);
            Assert.AreEqual(Ability.ReduceRangeSiege, Db.Get(CardIds.SkelligeStorm).ability);
            Assert.AreEqual(Ability.Berserker, Db.Get(Berserker).ability);
        }

        [Test]
        public void MardroemeGoesInSkelligesDecksOnlyAndTheStormInAny()
        {
            Assert.IsTrue(DeckRules.Fits(Db.Get(CardIds.Mardroeme), Faction.Skellige));
            Assert.IsFalse(DeckRules.Fits(Db.Get(CardIds.Mardroeme), Faction.Monsters));
            foreach (Faction faction in Factions.Playable)
                Assert.IsTrue(DeckRules.Fits(Db.Get(CardIds.SkelligeStorm), faction), faction.ToString());
            Assert.IsFalse(DeckRules.Fits(Db.Get(Berserker), Faction.NorthernRealms));

            Deck deck = new Deck { Faction = Faction.Monsters, Leader = 40, Name = "Mushrooms", Cards = Enumerable.Repeat(Fiend, 1).ToList() };
            deck.Cards.Add(CardIds.Mardroeme);
            CollectionAssert.Contains(DeckRules.Validate(deck, Db), "Mardroeme does not belong to this faction");
        }

        [Test]
        public void TheSummonedCardsCantBePutInADeck()
        {
            foreach (int card_id in new[] { CardIds.Hemdall, CardIds.TransformedVildkaarl, CardIds.TransformedYoungVildkaarl, CardIds.BovineDefenseForce })
            {
                Assert.IsTrue(CardIds.IsSummoned(card_id));
                Assert.AreEqual(0, DeckRules.MaxCopies(Db.Get(card_id)), Db.Get(card_id).name);
            }
            Assert.IsFalse(CardIds.IsSummoned(CardIds.Kambi));
        }

        //----------------------------------------Mardroeme and the Berserkers----------------------------------------//
        [Test]
        public void AMardroemeTurnsTheBerserkersOfItsRowIntoBears()
        {
            Board board = NewBoard();
            PlayerState p = board.Player;
            p.CloseList.AddRange(new[] { Berserker, Madman });
            p.RangeList.Add(YoungBerserker);

            Play(board, CardIds.Mardroeme, Row.Close);

            CollectionAssert.AreEqual(new[] { CardIds.TransformedVildkaarl, Madman }, p.CloseList, "in the Berserker's place");
            CollectionAssert.AreEqual(new[] { YoungBerserker }, p.RangeList, "another row's stays");
            CollectionAssert.AreEqual(new[] { CardIds.Mardroeme }, p.SpCloseList, "in the horn slot");
            CollectionAssert.AreEqual(new[] { Berserker }, p.Transformed, "the Berserker leaves the game");
            CollectionAssert.AreEqual(new[] { 14, 7 }, p.MCloseStrength, "the bear's morale boost");
        }

        [Test]
        public void ABerserkerPlayedOnAMardroemesRowTurnsAtOnce()
        {
            Board board = NewBoard();
            PlayerState p = board.Player;
            p.SpRangeList.Add(CardIds.Mardroeme);

            Play(board, YoungBerserker, Row.Range);
            Play(board, YoungBerserker, Row.Range);

            CollectionAssert.AreEqual(new[] { CardIds.TransformedYoungVildkaarl, CardIds.TransformedYoungVildkaarl }, p.RangeList);
            CollectionAssert.AreEqual(new[] { 16, 16 }, p.MRangeStrength, "two bonded bears");
        }

        [Test]
        public void ErmionIsAMardroemeOnHisRow()
        {
            Board board = NewBoard();
            PlayerState p = board.Player;
            p.RangeList.Add(YoungBerserker);
            p.CloseList.Add(Berserker);

            Play(board, Ermion, Row.Range);
            CollectionAssert.AreEqual(new[] { CardIds.TransformedYoungVildkaarl, Ermion }, p.RangeList);
            CollectionAssert.AreEqual(new[] { Berserker }, p.CloseList, "not on his row");

            Play(board, YoungBerserker, Row.Range);
            Assert.AreEqual(CardIds.TransformedYoungVildkaarl, p.RangeList.Last(), "while he stays, the next one turns too");
        }

        [Test]
        public void AMardroemeTakesTheHornSlot()
        {
            Board board = NewBoard();
            PlayerState p = board.Player;
            Play(board, CardIds.Mardroeme, Row.Siege);
            Play(board, CardIds.CommanderHorn, Row.Siege);

            CollectionAssert.AreEqual(new[] { CardIds.Mardroeme }, p.SpSiegeList);
            CollectionAssert.AreEqual(new[] { CardIds.CommanderHorn }, p.DiscardList, "no room for the horn");
            Assert.IsFalse(AbilityRules.PlaceLeaderHorn(p, Row.Siege));

            Board foltest = new Board { Player = TestBoard.Player(Faction.NorthernRealms, LeaderIds.FoltestTheSiegemaster), Enemy = TestBoard.Player(), Db = Db };
            foltest.Player.SpSiegeList.Add(CardIds.Mardroeme);
            Assert.AreEqual("That row's horn slot holds a Mardroeme", LeaderRules.Unavailable(foltest, LeaderIds.FoltestTheSiegemaster, true));
        }

        [Test]
        public void AtTheRoundsEndABearLeavesTheGameAndItsBerserkerIsDiscarded()
        {
            Board board = NewBoard();
            PlayerState p = board.Player;
            p.CloseList.Add(Berserker);
            Play(board, CardIds.Mardroeme, Row.Close);
            int cards = p.AllCards().Count(id => !CardIds.IsSummoned(id));

            RoundRules.CleanupBoard(p, new Random(0), Db);

            Assert.IsEmpty(p.CloseList);
            CollectionAssert.AreEquivalent(new[] { Berserker, CardIds.Mardroeme }, p.DiscardList, "the Berserker and the Mardroeme are discarded");
            Assert.IsEmpty(p.Transformed);
            Assert.AreEqual(cards, p.AllCards().Count(id => !CardIds.IsSummoned(id)), "no real card lost");
            CollectionAssert.AreEqual(new[] { Berserker }, AbilityRules.MedicCandidates(p.DiscardList, Db), "a medic can bring the Berserker back, never the bear");
        }

        [Test]
        public void AScorchedBearLeavesTheGameAndItsBerserkerIsDiscarded()
        {
            Board board = NewBoard();
            PlayerState p = board.Player, e = board.Enemy;
            p.RangeList.Add(YoungBerserker);
            p.SpRangeList.Add(CardIds.Mardroeme);
            board.Recalculate();
            AbilityRules.Transform(p, Db);
            e.CloseList.Add(Fiend);
            board.Recalculate();

            Assert.AreEqual(1, AbilityRules.Scorch(p, e, Db), "the bear (8) is the strongest");
            Assert.IsEmpty(p.RangeList);
            CollectionAssert.AreEqual(new[] { YoungBerserker }, p.DiscardList);
            Assert.IsEmpty(p.Transformed);
        }

        //----------------------------------------Skellige Storm and King Bran----------------------------------------//
        [Test]
        public void SkelligeStormIsBadWeatherOnTheRangedAndSiegeRows()
        {
            List<int> weather = new List<int> { CardIds.SkelligeStorm };
            Assert.IsFalse(StrengthCalculator.IsWeathered(Row.Close, weather));
            Assert.IsTrue(StrengthCalculator.IsWeathered(Row.Range, weather));
            Assert.IsTrue(StrengthCalculator.IsWeathered(Row.Siege, weather));

            Board board = NewBoard();
            PlayerState e = board.Enemy;
            e.CloseList.Add(Fiend);
            e.RangeList.Add(BrokvarArcher);
            e.SiegeList.Add(WarLongship);
            Play(board, CardIds.SkelligeStorm, Row.Weather);
            CollectionAssert.AreEqual(new[] { 6 }, e.MCloseStrength);
            CollectionAssert.AreEqual(new[] { 1 }, e.MRangeStrength);
            CollectionAssert.AreEqual(new[] { 1 }, e.MSiegeStrength);

            Play(board, CardIds.ClearWeather, Row.Weather);
            Assert.IsEmpty(board.Weather);
            CollectionAssert.AreEquivalent(new[] { CardIds.SkelligeStorm, CardIds.ClearWeather }, board.Player.DiscardList);
        }

        [Test]
        public void UnderKingBranBadWeatherOnlyHalvesTheUnits()
        {
            PlayerState bran = TestBoard.Player(Faction.Skellige, LeaderIds.KingBran);
            PlayerState other = TestBoard.Player(Faction.Monsters, 40);
            bran.CloseList.AddRange(new[] { Olaf, Madman, Birna, CardIds.Kambi });
            other.CloseList.Add(Fiend);
            TestBoard.Recalc(new List<int> { CardIds.BitingFrost }, bran, other);

            // Olaf 12 -> 6, Madman 6 -> 3, Birna 2 -> 1, Kambi 0 -> 0, then Olaf's morale boost
            CollectionAssert.AreEqual(new[] { 6, 4, 2, 1 }, bran.MCloseStrength);
            CollectionAssert.AreEqual(new[] { 1 }, other.MCloseStrength, "the opponent's units still drop to 1");

            bran.leaderDisabled = true; // the White Flame
            TestBoard.Recalc(new List<int> { CardIds.BitingFrost }, bran);
            CollectionAssert.AreEqual(new[] { 1, 2, 2, 1 }, bran.MCloseStrength);
            Assert.AreEqual("This leader ability is passive", LeaderRules.Unavailable(NewBoard(LeaderIds.KingBran), LeaderIds.KingBran, true));
        }

        //----------------------------------------Kambi, Cerys, the Clan Dimun Pirate----------------------------------------//
        [Test]
        public void AScorchedKambiCallsHemdallAtOnce()
        {
            Board board = NewBoard();
            PlayerState p = board.Player, e = board.Enemy;
            p.CloseList.Add(CardIds.Kambi);
            e.CloseList.Add(Draug); // a hero: Kambi (0) is the strongest unit Scorch can hit
            board.Recalculate();

            Assert.AreEqual(1, AbilityRules.Scorch(p, e, Db));
            CollectionAssert.AreEqual(new[] { CardIds.Hemdall }, p.CloseList);
            CollectionAssert.AreEqual(new[] { CardIds.Kambi }, p.DiscardList);
            board.Recalculate();
            Assert.AreEqual(11, p.RowScore(Row.Close));

            RoundRules.CleanupBoard(p, new Random(0), Db);
            Assert.IsEmpty(p.CloseList, "Hemdall came already");
            CollectionAssert.AreEqual(new[] { CardIds.Kambi }, p.DiscardList, "Hemdall leaves the game");
        }

        [Test]
        public void KambiDiscardedAtTheRoundsEndCallsHemdallNextRound()
        {
            PlayerState p = TestBoard.Player(Faction.Skellige, LeaderIds.CrachAnCraite);
            p.CloseList.Add(CardIds.Kambi);
            RoundRules.CleanupBoard(p, new Random(0), Db);
            CollectionAssert.AreEqual(new[] { CardIds.Hemdall }, p.CloseList);
            CollectionAssert.AreEqual(new[] { CardIds.Kambi }, p.DiscardList);
        }

        [Test]
        public void CerysCallsTheShieldMaidensButTheyDontCallHer()
        {
            Board board = NewBoard();
            PlayerState p = board.Player;
            p.DeckList.AddRange(new[] { ShieldMaiden, Madman, ShieldMaiden });
            p.HandList.Add(ShieldMaiden);
            Play(board, CardIds.Cerys, Row.Close);

            Assert.AreEqual(3, p.CloseList.Count(id => id == ShieldMaiden));
            CollectionAssert.AreEqual(new[] { Madman }, p.DeckList);
            Assert.IsEmpty(p.HandList);
            CollectionAssert.AreEqual(new[] { 12, 12, 12 }, p.CloseList.Select((id, i) => p.MCloseStrength[i]).Where((s, i) => p.CloseList[i] == ShieldMaiden), "three bonded maidens");

            Board other = NewBoard();
            other.Player.DeckList.Add(CardIds.Cerys);
            Play(other, ShieldMaiden, Row.Close);
            CollectionAssert.AreEqual(new[] { CardIds.Cerys }, other.Player.DeckList);
        }

        [Test]
        public void TheClanDimunPirateScorchesTheStrongestButNotHimself()
        {
            Board board = NewBoard();
            PlayerState p = board.Player, e = board.Enemy;
            e.CloseList.AddRange(new[] { Fiend, Draug });  // Fiend 6 (Draug is a hero)
            p.RangeList.Add(BrokvarArcher);                  // 6 too
            Play(board, DimunPirate, Row.Range);

            CollectionAssert.AreEqual(new[] { Draug }, e.CloseList);
            CollectionAssert.AreEqual(new[] { DimunPirate }, p.RangeList, "the archer burns, the Pirate (6 as well) lands");
            CollectionAssert.AreEqual(new[] { BrokvarArcher }, p.DiscardList);
            CollectionAssert.AreEqual(new[] { Fiend }, e.DiscardList);
        }

        //----------------------------------------Crach an Craite----------------------------------------//
        [Test]
        public void CrachAnCraiteShufflesBothDiscardPilesIntoTheDecks()
        {
            Board board = NewBoard();
            Assert.AreEqual("Both discard piles are empty", LeaderRules.Unavailable(board, LeaderIds.CrachAnCraite, true));

            board.Player.DiscardList.AddRange(new[] { Madman, Birna });
            board.Player.DeckList.Add(Olaf);
            board.Enemy.DiscardList.Add(Fiend);
            Assert.IsNull(LeaderRules.Unavailable(board, LeaderIds.CrachAnCraite, true));
            Assert.IsTrue(LeaderRules.IsInstant(LeaderIds.CrachAnCraite));

            string reason;
            Assert.IsTrue(LeaderRules.ApplyInstant(board, LeaderIds.CrachAnCraite, true, out reason), reason);
            Assert.IsEmpty(board.Player.DiscardList);
            Assert.IsEmpty(board.Enemy.DiscardList);
            CollectionAssert.AreEquivalent(new[] { Madman, Birna, Olaf }, board.Player.DeckList);
            CollectionAssert.AreEqual(new[] { Fiend }, board.Enemy.DeckList);
        }

        //----------------------------------------Skellige's perk----------------------------------------//
        [Test]
        public void SkelligeBringsTwoUnitsBackAsTheThirdRoundOpens()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                Board board = NewBoard();
                board.Rnd = new Random(seed);
                PlayerState p = board.Player;
                p.DiscardList.AddRange(new[] { Hjalmar, Prince, CardIds.Decoy, Madman, Olaf, Birna });
                Assert.IsFalse(RoundRules.SkelligeReinforces(p), "not in the first round");
                p.RoundsScore.AddRange(new[] { 50, 40 });
                Assert.IsTrue(RoundRules.SkelligeReinforces(p));

                List<int> placed = RoundRules.SkelligeReinforcements(board, true);

                Assert.AreEqual(2, placed.Count);
                CollectionAssert.IsSubsetOf(placed, new[] { Madman, Olaf, Birna }, "no hero, spy or special card");
                Assert.AreEqual(2, Rows.Units.Sum(r => p.UnitRow(r).Count));
                Assert.AreEqual(4, p.DiscardList.Count);
                Assert.IsTrue(p.CloseList.All(id => placed.Contains(id)), "on their own rows");
            }
        }

        [Test]
        public void SkelligesPerkBringsWhatThereIs()
        {
            Board board = NewBoard();
            PlayerState p = board.Player;
            p.RoundsScore.AddRange(new[] { 50, 40 });
            p.DiscardList.AddRange(new[] { Hjalmar, Madman });
            CollectionAssert.AreEqual(new[] { Madman }, RoundRules.SkelligeReinforcements(board, true));

            PlayerState monsters = board.Enemy;
            monsters.RoundsScore.AddRange(new[] { 50, 40 });
            Assert.IsFalse(RoundRules.SkelligeReinforces(monsters), "Skellige's own");
        }

        //----------------------------------------The AI----------------------------------------//
        [Test]
        public void TheAIPlaysAMardroemeWhereItTurnsTheBerserkers()
        {
            PlayerState me = TestBoard.Player(Faction.Skellige, LeaderIds.KingBran);
            PlayerState opp = TestBoard.Player(Faction.Monsters, 40);
            me.CloseList.Add(Berserker);
            me.HandList.Add(CardIds.Mardroeme);
            me.Lives = 1;
            opp.CloseList.Add(Draug);
            opp.hasPassed = true;
            AiAction action = AiBrain.Decide(new AiContext { Me = me, Opponent = opp, Db = Db });
            Assert.AreEqual(CardIds.Mardroeme, action.CardId, action.ToString());
            Assert.AreEqual(Row.Close, action.Row);
        }
    }
}

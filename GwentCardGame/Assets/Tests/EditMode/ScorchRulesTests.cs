using System;
using System.Collections.Generic;
using NUnit.Framework;
using Gwent.Rules;

namespace Gwent.Rules.Tests
{
    // Every way a scorch happens, played as the battle plays it (PlayRules, LeaderRules), against the boards that test it:
    // heroes on top (immune: the strongest non-heroes burn instead), ties, both sides, strengths as shown (weather, horns,
    // bonds, morale), spies, agile units, a row's 10 (heroes count), medics and decoys bringing a scorcher back. The
    // Witcher 3's cards: the Scorch card and the Clan Dimun Pirate burn the whole battlefield, Villentretenmerth, the Toad
    // and Schirru the enemy's close, ranged and siege rows (if they total 10 or more), as do the leaders Francesca Queen of
    // Dol Blathanna, Foltest Son of Medell and Foltest the Steel-Forged.
    public class ScorchRulesTests
    {
        // Units (row, strength)
        private const int Fiend = 47;               // close 6
        private const int Cahir = 10;               // close 6
        private const int Botchling = 8;            // close 4
        private const int Arachas = 1;              // close 4
        private const int Ballista = 4;             // siege 6
        private const int EarthElemental = 30;      // siege 6
        private const int Assire = 3;               // range 6
        private const int BlackInfantryArcher = 6;  // range 10
        private const int BarclayEls = 5;           // close or range 6
        private const int BlueStripes = 7;          // close 4, tight bond
        private const int ClanAnCraite = 151;       // close 6, tight bond
        private const int SiegeExpert = 82;         // siege 1, morale boost
        private const int StefanSkellen = 117;      // close 9, spy
        private const int DunBannerMedic = 28;      // siege 5, medic
        // Heroes
        private const int Draug = 27;               // close 10
        private const int Imlerith = 77;            // close 10
        private const int Geralt = 67;              // close 15
        private const int Eithne = 31;              // range 10
        private const int MorvranVoorhis = 91;      // siege 10
        // Scorchers
        private const int Villentretenmerth = 135;  // close 7, scorches the enemy's close row
        private const int Toad = 121;               // range 7, the enemy's ranged row
        private const int Schirru = 108;            // siege 8, the enemy's siege row
        private const int DimunPirate = 153;        // range 6, the whole battlefield (but himself)

        private static CardDatabase Db { get { return TestBoard.Db; } }

        private static Board NewBoard()
        {
            return new Board { Player = TestBoard.Player(), Enemy = TestBoard.Player(Faction.Monsters, 40), Db = Db, Rnd = new Random(1) };
        }

        private static PlayOutcome Play(Board board, int card_id, Row row, bool by_player = true, bool manual_medic = false)
        {
            return PlayRules.Resolve(board, Db.Get(card_id), row, by_player, manual_medic);
        }

        //----------------------------------------The Scorch card----------------------------------------//
        [Test]
        public void ScorchSkipsAHeroOnTopForTheStrongestNonHeroes()
        {
            Board board = NewBoard();
            board.Enemy.CloseList.AddRange(new[] { Draug, Fiend, Botchling });
            board.Player.SiegeList.Add(Ballista);
            Play(board, CardIds.Scorch, Row.Weather);

            CollectionAssert.AreEqual(new[] { Draug, Botchling }, board.Enemy.CloseList, "the hero (10) stands, the strongest non-hero (6) burns");
            CollectionAssert.AreEqual(new[] { Fiend }, board.Enemy.DiscardList);
            Assert.IsEmpty(board.Player.SiegeList, "the player's own 6 burns with it");
            CollectionAssert.AreEquivalent(new[] { Ballista, CardIds.Scorch }, board.Player.DiscardList);
        }

        [Test]
        public void ScorchWithOnlyHeroesOnTheFieldBurnsNothing()
        {
            Board board = NewBoard();
            board.Enemy.CloseList.AddRange(new[] { Draug, Imlerith });
            board.Player.CloseList.Add(Geralt);
            Play(board, CardIds.Scorch, Row.Weather);

            CollectionAssert.AreEqual(new[] { Draug, Imlerith }, board.Enemy.CloseList);
            CollectionAssert.AreEqual(new[] { Geralt }, board.Player.CloseList);
            CollectionAssert.AreEqual(new[] { CardIds.Scorch }, board.Player.DiscardList, "only the Scorch card itself");
        }

        [Test]
        public void ScorchOnAnEmptyFieldBurnsNothing()
        {
            Board board = NewBoard();
            Play(board, CardIds.Scorch, Row.Weather);
            CollectionAssert.AreEqual(new[] { CardIds.Scorch }, board.Player.DiscardList);
            Assert.IsEmpty(board.Enemy.DiscardList);
        }

        [Test]
        public void ScorchBurnsEveryTieOnEveryRowOfBothSides()
        {
            Board board = NewBoard();
            board.Player.SiegeList.Add(Ballista);           // 6
            board.Player.RangeList.Add(BarclayEls);         // 6, agile, on the ranged row
            board.Enemy.CloseList.AddRange(new[] { Fiend, Botchling }); // 6, 4
            board.Enemy.SiegeList.Add(EarthElemental);      // 6
            board.Enemy.RangeList.Add(Eithne);              // a hero, 10
            Play(board, CardIds.Scorch, Row.Weather);

            Assert.IsEmpty(board.Player.SiegeList);
            Assert.IsEmpty(board.Player.RangeList);
            CollectionAssert.AreEqual(new[] { Botchling }, board.Enemy.CloseList);
            Assert.IsEmpty(board.Enemy.SiegeList);
            CollectionAssert.AreEqual(new[] { Eithne }, board.Enemy.RangeList);
        }

        [Test]
        public void ScorchGoesByTheStrengthShownUnderWeather()
        {
            Board board = NewBoard();
            board.Weather.Add(CardIds.ImpenetrableFog);
            board.Enemy.RangeList.Add(BlackInfantryArcher); // 10, 1 in the fog
            board.Player.SiegeList.Add(Ballista);           // 6
            Play(board, CardIds.Scorch, Row.Weather);

            CollectionAssert.AreEqual(new[] { BlackInfantryArcher }, board.Enemy.RangeList, "1 in the fog: not the strongest");
            Assert.IsEmpty(board.Player.SiegeList, "the 6 is the strongest");
        }

        [Test]
        public void ScorchGoesByTheStrengthShownUnderAHorn()
        {
            Board board = NewBoard();
            board.Enemy.CloseList.AddRange(new[] { Botchling, Fiend }); // 4 and 6, doubled: 8 and 12
            board.Enemy.SpCloseList.Add(CardIds.CommanderHorn);
            board.Enemy.SiegeList.Add(EarthElemental);                   // 6
            Play(board, CardIds.Scorch, Row.Weather);

            CollectionAssert.AreEqual(new[] { Botchling }, board.Enemy.CloseList, "the horned Fiend (12) burns");
            CollectionAssert.AreEqual(new[] { EarthElemental }, board.Enemy.SiegeList);
        }

        [Test]
        public void ScorchBurnsATightBondTogether()
        {
            Board board = NewBoard();
            board.Player.CloseList.AddRange(new[] { BlueStripes, BlueStripes, BlueStripes }); // 4 each, bonded: 12 each
            board.Enemy.RangeList.Add(BlackInfantryArcher);                                    // 10
            Play(board, CardIds.Scorch, Row.Weather);

            Assert.IsEmpty(board.Player.CloseList, "all three at 12");
            CollectionAssert.AreEqual(new[] { BlackInfantryArcher }, board.Enemy.RangeList);
        }

        [Test]
        public void ScorchGoesByTheStrengthShownWithMorale()
        {
            Board board = NewBoard();
            board.Player.SiegeList.AddRange(new[] { SiegeExpert, Ballista }); // the Ballista 7 with the morale
            board.Enemy.SiegeList.Add(EarthElemental);                        // 6
            Play(board, CardIds.Scorch, Row.Weather);

            CollectionAssert.AreEqual(new[] { SiegeExpert }, board.Player.SiegeList);
            CollectionAssert.AreEqual(new[] { EarthElemental }, board.Enemy.SiegeList);
        }

        [Test]
        public void ScorchBurnsASpyOnTheSideItLandedOn()
        {
            Board board = NewBoard();
            board.Player.CloseList.Add(StefanSkellen); // the enemy's spy, 9, on the player's side
            board.Enemy.CloseList.Add(Fiend);
            Play(board, CardIds.Scorch, Row.Weather);

            Assert.IsEmpty(board.Player.CloseList);
            CollectionAssert.Contains(board.Player.DiscardList, StefanSkellen, "to the discard pile of the side it was on");
            CollectionAssert.AreEqual(new[] { Fiend }, board.Enemy.CloseList);
        }

        //----------------------------------------Villentretenmerth, the Toad, Schirru----------------------------------------//
        [Test]
        public void TheDragonSkipsAHeroOnTopForTheStrongestNonHero()
        {
            Board board = NewBoard();
            board.Enemy.CloseList.AddRange(new[] { Draug, Fiend, Botchling }); // 20
            Play(board, Villentretenmerth, Row.Close);

            CollectionAssert.AreEqual(new[] { Draug, Botchling }, board.Enemy.CloseList);
            CollectionAssert.AreEqual(new[] { Fiend }, board.Enemy.DiscardList);
            CollectionAssert.AreEqual(new[] { Villentretenmerth }, board.Player.CloseList, "the dragon itself stays");
        }

        [Test]
        public void TheDragonCountsAHerosStrengthTowardTheRowsTen()
        {
            Board board = NewBoard();
            board.Enemy.CloseList.AddRange(new[] { Draug, Botchling }); // 14, of which 4 a scorch can burn
            Play(board, Villentretenmerth, Row.Close);
            CollectionAssert.AreEqual(new[] { Draug }, board.Enemy.CloseList);
        }

        [Test]
        public void TheDragonFindsNothingToBurnInARowOfHeroes()
        {
            Board board = NewBoard();
            board.Enemy.CloseList.AddRange(new[] { Draug, Imlerith }); // 20, all heroes
            Play(board, Villentretenmerth, Row.Close);
            CollectionAssert.AreEqual(new[] { Draug, Imlerith }, board.Enemy.CloseList);
            Assert.IsEmpty(board.Enemy.DiscardList);
        }

        [Test]
        public void TheDragonNeedsTenOnTheEnemysRow()
        {
            Board board = NewBoard();
            board.Enemy.CloseList.AddRange(new[] { Botchling, Arachas }); // 8
            Play(board, Villentretenmerth, Row.Close);
            CollectionAssert.AreEqual(new[] { Botchling, Arachas }, board.Enemy.CloseList, "under 10: nothing burns");

            board = NewBoard();
            board.Enemy.CloseList.AddRange(new[] { Fiend, Botchling }); // exactly 10
            Play(board, Villentretenmerth, Row.Close);
            CollectionAssert.AreEqual(new[] { Botchling }, board.Enemy.CloseList);
        }

        [Test]
        public void TheDragonCountsTheRowAsShownUnderFrost()
        {
            Board board = NewBoard();
            board.Weather.Add(CardIds.BitingFrost);
            board.Enemy.CloseList.AddRange(new[] { Fiend, Botchling, Draug }); // 1 + 1 + 10 (a hero feels no frost)
            Play(board, Villentretenmerth, Row.Close);
            CollectionAssert.AreEqual(new[] { Draug }, board.Enemy.CloseList, "12 on the row: both units at 1 burn");

            board = NewBoard();
            board.Weather.Add(CardIds.BitingFrost);
            board.Enemy.CloseList.AddRange(new[] { Fiend, Cahir, Botchling }); // 16 without the frost, 3 with it
            Play(board, Villentretenmerth, Row.Close);
            Assert.AreEqual(3, board.Enemy.CloseList.Count, "3 on the row as shown: nothing burns");
        }

        [Test]
        public void TheDragonBurnsEveryTieOnTheRow()
        {
            Board board = NewBoard();
            board.Enemy.CloseList.AddRange(new[] { Fiend, Cahir, Botchling }); // 6, 6, 4
            Play(board, Villentretenmerth, Row.Close);
            CollectionAssert.AreEqual(new[] { Botchling }, board.Enemy.CloseList);
        }

        [Test]
        public void TheDragonBurnsATightBondTogether()
        {
            Board board = NewBoard();
            board.Enemy.CloseList.AddRange(new[] { ClanAnCraite, ClanAnCraite, ClanAnCraite, Draug }); // 18 each, and a hero
            Play(board, Villentretenmerth, Row.Close);
            CollectionAssert.AreEqual(new[] { Draug }, board.Enemy.CloseList);
        }

        [Test]
        public void TheDragonBurnsAnAgileUnitAndASpyOnItsRow()
        {
            Board board = NewBoard();
            board.Enemy.CloseList.AddRange(new[] { BarclayEls, Botchling }); // agile 6, 4
            Play(board, Villentretenmerth, Row.Close);
            CollectionAssert.AreEqual(new[] { Botchling }, board.Enemy.CloseList);

            board = NewBoard();
            board.Enemy.CloseList.AddRange(new[] { StefanSkellen, Fiend }); // the player's spy (9) on the enemy's row, 6
            Play(board, Villentretenmerth, Row.Close);
            CollectionAssert.AreEqual(new[] { Fiend }, board.Enemy.CloseList);
        }

        [Test]
        public void TheDragonOnlyBurnsTheOpponentsRow()
        {
            Board board = NewBoard();
            board.Player.CloseList.AddRange(new[] { BlueStripes, BlueStripes, BlueStripes }); // 36 on the player's own row
            board.Enemy.CloseList.AddRange(new[] { Fiend, Botchling });
            board.Enemy.RangeList.Add(BlackInfantryArcher);                                    // 10 on another row
            Play(board, Villentretenmerth, Row.Close);

            CollectionAssert.AreEqual(new[] { BlueStripes, BlueStripes, BlueStripes, Villentretenmerth }, board.Player.CloseList);
            CollectionAssert.AreEqual(new[] { Botchling }, board.Enemy.CloseList);
            CollectionAssert.AreEqual(new[] { BlackInfantryArcher }, board.Enemy.RangeList);
        }

        [Test]
        public void TheOpponentsDragonBurnsThePlayersRow()
        {
            Board board = NewBoard();
            board.Player.CloseList.AddRange(new[] { Geralt, Cahir, Botchling }); // a hero (15), 6, 4
            Play(board, Villentretenmerth, Row.Close, by_player: false);

            CollectionAssert.AreEqual(new[] { Geralt, Botchling }, board.Player.CloseList);
            CollectionAssert.AreEqual(new[] { Villentretenmerth }, board.Enemy.CloseList);
        }

        [Test]
        public void TheToadAndSchirruBurnTheirRowsPastAHero()
        {
            Board board = NewBoard();
            board.Enemy.RangeList.AddRange(new[] { Eithne, Assire, Botchling }); // a hero (10), 6; the Botchling counts as 4 there too
            Play(board, Toad, Row.Range);
            CollectionAssert.AreEqual(new[] { Eithne, Botchling }, board.Enemy.RangeList);

            board = NewBoard();
            board.Enemy.SiegeList.AddRange(new[] { MorvranVoorhis, EarthElemental }); // a hero (10), 6
            Play(board, Schirru, Row.Siege);
            CollectionAssert.AreEqual(new[] { MorvranVoorhis }, board.Enemy.SiegeList);
        }

        [Test]
        public void ADragonAMedicBringsBackScorchesAgain()
        {
            // The medic's pick made by the side (the AI's way), and chosen (the player's: the choice plays it the same way)
            foreach (bool chosen in new[] { false, true })
            {
                Board board = NewBoard();
                board.Player.DiscardList.Add(Villentretenmerth);
                board.Enemy.CloseList.AddRange(new[] { Draug, Fiend });
                PlayOutcome outcome = Play(board, DunBannerMedic, Row.Siege, manual_medic: chosen);
                if (chosen)
                {
                    Assert.AreEqual(PlayOutcome.WaitingForMedicChoice, outcome);
                    board.Player.DiscardList.Remove(Villentretenmerth);
                    Play(board, Villentretenmerth, Row.Close, manual_medic: true);
                }
                CollectionAssert.AreEqual(new[] { Villentretenmerth }, board.Player.CloseList, chosen ? "chosen" : "the side's pick");
                CollectionAssert.AreEqual(new[] { Draug }, board.Enemy.CloseList, chosen ? "chosen" : "the side's pick");
            }
        }

        [Test]
        public void ADecoyedDragonPlayedAgainScorchesAgain()
        {
            Board board = NewBoard();
            board.Enemy.CloseList.AddRange(new[] { Fiend, Botchling }); // 10
            Play(board, Villentretenmerth, Row.Close);
            CollectionAssert.AreEqual(new[] { Botchling }, board.Enemy.CloseList);

            PlayRules.Decoy(board, Villentretenmerth, Row.Close, true, CardIds.Decoy);
            CollectionAssert.Contains(board.Player.HandList, Villentretenmerth);
            board.Player.HandList.Remove(Villentretenmerth);
            board.Enemy.CloseList.AddRange(new[] { Cahir, Arachas }); // 4 + 6 + 4
            Play(board, Villentretenmerth, Row.Close);
            CollectionAssert.AreEqual(new[] { Botchling, Arachas }, board.Enemy.CloseList);
        }

        //----------------------------------------The leaders----------------------------------------//
        [Test]
        public void ALeadersScorchSkipsAHeroOnTop()
        {
            foreach ((int leader, Row row, int hero, int unit) in new[]
            {
                (LeaderIds.FrancescaQueenOfDolBlathanna, Row.Close, Draug, Fiend),
                (LeaderIds.FoltestSonOfMedell, Row.Range, Eithne, Assire),
                (LeaderIds.FoltestTheSteelForged, Row.Siege, MorvranVoorhis, EarthElemental),
            })
            {
                Board board = NewBoard();
                board.Enemy.UnitRow(row).AddRange(new[] { hero, unit, Botchling }); // a hero (10), 6, 4
                Assert.IsNull(LeaderRules.Unavailable(board, leader, true));
                Assert.IsTrue(LeaderRules.ApplyInstant(board, leader, true, out string reason), reason);
                CollectionAssert.AreEqual(new[] { hero, Botchling }, board.Enemy.UnitRow(row), "leader " + leader);
            }
        }

        [Test]
        public void ALeadersScorchCountsAHeroTowardTenButNeedsAUnitToBurn()
        {
            Board board = NewBoard();
            board.Enemy.CloseList.AddRange(new[] { Draug, Botchling }); // 14
            Assert.IsTrue(LeaderRules.ApplyInstant(board, LeaderIds.FrancescaQueenOfDolBlathanna, true, out string reason), reason);
            CollectionAssert.AreEqual(new[] { Draug }, board.Enemy.CloseList);

            board = NewBoard();
            board.Enemy.CloseList.AddRange(new[] { Draug, Imlerith }); // 20, all heroes: the ability says why nothing would burn
            StringAssert.Contains("only heroes", LeaderRules.Unavailable(board, LeaderIds.FrancescaQueenOfDolBlathanna, true));
            board.Enemy.CloseList.Clear();
            board.Enemy.CloseList.Add(Fiend);
            StringAssert.Contains("10 or more", LeaderRules.Unavailable(board, LeaderIds.FrancescaQueenOfDolBlathanna, true));
        }

        //----------------------------------------The Clan Dimun Pirate----------------------------------------//
        [Test]
        public void ThePirateSkipsAHeroOnTopAndLands()
        {
            Board board = NewBoard();
            board.Enemy.CloseList.AddRange(new[] { Geralt, Cahir }); // a hero (15), 6
            board.Player.SiegeList.Add(Ballista);                    // 6
            Play(board, DimunPirate, Row.Range);

            CollectionAssert.AreEqual(new[] { Geralt }, board.Enemy.CloseList);
            Assert.IsEmpty(board.Player.SiegeList);
            CollectionAssert.AreEqual(new[] { DimunPirate }, board.Player.RangeList, "he lands after his scorch (6 too)");
        }

        [Test]
        public void ThePirateAmongHeroesBurnsNothingAndLands()
        {
            Board board = NewBoard();
            board.Enemy.CloseList.AddRange(new[] { Draug, Imlerith });
            Play(board, DimunPirate, Row.Range);
            CollectionAssert.AreEqual(new[] { Draug, Imlerith }, board.Enemy.CloseList);
            CollectionAssert.AreEqual(new[] { DimunPirate }, board.Player.RangeList);
            Assert.IsEmpty(board.Player.DiscardList);
        }
    }
}

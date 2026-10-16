using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Gwent.Rules.Tests
{
    // The match (Match): a battle from its setup and the sides' commands. Whole battles with the AI deciding for both
    // sides; a battle replayed from its commands; what each side sees of it; commands it refuses; the choices it asks.
    public class MatchTests
    {
        private static CardDatabase Db { get { return TestBoard.Db; } }

        // Resources/Cards/AI_Monsters.json
        public static Deck Monsters(int leader = LeaderIds.EredinKingOfTheWildHunt)
        {
            return new Deck
            {
                Faction = Faction.Monsters,
                Name = "Monsters Slay",
                Leader = leader,
                Cards = new List<int> { 67, 14, 77, 83, 85, 27, 125, 126, 127, 128, 129, 1, 1, 1, 2, 18, 19, 20, 47, 30, 22, 135, 92, 94, 94, 94, 201, 202, 204 }
            };
        }

        //----------------------------------------Playing a match out----------------------------------------//
        // The AI's move or answer for the side the match waits for (its redraw: the cards it would redraw, then done)
        public static MatchCommand AiCommand(Match match, bool seat, int seed, AiDifficulty difficulty = AiDifficulty.Normal)
        {
            PlayerState side = match.Board.Side(seat);
            switch (match.Phase)
            {
                case MatchPhase.Redraw:
                    foreach (int card_id in AiBrain.Redraws(side, Db, difficulty))
                        if (side.CardsExchanged < 2 && side.HandList.Contains(card_id))
                            return MatchCommand.Redraw(card_id);
                    return MatchCommand.EndRedraw();
                case MatchPhase.Turn:
                    AiContext ctx = match.Board.AiContextFor(seat);
                    ctx.Difficulty = difficulty;
                    ctx.Seed = seed;
                    return MatchCommand.From(AiBrain.Decide(ctx));
                default:
                    return AiBrain.Answer(match.Board, seat, match.Choice);
            }
        }

        // Plays the match to its end, the AI deciding for both sides; after_each: a check after every command
        public static void PlayOut(Match match, int seed, Action<MatchCommand> after_each = null)
        {
            Random rnd = new Random(seed);
            match.Start();
            for (int step = 0; !match.IsOver; step++)
            {
                Assert.Less(step, 2000, "the battle never ended");
                bool seat = match.Awaits(true) || !match.Awaits(false) && rnd.Next(2) == 0;
                if (!match.Awaits(seat))
                    seat = !seat;
                Assert.IsTrue(match.Awaits(seat), "the match waits for nobody in " + match.Phase);
                MatchCommand command = AiCommand(match, seat, rnd.Next());
                Assert.IsTrue(match.Apply(seat, command), (seat ? "player " : "enemy ") + command + " refused in " + match.Phase
                    + " (" + (match.Choice != null ? match.Choice.Kind.ToString() : "-") + ")");
                after_each?.Invoke(command);
            }
        }

        public static int CountCards(Board board)
        {
            return new[] { board.Player, board.Enemy }.Sum(p => p.AllCards().Count(id => !CardIds.IsSummoned(id)) - p.LeaderHornRows.Count);
        }

        private static Match New(Deck player, Deck enemy, int seed)
        {
            return new Match(new MatchSetup { Player = player, Enemy = enemy, Seed = seed }, Db);
        }

        // Started, both hands kept: the first round under way
        private static Match Playing(Deck player, Deck enemy, int seed = 1)
        {
            Match match = New(player, enemy, seed);
            match.Start();
            if (match.Phase == MatchPhase.Opening)
                Assert.IsTrue(match.Apply(match.Choice.Seat, MatchCommand.ChooseFirst(true)));
            Assert.IsTrue(match.Apply(true, MatchCommand.EndRedraw()));
            Assert.IsTrue(match.Apply(false, MatchCommand.EndRedraw()));
            Assert.AreEqual(MatchPhase.Turn, match.Phase);
            return match;
        }

        // The player's turn, whoever started
        private static Match PlayersTurn(Deck player, Deck enemy)
        {
            for (int seed = 1; ; seed++)
            {
                Match match = Playing(player, enemy, seed);
                if (match.Turn)
                    return match;
            }
        }

        //----------------------------------------Whole battles----------------------------------------//
        [Test]
        public void AiBattlesEndAndKeepEveryCard()
        {
            for (int game = 0; game < 30; game++)
            {
                Match match = New(Monsters(), Monsters(game % 2 == 0 ? LeaderIds.EredinKingOfTheWildHunt : LeaderIds.EredinBringerOfDeath), game);
                int expected = -1;
                PlayOut(match, game, command =>
                {
                    if (expected < 0)
                        expected = CountCards(match.Board);
                    Assert.AreEqual(expected, CountCards(match.Board), "cards lost or made by " + command + " (seed " + game + ")");
                });
                Assert.AreEqual(MatchEventKind.Ended, match.Events.Last().Kind);
                Assert.LessOrEqual(match.Round, 3);
            }
        }

        [Test]
        public void ABattleReplaysTheSameFromItsSetupAndCommands()
        {
            Match match = New(Monsters(), Monsters(LeaderIds.EredinBringerOfDeath), 42);
            PlayOut(match, 42);
            Match replayed = Match.Replay(match.Setup, Db, match.Commands);
            Assert.AreEqual(match.Events.Count, replayed.Events.Count);
            Assert.AreEqual(match.Events.Select(e => e.ToString()), replayed.Events.Select(e => e.ToString()));
            Assert.AreEqual(Signature(match.Snapshot()), Signature(replayed.Snapshot()));
            Assert.AreEqual(match.Result, replayed.Result);
        }

        [Test]
        public void TheSeedDecidesTheShuffle()
        {
            Match a = New(Monsters(), Monsters(), 5), b = New(Monsters(), Monsters(), 5), c = New(Monsters(), Monsters(), 6);
            a.Start();
            b.Start();
            c.Start();
            CollectionAssert.AreEqual(a.Board.Player.HandList, b.Board.Player.HandList);
            CollectionAssert.AreEqual(a.Board.Enemy.DeckList, b.Board.Enemy.DeckList);
            CollectionAssert.AreNotEqual(a.Board.Player.DeckList.Concat(a.Board.Player.HandList), c.Board.Player.DeckList.Concat(c.Board.Player.HandList));
        }

        private static string Signature(MatchState state)
        {
            return string.Join("|", new[] { state.Player, state.Enemy }.Select(p => string.Join(";",
                string.Join(",", p.HandList), string.Join(",", p.DeckList), string.Join(",", p.DiscardList),
                string.Join(",", p.CloseList), string.Join(",", p.RangeList), string.Join(",", p.SiegeList), p.Lives)));
        }

        //----------------------------------------On the wire----------------------------------------//
        [Test]
        public void EveryEventAsTheGuestSeesItTravelsAsBytesUnchanged()
        {
            Match match = New(Monsters(), Monsters(LeaderIds.EredinBringerOfDeath), 11);
            PlayOut(match, 11);
            int largest = 0;
            foreach (MatchEvent e in match.Events)
            {
                byte[] sent = MatchWire.Bytes(w => MatchWire.Write(w, e.For(false)));
                MatchEvent received = MatchWire.ReadEvent(new System.IO.BinaryReader(new System.IO.MemoryStream(sent)));
                CollectionAssert.AreEqual(sent, MatchWire.Bytes(w => MatchWire.Write(w, received)), e.ToString());
                largest = Math.Max(largest, sent.Length);
            }
            Assert.Less(largest, 32 * 1024, "an event fits a LAN message");

            byte[] setup = MatchWire.Bytes(w => MatchWire.Write(w, match.Setup));
            MatchSetup read = MatchWire.ReadSetup(new System.IO.BinaryReader(new System.IO.MemoryStream(setup)));
            Assert.AreEqual(match.Setup.Seed, read.Seed);
            CollectionAssert.AreEqual(match.Setup.Enemy.Cards, read.Enemy.Cards);
            Assert.AreEqual(match.Setup.Enemy.Leader, read.Enemy.Leader);
        }

        //----------------------------------------What a side sees----------------------------------------//
        [Test]
        public void ASideSeesItsHandButNotTheOpponentsNorAnyDecksOrder()
        {
            Match match = New(Monsters(), Monsters(LeaderIds.EredinBringerOfDeath), 3);
            match.Start();
            MatchState mine = match.Events[0].State.For(true);
            CollectionAssert.AreEqual(match.Board.Player.HandList, mine.Player.HandList);
            Assert.That(mine.Enemy.HandList, Has.All.EqualTo(CardIds.Hidden));
            Assert.AreEqual(match.Board.Enemy.HandList.Count, mine.Enemy.HandList.Count);
            Assert.That(mine.Enemy.DeckList, Has.All.EqualTo(CardIds.Hidden));
            CollectionAssert.AreEquivalent(match.Board.Player.DeckList, mine.Player.DeckList);
            CollectionAssert.IsOrdered(mine.Player.DeckList);

            // The other side sees itself as the Player
            MatchState theirs = match.Events[0].State.For(false);
            CollectionAssert.AreEqual(match.Board.Enemy.HandList, theirs.Player.HandList);
            Assert.That(theirs.Enemy.HandList, Has.All.EqualTo(CardIds.Hidden));
            Assert.AreEqual(match.Board.Enemy.Leader, theirs.Player.Leader);
        }

        [Test]
        public void TheOpponentsRedrawsStayHidden()
        {
            Match match = New(Monsters(), Monsters(), 4);
            match.Start();
            if (match.Phase == MatchPhase.Opening)
                match.Apply(match.Choice.Seat, MatchCommand.ChooseFirst(true));
            int card_id = match.Board.Player.HandList[0];
            Assert.IsTrue(match.Apply(true, MatchCommand.Redraw(card_id, 0)));
            MatchEvent redrawn = match.Events.Last();
            Assert.AreEqual(MatchEventKind.Redrawn, redrawn.Kind);
            Assert.AreEqual(card_id, redrawn.For(true).Card);
            Assert.AreEqual(CardIds.Hidden, redrawn.For(false).Card);
            Assert.AreEqual(CardIds.Hidden, redrawn.For(false).Other);
            Assert.IsFalse(redrawn.For(false).Seat, "the guest sees the host's redraw as its opponent's");
        }

        [Test]
        public void ACardBothSidesSawGoBackIntoAHandStaysSeen()
        {
            Match match = PlayersTurn(Monsters(), Monsters());
            PlayerState me = match.Board.Player;
            me.HandList.Add(CardIds.Decoy);
            me.CloseList.Add(8); // Botchling
            match.Board.Recalculate();
            Assert.IsTrue(match.Apply(true, MatchCommand.Decoy(CardIds.Decoy, 8, Row.Close)));
            MatchState seen = match.Events.Last(e => e.Kind == MatchEventKind.Played).State.For(false);
            Assert.AreEqual(1, seen.Enemy.HandList.Count(id => id == 8), "the decoyed Botchling shows in the hand");
            Assert.AreEqual(me.HandList.Count - 1, seen.Enemy.HandList.Count(id => id == CardIds.Hidden));
        }

        //----------------------------------------Refused commands----------------------------------------//
        [Test]
        public void CommandsThatAreNotAllowedChangeNothing()
        {
            Match match = PlayersTurn(Monsters(), Monsters());
            int events = match.Events.Count;
            PlayerState me = match.Board.Player;
            int card_id = me.HandList[0];
            Assert.IsFalse(match.Apply(false, MatchCommand.Pass()), "not its turn");
            Assert.IsFalse(match.Apply(false, MatchCommand.Play(match.Board.Enemy.HandList[0], Row.Close)), "not its turn");
            Assert.IsFalse(match.Apply(true, MatchCommand.Play(999, Row.Close)), "a card it does not hold");
            Assert.IsFalse(match.Apply(true, MatchCommand.Redraw(card_id)), "the redraw is over");
            Assert.IsFalse(match.Apply(true, MatchCommand.Choose(card_id)), "nothing to choose");
            Assert.IsFalse(match.Apply(true, MatchCommand.Discard(card_id)), "nothing to discard");
            me.HandList.Add(CardIds.CommanderHorn);
            me.SpCloseList.Add(CardIds.Mardroeme);
            Assert.IsFalse(match.Apply(true, MatchCommand.Play(CardIds.CommanderHorn, Row.Close)), "a horn on a taken slot");
            me.HandList.Add(CardIds.Decoy);
            me.CloseList.Add(67); // Geralt
            Assert.IsFalse(match.Apply(true, MatchCommand.Decoy(CardIds.Decoy, 67, Row.Close)), "a decoy on a hero");
            me.HandList.Add(5); // Barclay Els, agile
            Assert.IsFalse(match.Apply(true, MatchCommand.Play(5, Row.Siege)), "an agile unit on the siege row");
            Assert.AreEqual(events, match.Events.Count);
            Assert.IsEmpty(match.Commands.Where(c => c.command.Kind != CommandKind.EndRedraw && c.command.Kind != CommandKind.ChooseFirst));
        }

        //----------------------------------------Choices----------------------------------------//
        [Test]
        public void AMedicAsksForItsUnitAndPlaysIt()
        {
            Match match = PlayersTurn(Monsters(), Monsters());
            PlayerState me = match.Board.Player;
            me.HandList.Add(28);    // Dun Banner Medic
            me.DiscardList.Add(4);  // Ballista
            Assert.IsTrue(match.Apply(true, MatchCommand.Play(28, Row.Siege)));
            Assert.AreEqual(MatchPhase.Choice, match.Phase);
            Assert.AreEqual(ChoiceKind.Revive, match.Choice.Kind);
            CollectionAssert.Contains(match.Choice.Options, 4);
            Assert.IsEmpty(match.Events.Last().State.For(false).Choice.Options, "the opponent does not see the options");
            Assert.IsFalse(match.Apply(false, MatchCommand.Choose(4)), "not the opponent's choice");
            Assert.IsFalse(match.Apply(true, MatchCommand.Pass()), "the choice first");

            Assert.IsTrue(match.Apply(true, MatchCommand.Choose(4, Row.Siege)));
            MatchEvent played = match.Events.Last(e => e.Kind == MatchEventKind.Played);
            Assert.AreEqual(4, played.Card);
            Assert.AreEqual(PlaySource.Discard, played.From);
            CollectionAssert.Contains(me.SiegeList, 4);
            CollectionAssert.DoesNotContain(me.DiscardList, 4);
            Assert.IsFalse(match.Awaits(true), "the turn went on");
        }

        [Test]
        public void DestroyerOfWorldsDiscardsTwoThenTakesACardOfTheDeck()
        {
            Match match = PlayersTurn(Monsters(LeaderIds.EredinDestroyerOfWorlds), Monsters());
            PlayerState me = match.Board.Player;
            Assert.IsTrue(match.Apply(true, MatchCommand.Leader()));
            Assert.AreEqual(ChoiceKind.Discard, match.Choice.Kind);
            Assert.IsFalse(match.Apply(true, MatchCommand.Skip()), "the two discards are not skipped");
            int hand = me.HandList.Count;
            Assert.IsTrue(match.Apply(true, MatchCommand.Discard(me.HandList[0], 0)));
            Assert.AreEqual(ChoiceKind.Discard, match.Choice.Kind);
            Assert.IsTrue(match.Apply(true, MatchCommand.Discard(me.HandList[0], 0)));
            Assert.AreEqual(ChoiceKind.DeckPick, match.Choice.Kind);
            CollectionAssert.AreEquivalent(me.DeckList, match.Choice.Options);
            int picked = match.Choice.Options[0];
            Assert.IsTrue(match.Apply(true, MatchCommand.Choose(picked)));
            Assert.AreEqual(hand - 1, me.HandList.Count);
            MatchEvent taken = match.Events.Last(e => e.Kind == MatchEventKind.Picked);
            Assert.AreEqual(picked, taken.For(true).Card);
            Assert.AreEqual(CardIds.Hidden, taken.For(false).Card, "the opponent does not see the card taken");
            Assert.IsFalse(me.canLeader);
        }

        [Test]
        public void EmperorOfNilfgaardShowsThreeOfTheOpponentsCardsToItsSideOnly()
        {
            Match match = PlayersTurn(new Deck { Faction = Faction.Nilfgaard, Leader = LeaderIds.EmhyrEmperorOfNilfgaard, Cards = Monsters().Cards }, Monsters());
            Assert.IsTrue(match.Apply(true, MatchCommand.Leader()));
            Assert.AreEqual(ChoiceKind.LookAtHand, match.Choice.Kind);
            Assert.AreEqual(3, match.Choice.Options.Count);
            CollectionAssert.IsSubsetOf(match.Choice.Options, match.Board.Enemy.HandList);
            Assert.IsEmpty(match.Events.Last().State.For(false).Choice.Options);
            Assert.IsTrue(match.Apply(true, MatchCommand.Choose(-1)));
            Assert.IsTrue(match.Events.Any(e => e.Kind == MatchEventKind.Skipped), "the look ended");
            Assert.IsFalse(match.Awaits(true));
        }

        [Test]
        public void GivingUpEndsTheBattleForTheOtherSide()
        {
            Match match = PlayersTurn(Monsters(), Monsters());
            Assert.IsTrue(match.Apply(false, MatchCommand.GiveUp()));
            Assert.IsTrue(match.IsOver);
            MatchEvent ended = match.Events.Last();
            Assert.AreEqual(MatchEventKind.Ended, ended.Kind);
            Assert.IsTrue(ended.Flag);
            Assert.AreEqual(RoundWinner.Player, ended.Winner);
            Assert.AreEqual(RoundWinner.Enemy, ended.For(false).Winner, "seen from the side that gave up");
            Assert.IsFalse(match.Apply(true, MatchCommand.Pass()), "nothing after the end");
        }

        [Test]
        public void AScoiataelSideChoosesWhoGoesFirst()
        {
            Deck scoiatael = new Deck { Faction = Faction.Scoiatael, Leader = LeaderIds.ByFaction[Faction.Scoiatael][0], Cards = Monsters().Cards };
            Match match = New(Monsters(), scoiatael, 9);
            match.Start();
            Assert.AreEqual(MatchPhase.Opening, match.Phase);
            Assert.IsTrue(match.Awaits(false));
            Assert.IsFalse(match.Apply(true, MatchCommand.ChooseFirst(true)), "not the player's perk");
            Assert.IsTrue(match.Apply(false, MatchCommand.ChooseFirst(false)));
            MatchEvent first = match.Events.Last();
            Assert.AreEqual(MatchEventKind.FirstPlayer, first.Kind);
            Assert.IsTrue(first.Seat, "the Scoia'tael side let the player go first");
            Assert.IsTrue(first.Flag);
            Assert.AreEqual(MatchPhase.Redraw, match.Phase);
        }
    }
}

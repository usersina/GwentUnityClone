using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Gwent.Rules.Tests
{
    // Fair play in a LAN battle (FairPlay): the seed both devices make, and the guest's check of the battle's record once
    // it is over. Battles played out by the AI for both sides, the host's match as it runs them; the guest's side as the
    // link gives it (its events through bytes). An honest record passes; a host that chose the seed alone, changed a deck,
    // showed other events, broke a rule, or made or dropped a move of the guest's is found out.
    public class FairPlayTests
    {
        private static CardDatabase Db { get { return TestBoard.Db; } }

        // A battle as a LAN host runs it, and what its guest knows of it
        private sealed class Played
        {
            public Match Match;
            public int Seed;            // the one both made
            public Deck GuestDeck;      // as the guest brought it
            public Deck HostShown;      // the host's faction and leader, as shown in the room
            public List<MatchEvent> Shown = new List<MatchEvent>();
            public List<FairPlay.Sent> Sent = new List<FairPlay.Sent>();

            public List<(bool seat, MatchCommand command)> Commands { get { return Match.Commands; } }

            public string Check(int? seed = -1, MatchSetup setup = null, List<(bool, MatchCommand)> commands = null, Deck guest_deck = null, Deck host_shown = null,
                List<FairPlay.Sent> sent = null, bool sent_known = true)
            {
                return FairPlay.Check(setup ?? Match.Setup, commands ?? Commands, Shown, guest_deck ?? GuestDeck, host_shown ?? HostShown,
                    seed == -1 ? Seed : seed, sent_known ? sent ?? Sent : null, Db);
            }
        }

        private static Deck Copy(Deck deck)
        {
            return new Deck { Faction = deck.Faction, Name = deck.Name, Leader = deck.Leader, Cards = new List<int>(deck.Cards) };
        }

        // The AI plays both sides to the end (instead: a command of its own for the guest, in place of the AI's, by step);
        // the guest's moves are kept as it sends them, its events as they reach it
        private static Played Play(int game, Func<Match, int, MatchCommand?> instead = null, MatchSetup setup = null)
        {
            byte[] host_part = FairPlay.NewPart(), guest_part = FairPlay.NewPart();
            int seed = FairPlay.Seed(host_part, guest_part);
            Deck host = MatchTests.Monsters(), guest = MatchTests.Monsters(LeaderIds.EredinBringerOfDeath);
            Played played = new Played
            {
                Match = new Match(setup ?? new MatchSetup { Player = Copy(host), Enemy = Copy(guest), Seed = seed }, Db),
                Seed = seed,
                GuestDeck = guest,
                HostShown = new Deck { Faction = host.Faction, Leader = host.Leader },
            };
            Match match = played.Match;
            Random rnd = new Random(game);
            match.Start();
            for (int step = 0; !match.IsOver; step++)
            {
                Assert.Less(step, 2000, "the battle never ended");
                bool seat = match.Awaits(true) || !match.Awaits(false) && rnd.Next(2) == 0;
                if (!match.Awaits(seat))
                    seat = !seat;
                MatchCommand command = (!seat ? instead?.Invoke(match, step) : null) ?? MatchTests.AiCommand(match, seat, rnd.Next());
                Assert.IsTrue(match.Apply(seat, command), command + " refused");
                if (!seat && command.Kind != CommandKind.TimeOut)
                    played.Sent.Add(new FairPlay.Sent { Command = command });
            }
            foreach (MatchEvent e in match.Events)
            {
                byte[] bytes = MatchWire.Bytes(w => MatchWire.Write(w, e.For(false)));
                using (BinaryReader r = new BinaryReader(new MemoryStream(bytes)))
                    played.Shown.Add(MatchWire.ReadEvent(r));
            }
            return played;
        }

        //----------------------------------------The seed----------------------------------------//
        [Test]
        public void TheSeedIsBothPartsAndTheHostIsHeldToTheOneItPromised()
        {
            byte[] host = FairPlay.NewPart(), guest = FairPlay.NewPart(), other = FairPlay.NewPart();
            Assert.AreEqual(FairPlay.PartSize, host.Length);
            Assert.IsFalse(host.SequenceEqual(guest), "parts no one can guess");
            byte[] promise = FairPlay.Promise(host);
            Assert.AreEqual(32, promise.Length);
            Assert.IsTrue(FairPlay.Kept(promise, host));
            Assert.IsFalse(FairPlay.Kept(promise, other), "another part than the one promised");
            Assert.IsFalse(FairPlay.Kept(promise, host.Take(8).ToArray()), "part of it");
            Assert.IsFalse(FairPlay.Kept(null, host), "nothing promised");
            Assert.AreEqual(FairPlay.Seed(host, guest), FairPlay.Seed(host, guest), "both devices make the same seed");
            Assert.GreaterOrEqual(FairPlay.Seed(host, guest), 0);
            // Either part changes it: neither side alone decides it
            Assert.AreNotEqual(FairPlay.Seed(host, guest), FairPlay.Seed(other, guest));
            Assert.AreNotEqual(FairPlay.Seed(host, guest), FairPlay.Seed(host, other));
        }

        //----------------------------------------An honest battle----------------------------------------//
        [Test]
        public void AnHonestBattlePasses()
        {
            for (int game = 0; game < 12; game++)
            {
                Played played = Play(game);
                Assert.IsNull(played.Check(), "game " + game);
                Assert.IsNull(played.Check(seed: null, sent_known: false), "a battle taken up again: in part (game " + game + ")");
            }
        }

        [Test]
        public void TheHostsClockAndAClaimedWinAreNotMovesMadeForTheGuest()
        {
            // The guest out of time in its first turn: the host's TimeOut, not a move it made
            bool timed_out = false;
            Played clock = Play(1, (match, step) =>
            {
                if (timed_out || match.Phase != MatchPhase.Turn)
                    return null;
                timed_out = true;
                return MatchCommand.TimeOut();
            });
            Assert.IsTrue(timed_out);
            Assert.IsTrue(clock.Commands.Any(c => !c.seat && c.command.Kind == CommandKind.TimeOut));
            Assert.IsNull(clock.Check());

            // The guest away, the host claims the win (the guest's give-up, last)
            Played claimed = Play(2, (match, step) => match.Phase == MatchPhase.Turn && match.Round == 2 ? MatchCommand.GiveUp() : (MatchCommand?)null);
            List<FairPlay.Sent> sent = claimed.Sent.Take(claimed.Sent.Count - 1).ToList(); // the guest never sent it
            Assert.AreEqual(CommandKind.GiveUp, claimed.Commands.Last().command.Kind);
            Assert.IsNull(claimed.Check(sent: sent));
        }

        [Test]
        public void AMoveLostWithTheLinkMayBeMissing()
        {
            Played played = Play(3);
            List<FairPlay.Sent> sent = new List<FairPlay.Sent>(played.Sent);
            sent.Insert(4, new FairPlay.Sent { Command = MatchCommand.Pass(), Unsure = true }); // sent, then the link went
            Assert.IsNull(played.Check(sent: sent));
            sent[4] = new FairPlay.Sent { Command = MatchCommand.Pass() };
            Assert.IsNotNull(played.Check(sent: sent), "the host had it: it must be there");

            // The same move sent as the link went, then again once back: the record holds it once (the second copy)
            for (int at = 0; at < played.Sent.Count; at += 3)
            {
                List<FairPlay.Sent> twice = new List<FairPlay.Sent>(played.Sent);
                twice.Insert(at, new FairPlay.Sent { Command = played.Sent[at].Command, Unsure = true });
                Assert.IsNull(played.Check(sent: twice), "move " + at + " sent twice");
            }
            // Unsure moves left out at the end (no answer before the battle was over)
            List<FairPlay.Sent> late = new List<FairPlay.Sent>(played.Sent) { new FairPlay.Sent { Command = MatchCommand.Pass(), Unsure = true } };
            Assert.IsNull(played.Check(sent: late));
        }

        //----------------------------------------A host's game changed----------------------------------------//
        [Test]
        public void AShuffleTheHostChoseAloneIsFound()
        {
            Played honest = Play(4);
            Played chosen = Play(4, setup: new MatchSetup { Player = MatchTests.Monsters(), Enemy = MatchTests.Monsters(LeaderIds.EredinBringerOfDeath), Seed = 12345 });
            Assert.AreEqual("The shuffle is not the one both devices made.", chosen.Check(seed: honest.Seed));
        }

        [Test]
        public void ADeckNotAsBroughtOrShownOrUnlawfulIsFound()
        {
            Played played = Play(5);
            // The guest played with other cards than its own
            Deck brought = Copy(played.GuestDeck);
            brought.Cards[0] = brought.Cards[0] == 1 ? 2 : 1;
            Assert.AreEqual("Your deck is not the one you brought.", played.Check(guest_deck: brought));
            // The host's leader not the one it showed
            Deck shown = new Deck { Faction = played.HostShown.Faction, Leader = LeaderIds.EredinTheTreacherous };
            Assert.AreEqual("The host's deck is not the one it showed.", played.Check(host_shown: shown));
            // The host's deck against the rules (far too few units)
            MatchSetup small = new MatchSetup { Player = Copy(played.Match.Setup.Player), Enemy = played.Match.Setup.Enemy, Seed = played.Seed };
            small.Player.Cards = small.Player.Cards.Take(6).ToList();
            Assert.AreEqual("The host's deck breaks the deck rules.", played.Check(setup: small));
        }

        [Test]
        public void EventsNotAsTheRecordPlaysThemAreFound()
        {
            Played played = Play(6);
            // A card shown to the guest that the record does not play
            MatchEvent played_card = played.Shown.First(e => e.Kind == MatchEventKind.Played && e.Card >= 0);
            int was = played_card.Card;
            played_card.Card = was == 1 ? 2 : 1;
            Assert.AreEqual("The battle you were shown is not the one its record plays.", played.Check());
            played_card.Card = was;
            // A score shown that the record does not make
            MatchEvent ended = played.Shown.Last(e => e.Kind == MatchEventKind.RoundEnded);
            ended.PlayerTotal++;
            Assert.AreEqual("The battle you were shown is not the one its record plays.", played.Check());
            ended.PlayerTotal--;
            Assert.IsNull(played.Check(), "as it was");
        }

        [Test]
        public void ARecordAgainstTheRulesOrCutShortIsFound()
        {
            Played played = Play(7);
            // A card played that the hand never held
            List<(bool, MatchCommand)> unlawful = new List<(bool, MatchCommand)>(played.Commands);
            int first_play = unlawful.FindIndex(c => c.Item2.Kind == CommandKind.Play);
            (bool seat, MatchCommand play) = unlawful[first_play];
            play.CardId = 999;
            unlawful[first_play] = (seat, play);
            Assert.AreEqual("A move in the battle breaks the rules.", played.Check(commands: unlawful));
            // The record without its last moves
            List<(bool, MatchCommand)> cut = played.Commands.Take(played.Commands.Count - 3).ToList();
            Assert.AreEqual("The battle's record stops before its end.", played.Check(commands: cut));
            Assert.AreEqual("The battle's record is missing.", played.Check(setup: new MatchSetup()));
        }

        [Test]
        public void AMoveMadeForTheGuestOrDroppedIsFound()
        {
            Played played = Play(8);
            // The guest remembers fewer moves than the record gives it: one was made for it
            List<FairPlay.Sent> fewer = new List<FairPlay.Sent>(played.Sent);
            fewer.RemoveAt(fewer.Count / 2);
            Assert.AreEqual("A move was made for you that you didn't make.", played.Check(sent: fewer));
            // A move it made (and the host took) is not in the record
            List<FairPlay.Sent> more = new List<FairPlay.Sent>(played.Sent);
            more.Add(new FairPlay.Sent { Command = MatchCommand.Pass() });
            Assert.AreEqual("A move you made is missing from the battle.", played.Check(sent: more));
            // Another move in the record than the one the guest made
            List<FairPlay.Sent> other = new List<FairPlay.Sent>(played.Sent);
            int last = other.Count - 1;
            other[last] = new FairPlay.Sent { Command = other[last].Command.Kind == CommandKind.Pass ? MatchCommand.Play(1, Row.Close) : MatchCommand.Pass() };
            Assert.AreEqual("A move was made for you that you didn't make.", played.Check(sent: other));
        }
    }
}

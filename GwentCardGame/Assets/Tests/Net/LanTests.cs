using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Gwent.Net;
using NUnit.Framework;

namespace Gwent.Net.Tests
{
    // Two players on this computer (over its loopback network, real sockets): finding a game, joining it, what passes
    // between them and how a link ends. On ports apart from the game's (a test host running meanwhile is left alone).
    public class LanTests
    {
        private const ushort TestPort = 26960;
        private const ushort NobodyPort = 26969;
        private const string Version = "test-1";

        private readonly List<IDisposable> made = new List<IDisposable>();

        [TearDown]
        public void LetGo()
        {
            foreach (IDisposable thing in made)
                thing.Dispose();
            made.Clear();
        }

        private T Made<T>(T thing) where T : IDisposable
        {
            Assert.IsNotNull(thing);
            made.Add(thing);
            return thing;
        }

        // Updates them until the condition holds (false if it does not in time)
        private static bool Until(Func<bool> condition, double seconds, params object[] links)
        {
            Stopwatch waited = Stopwatch.StartNew();
            while (!condition())
            {
                if (waited.Elapsed.TotalSeconds > seconds)
                    return false;
                foreach (object link in links)
                    if (link is LanLink lan)
                        lan.Update();
                    else
                        ((LanDiscovery)link).Update();
                Thread.Sleep(2);
            }
            return true;
        }

        private (LanLink host, LanLink guest) Joined(string guest_version = Version)
        {
            LanLink host = Made(LanLink.Host(TestPort, Version));
            LanLink guest = Made(LanLink.Join("127.0.0.1", TestPort, guest_version));
            Until(() => host.State == LanLink.Status.Connected && guest.State == LanLink.Status.Connected
                || guest.State == LanLink.Status.Closed, 5, host, guest);
            return (host, guest);
        }

        [Test]
        public void AGuestJoinsAndMessagesGoBothWaysWholeAndInOrder()
        {
            (LanLink host, LanLink guest) = Joined();
            Assert.AreEqual(LanLink.Status.Connected, guest.State, guest.Reason);
            Assert.AreEqual(LanLink.Status.Connected, host.State);
            Assert.AreEqual(Version, host.OtherVersion);
            Assert.AreEqual("127.0.0.1", guest.OtherAddress);
            host.Received += message => host.Send(message); // sent back

            List<byte[]> back = new List<byte[]>();
            guest.Received += back.Add;
            byte[] largest = new byte[LanLink.Largest];
            new Random(3).NextBytes(largest);
            List<byte[]> sent = new List<byte[]> { new byte[] { 1 }, largest };
            for (int i = 0; i < 300; i++)
                sent.Add(BitConverter.GetBytes(i)); // more than the reliable window at once
            foreach (byte[] message in sent)
                Assert.IsTrue(guest.Send(message));
            Assert.IsTrue(Until(() => back.Count == sent.Count, 10, host, guest), back.Count + " of " + sent.Count + " came back");
            for (int i = 0; i < sent.Count; i++)
                CollectionAssert.AreEqual(sent[i], back[i], "message " + i);
            Assert.IsFalse(guest.Send(new byte[LanLink.Largest + 1]), "a message over the largest is refused");
        }

        [Test]
        public void TheHostSeesAGuestLeaveAtOnceAndTakesTheNext()
        {
            (LanLink host, LanLink guest) = Joined();
            string left = null;
            host.Disconnected += reason => left = reason;
            guest.Dispose();
            Assert.IsTrue(Until(() => left != null, 2, host), "the leaving was not seen");
            StringAssert.Contains("left", left);
            Assert.AreEqual(LanLink.Status.Waiting, host.State);

            LanLink next = Made(LanLink.Join("127.0.0.1", TestPort, Version));
            Assert.IsTrue(Until(() => next.State == LanLink.Status.Connected, 5, host, next), next.Reason);
        }

        [Test]
        public void AGuestOfAnotherVersionIsTurnedAway()
        {
            bool host_connected = false;
            (LanLink host, LanLink guest) = Joined("test-2");
            host.Connected += () => host_connected = true;
            Assert.AreEqual(LanLink.Status.Closed, guest.State);
            StringAssert.Contains("version", guest.Reason);
            Until(() => false, 0.3, host);
            Assert.IsFalse(host_connected);
            Assert.AreEqual(LanLink.Status.Waiting, host.State);
        }

        [Test]
        public void ASecondGuestIsTurnedAwayAndTheFirstStays()
        {
            (LanLink host, LanLink first) = Joined();
            LanLink second = Made(LanLink.Join("127.0.0.1", TestPort, Version));
            Assert.IsTrue(Until(() => second.State == LanLink.Status.Closed, 5, host, first, second), "the second guest was let in");
            StringAssert.Contains("under way", second.Reason);
            Assert.AreEqual(LanLink.Status.Connected, first.State);
            Assert.AreEqual(LanLink.Status.Connected, host.State);
        }

        [Test]
        public void JoiningWhereNoGameIsGivesUpInAFewSeconds()
        {
            LanLink guest = Made(LanLink.Join("127.0.0.1", NobodyPort, Version));
            Assert.IsTrue(Until(() => guest.State == LanLink.Status.Closed, 8, guest), "still joining after 8 s");
            StringAssert.Contains("No game", guest.Reason);
            Assert.IsNull(LanLink.Join("not an address", TestPort, Version));
        }

        [Test]
        public void ASearchFindsAHostedGameAndTellsWhetherItCanJoin()
        {
            LanDiscovery answering = Made(LanDiscovery.Answer("Test table", 44, TestPort, Version));
            LanDiscovery same = Made(LanDiscovery.Search(Version));
            LanDiscovery other = Made(LanDiscovery.Search("test-2"));
            Assert.IsTrue(Until(() => same.Games.Any(game => game.Name == "Test table") && other.Games.Any(game => game.Name == "Test table"),
                3, answering, same, other), "not found");
            LanDiscovery.Game found = same.Games.First(game => game.Name == "Test table");
            Assert.AreEqual(TestPort, found.Port);
            Assert.AreEqual(Version, found.Version);
            Assert.AreEqual(44, found.Leader);
            Assert.IsTrue(found.Compatible);
            Assert.IsFalse(other.Games.First(game => game.Name == "Test table").Compatible);
        }

        [Test]
        public void ANameKeepsToLettersDigitsAndAFewMarksUpToSixteen()
        {
            // As it is typed: what else is typed is left out, a space only between words (one last stays: a word follows)
            Assert.AreEqual("Géralt of Rivia", PlayerNames.Typed("  Géralt  of Rivia!"));
            Assert.AreEqual("Ciri ", PlayerNames.Typed("Ciri "));
            Assert.AreEqual("O'Dimm_the-1.b", PlayerNames.Typed("O'Dimm_the-1.<b>")); // no rich text tags
            Assert.AreEqual(16, PlayerNames.Typed(new string('x', 30)).Length);
            // To show: trimmed, and nothing when no letter or digit is left (a name in another alphabet, marks only)
            Assert.AreEqual("Ciri", PlayerNames.Clean("Ciri\n "));
            Assert.AreEqual("Zoltan", PlayerNames.Clean("Zoltan☠"));
            Assert.IsNull(PlayerNames.Clean("Жрец"));
            Assert.IsNull(PlayerNames.Clean("- . _"));
            Assert.IsNull(PlayerNames.Clean(null));
        }
    }
}

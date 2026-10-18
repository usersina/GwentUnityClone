using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
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
            StringAssert.Contains("another build", guest.Reason);
            StringAssert.Contains(Version, guest.Reason, "the host's build named");
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

        [Test]
        public void AMessageKeepsToWhatTheFontsShowUpToAHundred()
        {
            // As it is typed: Latin-1 kept (accents, symbols), the rest left out, one space between words (one last stays)
            Assert.AreEqual("\u00C7a va? Tr\u00E8s bien!", Chat.Typed("\u00C7a va? Tr\u00E8s bien!"));
            Assert.AreEqual("Stra\u00DFe 12 \u00BD dash", Chat.Typed("Stra\u00DFe 12 \u00BD \u2013 dash"));
            Assert.AreEqual("Skull and smile ", Chat.Typed("Skull \u2620 and smile \U0001F600 "));
            Assert.AreEqual("Tab and no-break", Chat.Typed("\tTab\u00A0and\u00A0no-break"));
            Assert.AreEqual("line one two", Chat.Typed("line one\r\ntwo")); // a line break is a space
            Assert.AreEqual("ab", Chat.Typed("a\u0007b")); // control characters
            Assert.AreEqual(Chat.MaxLength, Chat.Typed(new string('x', 150)).Length);
            // Tags stay as typed (shown as plain text)
            Assert.AreEqual("<b>not bold</b>", Chat.Clean(" <b>not bold</b> "));
            // To send or show: trimmed; nothing when nothing is left
            Assert.AreEqual("spaced out words", Chat.Clean("  spaced    out   words  "));
            Assert.IsNull(Chat.Clean(" \u2620\U0001F600 "));
            Assert.IsNull(Chat.Clean(null));
            Assert.IsNull(Chat.Clean("\u00AD\u00AD")); // soft hyphens show nothing
            // The quick lines keep to it too
            foreach (string line in Chat.Lines)
                Assert.AreEqual(line, Chat.Clean(line));
        }

        [Test]
        public void ANetworkIsToldByItsAdapterAndOnlyReachableOnesShow()
        {
            string Kind(string adapter, string description, NetworkInterfaceType type, string address, bool mac = false)
            {
                LanDiscovery.Network network = LanDiscovery.Describe(adapter, description, type, IPAddress.Parse(address), mac);
                return network == null ? null : network.Kind + (network.Vpn ? " (VPN)" : "");
            }

            // Around the device
            Assert.AreEqual("Wi-Fi", Kind("wlan0", "wlan0", NetworkInterfaceType.Wireless80211, "192.168.1.20"));
            Assert.AreEqual("Wi-Fi", Kind("Wi-Fi", "Intel(R) Wi-Fi 6 AX201", NetworkInterfaceType.Wireless80211, "100.70.1.2")); // a hotel's
            Assert.AreEqual("Ethernet", Kind("enp3s0", "enp3s0", NetworkInterfaceType.Ethernet, "10.0.0.5"));
            Assert.AreEqual("Network", Kind("en0", "en0", NetworkInterfaceType.Ethernet, "192.168.1.30", mac: true));
            Assert.AreEqual("Hotspot", Kind("swlan0", "swlan0", NetworkInterfaceType.Unknown, "192.168.43.1"));
            Assert.AreEqual("Hotspot", Kind("Local Area Connection* 10", "Microsoft Wi-Fi Direct Virtual Adapter #2", NetworkInterfaceType.Wireless80211, "192.168.137.1"));
            // VPNs, by name when they say it
            Assert.AreEqual("Tailscale (VPN)", Kind("tailscale0", "tailscale0", NetworkInterfaceType.Unknown, "100.101.7.3"));
            Assert.AreEqual("NetBird (VPN)", Kind("wt0", "wt0", NetworkInterfaceType.Unknown, "100.103.77.25"));
            Assert.AreEqual("ZeroTier (VPN)", Kind("ZeroTier One [8056c2e21c000001]", "ZeroTier Virtual Port", NetworkInterfaceType.Ethernet, "10.147.17.4"));
            Assert.AreEqual("Hamachi (VPN)", Kind("Hamachi", "LogMeIn Hamachi Virtual Ethernet Adapter", NetworkInterfaceType.Ethernet, "25.1.2.3"));
            Assert.AreEqual("VPN (VPN)", Kind("utun4", "utun4", NetworkInterfaceType.Unknown, "100.101.7.3", mac: true));
            Assert.AreEqual("VPN (VPN)", Kind("tun0", "tun0", NetworkInterfaceType.Unknown, "10.8.0.2"));
            // Not reachable from another device
            Assert.IsNull(Kind("docker0", "docker0", NetworkInterfaceType.Ethernet, "172.17.0.1"));
            Assert.IsNull(Kind("br-4d1e3da24da6", "br-4d1e3da24da6", NetworkInterfaceType.Ethernet, "172.20.0.1"));
            Assert.IsNull(Kind("vEthernet (WSL)", "Hyper-V Virtual Ethernet Adapter", NetworkInterfaceType.Ethernet, "172.28.0.1"));
            Assert.IsNull(Kind("rmnet_data0", "rmnet_data0", NetworkInterfaceType.Unknown, "100.88.4.2"));
            Assert.IsNull(Kind("Ethernet 2", "Realtek PCIe GbE", NetworkInterfaceType.Ethernet, "169.254.12.7"));
        }

        [Test]
        public void AnotherGamesVersionShowsShortAndPlain()
        {
            // What a battle found on the network says its version is, as its list entry shows it
            Assert.AreEqual("1.0.0", LanDiscovery.Shown("1.0.0"));
            Assert.AreEqual("bnot-bold/b", LanDiscovery.Shown("<b>not-bold</b>"));
            Assert.AreEqual("1.0.0+1a2b3c4d.5e6f70", LanDiscovery.Shown("1.0.0+1a2b3c4d.5e6f70"), "a build id whole");
            Assert.AreEqual("12345678901234567890123456789012", LanDiscovery.Shown("1234567890123456789012345678901234567890"));
            Assert.AreEqual("2.0", LanDiscovery.Shown(" 2.0\n\t"));
            Assert.AreEqual("?", LanDiscovery.Shown(""));
            Assert.AreEqual("?", LanDiscovery.Shown(null));
        }
    }
}

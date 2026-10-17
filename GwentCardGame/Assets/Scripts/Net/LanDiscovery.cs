using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEngine;

namespace Gwent.Net
{
    // Finding games on the local network. A player looking asks everyone, every second (a broadcast to the whole network,
    // and to this computer); each host answers the one asking: its name, its game's version and the port it waits on. A
    // game not heard from for a few seconds is gone from the list. A phone hosting holds Android's multicast lock while
    // it answers: without it, many phones never see a broadcast. Nothing runs on its own: the owner calls Update every
    // frame.
    public sealed class LanDiscovery : IDisposable
    {
        public const ushort Port = 26946;      // under the range systems hand out for a while (32768 and up)
        private const float AskEvery = 1f;
        private const float ForgetAfter = 3.5f;
        private static readonly byte[] Magic = { (byte)'G', (byte)'W', (byte)'N', (byte)'T' };
        private const byte Asking = 1, Answering = 2;

        // A game found: where to join it, and whether this game can (the same version)
        public sealed class Game
        {
            public string Name;
            public int Leader = -1;     // the host's deck's leader (its portrait in the list)
            public string Address;
            public ushort Port;
            public string Version;
            public bool Compatible;
            internal uint Id;       // the host's, for the session (one host heard on several addresses is one game)
            internal float Seen;
        }

        private readonly UdpClient socket;
        private readonly bool answering;
        private readonly string name;
        private readonly int leader;
        private readonly ushort gamePort;
        private readonly uint id;
        private readonly string version;
        private readonly List<Game> games = new List<Game>();
        private float asked = float.MinValue;
        private AndroidJavaObject multicastLock;

        public IReadOnlyList<Game> Games { get { return games; } }

        private LanDiscovery(UdpClient socket, bool answering, string name, int leader, ushort game_port, string version)
        {
            this.socket = socket;
            this.answering = answering;
            this.name = name;
            this.leader = leader;
            gamePort = game_port;
            this.version = version;
            id = (uint)Environment.TickCount ^ (uint)UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            socket.EnableBroadcast = true;
        }

        // Answers searches for the game waiting on that port, under that name, with the host's leader (null if the
        // discovery port cannot be had)
        public static LanDiscovery Answer(string name, int leader, ushort game_port)
        {
            return Answer(name, leader, game_port, Application.version);
        }

        internal static LanDiscovery Answer(string name, int leader, ushort game_port, string version)
        {
            UdpClient socket = new UdpClient(AddressFamily.InterNetwork);
            try
            {
                // Shared: two games hosted on one computer both hear the searches
                socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                socket.Client.Bind(new IPEndPoint(IPAddress.Any, Port));
            }
            catch (SocketException)
            {
                socket.Close();
                return null;
            }
            LanDiscovery discovery = new LanDiscovery(socket, true, name, leader, game_port, version);
            discovery.HoldMulticastLock();
            return discovery;
        }

        // Searches for games (the list fills as they answer)
        public static LanDiscovery Search()
        {
            return Search(Application.version);
        }

        internal static LanDiscovery Search(string version)
        {
            return new LanDiscovery(new UdpClient(0, AddressFamily.InterNetwork), false, null, -1, 0, version);
        }

        public void Update()
        {
            float now = Time.realtimeSinceStartup;
            if (!answering && now - asked >= AskEvery)
            {
                asked = now;
                Ask();
            }
            try
            {
                while (socket.Available > 0)
                {
                    IPEndPoint from = new IPEndPoint(IPAddress.Any, 0);
                    byte[] packet = socket.Receive(ref from);
                    Heard(packet, from, now);
                }
            }
            catch (SocketException)
            {
                // A send that bounced (Windows reports it on the next receive): nothing to read
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            games.RemoveAll(game => now - game.Seen > ForgetAfter);
        }

        private void Ask()
        {
            byte[] packet = Packet(Asking, writer => writer.Write((byte)LanLink.Protocol));
            List<IPAddress> to = new List<IPAddress> { IPAddress.Broadcast, IPAddress.Loopback };
            foreach ((IPAddress _, IPAddress broadcast) in Networks())
                if (!to.Contains(broadcast))
                    to.Add(broadcast);
            foreach (IPAddress address in to)
            {
                try
                {
                    socket.Send(packet, packet.Length, new IPEndPoint(address, Port));
                }
                catch (SocketException)
                {
                    // That network is not there (no Wi-Fi yet): the others are tried
                }
            }
        }

        private void Heard(byte[] packet, IPEndPoint from, float now)
        {
            if (packet.Length < Magic.Length + 1)
                return;
            for (int i = 0; i < Magic.Length; i++)
                if (packet[i] != Magic[i])
                    return;
            byte kind = packet[Magic.Length];
            try
            {
                using (BinaryReader reader = new BinaryReader(new MemoryStream(packet, Magic.Length + 1, packet.Length - Magic.Length - 1)))
                {
                    if (kind == Asking && answering)
                        Reply(from);
                    else if (kind == Answering && !answering)
                    {
                        int protocol = reader.ReadByte();
                        uint host = reader.ReadUInt32();
                        ushort port = reader.ReadUInt16();
                        string their_version = Shown(reader.ReadString());
                        string their_name = PlayerNames.Clean(reader.ReadString()) ?? "Player";
                        int their_leader = reader.ReadInt16();
                        Found(host, from.Address, port, their_name, their_leader, their_version, protocol == LanLink.Protocol && their_version == version, now);
                    }
                }
            }
            catch (Exception)
            {
                // Not one of ours after all
            }
        }

        // Another game's version as the list shows it: printable, short
        internal static string Shown(string version)
        {
            string shown = new string((version ?? "").Where(c => c > ' ' && c < 0x7F && c != '<' && c != '>').Take(20).ToArray());
            return shown.Length > 0 ? shown : "?";
        }

        private void Reply(IPEndPoint to)
        {
            byte[] packet = Packet(Answering, writer =>
            {
                writer.Write((byte)LanLink.Protocol);
                writer.Write(id);
                writer.Write(gamePort);
                writer.Write(version);
                writer.Write(name ?? "");
                writer.Write((short)leader);
            });
            try
            {
                socket.Send(packet, packet.Length, to);
            }
            catch (SocketException)
            {
                // The one asking is gone
            }
        }

        private void Found(uint host, IPAddress address, ushort port, string their_name, int their_leader, string their_version, bool compatible, float now)
        {
            Game game = games.Find(known => known.Id == host);
            if (game == null)
            {
                game = new Game { Id = host };
                games.Add(game);
            }
            // Heard on this computer and on the network: the network's address, which the other devices can reach too
            if (game.Address == null || IPAddress.IsLoopback(IPAddress.Parse(game.Address)) && !IPAddress.IsLoopback(address))
                game.Address = address.ToString();
            game.Port = port;
            game.Name = their_name;
            game.Leader = their_leader;
            game.Version = their_version;
            game.Compatible = compatible;
            game.Seen = now;
        }

        private static byte[] Packet(byte kind, Action<BinaryWriter> write)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(Magic);
                    writer.Write(kind);
                    write(writer);
                }
                return stream.ToArray();
            }
        }

        //----------------------------------------This device's networks----------------------------------------//
        // A network this device is on that another device can reach it through, as the host's details show it
        public sealed class Network
        {
            public string Kind;     // "Wi-Fi", "Ethernet", "Hotspot", "Network", or a VPN's name ("Tailscale"...) or "VPN"
            public string Adapter;  // its adapter's name: "wlan0", "Wi-Fi", "tailscale0"...
            public string Address;  // this device's address there
            public bool Vpn;        // reached from anywhere, by the devices on the same VPN
        }

        // This device's addresses on its networks (what another player types to join it): those of the networks around it
        // first (Wi-Fi, a cable, a phone's hotspot), then the rest (VPNs, the computer's own virtual networks)
        public static List<string> LocalAddresses()
        {
            List<string> near = new List<string>(), rest = new List<string>();
            foreach ((UnicastIPAddressInformation unicast, Network network) in Addresses())
                (network != null && !network.Vpn ? near : rest).Add(unicast.Address.ToString());
            near.AddRange(rest);
            return near;
        }

        // The addresses another device can join: on the networks around this one, else on its VPNs (none: on no network
        // another device can reach, such as mobile data alone)
        public static List<string> JoinableAddresses()
        {
            List<Network> reachable = Reachable();
            List<string> near = reachable.Where(network => !network.Vpn).Select(network => network.Address).ToList();
            return near.Count > 0 ? near : reachable.Select(network => network.Address).ToList();
        }

        // The networks another device can reach this one through: those around it first, then VPNs
        public static List<Network> Reachable()
        {
            return Addresses().Select(found => found.network).Where(network => network != null).OrderBy(network => network.Vpn).ToList();
        }

        private static readonly string[] OwnNames = { "docker", "br-", "virbr", "veth", "vmnet", "vboxnet", "vethernet", "lxcbr", "lxdbr" };
        private static readonly string[] OwnDescriptions = { "vmware", "hyper-v", "virtualbox", "wsl", "docker", "loopback" };
        private static readonly string[] MobileNames = { "rmnet", "v4-rmnet", "ccmni", "pdp_ip", "wwan" };
        private static readonly (string word, string vpn)[] VpnNames =
            { ("tailscale", "Tailscale"), ("zerotier", "ZeroTier"), ("hamachi", "Hamachi"), ("radmin", "Radmin VPN"), ("netbird", "NetBird"), ("wireguard", "WireGuard") };
        private static readonly (string prefix, string vpn)[] VpnPrefixes = { ("zt", "ZeroTier"), ("wt", "NetBird"), ("wg", "WireGuard") };
        private static readonly string[] TunnelNames = { "tun", "tap", "utun", "ipsec" };
        private static readonly string[] TunnelDescriptions = { "tap-", "tunnel", "vpn", "virtual" };

        // What a network is, from its adapter (name, description, type) and this device's address there; null for one no
        // other device reaches this one through: the computer's own (containers, virtual machines), a phone's mobile data
        // (its carrier's), an address a device gives itself when no network gave one (169.254...). A macOS adapter (en0...)
        // does not say whether it is Wi-Fi or a cable: "Network".
        public static Network Describe(string adapter, string description, NetworkInterfaceType type, IPAddress address, bool mac)
        {
            string name = (adapter ?? "").ToLowerInvariant(), about = (description ?? "").ToLowerInvariant();
            byte[] bytes = address.GetAddressBytes();
            if (type == NetworkInterfaceType.Loopback || Array.Exists(OwnNames, name.StartsWith) || Array.Exists(OwnDescriptions, about.Contains)
                || Array.Exists(MobileNames, name.StartsWith) || bytes[0] == 169 && bytes[1] == 254)
                return null;
            Network network = new Network { Adapter = adapter, Address = address.ToString() };
            foreach ((string word, string vpn) in VpnNames)
                if (name.Contains(word) || about.Contains(word))
                    network.Kind = vpn;
            foreach ((string prefix, string vpn) in VpnPrefixes)
                if (network.Kind == null && name.StartsWith(prefix))
                    network.Kind = vpn;
            if (network.Kind == null && about.Contains("wi-fi direct"))
                network.Kind = "Hotspot"; // Windows' own hotspot
            else if (network.Kind == null && (type == NetworkInterfaceType.Tunnel || Array.Exists(TunnelNames, name.StartsWith)
                || Array.Exists(TunnelDescriptions, about.Contains)))
                network.Kind = "VPN";
            network.Vpn = network.Kind != null && network.Kind != "Hotspot";
            if (network.Kind != null)
                return network;

            if (name.StartsWith("swlan") || name.StartsWith("softap") || name.StartsWith("ap") && name.Length > 2 && char.IsDigit(name[2]))
                network.Kind = "Hotspot"; // a phone's
            else if (type == NetworkInterfaceType.Wireless80211 || name.StartsWith("wl") || name.Contains("wi-fi") || name.Contains("wireless"))
                network.Kind = "Wi-Fi";
            else if (!mac && (type == NetworkInterfaceType.Ethernet || type == NetworkInterfaceType.GigabitEthernet
                || type == NetworkInterfaceType.FastEthernetT || type == NetworkInterfaceType.FastEthernetFx
                || name.StartsWith("eth") || name.StartsWith("en")))
                network.Kind = "Ethernet";
            else if (IsVpnAddress(address))
            {
                network.Kind = "VPN";
                network.Vpn = true;
            }
            else
                network.Kind = "Network";
            return network;
        }

        // 100.64.0.0/10, the range VPNs give their devices (Tailscale, NetBird, ZeroTier)
        private static bool IsVpnAddress(IPAddress address)
        {
            byte[] bytes = address.GetAddressBytes();
            return bytes[0] == 100 && (bytes[1] & 0xC0) == 64;
        }

        // Each network this device is on: its address there, and that network's broadcast address
        private static List<(IPAddress address, IPAddress broadcast)> Networks()
        {
            return Addresses().Select(found => (found.unicast.Address, Broadcast(found.unicast))).ToList();
        }

        // This device's IPv4 addresses, each with what its network is (null: no other device reaches it there); loopback and
        // adapters down left out
        private static List<(UnicastIPAddressInformation unicast, Network network)> Addresses()
        {
            List<(UnicastIPAddressInformation, Network)> addresses = new List<(UnicastIPAddressInformation, Network)>();
            bool mac = Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor;
            try
            {
                foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback
                        || adapter.OperationalStatus != OperationalStatus.Up && adapter.OperationalStatus != OperationalStatus.Unknown)
                        continue;
                    foreach (UnicastIPAddressInformation unicast in adapter.GetIPProperties().UnicastAddresses)
                        if (unicast.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(unicast.Address))
                            addresses.Add((unicast, Describe(adapter.Name, adapter.Description, adapter.NetworkInterfaceType, unicast.Address, mac)));
                }
            }
            catch (Exception exception) when (exception is NetworkInformationException || exception is NotImplementedException
                || exception is NotSupportedException)
            {
                // The device does not say: the broadcast to everyone still goes
            }
            return addresses;
        }

        private static IPAddress Broadcast(UnicastIPAddressInformation unicast)
        {
            byte[] address = unicast.Address.GetAddressBytes();
            byte[] mask;
            try
            {
                mask = unicast.IPv4Mask?.GetAddressBytes();
            }
            catch (NotImplementedException)
            {
                mask = null;
            }
            if (mask == null || mask.Length != 4)
                mask = new byte[] { 255, 255, 255, 0 }; // a home network's, the likeliest
            byte[] broadcast = new byte[4];
            for (int i = 0; i < 4; i++)
                broadcast[i] = (byte)(address[i] | ~mask[i]);
            return new IPAddress(broadcast);
        }

        //----------------------------------------Android----------------------------------------//
        private void HoldMulticastLock()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaClass player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject wifi = activity.Call<AndroidJavaObject>("getSystemService", "wifi"))
                {
                    multicastLock = wifi.Call<AndroidJavaObject>("createMulticastLock", "Gwent LAN");
                    multicastLock.Call("setReferenceCounted", false);
                    multicastLock.Call("acquire");
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[lan] No multicast lock (searches from other devices may not be heard): " + exception.Message);
                multicastLock = null;
            }
#endif
        }

        public void Dispose()
        {
            socket.Close();
            if (multicastLock != null)
            {
                try
                {
                    multicastLock.Call("release");
                }
                catch (Exception)
                {
                    // Already let go
                }
                multicastLock.Dispose();
                multicastLock = null;
            }
        }
    }
}

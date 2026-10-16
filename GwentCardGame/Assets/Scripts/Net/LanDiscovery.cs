using System;
using System.Collections.Generic;
using System.IO;
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
        private readonly ushort gamePort;
        private readonly uint id;
        private readonly string version;
        private readonly List<Game> games = new List<Game>();
        private float asked = float.MinValue;
        private AndroidJavaObject multicastLock;

        public IReadOnlyList<Game> Games { get { return games; } }

        private LanDiscovery(UdpClient socket, bool answering, string name, ushort game_port, string version)
        {
            this.socket = socket;
            this.answering = answering;
            this.name = name;
            gamePort = game_port;
            this.version = version;
            id = (uint)Environment.TickCount ^ (uint)UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            socket.EnableBroadcast = true;
        }

        // Answers searches for the game waiting on that port, under that name (null if the discovery port cannot be had)
        public static LanDiscovery Answer(string name, ushort game_port)
        {
            return Answer(name, game_port, Application.version);
        }

        internal static LanDiscovery Answer(string name, ushort game_port, string version)
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
            LanDiscovery discovery = new LanDiscovery(socket, true, name, game_port, version);
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
            return new LanDiscovery(new UdpClient(0, AddressFamily.InterNetwork), false, null, 0, version);
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
                        string their_version = reader.ReadString();
                        string their_name = reader.ReadString();
                        Found(host, from.Address, port, their_name, their_version, protocol == LanLink.Protocol && their_version == version, now);
                    }
                }
            }
            catch (IOException)
            {
                // Not one of ours after all
            }
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

        private void Found(uint host, IPAddress address, ushort port, string their_name, string their_version, bool compatible, float now)
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
        // This device's addresses on its networks (what another player types to join it)
        public static List<string> LocalAddresses()
        {
            List<string> addresses = new List<string>();
            foreach ((IPAddress address, IPAddress _) in Networks())
                addresses.Add(address.ToString());
            return addresses;
        }

        // Each network this device is on: its address there, and that network's broadcast address
        private static List<(IPAddress address, IPAddress broadcast)> Networks()
        {
            List<(IPAddress, IPAddress)> networks = new List<(IPAddress, IPAddress)>();
            try
            {
                foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback
                        || adapter.OperationalStatus != OperationalStatus.Up && adapter.OperationalStatus != OperationalStatus.Unknown)
                        continue;
                    foreach (UnicastIPAddressInformation unicast in adapter.GetIPProperties().UnicastAddresses)
                    {
                        if (unicast.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(unicast.Address))
                            continue;
                        networks.Add((unicast.Address, Broadcast(unicast)));
                    }
                }
            }
            catch (Exception exception) when (exception is NetworkInformationException || exception is NotImplementedException
                || exception is NotSupportedException)
            {
                // The device does not say: the broadcast to everyone still goes
            }
            return networks;
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

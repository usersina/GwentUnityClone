using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Error;
using Unity.Networking.Transport.Utilities;
using Gwent.Core;
using UnityEngine;

namespace Gwent.Net
{
    // One player's link to the other over the local network (Unity Transport, over UDP): the host waits for a guest, the
    // guest joins the host at its address. A message arrives whole, once and in order (a reliable pipeline, split into
    // packets and put back together when it is bigger than one). Joining, the guest says which game it runs and the host
    // answers: a guest of another version is turned away (both must play by the same rules), and so is a second guest
    // while one is there. A side that stops answering is dropped after DropAfter. Nothing runs on its own: the owner
    // calls Update every frame, and the events are raised from there.
    public sealed class LanLink : IDisposable
    {
        public const ushort GamePort = 26947;      // or one of the next (PortsTried) when another program holds it
        private const int PortsTried = 10;
        public const int Protocol = 5;              // the messages' shape (LanSession's too): raised when it changes
        public const int Largest = 32 * 1024;       // the largest message (bytes)
        private const int ConnectTimeoutMs = 500;   // a join is retried this often...
        private const int ConnectAttempts = 10;     // ...this many times: no answer in 5 s, no game there
        private const int DropAfterMs = 10000;      // a side not heard from this long is gone
        private const int Window = 64;              // reliable packets in flight (the largest message fits)
        private const float RefusedHold = 2f;       // a guest turned away is cut off this long after the answer

        private enum Kind : byte { Hello = 1, Welcome = 2, Refused = 3, Message = 4 }

        public enum Status { Waiting, Joining, Connected, Closed }

        private NetworkDriver driver;
        private NetworkPipeline pipeline;
        private NetworkConnection other;            // the guest (hosting) or the host (joining)
        private readonly bool hosting;
        private readonly string version;
        private readonly Queue<byte[]> outgoing = new Queue<byte[]>(); // waiting for room in the reliable window
        private readonly List<(NetworkConnection connection, float until)> refused = new List<(NetworkConnection, float)>();

        public Status State { get; private set; }
        public bool Hosting { get { return hosting; } }
        public ushort Port { get; private set; }            // the host's, as bound
        public string OtherAddress { get; private set; }    // the other side's, once connected
        public string OtherVersion { get; private set; }
        public string Reason { get; private set; }          // why the last guest left, or why the link closed

        public event Action Connected;              // the other side is there and runs the same game
        public event Action<byte[]> Received;       // one of its messages
        public event Action<string> Disconnected;   // it left (why); a host then waits for the next guest

        private LanLink(bool hosting, string version)
        {
            this.hosting = hosting;
            this.version = version;
            NetworkSettings settings = new NetworkSettings(Allocator.Temp);
            settings.WithNetworkConfigParameters(connectTimeoutMS: ConnectTimeoutMs, maxConnectAttempts: ConnectAttempts,
                disconnectTimeoutMS: DropAfterMs);
            settings.WithFragmentationStageParameters(payloadCapacity: Largest + 1);
            settings.WithReliableStageParameters(windowSize: Window);
            driver = NetworkDriver.Create(settings);
            pipeline = driver.CreatePipeline(typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));
        }

        // Waits for a guest on the game's port, or the next free one (null if none is: other programs hold them all).
        // Ports below the range systems hand out for a while (32768 and up), where programs seldom sit.
        public static LanLink Host()
        {
            return Host(GamePort);
        }

        // The same, on that port first when it is free (a battle taken up again: its guest comes back to it)
        public static LanLink Host(ushort preferred)
        {
            if (preferred != GamePort && Free(preferred))
            {
                LanLink there = Host(preferred, BuildInfo.Id);
                if (there != null)
                    return there;
            }
            for (int port = GamePort; port < GamePort + PortsTried; port++)
                if (Free((ushort)port))
                {
                    LanLink link = Host((ushort)port, BuildInfo.Id);
                    if (link != null)
                        return link;
                }
            return null;
        }

        // Whether nothing holds that port on any of this device's addresses (asked first: Transport logs an error for a
        // port it cannot have)
        private static bool Free(ushort port)
        {
            try
            {
                using (UdpClient probe = new UdpClient(AddressFamily.InterNetwork))
                {
                    probe.ExclusiveAddressUse = true;
                    probe.Client.Bind(new IPEndPoint(IPAddress.Any, port));
                }
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
        }

        internal static LanLink Host(ushort port, string version)
        {
            LanLink link = new LanLink(true, version);
            if (link.driver.Bind(NetworkEndpoint.AnyIpv4.WithPort(port)) != 0 || link.driver.Listen() != 0)
            {
                link.Dispose();
                return null;
            }
            link.Port = link.driver.GetLocalEndpoint().Port;
            link.State = Status.Waiting;
            return link;
        }

        // Joins the host at that address (an IPv4 address, "192.168.1.20"); null if it is not one
        public static LanLink Join(string address, ushort port = GamePort)
        {
            return Join(address, port, BuildInfo.Id);
        }

        internal static LanLink Join(string address, ushort port, string version)
        {
            if (!NetworkEndpoint.TryParse(address, port, out NetworkEndpoint endpoint, NetworkFamily.Ipv4))
                return null;
            LanLink link = new LanLink(false, version);
            link.Port = port;
            link.other = link.driver.Connect(endpoint);
            link.State = Status.Joining;
            return link;
        }

        // Sends a message to the other side (false when there is none, or it is too big)
        public bool Send(byte[] message)
        {
            if (State != Status.Connected || message.Length > Largest)
                return false;
            Queue(Kind.Message, message);
            return true;
        }

        public void Update()
        {
            if (!driver.IsCreated)
                return;
            driver.ScheduleUpdate().Complete();

            if (hosting)
            {
                NetworkConnection guest;
                while ((guest = driver.Accept()) != default)
                {
                    if (other.IsCreated)
                        Refuse(guest, "A battle is already under way there.");
                    else
                        other = guest;
                }
                for (int i = refused.Count - 1; i >= 0; i--)
                    if (Time.realtimeSinceStartup > refused[i].until)
                    {
                        driver.Disconnect(refused[i].connection);
                        refused.RemoveAt(i);
                    }
            }

            NetworkEvent.Type type;
            while ((type = driver.PopEvent(out NetworkConnection from, out DataStreamReader reader)) != NetworkEvent.Type.Empty)
            {
                if (from != other)
                    continue; // a guest turned away: only its leaving, ignored
                if (type == NetworkEvent.Type.Connect)
                    Queue(Kind.Hello, Hello());
                else if (type == NetworkEvent.Type.Data)
                    Read(ref reader);
                else if (type == NetworkEvent.Type.Disconnect)
                    Lost(Why((DisconnectReason)reader.ReadByte()));
                if (!driver.IsCreated)
                    return; // closed by what was raised
            }
            Flush();
        }

        private void Read(ref DataStreamReader reader)
        {
            if (reader.Length < 1)
                return; // not one of ours
            Kind kind = (Kind)reader.ReadByte();
            byte[] body = new byte[reader.Length - 1];
            reader.ReadBytes(body);
            switch (kind)
            {
                case Kind.Hello when hosting:
                    (int protocol, string their_version) = Versions(body);
                    if (protocol != Protocol || their_version != version)
                    {
                        Refuse(other, "That battle runs another build of the game (" + BuildInfo.Short(version) + "; yours: " + BuildInfo.Short(their_version)
                            + "). Both players need the same build.");
                        other = default;
                        return;
                    }
                    Queue(Kind.Welcome, Hello());
                    Open(their_version);
                    break;
                case Kind.Welcome when !hosting:
                    Open(Versions(body).version);
                    break;
                case Kind.Refused when !hosting:
                    Close(Text(body));
                    break;
                case Kind.Message when State == Status.Connected:
                    Received?.Invoke(body);
                    break;
            }
        }

        private void Open(string their_version)
        {
            OtherVersion = their_version;
            string address = driver.GetRemoteEndpoint(other).Address; // "192.168.1.20:47380"
            int colon = address.LastIndexOf(':');
            OtherAddress = colon > 0 ? address.Substring(0, colon) : address;
            State = Status.Connected;
            Connected?.Invoke();
        }

        // The other side is gone: a host waits for the next guest, a guest's link closes
        private void Lost(string reason)
        {
            other = default;
            outgoing.Clear();
            if (hosting)
            {
                bool was = State == Status.Connected;
                Reason = reason;
                State = Status.Waiting;
                if (was)
                    Disconnected?.Invoke(reason);
            }
            else
                Close(reason);
        }

        private void Close(string reason)
        {
            bool was = State == Status.Connected;
            Reason = reason;
            Dispose();
            if (was)
                Disconnected?.Invoke(reason);
        }

        private static string Why(DisconnectReason reason)
        {
            switch (reason)
            {
                case DisconnectReason.Timeout: return "The other player stopped answering.";
                case DisconnectReason.MaxConnectionAttempts: return "No game answered at that address.";
                case DisconnectReason.ClosedByRemote: return "The other player left.";
                case DisconnectReason.HostNotFound: return "That address could not be found.";
                default: return "The link failed (" + reason + ").";
            }
        }

        private void Refuse(NetworkConnection guest, string reason)
        {
            WriteTo(guest, Kind.Refused, Bytes(writer => writer.Write(reason)));
            refused.Add((guest, Time.realtimeSinceStartup + RefusedHold));
        }

        //----------------------------------------Sending----------------------------------------//
        // In order: after any message still waiting for room
        private void Queue(Kind kind, byte[] body)
        {
            byte[] packet = new byte[body.Length + 1];
            packet[0] = (byte)kind;
            Buffer.BlockCopy(body, 0, packet, 1, body.Length);
            outgoing.Enqueue(packet);
            Flush();
        }

        private void Flush()
        {
            while (outgoing.Count > 0 && other.IsCreated && driver.GetConnectionState(other) == NetworkConnection.State.Connected)
            {
                if (!Put(other, outgoing.Peek()))
                    return; // the window is full: next frame
                outgoing.Dequeue();
            }
        }

        // A message to a guest being turned away (not queued: it is all it will get)
        private void WriteTo(NetworkConnection connection, Kind kind, byte[] body)
        {
            byte[] packet = new byte[body.Length + 1];
            packet[0] = (byte)kind;
            Buffer.BlockCopy(body, 0, packet, 1, body.Length);
            Put(connection, packet);
        }

        private bool Put(NetworkConnection connection, byte[] packet)
        {
            if (driver.BeginSend(pipeline, connection, out DataStreamWriter writer) != 0)
                return false;
            writer.WriteBytes(packet);
            if (writer.HasFailedWrites)
            {
                driver.AbortSend(writer);
                return true; // too big for the pipeline: dropped (Send keeps to Largest)
            }
            return driver.EndSend(writer) >= 0;
        }

        //----------------------------------------Joining's words----------------------------------------//
        private byte[] Hello()
        {
            return Bytes(writer =>
            {
                writer.Write(Protocol);
                writer.Write(version);
            });
        }

        private static (int protocol, string version) Versions(byte[] body)
        {
            try
            {
                using (BinaryReader reader = new BinaryReader(new MemoryStream(body)))
                    return (reader.ReadInt32(), reader.ReadString());
            }
            catch (Exception)
            {
                return (-1, "?");
            }
        }

        private static string Text(byte[] body)
        {
            try
            {
                using (BinaryReader reader = new BinaryReader(new MemoryStream(body)))
                    return reader.ReadString();
            }
            catch (Exception)
            {
                return "The host turned you away.";
            }
        }

        private static byte[] Bytes(Action<BinaryWriter> write)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                    write(writer);
                return stream.ToArray();
            }
        }

        // The host lets its guest go (it stopped answering, whatever the link says): it waits for the next one
        public void Drop(string reason)
        {
            if (!hosting || !driver.IsCreated || !other.IsCreated)
                return;
            driver.Disconnect(other);
            Lost(reason);
        }

        // Leaves (the other side is told) and lets the socket go
        public void Dispose()
        {
            if (driver.IsCreated)
            {
                driver.ScheduleUpdate().Complete(); // what was said last (a Leave) goes out before the link
                if (other.IsCreated)
                    driver.Disconnect(other);
                foreach ((NetworkConnection connection, float until) turned in refused)
                    driver.Disconnect(turned.connection);
                driver.ScheduleUpdate().Complete(); // the leaving goes out
                driver.Dispose();
            }
            refused.Clear();
            outgoing.Clear();
            other = default;
            State = Status.Closed;
        }
    }
}

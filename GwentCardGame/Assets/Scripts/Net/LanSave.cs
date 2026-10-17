using System;
using System.Collections.Generic;
using System.IO;
using Gwent.Rules;
using UnityEngine;

namespace Gwent.Net
{
    // A LAN battle kept on disk while it is on, so that a player whose game closed (Android closing it in the background,
    // a quit, a crash) can go back to it from the lobby: the host keeps the whole battle (its setup and the moves taken:
    // replayed, it is the same battle) and the port it waits on, the guest which battle it is and where its host is. Gone
    // once the battle is over or left; one left too long ago is not offered. The host's and the guest's are files of
    // their own (two copies of the game on one computer, testing, each keep theirs).
    public sealed class LanSave
    {
        private const byte Format = 1;
        private static readonly TimeSpan KeptFor = TimeSpan.FromHours(6);

        public bool Hosting;
        public int Battle;
        public string Address;          // the host's (guest)
        public ushort Port;
        public string MyName, TheirName;
        public Deck MyDeck, TheirDeck;
        public MatchSetup Setup;        // the host's
        public List<(bool seat, MatchCommand command)> Commands = new List<(bool, MatchCommand)>();
        public DateTime Saved;

        private static string FileOf(bool hosting)
        {
            return Path.Combine(Application.persistentDataPath, hosting ? "lan-battle-host.bin" : "lan-battle-guest.bin");
        }

        private string File { get { return FileOf(Hosting); } }

        // The battle left on (the latest kept, hosted or joined), or null (none, too old, or unreadable)
        public static LanSave Load()
        {
            LanSave hosted = Load(true), joined = Load(false);
            if (hosted == null || joined == null)
                return hosted ?? joined;
            return hosted.Saved >= joined.Saved ? hosted : joined;
        }

        // The battle left on as the host (or the guest), or null
        public static LanSave Load(bool hosting)
        {
            string file = FileOf(hosting);
            try
            {
                if (!System.IO.File.Exists(file))
                    return null;
                using (BinaryReader r = new BinaryReader(System.IO.File.OpenRead(file)))
                {
                    if (r.ReadByte() != Format)
                        return null;
                    LanSave save = new LanSave
                    {
                        Saved = DateTime.FromBinary(r.ReadInt64()),
                        Hosting = r.ReadBoolean(),
                        Battle = r.ReadInt32(),
                        Address = r.ReadString(),
                        Port = r.ReadUInt16(),
                        MyName = r.ReadString(),
                        TheirName = r.ReadString(),
                        MyDeck = MatchWire.ReadDeck(r),
                        TheirDeck = MatchWire.ReadDeck(r),
                    };
                    if (r.ReadBoolean())
                        save.Setup = MatchWire.ReadSetup(r);
                    int commands = r.ReadInt32();
                    for (int i = 0; i < commands; i++)
                        save.Commands.Add((r.ReadBoolean(), MatchCommand.Read(r)));
                    return DateTime.UtcNow - save.Saved < KeptFor ? save : null;
                }
            }
            catch (Exception e)
            {
                // Unreadable (cut short, another game's): as if there were none
                Debug.LogWarning("[lan] The LAN battle kept could not be read: " + e.Message);
                return null;
            }
        }

        public void Write()
        {
            Saved = DateTime.UtcNow;
            try
            {
                string temp = File + ".new";
                using (BinaryWriter w = new BinaryWriter(System.IO.File.Create(temp)))
                {
                    w.Write(Format);
                    w.Write(Saved.ToBinary());
                    w.Write(Hosting);
                    w.Write(Battle);
                    w.Write(Address ?? "");
                    w.Write(Port);
                    w.Write(MyName ?? "");
                    w.Write(TheirName ?? "");
                    MatchWire.Write(w, MyDeck);
                    MatchWire.Write(w, TheirDeck);
                    w.Write(Setup != null);
                    if (Setup != null)
                        MatchWire.Write(w, Setup);
                    w.Write(Commands.Count);
                    foreach ((bool seat, MatchCommand command) in Commands)
                    {
                        w.Write(seat);
                        command.Write(w);
                    }
                }
                if (System.IO.File.Exists(File))
                    System.IO.File.Delete(File);
                System.IO.File.Move(temp, File); // whole, or not at all: never a half-written battle
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning("[lan] The LAN battle could not be kept: " + e.Message);
            }
        }

        // Forgets this battle
        public void Delete()
        {
            Delete(Hosting);
        }

        // Forgets the battle kept as the host (or the guest)
        public static void Delete(bool hosting)
        {
            string file = FileOf(hosting);
            try
            {
                if (System.IO.File.Exists(file))
                    System.IO.File.Delete(file);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning("[lan] The LAN battle kept could not be removed: " + e.Message);
            }
        }
    }
}

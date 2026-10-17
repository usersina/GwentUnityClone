using System;
using System.Collections.Generic;
using System.IO;
using Gwent.Battle;
using Gwent.Core;
using Gwent.Rules;
using UnityEngine;

namespace Gwent.Net
{
    // A LAN battle, from the lobby to its end: the link to the other device (LanLink), both players' names and decks, and
    // the battle. The host runs the match (Match): the guest's moves come over the link and go to it, and each of its
    // events goes to the guest as the guest sees it (MatchEvent.For). The guest shows those (Source) and sends its moves.
    // A link lost during the battle is waited for: the guest joins again (every few seconds, at once when the app comes
    // back from the background), says how much of the battle it has, and the host sends it the rest. In a battle both
    // sides say they are there every second: a side not heard from for a few seconds is taken as gone, whatever the
    // link says (a device frozen a while, its old link seemingly kept alive by its attempts to come back). The battle is
    // kept on disk while it is on (LanSave): a player whose game closed takes it up again from the lobby (Rejoin), the
    // host replaying it and waiting on the same port, the guest joining its host again. Once a battle is over, either
    // side may ask for a rematch: when both have, the host begins the next battle on the same link (the same decks, a new
    // coin toss) and both screens load it. In a battle and after it, the players talk (Chat: quick lines and messages),
    // the other's heard unless muted; what was said is kept for the session (Talk). Lives across scenes: the lobby is the
    // menu's, the battle the game's.
    public class LanSession : MonoBehaviour
    {
        // Waiting: hosting, nobody there yet; Joining: on the way in (the host's answer not yet in); Ready: both there,
        // the host may start; Battle: under way; Closed: over (Problem says why, when something went wrong)
        public enum Stage { Waiting, Joining, Ready, Battle, Closed }

        private enum Message : byte { Join = 1, Resume, Welcome, Refused, Begin, Event, Command, CommandRefused, Leave, Resumed, Here, Rematch, Emote, Text }

        private const float RejoinEvery = 2f;
        private const float HereEvery = 1f;     // in a battle, each side says it is there
        private const float SilentFor = 6f;     // not heard from that long: gone
        private const float PausedTooLong = 8f; // in the background longer: the other side has dropped the link
        private const int TalkKept = 50;
        private const float HeardEvery = 0.5f;  // the other's lines at most (a changed game could flood)

        public static LanSession Current { get; private set; }

        // The session whose battle the Game scene is for: kept once it closes too (the other player left as the battle
        // loaded: the battle screen says why, instead of starting a battle against the AI); forgotten once the player leaves
        // it, or back in the main menu
        public static LanSession ForBattle { get; private set; }

        public static void ForgetBattle()
        {
            if (ForBattle is object && ForBattle.State != Stage.Battle) // closed: its object destroyed ("== null" would hide it)
                ForBattle = null;
        }

        public bool Hosting { get; private set; }
        public Stage State { get; private set; }
        public string Problem { get; private set; }     // why it closed, or why a guest was turned away
        public string MyName { get; private set; }
        public string TheirName { get; private set; }
        public Deck MyDeck { get; private set; }
        public Deck TheirDeck { get; private set; }      // the guest's whole deck (host); its faction and leader (guest)
        public Match Match { get; private set; }         // the host's
        public ushort Port { get { return port; } }

        // Whether the battle hosted shows in the others' lists (else, the search's port taken, it is joined by address)
        public bool Listed { get { return discovery != null; } }

        // Whether the other device is linked now (in a battle: false while it is away)
        public bool Linked { get { return link != null && link.State == LanLink.Status.Connected && (State != Stage.Battle || resumed); } }

        // How long the other device has been away (0 while it is there)
        public float AwayFor { get { return lostAt >= 0 ? Time.realtimeSinceStartup - lostAt : 0; } }

        // A battle taken up again from the disk (Rejoin): its screen catches up at once instead of playing every step
        public bool Rejoined { get; private set; }

        // Whether the screen has every event there is to show (the guest's, once its host said it sent the rest)
        public bool CaughtUp { get { return Hosting || resumed; } }

        // The battle under way (its number): a rematch begins another
        public int Battle { get { return battle; } }

        // The battle is over: a rematch may be asked
        public bool Over { get { return State == Stage.Battle && Ended; } }

        // The battle's end is in (its screen may not show it yet), whether the session goes on or closed since
        public bool Ended
        {
            get
            {
                return Hosting ? Match != null && Match.IsOver
                    : received.Count > 0 && received[received.Count - 1].Kind == MatchEventKind.Ended;
            }
        }

        // A rematch asked, by this side (AskRematch) and by the other
        public bool AskedRematch { get; private set; }
        public bool TheyAskedRematch { get; private set; }

        // A line said in the session: a quick one or a message, as shown, and by whom
        public sealed class Said
        {
            public bool Mine;
            public string Text;
        }

        // What the players said in the session (rematches too), the last 50
        public IReadOnlyList<Said> Talk { get { return talk; } }

        // The other player said something (muted: not heard, nor kept)
        public event Action<Said> Heard;

        // Whether this player hears the other (for the session, rematches too)
        public bool Muted { get; set; }

        public event Action Changed; // the lobby's state: someone joined or left, the battle begins, the session closed

        // The guest who just left the host's room (null once another joins)
        public string LeftRoom { get; private set; }

        private LanLink link;
        private LanDiscovery discovery;
        private string address;          // the host's (guest)
        private ushort port;
        private int battle;              // the battle's number, for a guest coming back to it
        private int previous;            // the battle before a rematch's (host: a guest that missed the rematch's start)
        private bool resumed;            // in a battle: the guest is back and caught up (or never left)
        private float lostAt = -1;
        private float nextJoin;
        private float pausedAt = -1;
        private float lastHeard;         // the other device's last message
        private float nextHere;
        private int sent;                // host: the events the guest has
        private int kept = -1;           // host: the moves on disk
        private readonly List<MatchEvent> received = new List<MatchEvent>(); // guest
        private readonly List<Said> talk = new List<Said>();
        private float heardAt = float.MinValue;
        private GuestSource source;

        //----------------------------------------Opening----------------------------------------//
        // Hosts a battle with that deck, under that name (closed at once, Problem saying why, when no port can be had)
        public static LanSession Host(string name, Deck deck)
        {
            LanSession session = Make(name, deck, true);
            session.link = LanLink.Host();
            if (session.link == null)
            {
                session.Close("No free port for a game here: other programs hold them");
                return session;
            }
            session.port = session.link.Port;
            session.Listen();
            session.discovery = LanDiscovery.Answer(name, deck.Leader, session.port);
            session.State = Stage.Waiting;
            return session;
        }

        // Joins the battle hosted at that address with that deck, under that name
        public static LanSession Join(string address, ushort port, string name, Deck deck)
        {
            LanSession session = Make(name, deck, false);
            session.address = address;
            session.port = port;
            session.State = Stage.Joining;
            session.Connect();
            return session;
        }

        // Takes up a battle kept on disk: the host replays it and waits for its guest on the same port, the guest joins its
        // host again (both then load the battle). Closed at once, Problem saying why, when it cannot be.
        public static LanSession Rejoin(LanSave save)
        {
            LanSession session = Make(save.MyName, save.MyDeck, save.Hosting);
            session.TheirName = save.TheirName;
            session.TheirDeck = save.TheirDeck;
            session.battle = save.Battle;
            session.Rejoined = true;
            session.lostAt = Time.realtimeSinceStartup;
            if (save.Hosting)
            {
                try
                {
                    session.Match = Match.Replay(save.Setup, CardLibrary.Get(), save.Commands);
                }
                catch (InvalidOperationException e)
                {
                    save.Delete();
                    session.Close("The battle kept could not be taken up again (" + e.Message + ")");
                    return session;
                }
                session.kept = session.Match.Commands.Count;
                session.link = LanLink.Host(save.Port);
                if (session.link == null)
                {
                    session.Close("No free port for a game here: other programs hold them");
                    return session;
                }
                session.port = session.link.Port;
                if (session.port != save.Port)
                {
                    // Its guest comes back to the port it knew: another program holds it now
                    session.Close("Port " + save.Port + " is taken by another program now. Close it, then rejoin.");
                    return session;
                }
                session.Listen();
                session.State = Stage.Battle; // waiting for the guest (resumed once it is back)
                ForBattle = session;
                Say("taken up again (" + session.Match.Events.Count + " events) on port " + session.port + ": waiting for " + save.TheirName);
            }
            else
            {
                session.address = save.Address;
                session.port = save.Port;
                session.State = Stage.Battle;
                ForBattle = session;
                session.Connect();
                Say("taken up again: joining " + save.TheirName + " at " + save.Address + ":" + save.Port);
            }
            return session;
        }

        private static LanSession Make(string name, Deck deck, bool hosting)
        {
            if (Current != null)
                Current.Leave();
            GameObject go = new GameObject("LanSession");
            DontDestroyOnLoad(go);
            LanSession session = go.AddComponent<LanSession>();
            session.Hosting = hosting;
            session.MyName = PlayerNames.Clean(name) ?? "Player";
            session.MyDeck = deck;
            Current = session;
            GameLog.Event(LogCategory.Game, hosting ? "lan_host" : "lan_join", "name", name, "deck", deck.Name);
            return session;
        }

        private void Connect()
        {
            if (link != null)
                link.Dispose();
            link = LanLink.Join(address, port);
            if (link == null)
            {
                Close("\"" + address + "\" is not an address.");
                return;
            }
            Listen();
            nextJoin = Time.realtimeSinceStartup + RejoinEvery;
        }

        private void Listen()
        {
            link.Connected += OnConnected;
            link.Received += OnReceived;
            link.Disconnected += OnDisconnected;
        }

        //----------------------------------------The battle----------------------------------------//
        // The host starts the battle: the match from both decks (its own the Player side's), its first events to the guest;
        // both load the battle (BattleController takes it from here)
        public void StartBattle()
        {
            if (!Hosting || State != Stage.Ready)
                return;
            if (discovery != null)
                discovery.Dispose(); // no longer a game to join
            discovery = null;
            BeginBattle();
            GameLog.Event(LogCategory.Game, "lan_battle", "host", MyName, "guest", TheirName);
            Changed?.Invoke();
        }

        // Asks for a rematch, the battle over; the other side asked too, the next battle begins (the host's: its number
        // changes, Battle, and both load it)
        public void AskRematch()
        {
            if (!Over || AskedRematch)
                return;
            AskedRematch = true;
            Send(Message.Rematch, w => w.Write(battle));
            Say("rematch asked" + (TheyAskedRematch ? ", as the other side did" : ""));
            if (Hosting && TheyAskedRematch)
                Rematch();
            Changed?.Invoke();
        }

        private void Rematch()
        {
            BeginBattle();
            GameLog.Event(LogCategory.Game, "lan_rematch", "host", MyName, "guest", TheirName);
        }

        // A battle from both decks (the host's own the Player side's), its number and first events to the guest
        private void BeginBattle()
        {
            previous = battle;
            battle = UnityEngine.Random.Range(1, int.MaxValue);
            Match = new Match(new MatchSetup { Player = MyDeck, Enemy = TheirDeck, Seed = Rng.Shared.Next() }, CardLibrary.Get());
            Match.Start();
            State = Stage.Battle;
            ForBattle = this;
            resumed = true;
            Rejoined = false;
            AskedRematch = TheyAskedRematch = false;
            sent = 0;
            lastHeard = Time.realtimeSinceStartup; // nothing is said in the room: the guest is not silent for that
            nextHere = 0;
            Send(Message.Begin, w => w.Write(battle));
            Keep();
        }

        // The battle on disk, as it is now (the host's after each move; the guest's once, as it begins); gone once over
        private void Keep()
        {
            if (Ended)
            {
                LanSave.Delete(Hosting);
                return;
            }
            LanSave save = new LanSave
            {
                Hosting = Hosting,
                Battle = battle,
                Address = address,
                Port = port,
                MyName = MyName,
                TheirName = TheirName,
                MyDeck = MyDeck,
                TheirDeck = TheirDeck ?? new Deck(),
            };
            if (Hosting)
            {
                save.Setup = Match.Setup;
                save.Commands.AddRange(Match.Commands);
                kept = Match.Commands.Count;
            }
            save.Write();
        }

        // What the battle screen shows: the host's own match, or the events the host sent (the guest's)
        public BattleSource Source
        {
            get
            {
                if (Hosting)
                    return Match != null ? new LocalSource(Match) : null;
                return source ?? (source = new GuestSource(this));
            }
        }

        // The guest's side of the battle on the host's screen: its moves come over the link
        public IOpponent Opponent { get { return new LanOpponent(); } }

        // Leaves the session (the other device is told), from the lobby or once the battle is over (giving up first is the
        // battle's business)
        public void Leave()
        {
            if (link != null && link.State == LanLink.Status.Connected)
                Send(Message.Leave, w => { });
            if (State == Stage.Battle || Rejoined)
                LanSave.Delete(Hosting);
            if (ForBattle == this)
                ForBattle = null;
            Close(null);
        }

        private void Close(string problem)
        {
            if (problem != null)
                Problem = problem;
            Say("closed" + (problem != null ? ": " + problem : ""));
            State = Stage.Closed;
            if (link != null)
                link.Dispose();
            if (discovery != null)
                discovery.Dispose();
            link = null;
            discovery = null;
            if (Current == this)
                Current = null;
            Changed?.Invoke();
            if (this != null)
                Destroy(gameObject);
        }

        //----------------------------------------The link----------------------------------------//
        private void Update()
        {
            discovery?.Update();
            link?.Update();
            if (State == Stage.Closed)
                return;
            // A join that never got through (no game at that address, the host's link turning it away) closes the link
            // without a word: the lobby is told why
            if (!Hosting && State == Stage.Joining && link != null && link.State == LanLink.Status.Closed)
            {
                Close(link.Reason ?? "No game answered at that address.");
                return;
            }
            // The guest away from the battle: it joins again now and then (the link it had gave up)
            if (!Hosting && State == Stage.Battle && (link == null || link.State == LanLink.Status.Closed) && Time.realtimeSinceStartup >= nextJoin)
                Connect();
            if (Hosting && State == Stage.Battle && Match.Commands.Count != kept)
                Keep();
            if (Hosting && State == Stage.Battle && resumed)
                SendEvents();
            if (State == Stage.Battle || State == Stage.Ready)
                KeepAlive();
        }

        // Says this side is there (in the room too); lets the other go once it has been silent too long (the host drops its
        // link and takes the next, the guest joins again at once, or out of a room, is told the host is gone)
        private void KeepAlive()
        {
            if (link == null || link.State != LanLink.Status.Connected)
                return;
            float now = Time.realtimeSinceStartup;
            if (now >= nextHere)
            {
                nextHere = now + HereEvery;
                Send(Message.Here, w => { });
            }
            if (now - lastHeard <= SilentFor)
                return;
            const string reason = "The other player stopped answering.";
            if (Hosting)
                link.Drop(reason); // OnDisconnected follows
            else
                JoinAgain(reason);
        }

        // The guest lets its link go and joins again at once (the host sends what it lacks)
        private void JoinAgain(string reason)
        {
            link.Dispose();
            link = null;
            nextJoin = 0;
            OnDisconnected(reason);
        }

        // The events the guest does not have yet, as it sees them
        private void SendEvents()
        {
            while (sent < Match.Events.Count && link != null && link.State == LanLink.Status.Connected)
            {
                MatchEvent e = Match.Events[sent].For(false);
                int index = sent;
                if (!Send(Message.Event, w => { w.Write(index); MatchWire.Write(w, e); }))
                    return;
                sent++;
            }
        }

        private void OnConnected()
        {
            lastHeard = Time.realtimeSinceStartup;
            if (Hosting)
                return; // the guest speaks first
            if (State == Stage.Battle)
                Send(Message.Resume, w => { w.Write(battle); w.Write(MyName); w.Write(received.Count); });
            else
                Send(Message.Join, w => { w.Write(MyName); MatchWire.Write(w, MyDeck); });
        }

        private void OnDisconnected(string reason)
        {
            if (State == Stage.Closed)
                return;
            if (State == Stage.Battle)
            {
                // Away: waited for (the guest joins again, the host takes it back)
                resumed = false;
                if (lostAt < 0)
                    lostAt = Time.realtimeSinceStartup;
                if (source != null && source.Waiting)
                    source.Refuse(); // a move on its way may be lost with the link: the card goes back, to be played again
                Say("link lost (" + reason + "): waiting");
                Changed?.Invoke();
                return;
            }
            if (Hosting)
                GuestLeftRoom(); // waiting for another
            else
                Close(Problem ?? reason);
        }

        private void GuestLeftRoom()
        {
            LeftRoom = TheirName;
            TheirName = null;
            TheirDeck = null;
            State = Stage.Waiting;
            Changed?.Invoke();
        }

        private void OnReceived(byte[] bytes)
        {
            lastHeard = Time.realtimeSinceStartup;
            try
            {
                using (BinaryReader r = new BinaryReader(new MemoryStream(bytes)))
                    Handle((Message)r.ReadByte(), r);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[lan] A message could not be read: " + e.Message);
            }
        }

        private void Handle(Message kind, BinaryReader r)
        {
            switch (kind)
            {
                // Host
                case Message.Join when Hosting:
                    string name = PlayerNames.Clean(r.ReadString()) ?? "Player";
                    Deck deck = DeckRules.Sanitize(MatchWire.ReadDeck(r), CardLibrary.Get());
                    if (State != Stage.Waiting)
                    {
                        Send(Message.Refused, w => w.Write(State == Stage.Battle ? "A battle is already under way there." : "Someone else joined first."));
                        return;
                    }
                    List<string> problems = DeckRules.Validate(deck, CardLibrary.Get());
                    if (problems.Count > 0)
                    {
                        Send(Message.Refused, w => w.Write("Your deck can't be used: " + problems[0]));
                        return;
                    }
                    TheirName = name;
                    TheirDeck = deck;
                    LeftRoom = null;
                    State = Stage.Ready;
                    Send(Message.Welcome, w => { w.Write(MyName); w.Write((byte)MyDeck.Faction); w.Write((short)MyDeck.Leader); });
                    GameLog.Event(LogCategory.Game, "lan_guest", "name", name, "deck", deck.Name);
                    Changed?.Invoke();
                    break;
                case Message.Resume when Hosting:
                    int number = r.ReadInt32();
                    r.ReadString();
                    int has = r.ReadInt32();
                    if (State == Stage.Battle && number == previous && previous != 0 && number != battle)
                    {
                        // Gone as the rematch began: its start, then all of it
                        Send(Message.Begin, w => w.Write(battle));
                        number = battle;
                        has = 0;
                    }
                    if (State != Stage.Battle || number != battle || has < 0 || has > Match.Events.Count)
                    {
                        Send(Message.Refused, w => w.Write("The host no longer has that battle."));
                        return;
                    }
                    sent = has;
                    resumed = true;
                    lostAt = -1;
                    SendEvents(); // all it missed, then word that it has it all
                    Send(Message.Resumed, w => { });
                    if (AskedRematch)
                        Send(Message.Rematch, w => w.Write(battle)); // asked while it was away
                    Say("the guest is back, " + (Match.Events.Count - has) + " events to send it");
                    Changed?.Invoke();
                    break;
                case Message.Command when Hosting && State == Stage.Battle:
                    MatchCommand command = MatchCommand.Read(r);
                    if (!Match.Apply(false, command))
                        Send(Message.CommandRefused, w => { });
                    break;

                // Guest
                case Message.Welcome when !Hosting:
                    TheirName = PlayerNames.Clean(r.ReadString()) ?? "Player";
                    TheirDeck = new Deck { Faction = (Faction)r.ReadByte(), Leader = r.ReadInt16() };
                    State = Stage.Ready;
                    Changed?.Invoke();
                    break;
                case Message.Refused when !Hosting:
                    Problem = r.ReadString();
                    if (State == Stage.Battle)
                        LanSave.Delete(Hosting); // the host has it no more
                    Close(Problem);
                    break;
                case Message.Begin when !Hosting && (State == Stage.Ready || Over):
                    // The first battle, or a rematch: the last one's events go (its screen is done with them)
                    battle = r.ReadInt32();
                    State = Stage.Battle;
                    ForBattle = this;
                    resumed = true;
                    received.Clear();
                    source = null;
                    Rejoined = false;
                    AskedRematch = TheyAskedRematch = false;
                    Keep();
                    Changed?.Invoke();
                    break;
                case Message.Resumed when !Hosting:
                    resumed = true;
                    lostAt = -1;
                    source?.Answered();
                    if (AskedRematch)
                        Send(Message.Rematch, w => w.Write(battle)); // asked while away
                    Say("back in the battle, after " + received.Count + " events");
                    Changed?.Invoke();
                    break;
                case Message.Event when !Hosting:
                    int index = r.ReadInt32();
                    MatchEvent e = MatchWire.ReadEvent(r);
                    if (index > received.Count)
                    {
                        JoinAgain("an event missed"); // the host sends the rest again
                        return;
                    }
                    if (index == received.Count)
                        received.Add(e);
                    if (e.Kind == MatchEventKind.Ended)
                        LanSave.Delete(Hosting);
                    source?.Answered();
                    break;
                case Message.CommandRefused when !Hosting:
                    source?.Refuse();
                    break;

                case Message.Rematch when State == Stage.Battle:
                    if (r.ReadInt32() != battle || !Over)
                        break; // asked for another battle than this one (sent before it began)
                    TheyAskedRematch = true;
                    Say("the other side asks for a rematch");
                    if (Hosting && AskedRematch && Over)
                        Rematch();
                    Changed?.Invoke();
                    break;

                case Message.Emote when State == Stage.Battle:
                    int line = r.ReadByte();
                    if (line < Chat.Lines.Length)
                        Hear(Chat.Lines[line]);
                    break;
                case Message.Text when State == Stage.Battle:
                    string text = Chat.Clean(r.ReadString());
                    if (text != null)
                        Hear(text);
                    break;

                case Message.Leave:
                    // The other side left on purpose: in the lobby, its seat is free again (host) or the game is gone
                    // (guest); in a battle it gave up first, and its events said so
                    if (Hosting && State != Stage.Battle)
                        GuestLeftRoom();
                    else if (State == Stage.Battle && !Over)
                    {
                        // Left a battle not over (given up on its own screen while away): it gave the battle up
                        if (Hosting)
                            Match.Apply(false, MatchCommand.GiveUp());
                        Close(Hosting ? null : TheirName + " left the battle.");
                    }
                    else
                        Close(State == Stage.Battle ? null : "The host closed the battle.");
                    break;
            }
        }

        // The session's turns, in the player's log too (a LAN battle's trouble is looked for there)
        private static void Say(string line)
        {
            Debug.Log("[lan] session: " + line + " @" + Time.realtimeSinceStartup.ToString("0.0") + " s");
        }

        private bool Send(Message kind, Action<BinaryWriter> write)
        {
            return link != null && link.Send(MatchWire.Bytes(w => { w.Write((byte)kind); write(w); }));
        }

        // A quick line of this player's (Chat.Lines' number) to the other, in a battle or after it: what was said, or null
        // (the other device away)
        public Said SendEmote(int line)
        {
            if (line < 0 || line >= Chat.Lines.Length || !Talking || !Send(Message.Emote, w => w.Write((byte)line)))
                return null;
            return Remember(true, Chat.Lines[line]);
        }

        // A message of this player's (kept to Chat's rule) to the other: what was said, or null (nothing of it left, the
        // other device away)
        public Said SendChat(string message)
        {
            string text = Chat.Clean(message);
            if (text == null || !Talking || !Send(Message.Text, w => w.Write(text)))
                return null;
            return Remember(true, text);
        }

        private bool Talking { get { return State == Stage.Battle && Linked; } }

        private Said Remember(bool mine, string text)
        {
            Said said = new Said { Mine = mine, Text = text };
            talk.Add(said);
            if (talk.Count > TalkKept)
                talk.RemoveAt(0);
            return said;
        }

        // The other player's line: heard, unless muted or too soon after the last
        private void Hear(string text)
        {
            float now = Time.realtimeSinceStartup;
            if (Muted || now - heardAt < HeardEvery)
                return;
            heardAt = now;
            Heard?.Invoke(Remember(false, text));
        }

        // A guest's move for the host's match
        private bool SendCommand(MatchCommand command)
        {
            return State == Stage.Battle && Linked && Send(Message.Command, command.Write);
        }

        //----------------------------------------The app in the background----------------------------------------//
        // A phone back after a long while: the other side has dropped the link by then (or is about to), so the guest joins
        // again at once
        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                pausedAt = Time.realtimeSinceStartup;
                return;
            }
            bool long_away = pausedAt >= 0 && Time.realtimeSinceStartup - pausedAt > PausedTooLong;
            pausedAt = -1;
            if (!long_away || State != Stage.Battle || Hosting)
                return; // the host's link finds out on its own, and the guest comes back to it
            if (lostAt < 0)
                lostAt = Time.realtimeSinceStartup;
            resumed = false;
            Say("back from the background after a long while: joining again");
            if (link != null)
                link.Dispose(); // the host hears it went, and takes the next link
            link = null;
            nextJoin = 0; // at once
            Changed?.Invoke();
        }

        // The game closed after a battle: the other player is told it left (not that it is away: no rematch is coming).
        // During one, it is away: the battle is kept, to be taken up again.
        private void OnApplicationQuit()
        {
            if (Over)
                Leave();
        }

        private void OnDestroy()
        {
            if (link != null)
                link.Dispose();
            if (discovery != null)
                discovery.Dispose();
            if (Current == this)
                Current = null;
        }

        //----------------------------------------The guest's battle----------------------------------------//
        // The events the host sent, and the guest's moves on their way to it
        private sealed class GuestSource : BattleSource
        {
            private readonly LanSession session;
            private bool waiting;

            public GuestSource(LanSession session)
            {
                this.session = session;
            }

            public override int Count { get { return session.received.Count; } }
            public override MatchEvent Seen(int index) { return session.received[index]; }
            public override MatchEvent All(int index) { return session.received[index]; }
            public override bool Waiting { get { return waiting; } }

            public override bool Submit(MatchCommand command)
            {
                if (waiting && command.Kind != CommandKind.GiveUp || !session.SendCommand(command))
                    return false;
                waiting = true;
                return true;
            }

            public void Answered()
            {
                waiting = false;
            }

            public void Refuse()
            {
                waiting = false;
                OnRefused();
            }
        }
    }

    // The guest's side of a LAN battle on the host's screen: its moves come over the link (LanSession), nothing to do here
    public sealed class LanOpponent : IOpponent
    {
        public Cysharp.Threading.Tasks.UniTask Act(Match match, bool seat, System.Threading.CancellationToken ct)
        {
            return Cysharp.Threading.Tasks.UniTask.CompletedTask;
        }
    }
}

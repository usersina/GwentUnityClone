using System;
using System.Collections.Generic;
using System.Linq;
using Gwent.Battle;
using Gwent.Core;
using Gwent.Net;
using Gwent.Rules;
using Gwent.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gwent.Menus
{
    // Play on LAN, laid out the way games' lobbies go. At the top, who you are: your leader, your name (remembered, kept to
    // PlayerNames' rule as it is typed) and the battle deck you play, with the way to the deck builder (the lobby opens
    // again on the way back).
    // Under it the battles hosted on your network, found as you look (a live dot says it searches; each its host's name,
    // faction and leader; one of another version shows and cannot be joined), one at an address for a network that hides
    // them, and Host a battle. Hosting or joined, the room: both players face to face (an empty seat breathes until
    // someone sits), what happens next, and the host's address to give. The main button does the next thing (Host a
    // battle, Start the battle, Rejoin), Back the way out (Stop hosting, Leave). A battle still on when the game closed
    // (LanSave) is offered first: rejoin it, or abandon it. What went wrong (turned away, the host gone, no port) shows
    // in place.
    // A UI Toolkit dialog: Assets/UI/Toolkit/LanLobby.uxml, an OpponentEntry.uxml per battle found.
    public static class LanLobby
    {
        private const string NameKey = "lanName";
        private const long TickMs = 100;
        private const int PulseTicks = 7; // the live dot and an empty seat breathe in and out every 0.7 s

        private static bool reopen; // the deck builder was opened from the lobby: the main menu opens it again

        public static void Open(Action on_close)
        {
            Lobby lobby = null;
            UIScreens.ShowDialog("LanLobby", UITheme.Current.lanLobby, root => lobby = new Lobby(root), () =>
            {
                lobby?.Closed();
                on_close?.Invoke();
            });
        }

        // True once when the main menu should open the lobby again: back from the deck builder the lobby sent the player to
        public static bool Reopen()
        {
            bool was = reopen;
            reopen = false;
            return was;
        }

        // The player's name for LAN battles: the one typed last, else the device's (both kept to the rule)
        public static string PlayerName
        {
            get { return PlayerNames.Clean(PlayerPrefs.GetString(NameKey, "")) ?? PlayerNames.Clean(SystemInfo.deviceName) ?? "Player"; }
        }

        private sealed class Lobby
        {
            private readonly VisualElement root;
            private readonly VisualElement you, youArt;
            private readonly TextField name;
            private readonly VisualElement nameRow;
            private readonly Label nameCount, nameHint, deckName, youDeck, problemText;
            private readonly VisualElement rejoin, rejoinArt;
            private readonly Label rejoinTitle, rejoinDetail;
            private readonly VisualElement browse, live, games, empty, addressRow;
            private readonly TextField address;
            private readonly Button addressLink;
            private readonly VisualElement room, seatMe, seatThem, reach;
            private readonly Label roomTitle, roomStatus, reachText;
            private readonly Button deckButton, copy, main, back;
            private LanSave kept;  // a battle still on when the game closed
            private LanSave shownKept;
            private LanDiscovery search;
            private string shownGames = "", shownSeats = "";
            private LanSession reachOf; // the session whose addresses show
            private string problem;
            private bool mainShown = true;
            private int ticks;
            private bool leaving; // the battle's loading took over: the session goes on

            public Lobby(VisualElement root)
            {
                this.root = root;
                you = root.Q("You");
                youArt = root.Q("YouArt");
                name = root.Q<TextField>("Name");
                nameRow = root.Q("NameRow");
                nameCount = root.Q<Label>("NameCount");
                nameHint = root.Q<Label>("NameHint");
                deckName = root.Q<Label>("DeckName");
                youDeck = root.Q<Label>("YouDeck");
                deckButton = root.Q<Button>("Button Deck");
                problemText = root.Q<Label>("Problem");
                rejoin = root.Q("Rejoin");
                rejoinArt = root.Q("RejoinArt");
                rejoinTitle = root.Q<Label>("RejoinTitle");
                rejoinDetail = root.Q<Label>("RejoinDetail");
                browse = root.Q("Browse");
                live = root.Q("Live");
                games = root.Q("Games");
                empty = root.Q("Empty");
                addressRow = root.Q("AddressRow");
                address = root.Q<TextField>("Address");
                addressLink = root.Q<Button>("Button Address");
                room = root.Q("Room");
                roomTitle = root.Q<Label>("RoomTitle");
                seatMe = root.Q("SeatMe");
                seatThem = root.Q("SeatThem");
                roomStatus = root.Q<Label>("RoomStatus");
                reach = root.Q("Reach");
                reachText = root.Q<Label>("ReachText");
                copy = root.Q<Button>("Button Copy");
                main = root.Q<Button>("Button Main");
                back = root.Q<Button>("Button Back");
                kept = LanSession.Current == null ? LanSave.Load() : null;

                // You: the name is kept to the rule as it is typed (what it may hold shows meanwhile) and kept as it is;
                // left with no letter or digit, it is the device's again
                name.value = PlayerName;
                name.RegisterValueChangedCallback(change => NameTyped(change.newValue));
                name.RegisterCallback<FocusInEvent>(_ => Editing(true));
                name.RegisterCallback<FocusOutEvent>(_ =>
                {
                    if (PlayerNames.Clean(name.value) == null)
                        name.SetValueWithoutNotify(PlayerName);
                    Editing(false);
                });
                Editing(false);
                name.RegisterCallback<KeyDownEvent>(key =>
                {
                    if (key.keyCode == KeyCode.Return || key.keyCode == KeyCode.KeypadEnter)
                        UIScreens.Focus(main);
                });
                root.Q("NameEdit").RegisterCallback<ClickEvent>(_ => UIScreens.Focus(name));
                ShowDeck();
                deckButton.clicked += ChangeDeck;

                root.Q<Button>("Button Abandon").clicked += () =>
                {
                    kept.Delete();
                    kept = LanSave.Load();
                    Refresh();
                };
                main.clicked += MainClicked;
                back.clicked += BackClicked;
                copy.clicked += CopyAddress;

                // Join by address: the field opens under the list on asking
                address.textEdition.placeholder = "Address, like 192.168.1.20";
                Show(addressRow, false);
                addressLink.clicked += () =>
                {
                    Show(addressRow, true);
                    Show(addressLink, false);
                    UIScreens.Focus(address);
                };
                root.Q<Button>("Button Join").clicked += JoinAddress;
                address.RegisterCallback<KeyDownEvent>(key =>
                {
                    if (key.keyCode == KeyCode.Return || key.keyCode == KeyCode.KeypadEnter)
                        JoinAddress();
                });

                root.schedule.Execute(Tick).Every(TickMs);
                Refresh();
                root.schedule.Execute(() => UIScreens.Focus(main));
            }

            private static LanSession Session { get { return LanSession.Current; } }

            private static void Show(VisualElement element, bool shown)
            {
                element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            }

            // The search runs while nothing is hosted or joined; what shows follows the session
            private void Tick()
            {
                LanSession session = Session;
                if (session != null && session.State == LanSession.Stage.Battle && !leaving)
                {
                    // The host started: to the battle (the guest follows its Begin)
                    leaving = true;
                    UIScreens.Close(root);
                    LoadingScreen.Load(SceneNames.Game);
                    return;
                }
                if (session == null && search == null && kept == null)
                    search = LanDiscovery.Search();
                else if (session != null && search != null)
                {
                    search.Dispose();
                    search = null;
                }
                search?.Update();

                // The live dot, and a seat waiting for its player, breathe
                ticks++;
                bool dim = ticks / PulseTicks % 2 == 1;
                live.EnableInClassList("lan__live--dim", dim);
                seatThem.Q(className: "lan__seat-wait")?.EnableInClassList("lan__seat-wait--dim", dim);
                Refresh();
            }

            private void Refresh()
            {
                LanSession session = Session;
                bool idle = session == null;
                bool rejoining = idle && kept != null; // the battle left on: rejoin it or abandon it, before anything else
                Show(you, idle && !rejoining);
                Show(problemText, problem != null);
                problemText.text = problem ?? "";
                Show(rejoin, rejoining);
                Show(browse, idle && !rejoining);
                Show(room, !idle);

                bool start = !idle && session.Hosting && session.State == LanSession.Stage.Ready;
                ShowMain(idle || start);
                main.text = rejoining ? "Rejoin" : idle ? "Host a battle" : "Start the battle";
                back.text = idle ? "Back" : session.Hosting ? "Stop hosting" : "Leave";

                if (rejoining)
                    ShowKept();
                else if (idle)
                    ShowGames();
                else
                    ShowRoom(session);
            }

            // The main button comes and goes with what there is to do: the keys go to it when it comes, to Back when it goes
            private void ShowMain(bool shown)
            {
                if (shown == mainShown)
                    return;
                mainShown = shown;
                Show(main, shown);
                root.schedule.Execute(() => UIScreens.Focus(shown ? main : back));
            }

            //----------------------------------------You----------------------------------------//
            // What is typed, kept to the rule (the cursor stays where it was, less what was left out), and remembered
            private void NameTyped(string typed)
            {
                string kept = PlayerNames.Typed(typed);
                if (kept != typed)
                {
                    int cursor = Mathf.Clamp(name.cursorIndex - (typed.Length - kept.Length), 0, kept.Length);
                    name.SetValueWithoutNotify(kept);
                    name.cursorIndex = name.selectIndex = cursor;
                }
                PlayerPrefs.SetString(NameKey, kept.Trim());
                nameCount.text = kept.Length + "/" + PlayerNames.MaxLength;
            }

            // Typing the name: its count in the field and the rule under it; else the pencil alone
            private void Editing(bool editing)
            {
                nameRow.EnableInClassList("lan__name-row--editing", editing);
                nameCount.text = name.value.Length + "/" + PlayerNames.MaxLength;
                nameHint.text = PlayerNames.Rule;
                Show(nameHint, editing);
            }

            // Your leader, and the battle deck you play (the default deck when yours cannot be played: why)
            private void ShowDeck()
            {
                Deck deck = BattleController.PlayerDeck(CardLibrary.Get(), out string unplayable);
                CardArt.Show(youArt, deck.Leader);
                deckName.text = unplayable == null ? deck.Name ?? deck.Faction.DisplayName() : "The default deck";
                youDeck.text = unplayable == null ? deck.Faction.DisplayName() + "  ·  " + deck.Cards.Count + " cards"
                    : "Yours can't be played: " + unplayable;
            }

            private void ChangeDeck()
            {
                reopen = true;
                UIScreens.Close(root);
                LoadingScreen.Load(SceneNames.Deck);
            }

            private static string FactionAndLeader(Deck deck)
            {
                return deck.Faction.DisplayName() + "  ·  " + LeaderName(deck.Leader);
            }

            private static string LeaderName(int leader)
            {
                CardDatabase db = CardLibrary.Get();
                return db.Contains(leader) ? db.Get(leader).DisplayName : "";
            }

            //----------------------------------------A battle still on----------------------------------------//
            private void ShowKept()
            {
                if (kept == shownKept)
                    return;
                shownKept = kept;
                rejoinTitle.text = "Your battle with " + kept.TheirName + " is still on";
                rejoinDetail.text = Ago(kept.Saved);
                if (kept.TheirDeck != null)
                    CardArt.Show(rejoinArt, kept.TheirDeck.Leader);
            }

            private static string Ago(DateTime saved)
            {
                TimeSpan ago = DateTime.UtcNow - saved;
                if (ago.TotalMinutes < 1)
                    return "Left just now";
                if (ago.TotalHours < 1)
                    return "Left " + Count((int)ago.TotalMinutes, "minute") + " ago";
                return "Left " + Count((int)ago.TotalHours, "hour") + " ago";
            }

            private static string Count(int n, string unit)
            {
                return n + " " + unit + (n == 1 ? "" : "s");
            }

            //----------------------------------------The battles on the network----------------------------------------//
            private void ShowGames()
            {
                List<LanDiscovery.Game> found = search != null ? search.Games.OrderBy(game => game.Name).ToList() : new List<LanDiscovery.Game>();
                string key = string.Join("|", found.Select(game => game.Name + game.Address + game.Port + game.Leader + game.Compatible));
                Show(empty, found.Count == 0);
                Show(games, found.Count > 0);
                if (key == shownGames)
                    return;
                shownGames = key;
                games.Clear();
                CardDatabase db = CardLibrary.Get();
                foreach (LanDiscovery.Game game in found)
                {
                    MenuEntry entry = UITheme.Current.opponentEntry.Instantiate().Q<MenuEntry>();
                    entry.name = "Game " + game.Name;
                    entry.Q<Label>("Title").text = game.Name;
                    bool known = db.Contains(game.Leader);
                    entry.Q<Label>("Detail").text = !game.Compatible ? "Another version of the game (" + game.Version + "): can't be joined"
                        : known ? db.Get(game.Leader).faction.DisplayName() + "  ·  " + db.Get(game.Leader).DisplayName : game.Address;
                    entry.EnableInClassList("lan__game--other", !game.Compatible);
                    if (known)
                    {
                        CardArt.Show(entry.Q("Art"), game.Leader);
                        entry.Q("Question").RemoveFromHierarchy();
                    }
                    else
                        entry.Q("Art").RemoveFromHierarchy();
                    // "Join" lights up on the battle under the pointer (or the keys)
                    VisualElement chosen = entry.Q("Chosen");
                    if (game.Compatible)
                    {
                        Label join = new Label("Join") { pickingMode = PickingMode.Ignore };
                        join.AddToClassList("lan__join");
                        chosen.parent.Insert(chosen.parent.IndexOf(chosen), join);
                    }
                    chosen.RemoveFromHierarchy();
                    LanDiscovery.Game picked = game;
                    entry.clicked += () => Join(picked.Address, picked.Port, picked.Compatible ? null
                        : "That battle runs version " + picked.Version + " of the game; yours is " + Application.version + ".");
                    games.Add(entry);
                }
            }

            // "192.168.1.20", or "192.168.1.20:26948" for a host on another port than the game's
            private void JoinAddress()
            {
                string typed = address.value.Trim();
                ushort port = LanLink.GamePort;
                int colon = typed.LastIndexOf(':');
                if (colon > 0 && ushort.TryParse(typed.Substring(colon + 1), out ushort given))
                {
                    port = given;
                    typed = typed.Substring(0, colon);
                }
                Join(typed, port, typed.Length == 0 ? "Type the address the host's screen shows." : null);
            }

            private void Join(string at, ushort port, string refused)
            {
                if (Session != null)
                    return;
                problem = refused;
                if (refused == null)
                {
                    LanSession joined = LanSession.Join(at, port, MyName(), BattleDeck());
                    if (joined.State == LanSession.Stage.Closed)
                        problem = joined.Problem;
                    else
                        joined.Changed += () => SessionChanged(joined);
                }
                Refresh();
            }

            //----------------------------------------The room----------------------------------------//
            // Both players face to face (you on the left), what happens next, and while the host waits, its address
            private void ShowRoom(LanSession session)
            {
                bool hosting = session.Hosting;
                LanSession.Stage stage = session.State;
                roomTitle.text = hosting ? "Your battle" : session.TheirName != null ? session.TheirName + "'s battle" : "Joining a battle";
                switch (stage)
                {
                    case LanSession.Stage.Ready:
                        roomStatus.text = hosting ? session.TheirName + " is here. Start the battle when you're ready."
                            : "Waiting for " + session.TheirName + " to start the battle";
                        break;
                    default:
                        roomStatus.text = ""; // the empty seat says what it waits for
                        break;
                }
                Show(roomStatus, roomStatus.text.Length > 0);

                string seats = session.MyName + session.MyDeck?.Leader + "|" + session.TheirName + session.TheirDeck?.Leader + "|" + stage;
                if (seats != shownSeats)
                {
                    shownSeats = seats;
                    bool ready = stage == LanSession.Stage.Ready;
                    Seat(seatMe, session.MyName, session.MyDeck, hosting ? "Host" : ready ? "Ready" : "Joining");
                    if (ready && session.TheirDeck != null)
                        Seat(seatThem, session.TheirName, session.TheirDeck, hosting ? "Ready" : "Host");
                    else
                        EmptySeat(seatThem, hosting ? "Waiting for a player" : "Joining…");
                }

                bool waiting = hosting && stage == LanSession.Stage.Waiting;
                Show(reach, waiting);
                if (waiting && reachOf != session)
                {
                    reachOf = session;
                    string port = session.Port != LanLink.GamePort ? ":" + session.Port : "";
                    List<string> at = LanDiscovery.JoinableAddresses().Take(2).Select(a => a + port).ToList();
                    Show(reach, at.Count > 0);
                    reachText.text = "Your address: " + string.Join(" or ", at);
                    copy.userData = at.FirstOrDefault();
                }
            }

            // A player's seat: their leader in a big ring, their faction's crest, name, faction and leader, and a tag
            private static void Seat(VisualElement seat, string player, Deck deck, string tag)
            {
                seat.Clear();
                seat.AddToClassList("lan__seat--filled");
                VisualElement portrait = Add(seat, new VisualElement(), "lan__seat-portrait");
                CardArt.Show(Add(portrait, new VisualElement(), "lan__seat-art"), deck.Leader);
                string emblem = BattleBanner.FactionEmblem(deck.Faction);
                if (emblem != null)
                    Add(seat, new VisualElement(), "lan__seat-crest").style.backgroundImage = new StyleBackground(SpriteCache.Load("W3/Notifications/" + emblem));
                Add(seat, new Label(player), "lan__seat-name");
                Add(seat, new Label(FactionAndLeader(deck)), "lan__seat-detail");
                Add(seat, new Label(tag), "lan__seat-chip");
            }

            // A seat no one sits in yet: a ring breathing in and out (Tick), and what it waits for
            private static void EmptySeat(VisualElement seat, string title)
            {
                seat.Clear();
                seat.RemoveFromClassList("lan__seat--filled");
                Add(seat, new VisualElement(), "lan__seat-wait");
                Add(seat, new Label(title), "lan__seat-name");
            }

            private static T Add<T>(VisualElement parent, T child, string style) where T : VisualElement
            {
                child.AddToClassList(style);
                child.pickingMode = PickingMode.Ignore;
                parent.Add(child);
                return child;
            }

            private void CopyAddress()
            {
                if (!(copy.userData is string at))
                    return;
                GUIUtility.systemCopyBuffer = at;
                copy.text = "Copied";
                root.schedule.Execute(() => copy.text = "Copy").StartingIn(1500);
            }

            //----------------------------------------The buttons----------------------------------------//
            // Host a battle, Rejoin (the battle left on), or Start the battle (hosting, someone there)
            private void MainClicked()
            {
                LanSession session = Session;
                if (session == null && kept != null)
                {
                    // Rejoin: Tick takes it to the battle
                    problem = null;
                    LanSession rejoined = LanSession.Rejoin(kept);
                    if (rejoined.State == LanSession.Stage.Closed)
                    {
                        problem = rejoined.Problem;
                        kept = LanSave.Load();
                    }
                }
                else if (session == null)
                {
                    problem = null;
                    LanSession hosted = LanSession.Host(MyName(), BattleDeck());
                    if (hosted.State == LanSession.Stage.Closed)
                        problem = hosted.Problem;
                    else
                        hosted.Changed += () => SessionChanged(hosted);
                }
                else if (session.Hosting && session.State == LanSession.Stage.Ready)
                    session.StartBattle(); // Tick takes it to the battle
                Refresh();
            }

            // Back closes the lobby; hosting or joined, it stops that first (the lobby stays)
            private void BackClicked()
            {
                if (Session == null)
                {
                    UIScreens.Close(root);
                    return;
                }
                Session.Leave();
                problem = null;
                Refresh();
            }

            // A session that closed by itself (the host gone, the guest turned away): why, in the lobby
            private void SessionChanged(LanSession session)
            {
                if (session.State == LanSession.Stage.Closed && !leaving)
                    problem = session.Problem;
                if (root.panel != null)
                    Refresh();
            }

            private string MyName()
            {
                return PlayerNames.Clean(name.value) ?? PlayerName;
            }

            // The deck builder's battle deck (the default deck when it cannot be played: said under your name)
            private static Deck BattleDeck()
            {
                return BattleController.PlayerDeck(CardLibrary.Get(), out _);
            }

            // The dialog closed (Back, Escape): the search stops, and a session not on its way to a battle closes
            public void Closed()
            {
                search?.Dispose();
                search = null;
                if (!leaving)
                    Session?.Leave();
            }
        }
    }
}

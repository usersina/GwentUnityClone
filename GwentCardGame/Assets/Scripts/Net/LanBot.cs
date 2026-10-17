using System.Linq;
using Gwent.Battle;
using Gwent.Core;
using Gwent.Rules;
using Gwent.UI;
using UnityEngine;

namespace Gwent.Net
{
    // A LAN battle played by the AI on both devices, for the connection test (LanProbe: "-lan bothost", "-lan botjoin
    // [address]"): it hosts (starting once someone joins) or joins (the first game found, or the address), loads the
    // battle, then plays this device's side from what its screen shows (the opponent's hand hidden), as Normal does, through
    // the screen's own moves; "-lan botrejoin" takes up the battle kept on disk (LanSave). Options (LanProbe.Option):
    // lanGiveUpAfter (gives up after that many moves of its own), lanRedrawWait (seconds before keeping its hand),
    // lanRejoinAs (host or guest: the battle kept as which, when a computer keeps both), lanRematches (asks for that many
    // rematches, a couple of seconds after each battle), lanEmoteEvery and lanChatEvery (says a random quick line, or one
    // of Messages, every that many seconds of a battle), lanChatBurst (that many messages at once: the other hears the
    // first), lanMute (1: hears nothing of the other), lanTalkOpen (opens its talk panel that many seconds into each
    // battle, for 6 s: screenshots), lanStallAfter (no more moves in its turns after that many: the turn clock's tests,
    // the host's clock lanTurnTime seconds long, LanSession). What it says and hears is logged. Logged as [lan] lines; in
    // batch mode the game quits a few seconds after the last battle ends.
    public class LanBot : MonoBehaviour
    {
        private const float SearchFor = 15f;
        private const float QuitAfter = 4f;
        private const float BatchSpeed = 4f; // a test run's battle, sped up (its waits and animations are in game time)
        private const float RematchAfter = 2f;

        private bool hosting;
        private string address;
        private float started;
        private LanDiscovery search;
        private bool loading;
        private float overAt = -1;
        private int moves;
        private int giveUpAfter = -1;
        private int stallAfter = -1;
        private bool stalling;
        private float redrawWait;
        private float redrawSeen = -1;
        private float lastMove;   // when this side last moved, or the battle last changed
        private int lastSeen = -1;
        private int rematches;
        private float emoteEvery = -1, chatEvery = -1, talkOpen = -1;
        private int chatBurst = 1;
        private bool mute;
        private float emotedAt, chattedAt, battleAt;
        private int nextMessage;
        private float talkOpenedAt = -1;
        private bool talkClosed;
        private LanSession listening;

        // What it says (the other side should hear each as Chat.Clean makes it)
        private static readonly string[] Messages =
        {
            "Evening, witcher.",
            "\u00C7a va? Tr\u00E8s bien, merci!",
            "<b>not bold</b> & <color=red>not red</color> </noparse> <noparse>",
            "Stra\u00DFe 12 \u00BD \u2013 a dash dropped",
            "Skull \u2620 and smile \U0001F600 dropped",
            "  spaced    out   words  ",
            "\tTab\u00A0and\u00A0no-break",
            "A long message that runs past the hundred characters a message may hold, so that it is cut short right there and no further",
        };
        private BattleController playing; // the battle screen played on (a rematch's is another)
        private int playingBattle;        // its battle's number in the session

        public static void Begin(bool host, string address)
        {
            LanBot bot = Make();
            bot.hosting = host;
            bot.address = address;
            Deck deck = BattleController.PlayerDeck(CardLibrary.Get(), out _);
            string name = (host ? "Host bot " : "Guest bot ") + SystemInfo.deviceName;
            if (host)
                LanSession.Host(name, deck);
            else if (address != null)
                bot.Join(address, deck, name);
            else
                bot.search = LanDiscovery.Search();
            Log((host ? "hosting" : "joining") + " as " + name + " with " + deck.Name + " (" + deck.Faction + ")");
        }

        // Takes up the battle kept on disk
        public static void Rejoin()
        {
            LanBot bot = Make();
            string side = LanProbe.Option("lanRejoinAs"); // host or guest: two copies on one computer keep both
            LanSave save = side == "host" ? LanSave.Load(true) : side == "guest" ? LanSave.Load(false) : LanSave.Load();
            if (save == null)
            {
                Log("FAIL: no battle kept to rejoin");
                return;
            }
            bot.hosting = save.Hosting;
            LanSession session = LanSession.Rejoin(save);
            Log("rejoining the battle with " + save.TheirName + (save.Hosting ? " as its host" : " at " + save.Address + ":" + save.Port)
                + (session.State == LanSession.Stage.Closed ? ": FAIL, " + session.Problem : ""));
        }

        private static LanBot Make()
        {
            // A test run takes no input from this computer's controllers: they reach every game on it at once (a press
            // was a Submit on two bots' end screens: both asked for a rematch)
            if (Application.isBatchMode)
                foreach (UnityEngine.InputSystem.InputDevice device in UnityEngine.InputSystem.InputSystem.devices.ToArray())
                    if (device is UnityEngine.InputSystem.Joystick || device is UnityEngine.InputSystem.Gamepad)
                        UnityEngine.InputSystem.InputSystem.DisableDevice(device);
            GameObject go = new GameObject("LanBot");
            DontDestroyOnLoad(go);
            LanBot bot = go.AddComponent<LanBot>();
            bot.started = Time.realtimeSinceStartup;
            if (int.TryParse(LanProbe.Option("lanGiveUpAfter"), out int give_up))
                bot.giveUpAfter = give_up;
            if (int.TryParse(LanProbe.Option("lanStallAfter"), out int stall))
                bot.stallAfter = stall;
            bot.redrawWait = Mathf.Max(0, Seconds("lanRedrawWait"));
            if (int.TryParse(LanProbe.Option("lanRematches"), out int rematches))
                bot.rematches = rematches;
            bot.emoteEvery = Seconds("lanEmoteEvery");
            bot.chatEvery = Seconds("lanChatEvery");
            bot.talkOpen = Seconds("lanTalkOpen");
            if (int.TryParse(LanProbe.Option("lanChatBurst"), out int burst))
                bot.chatBurst = Mathf.Max(1, burst);
            bot.mute = LanProbe.Option("lanMute") == "1";
            return bot;
        }

        private static float Seconds(string option)
        {
            return float.TryParse(LanProbe.Option(option), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float seconds)
                ? seconds : -1;
        }

        private static void Log(string line)
        {
            Debug.Log("[lan] bot: " + line + " @" + Time.realtimeSinceStartup.ToString("0.0") + " s");
        }

        private void Join(string at, Deck deck, string name)
        {
            ushort port = LanLink.GamePort;
            int colon = at.LastIndexOf(':');
            if (colon > 0 && ushort.TryParse(at.Substring(colon + 1), out ushort given))
            {
                port = given;
                at = at.Substring(0, colon);
            }
            LanSession.Join(at, port, name, deck);
        }

        private void Update()
        {
            LanSession session = LanSession.Current;
            if (search != null)
            {
                search.Update();
                LanDiscovery.Game game = search.Games.FirstOrDefault(found => found.Compatible);
                if (game != null || Time.realtimeSinceStartup - started > SearchFor)
                {
                    search.Dispose();
                    search = null;
                    if (game == null)
                    {
                        Log("FAIL: no game found");
                        return;
                    }
                    Log("found " + game.Name + " at " + game.Address + ":" + game.Port);
                    Join(game.Address + ":" + game.Port, BattleController.PlayerDeck(CardLibrary.Get(), out _), "Guest bot " + SystemInfo.deviceName);
                }
                return;
            }
            if (session == null)
            {
                Play(); // the other side left after the battle: its end
                return;
            }
            if (hosting && session.State == LanSession.Stage.Ready)
            {
                Log(session.TheirName + " joined with " + session.TheirDeck.Name + ": starting");
                session.StartBattle();
            }
            if (session.State == LanSession.Stage.Battle && !loading)
            {
                loading = true;
                LoadingScreen.Load(SceneNames.Game);
            }
            Play();
        }

        // Says a quick line or a message now and then, logs what it hears, opens its talk panel a while (all by option)
        private void Talk(BattleController battle)
        {
            LanSession session = LanSession.Current;
            if (session == null)
                return;
            if (session != listening)
            {
                listening = session;
                session.Muted = mute;
                session.Heard += said => Log("heard \"" + said.Text + "\"");
            }
            float now = Time.realtimeSinceStartup;
            if (talkOpen >= 0 && talkOpenedAt < 0 && now - battleAt > talkOpen && !UIOverlay.HasModal)
            {
                ChatView.KeyPressed(true);
                if (GameObject.Find("Talk") != null)
                    talkOpenedAt = now;
            }
            else if (talkOpenedAt >= 0 && !talkClosed && now - talkOpenedAt > 6)
            {
                talkClosed = true;
                GameObject talk = GameObject.Find("Talk");
                if (talk != null)
                    UIOverlay.CloseModal(talk);
            }
            if (emoteEvery > 0 && now - emotedAt > emoteEvery)
            {
                emotedAt = now;
                LanSession.Said said = ChatView.Say(Random.Range(0, Chat.Lines.Length)); // as a player says it
                if (said != null)
                    Log("says \"" + said.Text + "\"");
            }
            if (chatEvery > 0 && now - chattedAt > chatEvery)
            {
                chattedAt = now;
                for (int i = 0; i < chatBurst; i++)
                {
                    string message = Messages[nextMessage++ % Messages.Length];
                    LanSession.Said said = i == 0 ? ChatView.Say(message) : session.SendChat(message); // a burst: past the screen's limit
                    if (said != null)
                        Log("says \"" + said.Text + "\"" + (i > 0 ? " (burst)" : ""));
                }
            }
        }

        // This side's move when the screen waits for it: the hand kept, itself first, a choice as the AI makes it, a turn
        // as Normal plays it (refused: a pass)
        private void Play()
        {
            BattleController battle = BattleController.Instance;
            if (battle == null)
                return;
            if (battle != playing)
            {
                // A battle begins (the first, or a rematch)
                playing = battle;
                playingBattle = LanSession.Current != null ? LanSession.Current.Battle : 0;
                overAt = redrawSeen = -1;
                moves = 0;
                lastSeen = -1;
                lastMove = emotedAt = chattedAt = battleAt = Time.realtimeSinceStartup;
                talkOpenedAt = -1;
                talkClosed = false;
            }
            Talk(battle);
            if (Application.isBatchMode)
                Time.timeScale = BatchSpeed;
            if (battle.IsBattleOver)
            {
                LanSession session = LanSession.Current;
                if (overAt < 0)
                {
                    overAt = Time.realtimeSinceStartup;
                    Log("battle over: " + battle.battleState + " " + string.Join("-", battle.PlayerInfo.RoundsScore) + " vs " + string.Join("-", battle.EnemyInfo.RoundsScore)
                        + " after " + moves + " moves of mine");
                }
                else if (rematches > 0 && session != null && session.Over && session.Linked && !session.AskedRematch
                    && Time.realtimeSinceStartup - overAt > RematchAfter)
                {
                    rematches--;
                    Log("asking for a rematch" + (session.TheyAskedRematch ? " (the other side asked)" : ""));
                    session.AskRematch();
                }
                else if (Application.isBatchMode && Time.realtimeSinceStartup - overAt > QuitAfter && (session == null
                    || rematches == 0 && !session.AskedRematch && session.Battle == playingBattle)) // no rematch asked or loading
                    Application.Quit();
                return;
            }
            if (battle.TurnPicker.activeInHierarchy)
            {
                battle.ChooseFirstPlayer(true);
                battle.TurnPicker.SetActive(false);
                return;
            }
            MatchState seen = battle.Latest;
            if (seen != null && seen.GetHashCode() != lastSeen)
            {
                lastSeen = seen.GetHashCode();
                lastMove = Time.realtimeSinceStartup;
            }
            if (Time.realtimeSinceStartup - lastMove > 15)
            {
                // Nothing for a while: what the screen waits for
                lastMove = Time.realtimeSinceStartup;
                LanSession lan = LanSession.Current;
                Log("still: " + (seen != null ? seen.Phase + (seen.Turn ? " (mine)" : " (theirs)") + (seen.Choice != null ? " choice " + seen.Choice.Kind : "") : "nothing seen")
                    + ", presenting " + battle.IsPresenting + ", modal " + UIOverlay.HasModal + ", state " + battle.battleState
                    + (lan != null ? ", linked " + lan.Linked + ", " + lan.State : ""));
            }
            // The other side gone a while: the notice's way out (the host ends the battle, the guest leaves it)
            LanNotice notice = FindAnyObjectByType<LanNotice>();
            UnityEngine.UI.Button way_out = notice != null ? notice.GetComponentInChildren<UnityEngine.UI.Button>() : null;
            if (way_out != null && way_out.gameObject.activeInHierarchy)
            {
                Log("the other side away " + LanSession.Current?.AwayFor.ToString("0") + " s: " + way_out.GetComponentInChildren<TMPro.TextMeshProUGUI>().text);
                way_out.onClick.Invoke();
                return;
            }
            if (seen == null || battle.IsPresenting || UIOverlay.HasModal)
                return;
            if (giveUpAfter >= 0 && moves >= giveUpAfter && battle.CanGiveUp)
            {
                Log("giving up after " + moves + " moves");
                battle.GiveUp();
                return;
            }
            if (stallAfter >= 0 && moves >= stallAfter && seen.Phase != MatchPhase.Redraw && seen.Turn)
            {
                if (!stalling)
                    Log("stalling after " + moves + " moves");
                stalling = true;
                return; // the clock runs out
            }
            MatchCommand command;
            if (seen.Phase == MatchPhase.Redraw && battle.PlayerInfo.CanExchange)
            {
                // The hand kept (after a while, when asked to take its time)
                if (redrawSeen < 0)
                    redrawSeen = Time.realtimeSinceStartup;
                if (Time.realtimeSinceStartup - redrawSeen < redrawWait)
                    return;
                command = MatchCommand.EndRedraw();
            }
            else if (seen.Phase == MatchPhase.Choice && seen.Choice != null && seen.Choice.Seat)
            {
                battle.Panel.SetActive(false);
                command = AiBrain.Answer(seen.ToBoard(battle.Db), true, seen.Choice);
            }
            else if (seen.Phase == MatchPhase.Turn && seen.Turn && battle.IsPlayerActionAllowed)
            {
                AiContext ctx = seen.ToBoard(battle.Db).AiContextFor(true);
                ctx.Difficulty = AiDifficulty.Normal;
                ctx.Seed = moves;
                command = MatchCommand.From(AiBrain.Decide(ctx));
            }
            else
                return;
            moves++;
            lastMove = Time.realtimeSinceStartup;
            if (!battle.Command(command) && command.Kind == CommandKind.Play)
                battle.Command(MatchCommand.Pass());
        }
    }
}

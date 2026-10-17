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
    // rematches, a couple of seconds after each battle). Logged as [lan] lines; in batch mode the game quits a few seconds
    // after the last battle ends.
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
        private float redrawWait;
        private float redrawSeen = -1;
        private float lastMove;   // when this side last moved, or the battle last changed
        private int lastSeen = -1;
        private int rematches;
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
            GameObject go = new GameObject("LanBot");
            DontDestroyOnLoad(go);
            LanBot bot = go.AddComponent<LanBot>();
            bot.started = Time.realtimeSinceStartup;
            if (int.TryParse(LanProbe.Option("lanGiveUpAfter"), out int give_up))
                bot.giveUpAfter = give_up;
            if (float.TryParse(LanProbe.Option("lanRedrawWait"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float wait))
                bot.redrawWait = wait;
            if (int.TryParse(LanProbe.Option("lanRematches"), out int rematches))
                bot.rematches = rematches;
            return bot;
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
                lastMove = Time.realtimeSinceStartup;
            }
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

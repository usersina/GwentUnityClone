using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Gwent.Net
{
    // The local network's connection test, started only by a flag (as EffectsBenchmark is): on the desktop "-lan host" or
    // "-lan join [address]", on Android the launch extras "lan" and "address" ("adb shell am start ... --es lan join
    // --es address 192.168.1.20", or "192.168.1.20:26948" for a host on another port than the game's); in the editor,
    // LanProbe.Begin("host"). A host answers searches and waits for guests, one at a time, sending back whatever they
    // send. A guest finds a game (or joins the address given) and checks the link: round trips, one large message (split
    // into packets and put back together), a burst of small ones sent at once (all back, in order), then leaves ("join")
    // or stays until the host goes ("stay": to see how a lost player is noticed). Each step is logged ("[lan]") and shown
    // on screen. "bothost" and "botjoin [address]" play a whole LAN battle instead, the AI on both devices (LanBot);
    // "botrejoin" takes up the battle kept on disk (LanSave); "botsolo" plays battles against the AI alone (LanBot).
    public class LanProbe : MonoBehaviour
    {
        private const float SearchFor = 15f;
        private const float WaitFor = 10f;         // for the link, and for each answer
        private const int RoundTrips = 20;
        private const int LargeSize = 30000;
        private const int Burst = 200;
        private const int ShownLines = 24;

        private static LanProbe instance;
        private readonly List<string> lines = new List<string>();
        private LanLink link;
        private LanDiscovery discovery;
        private readonly Queue<byte[]> received = new Queue<byte[]>();
        private GUIStyle style;

        // Called as the menu starts: does nothing unless the flag asks for it
        public static void StartIfRequested()
        {
            (string role, string address) = ReadFlag();
            if (role == "host" || role == "join" || role == "stay")
                Begin(role, address);
            else if (role == "bothost" || role == "botjoin")
                LanBot.Begin(role == "bothost", address);
            else if (role == "botrejoin")
                LanBot.Rejoin();
            else if (role == "botsolo")
                LanBot.BeginSolo();
        }

        // Runs it ("host", or "join" or "stay" with the address to join or null to search)
        public static void Begin(string role, string address = null)
        {
            if (instance != null)
                return;
            GameObject go = new GameObject("LanProbe");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<LanProbe>();
            CancellationToken ct = go.GetCancellationTokenOnDestroy();
            if (role == "host")
                instance.Host(ct).Forget();
            else
                instance.Join(address, role == "stay", ct).Forget();
        }

        // Stops it (the editor's tests)
        public static void End()
        {
            if (instance != null)
                Destroy(instance.gameObject);
        }

        // A launch option of the test's: "-name value" on the desktop, the extra "name" on Android (null when not given)
        internal static string Option(string name)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaClass player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject intent = activity.Call<AndroidJavaObject>("getIntent"))
                    return intent.Call<string>("getStringExtra", name);
            }
            catch (Exception)
            {
                return null;
            }
#else
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-" + name);
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
#endif
        }

        private static (string role, string address) ReadFlag()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaClass player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject intent = activity.Call<AndroidJavaObject>("getIntent"))
                    return (intent.Call<string>("getStringExtra", "lan"), intent.Call<string>("getStringExtra", "address"));
            }
            catch (Exception)
            {
                return (null, null);
            }
#else
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-lan");
            if (at < 0 || at + 1 >= args.Length)
                return (null, null);
            string address = at + 2 < args.Length && !args[at + 2].StartsWith("-") ? args[at + 2] : null;
            return (args[at + 1], address);
#endif
        }

        private void Log(string line)
        {
            Debug.Log("[lan] " + line + " @" + Time.realtimeSinceStartup.ToString("0.0") + " s"); // when, from the start
            lines.Add(line);
            if (lines.Count > ShownLines)
                lines.RemoveAt(0);
        }

        private void Update()
        {
            discovery?.Update();
            link?.Update();
        }

        //----------------------------------------Host----------------------------------------//
        private async UniTaskVoid Host(CancellationToken ct)
        {
            link = LanLink.Host();
            if (link == null)
            {
                Log("FAIL: no free port from " + LanLink.GamePort + " (other programs hold them)");
                return;
            }
            string name = SystemInfo.deviceName;
            discovery = LanDiscovery.Answer(name, -1, link.Port);
            Log("hosting \"" + name + "\" (" + Application.platform + ", version " + Application.version + ") on port " + link.Port
                + (discovery == null ? "; not answering searches (port " + LanDiscovery.Port + " taken)" : ""));
            Log("this device's addresses: " + string.Join(", ", LanDiscovery.LocalAddresses()));
            link.Connected += () => Log("guest joined from " + link.OtherAddress + " (version " + link.OtherVersion + ")");
            link.Received += message => link.Send(message); // sent back as it came
            link.Disconnected += reason => Log("guest gone: " + reason);
            await UniTask.Never(ct);
        }

        //----------------------------------------Guest----------------------------------------//
        private async UniTaskVoid Join(string address, bool stay, CancellationToken ct)
        {
            Stopwatch total = Stopwatch.StartNew();
            ushort port = LanLink.GamePort;
            if (address == null)
            {
                discovery = LanDiscovery.Search();
                Log("searching from " + string.Join(", ", LanDiscovery.LocalAddresses()) + "...");
                Stopwatch searching = Stopwatch.StartNew();
                LanDiscovery.Game game = null;
                while (game == null && searching.Elapsed.TotalSeconds < SearchFor)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    game = discovery.Games.FirstOrDefault(found => found.Compatible);
                }
                foreach (LanDiscovery.Game found in discovery.Games)
                    Log("found \"" + found.Name + "\" at " + found.Address + ":" + found.Port + ", version " + found.Version
                        + (found.Compatible ? "" : " (not this game's version)"));
                discovery.Dispose();
                discovery = null;
                if (game == null)
                {
                    Log("FAIL: no game found in " + SearchFor + " s");
                    return;
                }
                Log("found in " + searching.ElapsedMilliseconds + " ms");
                address = game.Address;
                port = game.Port;
            }

            int colon = address.LastIndexOf(':'); // "192.168.1.20:26948": a host on another port than the game's
            if (colon > 0 && ushort.TryParse(address.Substring(colon + 1), out ushort given))
            {
                port = given;
                address = address.Substring(0, colon);
            }
            Log("joining " + address + ":" + port + "...");
            Stopwatch joining = Stopwatch.StartNew();
            link = LanLink.Join(address, port);
            if (link == null)
            {
                Log("FAIL: \"" + address + "\" is not an address");
                return;
            }
            string gone = null;
            link.Received += message => received.Enqueue(message);
            link.Disconnected += reason => gone = reason;
            while (link.State == LanLink.Status.Joining && joining.Elapsed.TotalSeconds < WaitFor)
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            if (link.State != LanLink.Status.Connected)
            {
                Log("FAIL: not joined: " + (link.Reason ?? "no answer in " + WaitFor + " s"));
                return;
            }
            Log("joined in " + joining.ElapsedMilliseconds + " ms (host version " + link.OtherVersion + ")");

            // Round trips: each message sent once the last came back
            List<double> trips = new List<double>();
            for (int i = 0; i < RoundTrips; i++)
            {
                Stopwatch trip = Stopwatch.StartNew();
                link.Send(BitConverter.GetBytes(i));
                byte[] back = await Next(ct);
                if (back == null || BitConverter.ToInt32(back, 0) != i)
                {
                    Log("FAIL: round trip " + i + (back == null ? " never came back" : " came back wrong") + Gone(gone));
                    return;
                }
                trips.Add(trip.Elapsed.TotalMilliseconds);
            }
            Log("round trips: " + trips.Min().ToString("0") + " / " + trips.Average().ToString("0") + " / " + trips.Max().ToString("0")
                + " ms (shortest / average / longest; a frame's wait on each side included)");

            // One large message
            byte[] large = new byte[LargeSize];
            new System.Random(7).NextBytes(large);
            Stopwatch carrying = Stopwatch.StartNew();
            link.Send(large);
            byte[] large_back = await Next(ct);
            if (large_back == null || !large_back.SequenceEqual(large))
            {
                Log("FAIL: the large message " + (large_back == null ? "never came back" : "came back changed") + Gone(gone));
                return;
            }
            Log("large message (" + LargeSize / 1000 + " KB) there and back in " + carrying.ElapsedMilliseconds + " ms");

            // A burst: all sent in one frame
            Stopwatch bursting = Stopwatch.StartNew();
            for (int i = 0; i < Burst; i++)
                link.Send(BitConverter.GetBytes(1000 + i));
            for (int i = 0; i < Burst; i++)
            {
                byte[] back = await Next(ct);
                if (back == null || BitConverter.ToInt32(back, 0) != 1000 + i)
                {
                    Log("FAIL: burst message " + i + (back == null ? " never came back" : " came back out of order") + Gone(gone));
                    return;
                }
            }
            Log(Burst + " messages sent at once, all back in order in " + bursting.ElapsedMilliseconds + " ms");

            if (!stay)
            {
                link.Dispose();
                Log("PASS in " + total.Elapsed.TotalSeconds.ToString("0.0") + " s; left the game");
                return;
            }
            Log("PASS in " + total.Elapsed.TotalSeconds.ToString("0.0") + " s; staying in the game");
            Stopwatch staying = Stopwatch.StartNew();
            while (link.State == LanLink.Status.Connected)
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            Log("host gone after " + staying.Elapsed.TotalSeconds.ToString("0.0") + " s: " + (gone ?? link.Reason));
        }

        private static string Gone(string reason)
        {
            return reason != null ? " (" + reason + ")" : "";
        }

        // The next message back (null if none comes in time, or the link went)
        private async UniTask<byte[]> Next(CancellationToken ct)
        {
            Stopwatch waiting = Stopwatch.StartNew();
            while (received.Count == 0 && link.State == LanLink.Status.Connected && waiting.Elapsed.TotalSeconds < WaitFor)
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            return received.Count > 0 ? received.Dequeue() : null;
        }

        //----------------------------------------On screen----------------------------------------//
        private void OnGUI()
        {
            if (style == null)
                style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(14, Screen.height / 45), wordWrap = true };
            style.normal.textColor = Color.white;
            GUI.color = new Color(0, 0, 0, 0.7f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUILayout.BeginArea(new Rect(20, 20, Screen.width - 40, Screen.height - 40));
            GUILayout.Label("LAN connection test", style);
            foreach (string line in lines)
                GUILayout.Label(line, style);
            GUILayout.EndArea();
        }

        private void OnDestroy()
        {
            link?.Dispose();
            discovery?.Dispose();
            if (instance == this)
                instance = null;
        }
    }
}

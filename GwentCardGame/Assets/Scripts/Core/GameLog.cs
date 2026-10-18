using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace Gwent.Core
{
    // What an event is about: the "cat" of a log line
    public enum LogCategory { Game, Battle, Ai, Ui, Deck, Perf, Unity }

    // The game's event log: what happened, one JSON object per line, for people and for AI agents reading a session
    // (grep it, or jq it). Every line starts with the time since the session began ("t", seconds), the frame ("f"), the
    // scene, the category and the event ("ev"), then the context set by the battle (battle, round, turn), then the
    // event's own fields:
    //
    //   {"t":41.27,"f":2210,"scene":"Game","cat":"battle","ev":"card_played","battle":1,"round":1,"turn":3,"side":"enemy",...}
    //
    // In the editor it is GwentCardGame/Logs/game.jsonl (a Play session each; the one before is game.previous.jsonl), in
    // a development build <persistentDataPath>/Logs/game.jsonl. Each event also shows in the console, on one line and
    // without a stack trace. Unity's own warnings, errors and exceptions go in too (category "unity", stack included).
    //
    // Never in production: a release build defines none of UNITY_EDITOR, DEBUG (a development build in Unity 6.6) or
    // GWENT_VERBOSE_LOGS, so the calls below and their arguments are compiled out and no file is ever opened. What a
    // player's build must still report goes through Debug.LogWarning and Debug.LogError.
    public static class GameLog
    {
#if UNITY_EDITOR || DEBUG || GWENT_VERBOSE_LOGS
        public static readonly bool Enabled = true;
#else
        public static readonly bool Enabled = false;
#endif

        private const float SlowFrameSeconds = 0.1f;

        private static readonly object gate = new object();
        private static readonly Stopwatch clock = new Stopwatch();
        private static readonly List<KeyValuePair<string, object>> context = new List<KeyValuePair<string, object>>();
        private static StreamWriter writer;
        private static bool ended; // the session is over: what comes after (objects torn down on leaving Play mode) is not written
        private static volatile int frame;
        private static volatile string scene = "";

        public static string FilePath { get; private set; }

        // An event, with its fields as name and value pairs: Event(LogCategory.Battle, "pass", "side", "enemy")
        [Conditional("UNITY_EDITOR"), Conditional("DEBUG"), Conditional("GWENT_VERBOSE_LOGS")]
        public static void Event(LogCategory category, string name, params object[] fields)
        {
            Write(category, name, fields, true, true);
        }

        // A field every event carries from now on (null takes it off): the battle, its round, the turn
        [Conditional("UNITY_EDITOR"), Conditional("DEBUG"), Conditional("GWENT_VERBOSE_LOGS")]
        public static void SetContext(string key, object value)
        {
            lock (gate)
            {
                context.RemoveAll(entry => entry.Key == key);
                if (value != null)
                    context.Add(new KeyValuePair<string, object>(key, value));
            }
        }

        //----------------------------------------The session----------------------------------------//
#if UNITY_EDITOR || DEBUG || GWENT_VERBOSE_LOGS
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Begin()
        {
            ended = false;
            Open();
            scene = SceneManager.GetActiveScene().name ?? "";
            Application.logMessageReceivedThreaded -= Captured;
            Application.logMessageReceivedThreaded += Captured;
            SceneManager.activeSceneChanged -= SceneChanged;
            SceneManager.activeSceneChanged += SceneChanged;
            Application.quitting -= End;
            Application.quitting += End;
            GameObject watcher = new GameObject("GameLog") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(watcher);
            watcher.AddComponent<FrameWatch>();
            Event(LogCategory.Game, "session_start", "version", BuildInfo.Id, "unity", Application.unityVersion,
                "platform", Application.platform, "editor", Application.isEditor, "os", SystemInfo.operatingSystem,
                "device", SystemInfo.deviceModel, "gpu", SystemInfo.graphicsDeviceName, "memory_mb", SystemInfo.systemMemorySize,
                "screen", Screen.width + "x" + Screen.height, "log", FilePath);
        }
#endif

        private static void Open()
        {
            lock (gate)
            {
                if (writer != null)
                    return;
                string folder = Application.isEditor ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"))
                    : Path.Combine(Application.persistentDataPath, "Logs");
                try
                {
                    Directory.CreateDirectory(folder);
                    FilePath = Path.Combine(folder, "game.jsonl");
                    if (File.Exists(FilePath))
                        File.Copy(FilePath, Path.Combine(folder, "game.previous.jsonl"), true);
                    writer = new StreamWriter(FilePath, false, new UTF8Encoding(false)) { AutoFlush = true };
                    clock.Restart();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("The game log could not be opened in " + folder + ": " + e.Message);
                }
            }
        }

        private static void End()
        {
            Event(LogCategory.Game, "session_end", "seconds", Math.Round(clock.Elapsed.TotalSeconds, 1));
            lock (gate)
            {
                Application.logMessageReceivedThreaded -= Captured;
                if (writer != null)
                    writer.Dispose();
                writer = null;
                ended = true;
            }
        }

        private static void SceneChanged(Scene from, Scene to)
        {
            if (string.IsNullOrEmpty(to.name))
                return; // a player's first, empty scene
            scene = to.name;
            Event(LogCategory.Game, "scene", "name", to.name);
        }

        // Unity's own warnings, errors and exceptions (from any thread), with their stack
        private static void Captured(string message, string stack, LogType type)
        {
            if (type == LogType.Log)
                return;
            Write(LogCategory.Unity, type.ToString().ToLowerInvariant(),
                new object[] { "message", message, "stack", string.IsNullOrEmpty(stack) ? null : stack.Trim() }, false, false);
        }

        // The frame number for lines written from other threads, and frames that took too long
        private class FrameWatch : MonoBehaviour
        {
            private float lastSlow = -10;

            private void Update()
            {
                frame = Time.frameCount;
                float seconds = Time.unscaledDeltaTime;
                if (seconds >= SlowFrameSeconds && frame > 10 && Time.realtimeSinceStartup - lastSlow > 1)
                {
                    lastSlow = Time.realtimeSinceStartup;
                    Event(LogCategory.Perf, "slow_frame", "ms", Mathf.RoundToInt(seconds * 1000));
                }
            }
        }

        //----------------------------------------Lines----------------------------------------//
        // main_thread: the file may be opened here (Unity's paths are only read on the main thread)
        private static void Write(LogCategory category, string name, object[] fields, bool echo, bool main_thread)
        {
            if (main_thread)
            {
                // A file per Play session: none is opened after the session ends or outside Play mode
                if (writer == null && !ended && Application.isPlaying)
                    Open();
                frame = Time.frameCount;
            }
            StringBuilder line = new StringBuilder(256);
            line.Append("{\"t\":").Append(clock.Elapsed.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture));
            line.Append(",\"f\":").Append(frame);
            Field(line, "scene", scene);
            Field(line, "cat", category.ToString().ToLowerInvariant());
            Field(line, "ev", name);
            lock (gate)
            {
                foreach (KeyValuePair<string, object> entry in context)
                    Field(line, entry.Key, entry.Value);
            }
            for (int i = 0; fields != null && i + 1 < fields.Length; i += 2)
                Field(line, Convert.ToString(fields[i], CultureInfo.InvariantCulture), fields[i + 1]);
            line.Append('}');

            lock (gate)
            {
                if (writer != null)
                    writer.WriteLine(line.ToString());
            }
            if (echo)
                Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0}", Echo(category, name, fields));
        }

        // The console's line: "[battle] card_played side=enemy card=Ballista"
        private static string Echo(LogCategory category, string name, object[] fields)
        {
            StringBuilder echo = new StringBuilder("[").Append(category.ToString().ToLowerInvariant()).Append("] ").Append(name);
            for (int i = 0; fields != null && i + 1 < fields.Length; i += 2)
            {
                object value = fields[i + 1];
                if (value is IDictionary || value is ICollection && !(value is string))
                    continue; // snapshots and lists stay in the file
                echo.Append(' ').Append(fields[i]).Append('=').Append(Convert.ToString(value, CultureInfo.InvariantCulture));
            }
            return echo.ToString();
        }

        private static void Field(StringBuilder json, string key, object value)
        {
            json.Append(',');
            Text(json, key);
            json.Append(':');
            Value(json, value, 0);
        }

        private static void Value(StringBuilder json, object value, int depth)
        {
            switch (value)
            {
                case null:
                    json.Append("null");
                    break;
                case string s:
                    Text(json, s);
                    break;
                case bool b:
                    json.Append(b ? "true" : "false");
                    break;
                case float f:
                    Number(json, f);
                    break;
                case double d:
                    Number(json, d);
                    break;
                case int _:
                case long _:
                case short _:
                case byte _:
                case uint _:
                    json.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                    break;
                case Enum e:
                    Text(json, e.ToString());
                    break;
                case IDictionary dictionary when depth < 6:
                    json.Append('{');
                    bool first = true;
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        if (!first)
                            json.Append(',');
                        first = false;
                        Text(json, Convert.ToString(entry.Key, CultureInfo.InvariantCulture));
                        json.Append(':');
                        Value(json, entry.Value, depth + 1);
                    }
                    json.Append('}');
                    break;
                case IEnumerable list when depth < 6:
                    json.Append('[');
                    bool next = false;
                    foreach (object item in list)
                    {
                        if (next)
                            json.Append(',');
                        next = true;
                        Value(json, item, depth + 1);
                    }
                    json.Append(']');
                    break;
                default:
                    Text(json, Convert.ToString(value, CultureInfo.InvariantCulture));
                    break;
            }
        }

        private static void Number(StringBuilder json, double number)
        {
            if (double.IsNaN(number) || double.IsInfinity(number))
                json.Append("null");
            else
                json.Append(number.ToString("0.####", CultureInfo.InvariantCulture));
        }

        private static void Text(StringBuilder json, string text)
        {
            json.Append('"');
            foreach (char c in text)
            {
                switch (c)
                {
                    case '"': json.Append("\\\""); break;
                    case '\\': json.Append("\\\\"); break;
                    case '\n': json.Append("\\n"); break;
                    case '\r': json.Append("\\r"); break;
                    case '\t': json.Append("\\t"); break;
                    default:
                        if (c < ' ')
                            json.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            json.Append(c);
                        break;
                }
            }
            json.Append('"');
        }
    }
}

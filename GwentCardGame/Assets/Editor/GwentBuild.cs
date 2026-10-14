using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

// Release builds for every platform this editor has the build support of, ready to hand out:
// Builds/<version>/ holds GwentLegacy-<version>-linux.tar.gz, -windows.zip, -macos.zip, .apk and -web.zip, with
// build-report.txt. Menu Gwent > Build, or from the command line (the editor closed):
//   Unity -batchmode -projectPath GwentCardGame -executeMethod GwentBuild.BuildAllBatch
// The run starts at once, the editor focused or not (in the background it may not update for a long time), and goes
// through every platform in one go: script reloads (a platform switch asks for one) wait until it is over. The current
// platform is built first, the others after it, and the editor is switched back to it at the end. The targets left
// are also kept in SessionState, should a reload come anyway: the run then picks up again after it.
//
// Android: signed with a release key when GWENT_KEYSTORE (the .keystore file), GWENT_KEYSTORE_PASS, GWENT_KEY_ALIAS and
// GWENT_KEY_PASS are set in the environment (the passwords never go into the project); otherwise with Unity's debug
// key, which only installs over a build signed on the same machine.
public static class GwentBuild
{
    private const string QueueKey = "GwentBuild.Queue";
    private const string ReportKey = "GwentBuild.Report";
    private const string BatchKey = "GwentBuild.Batch";
    private const string HomeKey = "GwentBuild.Home"; // the platform the editor was on
    private const string ProductFile = "GwentLegacy";

    private class Target
    {
        public string Name;
        public BuildTarget Build;
        public BuildTargetGroup Group;
        public string Folder;       // under Builds/<version>/
        public string Player;       // the file BuildPlayer writes, in that folder
    }

    private static readonly Target[] Targets =
    {
        new Target { Name = "linux", Build = BuildTarget.StandaloneLinux64, Group = BuildTargetGroup.Standalone, Folder = "linux", Player = ProductFile + ".x86_64" },
        new Target { Name = "windows", Build = BuildTarget.StandaloneWindows64, Group = BuildTargetGroup.Standalone, Folder = "windows", Player = ProductFile + ".exe" },
        new Target { Name = "macos", Build = BuildTarget.StandaloneOSX, Group = BuildTargetGroup.Standalone, Folder = "macos", Player = ProductFile + ".app" },
        new Target { Name = "android", Build = BuildTarget.Android, Group = BuildTargetGroup.Android, Folder = "android", Player = ProductFile + ".apk" },
        new Target { Name = "web", Build = BuildTarget.WebGL, Group = BuildTargetGroup.WebGL, Folder = "web", Player = "" },
    };

    private static string Version { get { return PlayerSettings.bundleVersion; } }
    private static string Root { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", Version)); } }

    //----------------------------------------Entry points----------------------------------------//
    [MenuItem("Gwent/Build/All platforms")]
    public static void BuildAll() { Start(Targets.Select(t => t.Name), false); }

    [MenuItem("Gwent/Build/Linux")]
    public static void BuildLinux() { Start(new[] { "linux" }, false); }

    [MenuItem("Gwent/Build/Windows")]
    public static void BuildWindows() { Start(new[] { "windows" }, false); }

    [MenuItem("Gwent/Build/macOS")]
    public static void BuildMac() { Start(new[] { "macos" }, false); }

    [MenuItem("Gwent/Build/Android")]
    public static void BuildAndroid() { Start(new[] { "android" }, false); }

    [MenuItem("Gwent/Build/Web")]
    public static void BuildWeb() { Start(new[] { "web" }, false); }

    // -executeMethod GwentBuild.BuildAllBatch [-buildTargets linux,windows]: exits 0 when every build succeeded
    public static void BuildAllBatch()
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-buildTargets");
        IEnumerable<string> names = index >= 0 && index + 1 < args.Length ? args[index + 1].Split(',') : Targets.Select(t => t.Name);
        Start(names, true);
    }

    private static void Start(IEnumerable<string> names, bool batch)
    {
        // The current platform first, then the others of its group (no switch between them), then the rest in order
        BuildTarget home = EditorUserBuildSettings.activeBuildTarget;
        BuildTargetGroup home_group = BuildPipeline.GetBuildTargetGroup(home);
        List<string> queue = names.Select(n => n.Trim().ToLowerInvariant()).Distinct().Where(n => Targets.Any(t => t.Name == n))
            .OrderBy(n => Targets.First(t => t.Name == n).Build == home ? 0 : Targets.First(t => t.Name == n).Group == home_group ? 1 : 2)
            .ThenBy(n => Array.FindIndex(Targets, t => t.Name == n)).ToList();
        Directory.CreateDirectory(Root);
        SessionState.SetString(QueueKey, string.Join(",", queue));
        SessionState.SetString(ReportKey, "Gwent Legacy " + Version + ", built " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " with Unity " + Application.unityVersion + "\n");
        SessionState.SetBool(BatchKey, batch);
        SessionState.SetInt(HomeKey, (int)home);

        EditorApplication.LockReloadAssemblies();
        try
        {
            Continue();
        }
        finally
        {
            EditorApplication.UnlockReloadAssemblies();
        }
    }

    // After a domain reload in the middle of a run
    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!string.IsNullOrEmpty(SessionState.GetString(QueueKey, "")))
            EditorApplication.delayCall += Continue;
    }

    //----------------------------------------The run----------------------------------------//
    private static void Continue()
    {
        while (true)
        {
            List<string> queue = SessionState.GetString(QueueKey, "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (queue.Count == 0)
            {
                Finish();
                return;
            }
            // Taken off the queue first: a build that crashes the run is not tried again forever
            Target target = Targets.First(t => t.Name == queue[0]);
            queue.RemoveAt(0);
            SessionState.SetString(QueueKey, string.Join(",", queue));
            Report(Build(target));
        }
    }

    private static string Build(Target target)
    {
        if (!BuildPipeline.IsBuildTargetSupported(target.Group, target.Build))
            return target.Name + ": SKIPPED, the editor has no build support for it (Unity Hub > Installs > Add modules)";

        string folder = Path.Combine(Root, target.Folder);
        if (Directory.Exists(folder))
            Directory.Delete(folder, true);
        Directory.CreateDirectory(folder);
        Prepare(target);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            target = target.Build,
            targetGroup = target.Group,
            locationPathName = target.Player.Length > 0 ? Path.Combine(folder, target.Player) : folder,
            options = BuildOptions.None,
        };
        Stopwatch watch = Stopwatch.StartNew();
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        string line = target.Name + ": " + summary.result + " in " + watch.Elapsed.TotalMinutes.ToString("F1") + " min, " + summary.totalErrors + " errors, " + summary.totalWarnings + " warnings";
        if (summary.result != BuildResult.Succeeded)
            return line;

        string archive = Package(target, folder);
        return line + "\n  " + Path.GetFileName(archive) + " (" + (new FileInfo(archive).Length / 1048576.0).ToString("F1") + " MB)" + Signing(target);
    }

    // The settings each platform's build depends on, applied every time (some are this machine's editor settings)
    private static void Prepare(Target target)
    {
        switch (target.Build)
        {
            case BuildTarget.StandaloneOSX:
                UnityEditor.OSXStandalone.UserBuildSettings.architecture = OSArchitecture.x64ARM64; // Intel and Apple Silicon
                break;
            case BuildTarget.Android:
                EditorUserBuildSettings.buildAppBundle = false; // an .apk, to install directly
                string keystore = Environment.GetEnvironmentVariable("GWENT_KEYSTORE");
                PlayerSettings.Android.useCustomKeystore = !string.IsNullOrEmpty(keystore) && File.Exists(keystore);
                if (PlayerSettings.Android.useCustomKeystore)
                {
                    PlayerSettings.Android.keystoreName = keystore;
                    PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable("GWENT_KEYSTORE_PASS");
                    PlayerSettings.Android.keyaliasName = Environment.GetEnvironmentVariable("GWENT_KEY_ALIAS");
                    PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable("GWENT_KEY_PASS");
                }
                break;
        }
    }

    private static string Signing(Target target)
    {
        if (target.Build != BuildTarget.Android)
            return "";
        return PlayerSettings.Android.useCustomKeystore ? ", signed with the release key" : ", signed with Unity's debug key (no GWENT_KEYSTORE set)";
    }

    //----------------------------------------Archives----------------------------------------//
    private static string Package(Target target, string folder)
    {
        string name = ProductFile + "-" + Version + "-" + target.Name;
        switch (target.Build)
        {
            case BuildTarget.Android:
            {
                string apk = Path.Combine(Root, name + ".apk");
                File.Copy(Path.Combine(folder, target.Player), apk, true);
                return apk;
            }
            case BuildTarget.StandaloneLinux64:
            {
                // tar keeps the executable permission
                string archive = Path.Combine(Root, name + ".tar.gz");
                Run("tar", "-czf \"" + archive + "\" -C \"" + Root + "\" --transform \"s,^" + target.Folder + "," + name + ",\" --exclude \"*_BurstDebugInformation_DoNotShip\" " + target.Folder);
                return archive;
            }
            case BuildTarget.StandaloneOSX:
            {
                // zip -y keeps the app's executable permission and symbolic links
                string archive = Path.Combine(Root, name + ".zip");
                File.Delete(archive);
                Run("zip", "-qry \"" + archive + "\" " + target.Player, folder);
                return archive;
            }
            default:
            {
                string archive = Path.Combine(Root, name + ".zip");
                File.Delete(archive);
                ZipFile.CreateFromDirectory(folder, archive, System.IO.Compression.CompressionLevel.Optimal, false);
                return archive;
            }
        }
    }

    private static void Run(string program, string arguments, string directory = null)
    {
        ProcessStartInfo start = new ProcessStartInfo(program, arguments)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            WorkingDirectory = directory ?? Root,
        };
        using (Process process = Process.Start(start))
        {
            string errors = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new Exception(program + " failed (" + process.ExitCode + "): " + errors);
        }
    }

    //----------------------------------------Report----------------------------------------//
    private static void Report(string line)
    {
        Debug.Log("[GwentBuild] " + line);
        SessionState.SetString(ReportKey, SessionState.GetString(ReportKey, "") + line + "\n");
    }

    private static void Finish()
    {
        // Back to the platform the editor was on (its imports are cached: a matter of seconds)
        BuildTarget home = (BuildTarget)SessionState.GetInt(HomeKey, (int)EditorUserBuildSettings.activeBuildTarget);
        if (EditorUserBuildSettings.activeBuildTarget != home)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(home), home);
        string report = SessionState.GetString(ReportKey, "");
        File.WriteAllText(Path.Combine(Root, "build-report.txt"), report);
        SessionState.EraseString(QueueKey);
        Debug.Log("[GwentBuild] done:\n" + report);
        bool batch = SessionState.GetBool(BatchKey, false);
        if (batch)
            EditorApplication.Exit(report.Contains(": Succeeded") && !report.Contains(": Failed") ? 0 : 1);
    }
}

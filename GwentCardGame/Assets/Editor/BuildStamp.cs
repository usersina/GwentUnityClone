using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using Debug = UnityEngine.Debug;

namespace Gwent.EditorTools
{
    // Stamps which source a build comes from (BuildInfo): the commit ("1a2b3c4d"), and when the working tree has changes
    // not committed, a hash of them too ("1a2b3c4d.5e6f70"), so that two builds match only when built from the same files.
    // Written to Assets/Resources/BuildStamp.txt (ignored by git) as each build starts, and as the editor loads its scripts
    // (the editor's own play is the working tree's build). "unknown" without git.
    public class BuildStamp : IPreprocessBuildWithReport
    {
        private const string StampPath = "Assets/Resources/BuildStamp.txt";

        public int callbackOrder { get { return -100; } }

        public void OnPreprocessBuild(BuildReport report)
        {
            Write();
        }

        [InitializeOnLoadMethod]
        private static void OnScriptsLoaded()
        {
            if (!BuildPipeline.isBuildingPlayer)
                EditorApplication.delayCall += () => Write();
        }

        // Writes the stamp (only when it changed: no import otherwise); the stamp written
        public static string Write()
        {
            string stamp = Compute();
            string old = File.Exists(StampPath) ? File.ReadAllText(StampPath).Trim() : null;
            if (old != stamp)
            {
                File.WriteAllText(StampPath, stamp + "\n");
                AssetDatabase.ImportAsset(StampPath, ImportAssetOptions.ForceSynchronousImport);
            }
            return stamp;
        }

        // The commit, and a hash of what differs from it (changed files, new files not ignored)
        public static string Compute()
        {
            string commit = Git("rev-parse --short=8 HEAD");
            if (string.IsNullOrEmpty(commit))
                return "unknown";
            string changes = Git("status --porcelain --untracked-files=all");
            if (string.IsNullOrEmpty(changes))
                return commit;
            using (SHA1 sha = SHA1.Create())
            {
                StringBuilder all = new StringBuilder(Git("diff HEAD --binary"));
                foreach (string untracked in Git("ls-files --others --exclude-standard --full-name :/").Split('\n'))
                {
                    string file = untracked.Trim();
                    string path = Path.Combine(RepositoryRoot(), file);
                    if (file.Length > 0 && File.Exists(path))
                        all.Append(file).Append(Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path))));
                }
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(all.ToString()));
                return commit + "." + BitConverter.ToString(hash, 0, 3).Replace("-", "").ToLowerInvariant();
            }
        }

        private static string root;

        private static string RepositoryRoot()
        {
            return root ?? (root = Git("rev-parse --show-toplevel") ?? ".");
        }

        private static string Git(string arguments)
        {
            try
            {
                ProcessStartInfo start = new ProcessStartInfo("git", arguments)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Directory.GetCurrentDirectory(),
                };
                using (Process git = Process.Start(start))
                {
                    string output = git.StandardOutput.ReadToEnd();
                    git.WaitForExit(10000);
                    return git.ExitCode == 0 ? output.Trim() : null;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("BuildStamp: git not available (" + e.Message + ")");
                return null;
            }
        }
    }
}

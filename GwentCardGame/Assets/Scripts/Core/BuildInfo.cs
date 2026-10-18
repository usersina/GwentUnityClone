using UnityEngine;

namespace Gwent.Core
{
    // Which build of the game this is: its version and the source it was built from, "1.0.0+1a2b3c4d" (the commit), or
    // "1.0.0+1a2b3c4d.5e6f70" when it had changes not committed yet (a hash of them). Two devices play each other on LAN
    // only with the same build (LanLink, LanDiscovery). Stamped by the editor (EditorTools.BuildStamp) into
    // Resources/BuildStamp.txt as each build starts and as the editor loads its scripts; not kept in git.
    public static class BuildInfo
    {
        private static string id;

        // The whole build id ("1.0.0+1a2b3c4d")
        public static string Id
        {
            get
            {
                if (id == null)
                {
                    TextAsset stamp = Resources.Load<TextAsset>("BuildStamp");
                    string source = stamp != null ? stamp.text.Trim() : "";
                    id = Application.version + "+" + (source.Length > 0 ? source : "unknown");
                    if (stamp != null)
                        Resources.UnloadAsset(stamp);
                }
                return id;
            }
        }

        // The part after the version, as players compare it ("1a2b3c4d", "1a2b3c4d.5e6f70")
        public static string Build { get { return Short(Id); } }

        // The build part of another device's id (its version too, when the versions differ)
        public static string Short(string build_id)
        {
            if (string.IsNullOrEmpty(build_id))
                return "?";
            int plus = build_id.IndexOf('+');
            if (plus < 0)
                return build_id;
            string version = build_id.Substring(0, plus);
            return version == Application.version ? build_id.Substring(plus + 1) : build_id;
        }
    }
}

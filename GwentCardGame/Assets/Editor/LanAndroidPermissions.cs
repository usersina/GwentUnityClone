using System.IO;
using System.Xml;
using UnityEditor.Android;

namespace Gwent.EditorTools
{
    // The Android build's permissions for LAN play, added to the generated manifest: the network itself (Unity adds it on
    // its own when it sees the game use one; asked for here all the same, as LAN play cannot do without it) and the
    // multicast lock, without which many phones never see another device's search (LanDiscovery). A game targeting
    // Android 17 (API 37) or later will also need ACCESS_LOCAL_NETWORK, asked for at run time: the build targets 36.
    public class LanAndroidPermissions : IPostGenerateGradleAndroidProject
    {
        private const string Android = "http://schemas.android.com/apk/res/android";

        private static readonly string[] Needed =
        {
            "android.permission.INTERNET",
            "android.permission.CHANGE_WIFI_MULTICAST_STATE",
        };

        public int callbackOrder { get { return 0; } }

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string file = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            XmlDocument manifest = new XmlDocument();
            manifest.Load(file);
            XmlElement root = manifest.DocumentElement;
            bool changed = false;
            foreach (string permission in Needed)
            {
                bool there = false;
                foreach (XmlElement uses in root.GetElementsByTagName("uses-permission"))
                    there |= uses.GetAttribute("name", Android) == permission;
                if (there)
                    continue;
                XmlElement element = manifest.CreateElement("uses-permission");
                XmlAttribute name = manifest.CreateAttribute("android", "name", Android);
                name.Value = permission;
                element.Attributes.Append(name);
                root.PrependChild(element);
                changed = true;
            }
            if (changed)
                manifest.Save(file);
        }
    }
}

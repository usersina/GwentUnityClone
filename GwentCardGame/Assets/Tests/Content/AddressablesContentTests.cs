using System.Collections.Generic;
using System.Linq;
using Gwent.Core;
using Gwent.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Gwent.Content.Tests
{
    // The content the game loads through Addressables (see GwentContent) is all there, at the addresses the game asks for
    public class AddressablesContentTests
    {
        private static List<AddressableAssetEntry> Entries(string group_name)
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            Assert.IsNotNull(settings, "No Addressables settings (menu Gwent > Content > Update Addressables makes them)");
            AddressableAssetGroup group = settings.FindGroup(group_name);
            Assert.IsNotNull(group, "No \"" + group_name + "\" group (menu Gwent > Content > Update Addressables makes it)");
            return group.entries.ToList();
        }

        [Test]
        public void EveryCardHasItsArt()
        {
            Dictionary<string, AddressableAssetEntry> art = Entries(GwentContent.CardArtGroup).ToDictionary(entry => entry.address);
            List<string> missing = CardLibrary.Get().All.Select(card => CardArt.Address(card._id)).Where(address => !art.ContainsKey(address)).ToList();
            Assert.IsEmpty(missing, "Cards with no art in Addressables");
            foreach (AddressableAssetEntry entry in art.Values)
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Sprite>(entry.AssetPath), entry.address + " (" + entry.AssetPath + ") is not a sprite");
        }

        [Test]
        public void EveryTrackIsLabelledMusic()
        {
            List<AddressableAssetEntry> tracks = Entries(GwentContent.MusicGroup);
            Assert.IsNotEmpty(tracks);
            foreach (AddressableAssetEntry track in tracks)
            {
                Assert.IsTrue(track.labels.Contains(GwentContent.MusicLabel), track.address + " has no \"" + GwentContent.MusicLabel + "\" label: it would never play");
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<AudioClip>(track.AssetPath), track.address + " is not an audio clip");
            }
        }

        // A file added to a folder is in its group (every build updates the groups; this catches it before)
        [Test]
        public void TheGroupsHoldTheirWholeFolders()
        {
            foreach (KeyValuePair<string, string> pair in new Dictionary<string, string>
            {
                { GwentContent.CardArtFolder, GwentContent.CardArtGroup },
                { GwentContent.MusicFolder, GwentContent.MusicGroup },
            })
            {
                HashSet<string> entries = new HashSet<string>(Entries(pair.Value).Select(entry => entry.guid));
                List<string> left_out = AssetDatabase.FindAssets("", new[] { pair.Key })
                    .Where(guid => !AssetDatabase.IsValidFolder(AssetDatabase.GUIDToAssetPath(guid)) && !entries.Contains(guid))
                    .Select(AssetDatabase.GUIDToAssetPath).ToList();
                Assert.IsEmpty(left_out, "Not in the \"" + pair.Value + "\" group (menu Gwent > Content > Update Addressables)");
            }
        }

        // An asset both in Resources and in a bundle would ship twice
        [Test]
        public void NothingAddressableIsInResources()
        {
            List<string> twice = AddressableAssetSettingsDefaultObject.Settings.groups
                .SelectMany(group => group.entries).Select(entry => entry.AssetPath)
                .Where(path => path.Contains("/Resources/")).ToList();
            Assert.IsEmpty(twice);
        }
    }
}

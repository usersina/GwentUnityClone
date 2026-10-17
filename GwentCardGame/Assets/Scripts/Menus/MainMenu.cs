using System.Collections.Generic;
using Gwent.Core;
using Gwent.UI;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Image = UnityEngine.UI.Image;

namespace Gwent.Menus
{
    // The main menu: Witcher-style entries on the left of the key art. The entries are a UI Toolkit screen
    // (Assets/UI/Toolkit/MainMenu.uxml); the key art and its "Legacy" title are the scene's.
    public class MainMenu : MonoBehaviour
    {
        private readonly List<MenuEntry> items = new List<MenuEntry>();

        public IList<MenuEntry> Items { get { return items; } }

        private void Start()
        {
            Gwent.Net.LanSession.ForgetBattle(); // a LAN battle that closed is no battle to load any more
            GameAudio.PlayMenuMusic();
            UIScreens.Show("MainMenu", UITheme.Current.mainMenu, Bind);
            CoverWithKeyArt();
            if (LanLobby.Reopen()) // back from the deck builder the lobby sent the player to
                PlayOnLan();
            else if (!GameSettings.IsWeb && Gwent.Net.LanSession.Current == null && Gwent.Net.LanSave.Load() != null)
                UIOverlay.Toast("A LAN battle is still on: Play on LAN to rejoin it", 4f);
        }

        private void Bind(VisualElement root)
        {
            items.Clear();
            AddItem(root, "Item Play", PlayGame);
            MenuEntry lan = root.Q<MenuEntry>("Item Play on LAN");
            if (GameSettings.IsWeb) // a browser cannot host or find games on the network
                lan.RemoveFromHierarchy();
            else
                AddItem(root, "Item Play on LAN", PlayOnLan);
            AddItem(root, "Item Deck builder", EditDeck);
            AddItem(root, "Item How to play", OpenRules);
            AddItem(root, "Item Settings", OpenSettings);
            MenuEntry quit = root.Q<MenuEntry>("Item Quit");
            if (GameSettings.IsWeb) // a browser tab is closed, not quit
                quit.RemoveFromHierarchy();
            else
                AddItem(root, "Item Quit", QuitGame);
            // Once the panel has laid it out
            root.schedule.Execute(() => Focus(0));
        }

        private void AddItem(VisualElement root, string name, System.Action on_click)
        {
            MenuEntry item = root.Q<MenuEntry>(name);
            item.clicked += on_click;
            items.Add(item);
        }

        private void Focus(int index)
        {
            if (index >= 0 && index < items.Count)
                UIScreens.Focus(items[index]);
        }

        // The entry of that name, focused (a dialog closed gives it the keys back)
        private void Focus(string name)
        {
            Focus(items.FindIndex(item => item.name == name));
        }

        private void CoverWithKeyArt()
        {
            // The key art covers the screen whatever its shape (its edges are cropped rather than bars shown), "Legacy" under
            // its logo
            Transform background = transform.parent != null ? transform.parent.Find("Background") : null;
            Image art = background != null ? background.GetComponent<Image>() : null;
            if (art != null && art.sprite != null)
            {
                art.preserveAspect = false;
                AspectRatioFitter cover = art.gameObject.AddComponent<AspectRatioFitter>();
                cover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                cover.aspectRatio = art.sprite.rect.width / art.sprite.rect.height;
                LegacyTitle.Create(art);
            }
        }

        // Play: choose the opponent first
        public void PlayGame()
        {
            OpponentPicker.Open(() => Focus(0));
        }

        // Play on LAN: host a battle or join one on the network (LanLobby)
        public void PlayOnLan()
        {
            LanLobby.Open(() => Focus("Item Play on LAN"));
        }

        public void EditDeck()
        {
            LoadingScreen.Load(SceneNames.Deck);
        }

        public void OpenRules()
        {
            RulesPage.Open(() => Focus("Item How to play"));
        }

        public void OpenSettings()
        {
            SettingsPanel.Open(() => Focus("Item Settings"));
        }

        public void QuitGame()
        {
            GameLog.Event(LogCategory.Ui, "quit");
            Application.Quit();
        }
    }
}

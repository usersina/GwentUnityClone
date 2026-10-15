using Gwent.Battle;
using Gwent.Core;
using Gwent.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Gwent.Menus
{
    // The battle's pause menu (Escape, the Android back button or the menu button at the top right).
    // Time stops while it is open, so the AI waits too. A UI Toolkit dialog (Assets/UI/Toolkit/PauseMenu.uxml); this
    // component sits on its object to put time back should the scene close under it.
    public class PauseMenu : MonoBehaviour
    {
        private static PauseMenu current;
        private float previousTimeScale = 1f;

        public static bool IsOpen { get { return current != null; } }

        public static void Open()
        {
            if (current != null || SettingsPanel.IsOpen)
                return;

            float time_scale = Time.timeScale;
            Time.timeScale = 0f;
            // The battle reads its keys itself (BattleKeys): the menu button that opened this must not get the selection
            // back on Resume, or Enter would press it again instead of taking a card
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
            GameObject dialog = UIScreens.ShowDialog("PauseMenu", UITheme.Current.pauseMenu, Bind, Resumed);
            current = dialog.AddComponent<PauseMenu>();
            current.previousTimeScale = time_scale;
        }

        public static void Close()
        {
            if (current != null)
                UIOverlay.CloseModal(current.gameObject);
        }

        private static void Bind(VisualElement root)
        {
            MenuEntry resume = root.Q<MenuEntry>("Item Resume");
            resume.clicked += Close;
            MenuEntry settings = root.Q<MenuEntry>("Item Settings");
            settings.clicked += () => SettingsPanel.Open(() => UIScreens.Focus(settings));
            // Give up (The Witcher 3's forfeit): the battle counts as lost and the end screen follows; once it is over,
            // back to the main menu instead
            BattleController battle = BattleController.Instance;
            bool battle_on = battle != null && !battle.IsBattleOver;
            Entry(root, "Item Give up", battle_on, () => UIOverlay.Confirm("Give up this battle? It counts as a loss.", "Give up", "Keep playing", GiveUp));
            Entry(root, "Item Main menu", !battle_on, () => UIOverlay.Confirm("Leave and go back to the main menu?", "Leave", "Stay", LeaveBattle));
            Entry(root, "Item Quit game", !GameSettings.IsMobile && !GameSettings.IsWeb, () => UIOverlay.Confirm("Quit the game?", "Quit", "Cancel", Application.Quit));
            root.schedule.Execute(() => UIScreens.Focus(resume));
        }

        // An entry shown only where it applies
        private static void Entry(VisualElement root, string name, bool shown, System.Action on_click)
        {
            MenuEntry entry = root.Q<MenuEntry>(name);
            if (shown)
                entry.clicked += on_click;
            else
                entry.RemoveFromHierarchy();
        }

        private static void Resumed()
        {
            // In a battle, at the game speed (it may have changed in the settings)
            float previous = current != null ? current.previousTimeScale : 1f;
            Time.timeScale = BattleController.Instance != null ? GameSettings.GameTimeScale : previous > 0 ? previous : 1f;
            current = null;
        }

        private static void GiveUp()
        {
            Close();
            if (BattleController.Instance != null)
                BattleController.Instance.GiveUp();
        }

        private static void LeaveBattle()
        {
            Time.timeScale = 1f;
            current = null;
            SceneManager.LoadScene(SceneNames.Menu);
        }

        private void OnDestroy()
        {
            // Destroyed with the scene while open: never leave the next scene frozen
            if (current == this)
            {
                current = null;
                Time.timeScale = 1f;
            }
        }
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The battle's pause menu (Escape, the Android back button or the menu button at the top right).
// Time stops while it is open, so the AI waits too.
public class PauseMenu : MonoBehaviour
{
    private static PauseMenu current;
    private float previousTimeScale = 1f;
    private Button settingsItem;

    public static bool IsOpen { get { return current != null; } }

    public static void Open()
    {
        if (current != null || SettingsPanel.IsOpen)
            return;

        RectTransform dimmer = UIKit.Dimmer(UIOverlay.Root, "PauseMenu");
        current = dimmer.gameObject.AddComponent<PauseMenu>();
        current.previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        current.Build(dimmer);
        UIOverlay.PushModal(dimmer.gameObject, current.Resumed);
    }

    public static void Close()
    {
        if (current != null)
            UIOverlay.CloseModal(current.gameObject);
    }

    private void Build(RectTransform dimmer)
    {
        bool can_quit = !GameSettings.IsMobile && !GameSettings.IsWeb;
        float height = can_quit ? 560 : 472;
        RectTransform panel = UIKit.Panel(dimmer, "Panel").Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640, height));

        TextMeshProUGUI title = UIKit.Text(panel, "Paused", UIKit.TextStyle.Heading, TextAlignmentOptions.Center);
        title.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -64), new Vector2(560, 64));
        UIKit.Ornament(panel, new Vector2(0.5f, 1), new Vector2(0, -112), 360);

        float y = -180;
        Button resume = Item(panel, "Resume", Close, ref y);
        settingsItem = Item(panel, "Settings", () => SettingsPanel.Open(() => UIKit.Focus(settingsItem)), ref y);
        // Give up (The Witcher 3's forfeit): the battle counts as lost and the end screen follows
        BattleController battle = BattleController.Instance;
        if (battle != null && !battle.IsBattleOver)
            Item(panel, "Give up", () => UIOverlay.Confirm("Give up this battle? It counts as a loss.", "Give up", "Keep playing", GiveUp), ref y);
        else
            Item(panel, "Main menu", () => UIOverlay.Confirm("Leave and go back to the main menu?", "Leave", "Stay", LeaveBattle), ref y);
        if (can_quit)
            Item(panel, "Quit game", () => UIOverlay.Confirm("Quit the game?", "Quit", "Cancel", Application.Quit), ref y);
        UIKit.Focus(resume);
    }

    private Button Item(RectTransform panel, string text, System.Action on_click, ref float y)
    {
        Button item = UIKit.MenuItem(panel, text, on_click, true, new Vector2(520, 76));
        ((RectTransform)item.transform).Place(new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(520, 76));
        y -= 88;
        return item;
    }

    private void Resumed()
    {
        // In a battle, at the game speed (it may have changed in the settings)
        Time.timeScale = BattleController.Instance != null ? GameSettings.GameTimeScale : previousTimeScale > 0 ? previousTimeScale : 1f;
        if (current == this)
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

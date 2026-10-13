using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The main menu: Witcher-style entries on the left of the key art (built in code, in the UI theme)
public class MainMenu : MonoBehaviour
{
    private readonly List<Button> items = new List<Button>();

    public IList<Button> Items { get { return items; } }

    private void Start()
    {
        GameAudio.PlayMenuMusic();
        BuildMenu();
    }

    private void BuildMenu()
    {
        UITheme theme = UITheme.Current;
        Transform root = UIOverlay.Root;

        // Darkens the left of the key art so the entries read on top of it
        Image scrim = UIKit.NewImage(root, "Scrim", theme.gradientH, new Color(0, 0, 0, 0.9f));
        RectTransform scrim_rect = scrim.rectTransform;
        scrim_rect.anchorMin = new Vector2(0, 0);
        scrim_rect.anchorMax = new Vector2(0, 1);
        scrim_rect.pivot = new Vector2(0, 0.5f);
        scrim_rect.anchoredPosition = Vector2.zero;
        scrim_rect.sizeDelta = new Vector2(840, 0);
        scrim.transform.SetAsFirstSibling();

        RectTransform list = UIKit.Rect("Menu", root).Place(new Vector2(0, 0.5f), new Vector2(120, -40), new Vector2(600, 5 * 88), new Vector2(0, 0.5f));
        AddItem(list, "Play", PlayGame);
        AddItem(list, "Deck builder", EditDeck);
        AddItem(list, "How to play", OpenRules);
        AddItem(list, "Settings", OpenSettings);
        AddItem(list, "Quit", QuitGame);

        TextMeshProUGUI note = UIKit.Text(root, "Fan project, not affiliated with CD PROJEKT RED", UIKit.TextStyle.Caption);
        note.rectTransform.Place(new Vector2(0, 0), new Vector2(156, 48), new Vector2(800, 36), new Vector2(0, 0));

        UIKit.Focus(items[0]);
    }

    private void AddItem(RectTransform list, string text, System.Action on_click)
    {
        Button item = UIKit.MenuItem(list, text, on_click);
        ((RectTransform)item.transform).Place(new Vector2(0, 1), new Vector2(0, -items.Count * 88), new Vector2(600, 76), new Vector2(0, 1));
        items.Add(item);
    }

    // Play: choose the opponent first
    public void PlayGame()
    {
        OpponentPicker.Open(() => UIKit.Focus(items[0]));
    }

    public void EditDeck()
    {
        SceneManager.LoadScene(SceneNames.Deck);
    }

    public void OpenRules()
    {
        RulesPage.Open(() => UIKit.Focus(items[2]));
    }

    public void OpenSettings()
    {
        SettingsPanel.Open(() => UIKit.Focus(items[3]));
    }

    public void QuitGame()
    {
        GameLog.Info("Quitting...");
        Application.Quit();
    }
}

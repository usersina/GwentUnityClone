using System;
using System.Collections.Generic;
using System.Linq;
using Gwent.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The settings screen (Audio, Display), opened from the main menu and the pause menu.
// Every change applies and saves at once; Escape, right click or Back closes it.
public class SettingsPanel : MonoBehaviour
{
    private const float RowHeight = 92;
    private const float ControlWidth = 420;

    private static readonly string[] Tabs = { "Audio", "Display", "Game" };

    private readonly List<RectTransform> pages = new List<RectTransform>();
    private readonly List<TextMeshProUGUI> tabLabels = new List<TextMeshProUGUI>();
    private readonly List<Image> tabUnderlines = new List<Image>();
    private readonly List<Selectable> firstControls = new List<Selectable>();
    private Button backButton;
    private Action onClosed;
    private int currentTab;

    private static SettingsPanel current;

    public static bool IsOpen { get { return current != null; } }

    public static SettingsPanel Open(Action on_closed = null, int tab = 0)
    {
        RectTransform dimmer = UIKit.Dimmer(UIOverlay.Root, "Settings");
        SettingsPanel panel = dimmer.gameObject.AddComponent<SettingsPanel>();
        panel.onClosed = on_closed;
        panel.Build(dimmer, tab);
        UIOverlay.PushModal(dimmer.gameObject, panel.OnClosed);
        UIKit.Focus(panel.firstControls[tab] != null ? panel.firstControls[tab] : panel.backButton);
        current = panel;
        return panel;
    }

    public void Close()
    {
        UIOverlay.CloseModal(gameObject);
    }

    private void OnClosed()
    {
        PlayerPrefs.Save();
        if (current == this)
            current = null;
        if (onClosed != null)
            onClosed();
    }

    private void OnDestroy()
    {
        if (current == this)
            current = null;
    }

    //----------------------------------------Layout----------------------------------------//
    private void Build(RectTransform dimmer, int tab)
    {
        RectTransform panel = UIKit.Panel(dimmer, "Panel").Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1040, 800)).FitInside(dimmer);

        TextMeshProUGUI title = UIKit.Text(panel, "Settings", UIKit.TextStyle.Heading);
        title.rectTransform.Place(new Vector2(0, 1), new Vector2(64, -52), new Vector2(600, 60), new Vector2(0, 0.5f));
        if (!GameSettings.IsMobile)
        {
            TextMeshProUGUI hint = UIKit.Text(panel, "Esc to close", UIKit.TextStyle.Caption, TextAlignmentOptions.Right);
            hint.rectTransform.Place(new Vector2(1, 1), new Vector2(-64, -56), new Vector2(300, 40), new Vector2(1, 0.5f));
        }

        // Tabs
        for (int i = 0; i < Tabs.Length; i++)
        {
            int index = i;
            Button tab_button = UIKit.Button(panel, Tabs[i], UIKit.ButtonStyle.Ghost, () => ShowTab(index), new Vector2(200, 56));
            ((RectTransform)tab_button.transform).Place(new Vector2(0, 1), new Vector2(64 + i * 216, -122), new Vector2(200, 56), new Vector2(0, 0.5f));
            TextMeshProUGUI label = tab_button.GetComponentInChildren<TextMeshProUGUI>();
            UIKit.ApplyStyle(label, UIKit.TextStyle.Tab);
            label.alignment = TextAlignmentOptions.Left;
            DestroyImmediate(tab_button.GetComponent<UIStateColors>()); // colored by ShowTab instead
            tabLabels.Add(label);
            Image underline = UIKit.NewImage(tab_button.transform, "Underline", null, UITheme.Current.gold);
            underline.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 4), new Vector2(120, 3), new Vector2(0, 0.5f));
            tabUnderlines.Add(underline);
        }
        UIKit.Divider(panel, -152, 64);

        RectTransform content = UIKit.Rect("Content", panel).Stretch(64, 64, 184, 130);
        pages.Add(BuildAudioPage(content));
        pages.Add(BuildDisplayPage(content));
        pages.Add(BuildGamePage(content));

        // Footer
        Button reset = UIKit.Button(panel, "Reset to defaults", UIKit.ButtonStyle.Ghost, ResetToDefaults, new Vector2(300, 64));
        ((RectTransform)reset.transform).Place(new Vector2(0, 0), new Vector2(64, 64), new Vector2(300, 64), new Vector2(0, 0.5f));
        reset.GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;
        backButton = UIKit.Button(panel, "Back", UIKit.ButtonStyle.Secondary, Close, new Vector2(240, 68));
        ((RectTransform)backButton.transform).Place(new Vector2(1, 0), new Vector2(-64, 64), new Vector2(240, 68), new Vector2(1, 0.5f));

        ShowTab(tab);
    }

    private void ShowTab(int index)
    {
        currentTab = index;
        UITheme theme = UITheme.Current;
        for (int i = 0; i < pages.Count; i++)
        {
            pages[i].gameObject.SetActive(i == index);
            tabLabels[i].color = i == index ? theme.textBright : theme.textMuted;
            tabUnderlines[i].enabled = i == index;
        }
    }

    private RectTransform Page(RectTransform content, string name)
    {
        return UIKit.Rect(name, content).Stretch();
    }

    // A labelled row; returns the area on the right where the control goes
    private RectTransform Row(RectTransform page, int index, string label, string caption = null)
    {
        RectTransform row = UIKit.Rect("Row " + label, page);
        row.anchorMin = new Vector2(0, 1);
        row.anchorMax = new Vector2(1, 1);
        row.pivot = new Vector2(0.5f, 1);
        row.anchoredPosition = new Vector2(0, -index * RowHeight);
        row.sizeDelta = new Vector2(0, RowHeight);

        TextMeshProUGUI text = UIKit.Text(row, label, UIKit.TextStyle.Label);
        text.rectTransform.Stretch(0, ControlWidth + 120, 0, caption != null ? 30 : 0);
        text.alignment = caption != null ? TextAlignmentOptions.BottomLeft : TextAlignmentOptions.Left;
        if (caption != null)
        {
            TextMeshProUGUI small = UIKit.Text(row, caption, UIKit.TextStyle.Caption);
            small.name = "Caption";
            small.rectTransform.Stretch(0, ControlWidth + 120, RowHeight - 34, 4);
            small.alignment = TextAlignmentOptions.TopLeft;
        }

        RectTransform control = UIKit.Rect("Control", row);
        control.anchorMin = new Vector2(1, 0);
        control.anchorMax = new Vector2(1, 1);
        control.pivot = new Vector2(1, 0.5f);
        control.anchoredPosition = Vector2.zero;
        control.sizeDelta = new Vector2(ControlWidth + 100, 0);
        return control;
    }

    private Slider VolumeRow(RectTransform page, int index, string label, float value, Action<float> set)
    {
        RectTransform control = Row(page, index, label);
        TextMeshProUGUI percent = UIKit.Text(control, "", UIKit.TextStyle.Body, TextAlignmentOptions.Right);
        percent.rectTransform.Place(new Vector2(1, 0.5f), Vector2.zero, new Vector2(84, 44), new Vector2(1, 0.5f));
        Action<float> show = v => percent.text = Mathf.RoundToInt(v * 100) + "%";
        show(value);
        Slider slider = UIKit.Slider(control, value, v =>
        {
            set(v);
            show(v);
        }, ControlWidth - 20);
        ((RectTransform)slider.transform).Place(new Vector2(0, 0.5f), Vector2.zero, new Vector2(ControlWidth - 20, 44), new Vector2(0, 0.5f));
        return slider;
    }

    private UIStepper StepperRow(RectTransform page, int index, string label, string[] options, int selected, Action<int> set, string caption = null)
    {
        RectTransform control = Row(page, index, label, caption);
        UIStepper stepper = UIKit.Stepper(control, options, selected, set, ControlWidth);
        ((RectTransform)stepper.transform).Place(new Vector2(1, 0.5f), Vector2.zero, new Vector2(ControlWidth, 52), new Vector2(1, 0.5f));
        return stepper;
    }

    //----------------------------------------Pages----------------------------------------//
    private RectTransform BuildAudioPage(RectTransform content)
    {
        RectTransform page = Page(content, "Audio");
        int row = 0;
        Slider master = VolumeRow(page, row++, "Master volume", GameSettings.MasterVolume, v => GameSettings.MasterVolume = v);
        VolumeRow(page, row++, "Music", GameSettings.MusicVolume, v => GameSettings.MusicVolume = v);
        VolumeRow(page, row++, "Sound effects", GameSettings.SfxVolume, v => GameSettings.SfxVolume = v);
        if (!GameSettings.IsMobile)
        {
            RectTransform control = Row(page, row++, "Mute when in the background", "When another window is in front");
            Toggle toggle = UIKit.Switch(control, GameSettings.MuteInBackground, v => GameSettings.MuteInBackground = v);
            ((RectTransform)toggle.transform).Place(new Vector2(1, 0.5f), Vector2.zero, new Vector2(76, 40), new Vector2(1, 0.5f));
        }
        firstControls.Add(master);
        return page;
    }

    private RectTransform BuildDisplayPage(RectTransform content)
    {
        RectTransform page = Page(content, "Display");
        int row = 0;
        Selectable first = null;

        if (!GameSettings.IsMobile)
        {
            UIStepper window = StepperRow(page, row++, "Window", new[] { "Fullscreen", "Windowed" }, GameSettings.Fullscreen ? 0 : 1,
                i => GameSettings.Fullscreen = i == 0);
            first = window.left;

            List<Vector2Int> sizes = GameSettings.AvailableResolutions();
            Vector2Int current = GameSettings.Resolution;
            StepperRow(page, row++, "Resolution", sizes.Select(r => r.x + " × " + r.y).ToArray(), Mathf.Max(0, sizes.IndexOf(current)),
                i => GameSettings.Resolution = sizes[i]);
        }

        int[] rates = GameSettings.FrameRates;
        UIStepper frame_rate = StepperRow(page, row++, "Frame rate", rates.Select(GameSettings.FrameRateLabel).ToArray(),
            Mathf.Max(0, Array.IndexOf(rates, GameSettings.FrameRate)), i => GameSettings.FrameRate = rates[i],
            GameSettings.IsMobile ? "Lower values save battery" : "VSync follows your screen");
        if (first == null)
            first = frame_rate.left;

        float[] scales = GameSettings.MenuScales;
        int scale_index = Array.FindIndex(scales, s => Mathf.Approximately(s, GameSettings.MenuScale));
        StepperRow(page, row++, "Menu size", scales.Select(s => Mathf.RoundToInt(s * 100) + "%").ToArray(), scale_index < 0 ? 1 : scale_index,
            i => GameSettings.MenuScale = scales[i], "Menus and dialogs only");

        firstControls.Add(first);
        return page;
    }

    // What each difficulty means, under its name
    private static readonly string[] DifficultyCaptions =
    {
        "Your opponent plays like a beginner",
        "Your opponent saves cards for later rounds",
        "Your opponent thinks the battle through",
    };

    private RectTransform BuildGamePage(RectTransform content)
    {
        RectTransform page = Page(content, "Game");
        UIStepper speed = StepperRow(page, 0, "Game speed", GameSettings.GameSpeedNames, GameSettings.GameSpeed,
            i => GameSettings.GameSpeed = i, "Cards, notices and the opponent");

        TextMeshProUGUI about = null;
        UIStepper difficulty = StepperRow(page, 1, "Difficulty", GameSettings.AiDifficultyNames, (int)GameSettings.AiDifficulty, i =>
        {
            GameSettings.AiDifficulty = (AiDifficulty)i;
            if (about != null)
                about.text = DifficultyCaptions[i];
        }, DifficultyCaptions[(int)GameSettings.AiDifficulty]);
        about = difficulty.transform.parent.parent.Find("Caption").GetComponent<TextMeshProUGUI>();

        firstControls.Add(speed.left);
        return page;
    }

    private void ResetToDefaults()
    {
        GameSettings.ResetToDefaults();
        // Rebuild with the default values, on the same tab
        Action closed = onClosed;
        int tab = currentTab;
        onClosed = null;
        Close();
        Open(closed, tab);
    }
}

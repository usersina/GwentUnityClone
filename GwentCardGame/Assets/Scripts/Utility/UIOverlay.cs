using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Small runtime-built UI on top of the current scene: toasts, confirmation dialogs and extra buttons.
// Built in code so it works without editing the scenes; uses the same 1920x1080 reference as the scene canvases.
public static class UIOverlay
{
    public static readonly Color Gold = new Color32(255, 188, 0, 255);
    private static readonly Color PanelColor = new Color32(24, 20, 16, 235);
    private static readonly Color ButtonColor = new Color32(70, 52, 30, 255);

    private static Canvas canvas;
    private static GameObject currentToast;

    public static Transform Root
    {
        get
        {
            if (canvas == null)
                CreateCanvas();
            return canvas.transform;
        }
    }

    private static void CreateCanvas()
    {
        GameObject canvas_go = new GameObject("UIOverlay", typeof(RectTransform));
        canvas = canvas_go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        CanvasScaler scaler = canvas_go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1;
        canvas_go.AddComponent<GraphicRaycaster>();

        if (EventSystem.current == null && UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    //----------------------------------------Building blocks----------------------------------------//
    public static RectTransform CreateRect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    public static TextMeshProUGUI CreateText(Transform parent, string text, float font_size, Vector2 anchor, Vector2 position, Vector2 size)
    {
        RectTransform rect = CreateRect("Text", parent, anchor, position, size);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = font_size;
        label.color = Gold;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    public static Button CreateButton(Transform parent, string text, Vector2 anchor, Vector2 position, Vector2 size, Action on_click)
    {
        RectTransform rect = CreateRect("Button " + text, parent, anchor, position, size);
        Image background = rect.gameObject.AddComponent<Image>();
        background.color = ButtonColor;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        if (on_click != null)
            button.onClick.AddListener(() => on_click());

        TextMeshProUGUI label = CreateText(rect, text, 30, new Vector2(0.5f, 0.5f), Vector2.zero, size);
        label.enableWordWrapping = false;
        return button;
    }

    //----------------------------------------Toast----------------------------------------//
    // Short message at the top of the screen, replaced by the next one
    public static void Toast(string message, float seconds = 2.5f)
    {
        GameLog.Info("Toast: " + message);
        if (currentToast != null)
            UnityEngine.Object.Destroy(currentToast);

        RectTransform rect = CreateRect("Toast", Root, new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(1100, 80));
        Image background = rect.gameObject.AddComponent<Image>();
        background.color = PanelColor;
        background.raycastTarget = false; // never blocks clicks on the board
        CreateText(rect, message, 32, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1060, 76));
        currentToast = rect.gameObject;
        UnityEngine.Object.Destroy(currentToast, seconds);
    }

    //----------------------------------------Confirmation dialog----------------------------------------//
    public static void Confirm(string message, string yes_text, string no_text, Action on_yes)
    {
        // Full-screen dimmer blocks clicks on the scene behind the dialog
        RectTransform dimmer = CreateRect("ConfirmDialog", Root, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        dimmer.anchorMin = Vector2.zero;
        dimmer.anchorMax = Vector2.one;
        dimmer.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.6f);
        GameObject dialog_go = dimmer.gameObject;
        dialog_go.AddComponent<CloseOnEscape>();

        RectTransform panel = CreateRect("Panel", dimmer, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 320));
        panel.gameObject.AddComponent<Image>().color = PanelColor;
        TextMeshProUGUI label = CreateText(panel, message, 36, new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(840, 160));
        label.enableWordWrapping = true;

        CreateButton(panel, yes_text, new Vector2(0.5f, 0.5f), new Vector2(-170, -90), new Vector2(280, 80), () =>
        {
            UnityEngine.Object.Destroy(dialog_go);
            on_yes();
        });
        CreateButton(panel, no_text, new Vector2(0.5f, 0.5f), new Vector2(170, -90), new Vector2(280, 80), () => UnityEngine.Object.Destroy(dialog_go));
    }

    // Right click / Escape dismisses a dialog, like the other popups of the game
    private class CloseOnEscape : MonoBehaviour
    {
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
                Destroy(gameObject);
        }
    }
}

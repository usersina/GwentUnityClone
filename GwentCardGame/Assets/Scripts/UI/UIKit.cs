using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Builds the themed menus and dialogs in code: text styles, panels, buttons, menu items, sliders, switches and
// option steppers. Sizes are in the 1920x1080 reference space of the overlay canvas.
public static class UIKit
{
    public enum TextStyle { Title, Heading, Tab, MenuItem, Button, Body, Label, Caption }
    public enum ButtonStyle { Primary, Secondary, Ghost }

    private static UITheme Theme { get { return UITheme.Current; } }

    //----------------------------------------Layout----------------------------------------//
    public static RectTransform Rect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    // Fixed size at an anchor point
    public static RectTransform Place(this RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    // Fills the parent, minus margins
    public static RectTransform Stretch(this RectTransform rect, float left = 0, float right = 0, float top = 0, float bottom = 0)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        return rect;
    }

    public static Image NewImage(Transform parent, string name, Sprite sprite, Color color, bool raycast = false)
    {
        Image image = Rect(name, parent).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    //----------------------------------------Text----------------------------------------//
    public static TextMeshProUGUI Text(Transform parent, string text, TextStyle style, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        TextMeshProUGUI label = Rect("Text", parent).gameObject.AddComponent<TextMeshProUGUI>();
        ApplyStyle(label, style);
        label.text = text;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }

    public static void ApplyStyle(TextMeshProUGUI label, TextStyle style)
    {
        UITheme theme = Theme;
        bool heading = style == TextStyle.Title || style == TextStyle.Heading || style == TextStyle.Tab || style == TextStyle.MenuItem || style == TextStyle.Button;
        TMP_FontAsset font = heading ? theme.headingFont : theme.bodyFont;
        if (font != null)
            label.font = font;
        label.characterSpacing = heading ? 4 : 0;
        switch (style)
        {
            case TextStyle.Title: label.fontSize = 60; label.color = theme.goldBright; break;
            case TextStyle.Heading: label.fontSize = 44; label.color = theme.goldBright; break;
            case TextStyle.Tab: label.fontSize = 28; label.color = theme.textSecondary; break;
            case TextStyle.MenuItem: label.fontSize = 40; label.color = theme.textSecondary; break;
            case TextStyle.Button: label.fontSize = 28; label.color = theme.gold; break;
            case TextStyle.Body: label.fontSize = 32; label.color = theme.text; break;
            case TextStyle.Label: label.fontSize = 30; label.color = theme.textSecondary; break;
            case TextStyle.Caption: label.fontSize = 24; label.color = theme.textMuted; break;
        }
    }

    //----------------------------------------Surfaces----------------------------------------//
    // Rounded panel with a thin bronze outline; blocks clicks to what is behind it
    public static RectTransform Panel(Transform parent, string name, bool raised = false)
    {
        Image fill = NewImage(parent, name, Theme.panelSprite, raised ? Theme.panelRaised : Theme.panel, true);
        Image outline = NewImage(fill.transform, "Outline", Theme.panelOutline, Theme.line);
        outline.rectTransform.Stretch();
        return fill.rectTransform;
    }

    // Full-screen darkening behind a dialog; blocks clicks to the scene
    public static RectTransform Dimmer(Transform parent, string name)
    {
        Image image = NewImage(parent, name, null, Theme.dimmer, true);
        return image.rectTransform.Stretch();
    }

    // Thin gold rule with a diamond in the middle
    public static Image Ornament(Transform parent, Vector2 anchor, Vector2 position, float width = 420)
    {
        Image rule = NewImage(parent, "Ornament", Theme.rule, UITheme.WithAlpha(Theme.gold, 0.85f));
        rule.rectTransform.Place(anchor, position, new Vector2(width, 12));
        return rule;
    }

    public static Image Divider(Transform parent, float y, float margin)
    {
        Image line = NewImage(parent, "Divider", null, Theme.lineSoft);
        RectTransform rect = line.rectTransform;
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(margin, y - 1);
        rect.offsetMax = new Vector2(-margin, y + 1);
        return line;
    }

    //----------------------------------------Buttons----------------------------------------//
    public static Button Button(Transform parent, string text, ButtonStyle style, Action on_click, Vector2? size = null)
    {
        UITheme theme = Theme;
        Image fill = NewImage(parent, "Button " + text, theme.panelSprite, Color.clear, true);
        fill.rectTransform.sizeDelta = size ?? new Vector2(280, 68);
        Button button = fill.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = fill;

        TextMeshProUGUI label = Text(fill.transform, text, TextStyle.Button, TextAlignmentOptions.Center);
        label.rectTransform.Stretch(12, 12, 0, 2);

        UIStateColors states = fill.gameObject.AddComponent<UIStateColors>();
        switch (style)
        {
            case ButtonStyle.Primary:
                states.Add(fill, theme.gold, theme.goldBright, UITheme.Shade(theme.gold, 0.85f));
                states.Add(label, theme.background, theme.background, theme.background);
                break;
            case ButtonStyle.Secondary:
                Image outline = NewImage(fill.transform, "Outline", theme.panelOutline, theme.gold);
                outline.rectTransform.Stretch();
                outline.transform.SetAsFirstSibling();
                states.Add(fill, UITheme.WithAlpha(theme.gold, 0), UITheme.WithAlpha(theme.gold, 0.16f), UITheme.WithAlpha(theme.gold, 0.28f));
                states.Add(outline, theme.gold, theme.goldBright, theme.goldBright);
                states.Add(label, theme.gold, theme.goldBright, theme.goldBright);
                break;
            default:
                label.font = theme.bodyFont != null ? theme.bodyFont : label.font;
                label.characterSpacing = 0;
                label.fontSize = 28;
                states.Add(label, theme.textMuted, theme.text, theme.textSecondary);
                break;
        }

        if (on_click != null)
            button.onClick.AddListener(() =>
            {
                GameAudio.PlaySfx("ui_click");
                on_click();
            });
        return button;
    }

    // Witcher-style menu entry: serif text that lights up with a gold rule and a fading band when hovered or selected
    public static Button MenuItem(Transform parent, string text, Action on_click, bool centered = false, Vector2? size = null)
    {
        UITheme theme = Theme;
        RectTransform root = Rect("Item " + text, parent);
        root.sizeDelta = size ?? new Vector2(560, 76);
        Image hit = root.gameObject.AddComponent<Image>();
        hit.color = Color.clear;

        Image band = NewImage(root, "Band", centered ? theme.radial : theme.gradientH, Color.clear);
        band.rectTransform.Stretch();
        Image rule = NewImage(root, "Rule", null, Color.clear);
        if (centered)
            rule.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0, 4), new Vector2(160, 2));
        else
            rule.rectTransform.Place(new Vector2(0f, 0.5f), Vector2.zero, new Vector2(4, 0), new Vector2(0, 0.5f));
        if (!centered)
        {
            rule.rectTransform.anchorMin = new Vector2(0, 0.12f);
            rule.rectTransform.anchorMax = new Vector2(0, 0.88f);
        }

        TextMeshProUGUI label = Text(root, text, TextStyle.MenuItem, centered ? TextAlignmentOptions.Center : TextAlignmentOptions.Left);
        label.rectTransform.Stretch(centered ? 0 : 36, 0, 0, 2);

        Button button = root.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = hit;

        UIStateColors states = root.gameObject.AddComponent<UIStateColors>();
        states.selectOnHover = true;
        states.Add(band, UITheme.WithAlpha(theme.gold, 0), UITheme.WithAlpha(theme.gold, 0.2f), UITheme.WithAlpha(theme.gold, 0.3f));
        states.Add(rule, UITheme.WithAlpha(theme.gold, 0), theme.gold, theme.goldBright);
        states.Add(label, theme.textSecondary, theme.textBright, theme.goldBright);

        if (on_click != null)
            button.onClick.AddListener(() =>
            {
                GameAudio.PlaySfx("ui_click");
                on_click();
            });
        return button;
    }

    // Small icon-only button (chevrons, menu, close)
    public static Button IconButton(Transform parent, Sprite icon, Action on_click, float size = 48)
    {
        UITheme theme = Theme;
        Image hit = NewImage(parent, "IconButton", null, Color.clear, true);
        hit.rectTransform.sizeDelta = new Vector2(size, size);
        Image glyph = NewImage(hit.transform, "Icon", icon, theme.gold);
        glyph.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.75f, size * 0.75f));
        Button button = hit.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = hit;
        UIStateColors states = hit.gameObject.AddComponent<UIStateColors>();
        states.Add(glyph, theme.gold, theme.goldBright, theme.textBright);
        if (on_click != null)
            button.onClick.AddListener(() =>
            {
                GameAudio.PlaySfx("ui_click");
                on_click();
            });
        return button;
    }

    //----------------------------------------Controls----------------------------------------//
    public static Slider Slider(Transform parent, float value, Action<float> on_change, float width = 400)
    {
        UITheme theme = Theme;
        RectTransform root = Rect("Slider", parent);
        root.sizeDelta = new Vector2(width, 44);
        root.gameObject.AddComponent<Image>().color = Color.clear; // the whole row height is clickable

        Image track = NewImage(root, "Track", theme.pill, theme.lineSoft);
        track.rectTransform.anchorMin = new Vector2(0, 0.5f);
        track.rectTransform.anchorMax = new Vector2(1, 0.5f);
        track.rectTransform.sizeDelta = new Vector2(0, 8);

        RectTransform fill_area = Rect("Fill Area", root);
        fill_area.anchorMin = new Vector2(0, 0.5f);
        fill_area.anchorMax = new Vector2(1, 0.5f);
        fill_area.offsetMin = new Vector2(0, -4);
        fill_area.offsetMax = new Vector2(-14, 4);
        Image fill = NewImage(fill_area, "Fill", theme.pill, theme.gold);
        fill.rectTransform.sizeDelta = Vector2.zero;

        // The Slider stretches the handle over the slide area's height, so the area is exactly the knob's height
        RectTransform handle_area = Rect("Handle Slide Area", root);
        handle_area.anchorMin = new Vector2(0, 0.5f);
        handle_area.anchorMax = new Vector2(1, 0.5f);
        handle_area.offsetMin = new Vector2(14, -14);
        handle_area.offsetMax = new Vector2(-14, 14);
        Image handle = NewImage(handle_area, "Handle", theme.circle, theme.background);
        handle.rectTransform.sizeDelta = new Vector2(28, 0);
        Image ring = NewImage(handle.transform, "Ring", theme.ring, theme.gold);
        ring.rectTransform.Stretch();

        Slider slider = root.gameObject.AddComponent<Slider>();
        slider.transition = Selectable.Transition.None;
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = ring;
        slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
        slider.minValue = 0;
        slider.maxValue = 1;
        slider.value = value;
        slider.onValueChanged.AddListener(v => on_change(v));

        UIStateColors states = root.gameObject.AddComponent<UIStateColors>();
        states.Add(ring, theme.gold, theme.goldBright, theme.textBright);
        return slider;
    }

    // On/off switch
    public static Toggle Switch(Transform parent, bool value, Action<bool> on_change)
    {
        UITheme theme = Theme;
        RectTransform root = Rect("Switch", parent);
        root.sizeDelta = new Vector2(76, 40);
        Image track = NewImage(root, "Track", theme.pill, theme.lineSoft, true);
        track.rectTransform.Stretch();
        Image knob = NewImage(track.transform, "Knob", theme.circle, theme.textSecondary);
        knob.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));

        Toggle toggle = root.gameObject.AddComponent<Toggle>();
        toggle.transition = Selectable.Transition.None;
        toggle.targetGraphic = track;
        toggle.isOn = value;
        UISwitchVisual visual = root.gameObject.AddComponent<UISwitchVisual>();
        visual.Init(toggle, track, knob);
        toggle.onValueChanged.AddListener(v =>
        {
            GameAudio.PlaySfx("ui_click");
            on_change(v);
        });
        return toggle;
    }

    // "<  value  >" selector, the Witcher 3 way of picking among a few options
    public static UIStepper Stepper(Transform parent, string[] options, int index, Action<int> on_change, float width = 400)
    {
        UITheme theme = Theme;
        RectTransform root = Rect("Stepper", parent);
        root.sizeDelta = new Vector2(width, 52);
        Image frame = NewImage(root, "Frame", theme.panelOutline, theme.lineSoft);
        frame.rectTransform.Stretch();

        UIStepper stepper = root.gameObject.AddComponent<UIStepper>();
        stepper.left = IconButton(root, theme.chevronLeft, () => stepper.Step(-1), 52);
        stepper.left.GetComponent<RectTransform>().Place(new Vector2(0, 0.5f), Vector2.zero, new Vector2(52, 52), new Vector2(0, 0.5f));
        stepper.right = IconButton(root, theme.chevronRight, () => stepper.Step(1), 52);
        stepper.right.GetComponent<RectTransform>().Place(new Vector2(1, 0.5f), Vector2.zero, new Vector2(52, 52), new Vector2(1, 0.5f));
        stepper.label = Text(root, "", TextStyle.Body, TextAlignmentOptions.Center);
        stepper.label.rectTransform.Stretch(52, 52, 0, 2);
        stepper.options = options;
        stepper.index = Mathf.Clamp(index, 0, options.Length - 1);
        stepper.onChange = on_change;
        stepper.Refresh();
        return stepper;
    }

    // Puts keyboard / gamepad focus on a control
    public static void Focus(Selectable selectable)
    {
        if (selectable != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(selectable.gameObject);
    }
}

// Color states of a themed control (normal, highlighted, pressed, disabled), eased in unscaled time so menus
// also animate while the game is paused
public class UIStateColors : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    private struct Entry
    {
        public Graphic graphic;
        public Color normal, highlighted, pressed;
    }

    // Menu items: hovering selects, so the mouse and the keyboard share one highlight (the Witcher 3 behaviour)
    public bool selectOnHover;

    private readonly List<Entry> entries = new List<Entry>();
    private Selectable selectable;
    private bool hovered, pressed, selected, selectedByPointer;

    public void Add(Graphic graphic, Color normal, Color highlighted, Color pressed_color)
    {
        entries.Add(new Entry { graphic = graphic, normal = normal, highlighted = highlighted, pressed = pressed_color });
        graphic.color = normal;
    }

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
    }

    private void OnEnable()
    {
        hovered = pressed = false;
        Apply(1f);
    }

    private void Update()
    {
        Apply(1f - Mathf.Exp(-Time.unscaledDeltaTime * 20f));
    }

    private void Apply(float blend)
    {
        bool interactable = selectable == null || selectable.IsInteractable();
        bool highlighted = interactable && (hovered || (selected && (selectOnHover || !selectedByPointer)));
        foreach (Entry entry in entries)
        {
            if (entry.graphic == null)
                continue;
            Color target = !interactable ? UITheme.WithAlpha(entry.normal, entry.normal.a * 0.35f)
                : pressed && hovered ? entry.pressed
                : highlighted ? entry.highlighted
                : entry.normal;
            entry.graphic.color = Color.Lerp(entry.graphic.color, target, blend);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        if (selectOnHover && EventSystem.current != null && selectable != null && selectable.IsInteractable())
            EventSystem.current.SetSelectedGameObject(gameObject);
    }

    public void OnPointerExit(PointerEventData eventData) { hovered = false; pressed = false; }
    public void OnPointerDown(PointerEventData eventData) { pressed = true; }
    public void OnPointerUp(PointerEventData eventData) { pressed = false; }

    public void OnSelect(BaseEventData eventData)
    {
        selected = true;
        // Selected by a click: not highlighted once the pointer leaves (keyboard selection stays highlighted)
        selectedByPointer = eventData is PointerEventData;
    }

    public void OnDeselect(BaseEventData eventData) { selected = false; }
}

// Moves the knob and recolors the track of a UIKit switch
public class UISwitchVisual : MonoBehaviour
{
    private Toggle toggle;
    private Image track;
    private Image knob;

    public void Init(Toggle target, Image track_image, Image knob_image)
    {
        toggle = target;
        track = track_image;
        knob = knob_image;
        Apply(1f);
    }

    private void OnEnable()
    {
        Apply(1f);
    }

    private void Update()
    {
        Apply(1f - Mathf.Exp(-Time.unscaledDeltaTime * 18f));
    }

    private void Apply(float blend)
    {
        if (toggle == null)
            return;
        UITheme theme = UITheme.Current;
        bool on = toggle.isOn;
        RectTransform knob_rect = knob.rectTransform;
        knob_rect.anchoredPosition = Vector2.Lerp(knob_rect.anchoredPosition, new Vector2(on ? 18 : -18, 0), blend);
        track.color = Color.Lerp(track.color, on ? theme.gold : theme.lineSoft, blend);
        knob.color = Color.Lerp(knob.color, on ? theme.background : theme.textSecondary, blend);
    }
}

// Option selector built by UIKit.Stepper
public class UIStepper : MonoBehaviour
{
    public string[] options;
    public int index;
    public Action<int> onChange;
    public TextMeshProUGUI label;
    public Button left;
    public Button right;

    public void Step(int delta)
    {
        int next = Mathf.Clamp(index + delta, 0, options.Length - 1);
        if (next == index)
            return;
        index = next;
        Refresh();
        if (onChange != null)
            onChange(index);
    }

    public void Refresh()
    {
        label.text = options.Length > 0 ? options[index] : "";
        left.interactable = index > 0;
        right.interactable = index < options.Length - 1;
    }
}

using TMPro;
using UnityEngine;

// Fonts, colors and sprites of the menus, dialogs and buttons (the battle board keeps its own art).
// The asset lives in Resources/UITheme.asset; Gwent > UI > Regenerate Sprites and Theme rebuilds its sprites and fonts.
[CreateAssetMenu(menuName = "Gwent/UI Theme", fileName = "UITheme")]
public class UITheme : ScriptableObject
{
    [Header("Fonts")]
    public TMP_FontAsset headingFont;
    public TMP_FontAsset bodyFont;
    public TMP_FontAsset bodyMediumFont;

    [Header("Colors")]
    public Color background = new Color32(0x16, 0x12, 0x0E, 0xFF);
    public Color panel = new Color32(0x20, 0x1A, 0x15, 0xF5);
    public Color panelRaised = new Color32(0x2A, 0x22, 0x19, 0xFF);
    public Color line = new Color32(0x4D, 0x3F, 0x2C, 0xFF);
    public Color lineSoft = new Color32(0x3A, 0x2F, 0x22, 0xFF);
    public Color gold = new Color32(0xC9, 0xA4, 0x5C, 0xFF);
    public Color goldBright = new Color32(0xE3, 0xC2, 0x7E, 0xFF);
    public Color text = new Color32(0xEC, 0xE1, 0xC9, 0xFF);
    public Color textBright = new Color32(0xF6, 0xEC, 0xD4, 0xFF);
    public Color textSecondary = new Color32(0xB9, 0xAD, 0x95, 0xFF);
    public Color textMuted = new Color32(0x8F, 0x82, 0x6C, 0xFF);
    public Color danger = new Color32(0xC9, 0x5E, 0x4E, 0xFF);
    public Color dimmer = new Color(0, 0, 0, 0.65f);

    [Header("Sprites")]
    public Sprite panelSprite;
    public Sprite panelOutline;
    public Sprite pill;
    public Sprite pillOutline;
    public Sprite circle;
    public Sprite ring;
    public Sprite diamond;
    public Sprite gradientH;
    public Sprite gradientV;
    public Sprite radial;
    public Sprite rule;
    public Sprite chevronDown;
    public Sprite chevronLeft;
    public Sprite chevronRight;
    public Sprite iconMenu;
    public Sprite iconClose;
    public Sprite iconPlus;
    public Sprite iconCheck;
    public Sprite iconBack;
    public Sprite iconTrash;
    public Sprite iconEdit;
    public Sprite softShadow; // soft blurred rounded rectangle (card shadows, glows)

    [Header("Cursors (tip at 5, 4)")]
    public Texture2D cursorDefault;
    public Texture2D cursorInteract;
    public Texture2D cursorInvalid;

    private static UITheme current;

    public static UITheme Current
    {
        get
        {
            if (current == null)
            {
                current = Resources.Load<UITheme>("UITheme");
                if (current == null)
                    current = CreateInstance<UITheme>(); // default colors, Unity's default sprites and fonts
            }
            return current;
        }
    }

    public static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    // Darker (factor < 1) or lighter (> 1) version of a color, same alpha
    public static Color Shade(Color color, float factor)
    {
        return new Color(color.r * factor, color.g * factor, color.b * factor, color.a);
    }
}

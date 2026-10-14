using TMPro;
using UnityEngine;
using UnityEngine.UI;

// "Legacy" under the key art's GWENT logo, set the way the official logo sets "The Witcher Card Game": gold capitals,
// widely spaced, between fine rules ending in diamonds. When the menu opens the letters come together out of a wide
// spacing as they fade in and the rules draw outwards; then, every few seconds, a soft light runs across the letters.
public class LegacyTitle : MonoBehaviour
{
    // Under the logo in the key art (Resources/Background/gwent_cover 1.png, 1920 px wide): centred, the logo's letters
    // ending 487 px from the top
    private static readonly Vector2 UnderLogo = new Vector2(960, 518);

    private const float Delay = 0.35f;        // after the menu opens
    private const float IntroSeconds = 1.6f;
    private const float StartSpacing = 70;    // TMP character spacing, in em/100
    private const float EndSpacing = 24;
    private const float RuleLength = 170;
    private const float SheenEvery = 7f;
    private const float SheenSeconds = 1.4f;
    private const float SheenWidth = 0.16f;  // the light's spread, of the word's width (wider than a letter: the light
                                              // is laid on the letters' corners)

    private static readonly Color32 GoldTop = new Color32(0xFF, 0xF0, 0xC8, 0xFF);
    private static readonly Color32 GoldBottom = new Color32(0xD9, 0xA2, 0x4E, 0xFF);
    private static readonly Color32 Shine = new Color32(0xFF, 0xFB, 0xEE, 0xFF);

    private TextMeshProUGUI text;
    private RectTransform[] rules;
    private Image[] diamonds;
    private float age;

    // On the menu's key art (an image exactly the art's shape), so the word stays under the logo whatever the screen's shape
    public static LegacyTitle Create(Image art)
    {
        Vector2 size = art.sprite != null ? art.sprite.rect.size : new Vector2(1920, 1010);
        RectTransform root = UIKit.Rect("Legacy", art.rectTransform);
        Vector2 anchor = new Vector2(UnderLogo.x / size.x, 1 - UnderLogo.y / size.y);
        root.Place(anchor, Vector2.zero, new Vector2(800, 64));
        LegacyTitle title = root.gameObject.AddComponent<LegacyTitle>();
        title.Build();
        return title;
    }

    private void Build()
    {
        UITheme theme = UITheme.Current;
        text = UIKit.Text(transform, "Legacy", UIKit.TextStyle.Title, TextAlignmentOptions.Center);
        text.fontSize = 42;
        text.fontStyle = FontStyles.UpperCase;
        text.characterSpacing = StartSpacing;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.color = Color.white; // the gold is laid on the letters' vertices
        if (theme.headingShadow != null)
            text.fontSharedMaterial = theme.headingShadow; // a dark outline and a soft shadow: it reads on any art
        text.rectTransform.Stretch();

        // The rules: a fine gold line on each side, fading outwards, a diamond at its inner end
        rules = new RectTransform[2];
        diamonds = new Image[2];
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1 : 1;
            Image line = UIKit.NewImage(transform, "Rule", theme.gradientH, UITheme.WithAlpha(theme.goldBright, 0.85f));
            RectTransform rect = line.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0, 0.5f); // grows from its inner end
            rect.sizeDelta = new Vector2(RuleLength, 2);
            if (side < 0)
                rect.localEulerAngles = new Vector3(0, 0, 180); // fades out to the left
            rules[i] = rect;

            Image diamond = UIKit.NewImage(transform, "Diamond", theme.diamond, theme.goldBright);
            diamond.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12, 12));
            diamonds[i] = diamond;
        }
        Layout(0);
    }

    private void LateUpdate()
    {
        age += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01((age - Delay) / IntroSeconds);
        float eased = 1 - (1 - t) * (1 - t) * (1 - t);
        if (t < 1 || text.characterSpacing != EndSpacing)
        {
            text.characterSpacing = Mathf.Lerp(StartSpacing, EndSpacing, eased);
            Layout(eased);
        }
        // The light passes from just before the word to just after it
        float since = age - Delay - IntroSeconds - 1.2f;
        float sheen = since > 0 ? (since % SheenEvery) / SheenSeconds : 2;
        Paint(eased, sheen <= 1 ? Mathf.Lerp(-SheenWidth * 3, 1 + SheenWidth * 3, sheen * sheen * (3 - 2 * sheen)) : float.NaN);
    }

    // The rules start at the word's ends and draw outwards; the diamonds come last
    private void Layout(float eased)
    {
        text.ForceMeshUpdate();
        float half = text.textBounds.size.x / 2 + 26;
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1 : 1;
            rules[i].anchoredPosition = new Vector2(side * (half + 14), 0);
            rules[i].localScale = new Vector3(eased, 1, 1);
            diamonds[i].rectTransform.anchoredPosition = new Vector2(side * half, 0);
            diamonds[i].rectTransform.localEulerAngles = new Vector3(0, 0, 45 * (1 - eased));
            Color color = diamonds[i].color;
            color.a = Mathf.Clamp01((eased - 0.5f) / 0.5f);
            diamonds[i].color = color;
            Color line = rules[i].GetComponent<Image>().color;
            line.a = 0.85f * eased;
            rules[i].GetComponent<Image>().color = line;
        }
    }

    // Gold from the letters' tops to their feet, the light where it passes, everything faded in by the intro
    private void Paint(float alpha, float sheen_at)
    {
        text.ForceMeshUpdate();
        TMP_TextInfo info = text.textInfo;
        Bounds bounds = text.textBounds;
        float left = bounds.min.x, width = Mathf.Max(1, bounds.size.x);
        float bottom = bounds.min.y, height = Mathf.Max(1, bounds.size.y);
        byte a = (byte)Mathf.RoundToInt(255 * alpha);
        for (int c = 0; c < info.characterCount; c++)
        {
            TMP_CharacterInfo character = info.characterInfo[c];
            if (!character.isVisible)
                continue;
            Color32[] colors = info.meshInfo[character.materialReferenceIndex].colors32;
            Vector3[] vertices = info.meshInfo[character.materialReferenceIndex].vertices;
            for (int v = 0; v < 4; v++)
            {
                int index = character.vertexIndex + v;
                Vector3 p = vertices[index];
                Color32 gold = Color32.Lerp(GoldBottom, GoldTop, Mathf.Clamp01((p.y - bottom) / height));
                if (!float.IsNaN(sheen_at))
                {
                    float x = (p.x - left) / width;
                    float light = Mathf.Exp(-(x - sheen_at) * (x - sheen_at) / (2 * SheenWidth * SheenWidth));
                    gold = Color32.Lerp(gold, Shine, light);
                }
                gold.a = a;
                colors[index] = gold;
            }
        }
        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }
}

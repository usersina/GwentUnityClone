using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

// Draws the UI's simple shapes (rounded panels, pills, circles, gradients, chevrons, icons) as anti-aliased PNGs
// and wires them into the UI theme (Resources/UITheme.asset) together with the fonts.
// Shapes are drawn at twice their on-screen size and imported at 200 pixels per unit, so they stay sharp on 4K screens.
public static class UISpriteGenerator
{
    public const string SpriteFolder = "Assets/UI/Sprites";
    public const string BoardFolder = "Assets/UI/Board";
    public const string ThemePath = "Assets/Resources/UITheme.asset";
    private const int PixelsPerUnit = 200;

    private delegate float Shape(float x, float y); // signed distance in texture pixels (< 0 inside)
    private delegate float Alpha(float u, float v);  // 0..1 coverage from normalized coordinates

    [MenuItem("Gwent/UI/Regenerate Sprites and Theme")]
    public static void Generate()
    {
        Directory.CreateDirectory(SpriteFolder);

        // Panels: 96 px texture, 20 px corner radius (10 px on screen), 24 px nine-slice border
        Write("Panel", 96, 96, (x, y) => RoundedBox(x, y, 96, 96, 20), new Vector4(24, 24, 24, 24));
        Write("PanelOutline", 96, 96, (x, y) => Mathf.Abs(RoundedBox(x, y, 96, 96, 20, 1.5f)) - 1.5f, new Vector4(24, 24, 24, 24));
        // Pills (slider tracks, switches): fully rounded ends
        Write("Pill", 64, 32, (x, y) => RoundedBox(x, y, 64, 32, 16), new Vector4(16, 16, 16, 16));
        Write("PillOutline", 64, 32, (x, y) => Mathf.Abs(RoundedBox(x, y, 64, 32, 16, 1.5f)) - 1.5f, new Vector4(16, 16, 16, 16));
        Write("Circle", 64, 64, (x, y) => Circle(x, y, 32, 32, 31), Vector4.zero);
        Write("Ring", 64, 64, (x, y) => Mathf.Abs(Circle(x, y, 32, 32, 28.5f)) - 2.5f, Vector4.zero);
        Write("Diamond", 24, 24, (x, y) => (Mathf.Abs(x - 12) + Mathf.Abs(y - 12) - 11) * 0.7071f, Vector4.zero);

        // Gradients (white, tinted by the Image color): horizontal fades out to the right, vertical fades out to the top
        WriteAlpha("GradientH", 256, 4, (u, v) => Smooth(1 - u));
        WriteAlpha("GradientV", 4, 256, (u, v) => Smooth(1 - v));
        WriteAlpha("Radial", 128, 128, (u, v) => Smooth(Mathf.Clamp01(1 - Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2)));
        // Thin rule that fades at both ends, with a diamond in the middle
        Write("Rule", 800, 24, (x, y) =>
        {
            float line = Mathf.Abs(y - 12) - 1f;
            float diamond = (Mathf.Abs(x - 400) + Mathf.Abs(y - 12) - 9) * 0.7071f;
            return Mathf.Min(line, diamond);
        }, Vector4.zero, (u, v) => Mathf.Min(1, Mathf.Min(u, 1 - u) * 6));

        // Icons: 48 px texture (24 px on screen), 2.5 px strokes
        Write("ChevronDown", 48, 48, (x, y) => Mathf.Min(Segment(x, y, 14, 30, 24, 19), Segment(x, y, 24, 19, 34, 30)) - 2.5f, Vector4.zero);
        Write("ChevronLeft", 48, 48, (x, y) => Mathf.Min(Segment(x, y, 29, 13, 18, 24), Segment(x, y, 18, 24, 29, 35)) - 2.5f, Vector4.zero);
        Write("ChevronRight", 48, 48, (x, y) => Mathf.Min(Segment(x, y, 19, 13, 30, 24), Segment(x, y, 30, 24, 19, 35)) - 2.5f, Vector4.zero);
        Write("IconMenu", 48, 48, (x, y) => Mathf.Min(Segment(x, y, 12, 14, 36, 14), Mathf.Min(Segment(x, y, 12, 24, 36, 24), Segment(x, y, 12, 34, 36, 34))) - 2.5f, Vector4.zero);
        Write("IconClose", 48, 48, (x, y) => Mathf.Min(Segment(x, y, 14, 14, 34, 34), Segment(x, y, 14, 34, 34, 14)) - 2.5f, Vector4.zero);
        Write("IconPlus", 48, 48, (x, y) => Mathf.Min(Segment(x, y, 24, 11, 24, 37), Segment(x, y, 11, 24, 37, 24)) - 2.5f, Vector4.zero);
        Write("IconCheck", 48, 48, (x, y) => Mathf.Min(Segment(x, y, 12, 25, 20, 33), Segment(x, y, 20, 33, 36, 15)) - 2.5f, Vector4.zero);
        Write("IconBack", 48, 48, (x, y) => Mathf.Min(Segment(x, y, 13, 24, 36, 24), Mathf.Min(Segment(x, y, 23, 14, 13, 24), Segment(x, y, 13, 24, 23, 34))) - 2.5f, Vector4.zero);
        Write("IconTrash", 48, 48, (x, y) =>
        {
            float lid = Mathf.Min(Segment(x, y, 11, 14, 37, 14), Segment(x, y, 20, 9, 28, 9));
            float body = Mathf.Min(Mathf.Min(Segment(x, y, 15, 19, 17, 38), Segment(x, y, 17, 38, 31, 38)), Segment(x, y, 31, 38, 33, 19));
            float ribs = Mathf.Min(Segment(x, y, 21, 23, 21, 33), Segment(x, y, 27, 23, 27, 33));
            return Mathf.Min(lid, Mathf.Min(body, ribs)) - 2f;
        }, Vector4.zero);
        // Pencil: hollow rounded body along the diagonal, a band near the top and the tip
        Write("IconEdit", 48, 48, (x, y) =>
        {
            float body = Mathf.Abs(Segment(x, y, 17, 31, 32, 16) - 4.5f) - 1.75f;
            float band = Segment(x, y, 25.5f, 16.5f, 31.5f, 22.5f) - 1.75f;
            float tip = Segment(x, y, 11, 37, 15.5f, 32.5f) - 2f;
            return Mathf.Min(body, Mathf.Min(band, tip));
        }, Vector4.zero);

        GenerateBoard();

        AssetDatabase.Refresh();
        foreach (string folder in new[] { SpriteFolder, BoardFolder })
            foreach (string file in Directory.GetFiles(folder, "*.png"))
                ConfigureImporter(file.Replace('\\', '/'), folder == BoardFolder ? 100 : PixelsPerUnit);
        AssetDatabase.Refresh();
        WireTheme();
        Debug.Log("UI sprites and theme regenerated in " + SpriteFolder);
    }

    //----------------------------------------Shapes (signed distances in texture pixels)----------------------------------------//
    // Box filling the texture (inset by `inset` on every side), with rounded corners
    private static float RoundedBox(float x, float y, float w, float h, float radius, float inset = 0)
    {
        float hx = w / 2 - inset, hy = h / 2 - inset;
        float qx = Mathf.Abs(x - w / 2) - (hx - radius);
        float qy = Mathf.Abs(y - h / 2) - (hy - radius);
        return new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - radius;
    }

    private static float Circle(float x, float y, float cx, float cy, float radius)
    {
        return new Vector2(x - cx, y - cy).magnitude - radius;
    }

    // Distance to a segment (y measured from the top, like the drawings above)
    private static float Segment(float x, float y, float ax, float ay, float bx, float by)
    {
        Vector2 p = new Vector2(x, y), a = new Vector2(ax, ay), b = new Vector2(bx, by);
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return (p - (a + ab * t)).magnitude;
    }

    private static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3 - 2 * t);
    }

    //----------------------------------------Battle board----------------------------------------//
    // The board keeps its layout and wood; these replace the flat score discs, gems and portrait placeholder,
    // and give the rows a recessed frame (gold when highlighted) instead of the bright orange border.
    private static readonly Color Amber = new Color32(0xF0, 0xB2, 0x45, 0xFF);
    private static readonly Color AmberDeep = new Color32(0xB8, 0x72, 0x14, 0xFF);
    private static readonly Color Brass = new Color32(0xC2, 0x93, 0x48, 0xFF);
    private static readonly Color Ice = new Color32(0xA8, 0xDA, 0xEE, 0xFF);
    private static readonly Color IceDeep = new Color32(0x4E, 0x96, 0xB8, 0xFF);
    private static readonly Color Steel = new Color32(0x9A, 0xA8, 0xB4, 0xFF);
    private static readonly Color Bronze = new Color32(0x8A, 0x6A, 0x3E, 0xFF);
    private static readonly Color Gold = new Color32(0xE3, 0xC2, 0x7E, 0xFF);
    private static readonly Color DarkLine = new Color32(0x14, 0x0C, 0x07, 0xFF);
    private static readonly Vector2 Light = new Vector2(-0.6f, -0.8f); // from the top left (y down)

    private static void GenerateBoard()
    {
        Directory.CreateDirectory(BoardFolder);
        WriteColor("MedallionAmber", 192, (x, y) => Medallion(x, y, 192, Amber, AmberDeep, Brass));
        WriteColor("MedallionSteel", 192, (x, y) => Medallion(x, y, 192, Ice, IceDeep, Steel));
        WriteColor("GemRed", 96, (x, y) => Gem(x, y, 96, new Color(0.82f, 0.16f, 0.14f)));
        WriteColor("GemGrey", 96, (x, y) => Gem(x, y, 96, new Color(0.40f, 0.38f, 0.36f)));
        WriteColor("PortraitRing", 192, (x, y) => Ring(x, y, 192, 80, 94, Brass));

        foreach (string row in new[] { "close_combat", "range_combat", "siege_combat" })
        {
            Reframe("Assets/Resources/UnitsRows/" + row + ".png", "Row_" + row, false);
            Reframe("Assets/Resources/UnitsRows/" + row + "_selected.png", "Row_" + row + "_selected", true);
        }
        Reframe("Assets/Resources/UnitsRows/hand_row.png", "Row_hand", false);
        Reframe("Assets/Resources/PowerRows/special.png", "Row_special", false);
        Reframe("Assets/Resources/PowerRows/special_selected.png", "Row_special_selected", true);
    }

    // Score medallion: beveled metal rim around a colored disc (dark numbers go on top)
    private static Color Medallion(float x, float y, int size, Color top, Color bottom, Color rim)
    {
        float c = size / 2f, outer = c - 2, rim_in = outer * 0.8f;
        Vector2 p = new Vector2(x - c, y - c);
        float r = p.magnitude;
        float light = r > 0.001f ? Vector2.Dot(p / r, Light) : 0;
        Color color;
        if (r > rim_in)
        {
            float t = (r - rim_in) / (outer - rim_in);
            float bevel = t < 0.5f ? -light : light; // inner half faces the light from below, outer half from above
            color = Shade(rim, 0.78f + 0.32f * bevel);
        }
        else
        {
            color = Color.Lerp(top, bottom, Mathf.Clamp01((p.y / rim_in + 1) / 2));
            color = Shade(color, 1 - 0.32f * Smooth((r - (rim_in - 14)) / 14)); // inner shadow under the rim
            float glint = Mathf.Exp(-(p.x * p.x / (2 * 900f) + (p.y + rim_in * 0.45f) * (p.y + rim_in * 0.45f) / (2 * 200f)));
            color = Color.Lerp(color, Color.white, glint * 0.22f);
        }
        if (Mathf.Abs(r - rim_in) < 1.5f || r > outer - 2)
            color = Shade(color, 0.45f); // groove and outer edge
        color.a = Mathf.Clamp01(outer + 0.5f - r);
        return color;
    }

    // Faceted octagonal gem lit from the top left
    private static Color Gem(float x, float y, int size, Color base_color)
    {
        float c = size / 2f, apothem = c - 4, table = apothem * 0.48f;
        Vector2 p = new Vector2(x - c, y - c);
        float distance = -1e9f, table_distance = -1e9f;
        int facet = 0;
        for (int k = 0; k < 8; k++)
        {
            float angle = k * Mathf.PI / 4;
            float d = Vector2.Dot(p, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
            if (d - apothem > distance)
            {
                distance = d - apothem;
                facet = k;
            }
            table_distance = Mathf.Max(table_distance, d - table);
        }
        Color color;
        if (table_distance < 0)
            color = Shade(base_color, 1.25f);
        else
        {
            float angle = facet * Mathf.PI / 4;
            color = Shade(base_color, 0.72f + 0.42f * Vector2.Dot(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), Light));
        }
        if (distance > -2.5f || Mathf.Abs(table_distance) < 0.8f)
            color = Shade(color, 0.55f); // outline and table edge
        Vector2 sparkle = p - new Vector2(-table * 0.4f, -table * 0.45f);
        color = Color.Lerp(color, Color.white, Mathf.Exp(-sparkle.sqrMagnitude / 20f) * 0.7f);
        color.a = Mathf.Clamp01(0.5f - distance);
        return color;
    }

    // Beveled metal ring (portrait frame), transparent inside
    private static Color Ring(float x, float y, int size, float inner, float outer, Color metal)
    {
        float c = size / 2f;
        Vector2 p = new Vector2(x - c, y - c);
        float r = p.magnitude;
        float light = r > 0.001f ? Vector2.Dot(p / r, Light) : 0;
        float t = (r - inner) / (outer - inner);
        Color color = Shade(metal, 0.78f + 0.32f * (t < 0.5f ? -light : light));
        if (r < inner + 1.5f || r > outer - 1.5f)
            color = Shade(color, 0.45f);
        color.a = Mathf.Clamp01(Mathf.Min(r - inner + 0.5f, outer + 0.5f - r));
        return color;
    }

    // Row art: the wood is extended over the old border, then a recessed dark frame with a bronze line
    // (or a gold edge and a soft glow for the highlighted version) is drawn
    private static void Reframe(string source, string name, bool highlighted)
    {
        Texture2D input = new Texture2D(2, 2);
        input.LoadImage(File.ReadAllBytes(source));
        int w = input.width, h = input.height;
        Color32[] src = input.GetPixels32();
        UnityEngine.Object.DestroyImmediate(input);
        const int band = 7;
        Color32[] pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(w - 1 - x, h - 1 - y));
                int sx = Mathf.Clamp(x, band, w - 1 - band), sy = Mathf.Clamp(y, band, h - 1 - band);
                Color color = src[sy * w + sx];
                float near = 1 - Smooth((edge - 2) / (highlighted ? 14f : 12f));
                color = highlighted ? Color.Lerp(color, Gold, near * 0.22f) : Shade(color, 1 - 0.38f * near);
                if (edge < 2)
                    color = highlighted ? Shade(Gold, 0.8f) : DarkLine;
                else if (edge < 4)
                    color = Color.Lerp(color, highlighted ? Gold : Bronze, highlighted ? 0.9f : 0.6f);
                color.a = 1;
                pixels[y * w + x] = color;
            }
        Save(name, new Texture2D(w, h, TextureFormat.RGBA32, false), pixels, Vector4.zero, BoardFolder);
    }

    private delegate Color ColorAt(float x, float y); // y measured from the top

    private static void WriteColor(string name, int size, ColorAt color_at)
    {
        Color32[] pixels = new Color32[size * size];
        for (int py = 0; py < size; py++)
            for (int px = 0; px < size; px++)
                pixels[py * size + px] = color_at(px + 0.5f, size - py - 0.5f);
        Save(name, new Texture2D(size, size, TextureFormat.RGBA32, false), pixels, Vector4.zero, BoardFolder);
    }

    private static Color Shade(Color color, float factor)
    {
        return new Color(Mathf.Clamp01(color.r * factor), Mathf.Clamp01(color.g * factor), Mathf.Clamp01(color.b * factor), color.a);
    }

    //----------------------------------------Output----------------------------------------//
    private static void Write(string name, int width, int height, Shape shape, Vector4 border, Alpha fade = null)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[width * height];
        for (int py = 0; py < height; py++)
            for (int px = 0; px < width; px++)
            {
                // Texture rows go bottom-up; the shapes are described top-down
                float x = px + 0.5f, y = height - py - 0.5f;
                float coverage = Mathf.Clamp01(0.5f - shape(x, y));
                if (fade != null)
                    coverage *= fade(x / width, y / height);
                pixels[py * width + px] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(coverage * 255));
            }
        Save(name, texture, pixels, border);
    }

    private static void WriteAlpha(string name, int width, int height, Alpha alpha)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[width * height];
        for (int py = 0; py < height; py++)
            for (int px = 0; px < width; px++)
            {
                float u = (px + 0.5f) / width, v = (height - py - 0.5f) / height;
                pixels[py * width + px] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(u, v)) * 255));
            }
        Save(name, texture, pixels, Vector4.zero);
    }

    private static void Save(string name, Texture2D texture, Color32[] pixels, Vector4 border, string folder = SpriteFolder)
    {
        texture.SetPixels32(pixels);
        texture.Apply();
        string path = folder + "/" + name + ".png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        Borders[path] = border;
    }

    private static readonly System.Collections.Generic.Dictionary<string, Vector4> Borders = new System.Collections.Generic.Dictionary<string, Vector4>();

    private static void ConfigureImporter(string path, int pixels_per_unit)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixels_per_unit;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        Vector4 border;
        if (Borders.TryGetValue(path, out border))
            importer.spriteBorder = border;
        importer.SaveAndReimport();
    }

    //----------------------------------------Theme----------------------------------------//
    private static void WireTheme()
    {
        UITheme theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
        if (theme == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ThemePath));
            theme = ScriptableObject.CreateInstance<UITheme>();
            AssetDatabase.CreateAsset(theme, ThemePath);
        }

        theme.headingFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Cinzel SDF.asset");
        theme.bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/AlegreyaSans-Regular SDF.asset");
        theme.bodyMediumFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/AlegreyaSans-Medium SDF.asset");

        Func<string, Sprite> sprite = name => AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + "/" + name + ".png");
        theme.panelSprite = sprite("Panel");
        theme.panelOutline = sprite("PanelOutline");
        theme.pill = sprite("Pill");
        theme.pillOutline = sprite("PillOutline");
        theme.circle = sprite("Circle");
        theme.ring = sprite("Ring");
        theme.diamond = sprite("Diamond");
        theme.gradientH = sprite("GradientH");
        theme.gradientV = sprite("GradientV");
        theme.radial = sprite("Radial");
        theme.rule = sprite("Rule");
        theme.chevronDown = sprite("ChevronDown");
        theme.chevronLeft = sprite("ChevronLeft");
        theme.chevronRight = sprite("ChevronRight");
        theme.iconMenu = sprite("IconMenu");
        theme.iconClose = sprite("IconClose");
        theme.iconPlus = sprite("IconPlus");
        theme.iconCheck = sprite("IconCheck");
        theme.iconBack = sprite("IconBack");
        theme.iconTrash = sprite("IconTrash");
        theme.iconEdit = sprite("IconEdit");
        EditorUtility.SetDirty(theme);
        AssetDatabase.SaveAssets();
    }
}

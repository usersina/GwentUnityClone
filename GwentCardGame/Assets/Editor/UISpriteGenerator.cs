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

        AssetDatabase.Refresh();
        foreach (string file in Directory.GetFiles(SpriteFolder, "*.png"))
            ConfigureImporter(file.Replace('\\', '/'));
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

    private static void Save(string name, Texture2D texture, Color32[] pixels, Vector4 border)
    {
        texture.SetPixels32(pixels);
        texture.Apply();
        string path = SpriteFolder + "/" + name + ".png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        Borders[path] = border;
    }

    private static readonly System.Collections.Generic.Dictionary<string, Vector4> Borders = new System.Collections.Generic.Dictionary<string, Vector4>();

    private static void ConfigureImporter(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
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

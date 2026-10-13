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
            foreach (string file in Directory.GetFiles(folder))
                if (file.EndsWith(".png") || file.EndsWith(".jpg"))
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
    // The board keeps the original Witcher 3 layout; these match its look: pale score discs with a thin silver rim
    // (cream for the player, frost blue for the opponent, white totals), a silver portrait frame with a point at
    // the bottom, rows recessed into the table, a warm golden hand tray and soft card shadows.
    private static readonly Color Cream = new Color32(0xF4, 0xEA, 0xD5, 0xFF);
    private static readonly Color CreamDeep = new Color32(0xD3, 0xBD, 0x96, 0xFF);
    private static readonly Color Frost = new Color32(0xE8, 0xF1, 0xF6, 0xFF);
    private static readonly Color FrostDeep = new Color32(0xA9, 0xC5, 0xD5, 0xFF);
    private static readonly Color Chalk = new Color32(0xF7, 0xF5, 0xF0, 0xFF);
    private static readonly Color ChalkDeep = new Color32(0xCE, 0xCA, 0xC1, 0xFF);
    private static readonly Color Silver = new Color32(0xB6, 0xBA, 0xBE, 0xFF);
    private static readonly Color Bronze = new Color32(0x8A, 0x6A, 0x3E, 0xFF);
    private static readonly Color TrayGold = new Color32(0xC0, 0x8E, 0x4C, 0xFF);
    private static readonly Color Gold = new Color32(0xE3, 0xC2, 0x7E, 0xFF);
    private static readonly Color DarkLine = new Color32(0x14, 0x0C, 0x07, 0xFF);
    private static readonly Vector2 Light = new Vector2(-0.6f, -0.8f); // from the top left (y down)

    private static void GenerateBoard()
    {
        Directory.CreateDirectory(BoardFolder);
        WriteColor("ScorePlayer", 192, 192, (x, y) => Medallion(x, y, 192, Cream, CreamDeep, Silver));
        WriteColor("ScoreOpponent", 192, 192, (x, y) => Medallion(x, y, 192, Frost, FrostDeep, Silver));
        WriteColor("ScoreTotal", 192, 192, (x, y) => Medallion(x, y, 192, Chalk, ChalkDeep, Silver));
        WriteColor("GemRed", 96, 96, (x, y) => Gem(x, y, 96, new Color(0.82f, 0.16f, 0.14f)));
        WriteColor("GemGrey", 96, 96, (x, y) => Gem(x, y, 96, new Color(0.40f, 0.38f, 0.36f)));
        WriteColor("PortraitFrame", 192, 212, (x, y) => PortraitFrame(x, y, Silver));
        WriteColor("AbilityButton", 192, 192, AbilityButton);
        WriteColorSliced("CardSlot", 96, 96, CardSlot, new Vector4(24, 24, 24, 24));
        WriteAlpha("SoftShadow", 64, 64, (u, v) => 1 - Smooth((RoundedBox(u * 64, v * 64, 64, 64, 14, 16) + 10) / 22), new Vector4(32, 32, 32, 32), BoardFolder);

        foreach (string row in new[] { "close_combat", "range_combat", "siege_combat" })
        {
            Reframe("Assets/Resources/UnitsRows/" + row + ".png", "Row_" + row, RowStyle.Recessed);
            Reframe("Assets/Resources/UnitsRows/" + row + "_selected.png", "Row_" + row + "_selected", RowStyle.Highlighted);
        }
        Reframe("Assets/Resources/UnitsRows/hand_row.png", "Row_hand", RowStyle.Tray);
        Reframe("Assets/Resources/PowerRows/special.png", "Row_special", RowStyle.Recessed);
        Reframe("Assets/Resources/PowerRows/special_selected.png", "Row_special_selected", RowStyle.Highlighted);

        string sources = BoardFolder + "/Sources";
        if (Directory.Exists(sources))
            foreach (string source in Directory.GetFiles(sources, "*.jpg"))
                Table(source, "Table_" + Path.GetFileNameWithoutExtension(source).Replace("_diff_2k", ""));
    }

    // Table under the board: the middle of a square wood texture cropped to 16:9, darkened so the rows stand
    // out, with a soft vignette
    private static void Table(string source, string name)
    {
        Texture2D input = new Texture2D(2, 2);
        input.LoadImage(File.ReadAllBytes(source));
        const int w = 1920, h = 1080;
        float v_span = (float)h / w * input.width / input.height;
        Color[] pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w, t = (y + 0.5f) / h;
                Color color = input.GetPixelBilinear(u, 0.5f + (t - 0.5f) * v_span);
                float dx = (u - 0.5f) * 2, dy = (t - 0.5f) * 2;
                float vignette = 1 - 0.5f * Smooth((Mathf.Sqrt(dx * dx * 0.8f + dy * dy) - 0.45f) / 0.9f);
                pixels[y * w + x] = Shade(color, 0.85f * vignette);
            }
        Texture2D output = new Texture2D(w, h, TextureFormat.RGB24, false);
        output.SetPixels(pixels);
        output.Apply();
        string path = BoardFolder + "/" + name + ".jpg";
        File.WriteAllBytes(path, output.EncodeToJPG(92));
        Borders[path] = Vector4.zero;
        UnityEngine.Object.DestroyImmediate(input);
        UnityEngine.Object.DestroyImmediate(output);
    }

    // Score disc: thin beveled metal rim around a pale disc (dark numbers go on top), with a soft shadow below it
    private static Color Medallion(float x, float y, int size, Color top, Color bottom, Color rim)
    {
        float c = size / 2f, outer = c - 12, rim_in = outer * 0.86f;
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
        return OverShadow(color, Mathf.Clamp01(outer + 0.5f - r), (new Vector2(x - c - 3, y - c - 5).magnitude - outer) / 8f);
    }

    // The shape over a soft dark shadow; shadow_distance is in shadow widths from the shadow's edge
    private static Color OverShadow(Color color, float coverage, float shadow_distance)
    {
        float shadow = 0.55f * (1 - Smooth((shadow_distance + 1) / 2));
        float alpha = coverage + shadow * (1 - coverage);
        if (alpha <= 0)
            return new Color(0, 0, 0, 0);
        Color result = color * (coverage / alpha);
        result.a = alpha;
        return result;
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

    // Beveled metal portrait ring with a small point at the bottom (the Witcher 3 frame), transparent inside.
    // 192x212 texture: the ring is centered at (96, 96)
    private static Color PortraitFrame(float x, float y, Color metal)
    {
        const float c = 96, inner = 80, outer = 92;
        Vector2 p = new Vector2(x - c, y - c);
        float r = p.magnitude;
        float ring = Mathf.Max(inner - r, r - outer); // < 0 inside the band
        // The point: a downward wedge from the ring's lower edge to its tip at y = 207
        float wedge = Mathf.Max(Mathf.Abs(p.x) - (207 - y) * 0.7f, Mathf.Max(y - 207, c + inner + 4 - y)) * 0.8f;
        float shape = Mathf.Min(ring, wedge);
        float light = r > 0.001f ? Vector2.Dot(p / r, Light) : 0;
        float t = (r - inner) / (outer - inner);
        Color color = wedge < ring ? Shade(metal, 0.8f - 0.25f * Mathf.Sign(p.x) * 0.5f)
                                   : Shade(metal, 0.8f + 0.3f * (t < 0.5f ? -light : light));
        if (shape > -1.5f)
            color = Shade(color, 0.5f); // dark edge
        color.a = Mathf.Clamp01(0.5f - shape);
        return color;
    }

    private enum RowStyle { Recessed, Highlighted, Tray }

    // Row art: the wood is extended over the old border and darkened so the row sits recessed in the table, then
    // a dark frame with a bronze line is drawn (a gold edge and a soft glow when highlighted; a warm golden frame
    // for the hand tray)
    private static void Reframe(string source, string name, RowStyle style)
    {
        bool highlighted = style == RowStyle.Highlighted;
        float tone = style == RowStyle.Recessed ? 0.66f : style == RowStyle.Tray ? 0.74f : 0.9f;
        Color line = style == RowStyle.Tray ? TrayGold : Bronze;
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
                Color color = Shade(src[sy * w + sx], tone);
                color = new Color(color.r, color.g * 0.95f, color.b * 0.9f); // toward the table's warm red-brown
                float near = 1 - Smooth((edge - 2) / (highlighted ? 14f : 12f));
                color = highlighted ? Color.Lerp(color, Gold, near * 0.22f) : Shade(color, 1 - 0.38f * near);
                if (edge < 2)
                    color = highlighted ? Shade(Gold, 0.8f) : DarkLine;
                else if (edge < (style == RowStyle.Tray ? 5 : 4))
                    color = Color.Lerp(color, highlighted ? Gold : line, highlighted ? 0.9f : style == RowStyle.Tray ? 0.85f : 0.6f);
                color.a = 1;
                pixels[y * w + x] = color;
            }
        Save(name, new Texture2D(w, h, TextureFormat.RGBA32, false), pixels, Vector4.zero, BoardFolder);
    }

    // Leader ability button: a beveled gold ring around a dark face with a gold crown, a soft shadow below.
    // Drawn bright: Unity's colour tint dims it for the normal, pressed and disabled states.
    private static Color AbilityButton(float x, float y)
    {
        const float c = 96, outer = 84, ring_in = 70;
        Vector2 p = new Vector2(x - c, y - c);
        float r = p.magnitude;
        float light = r > 0.001f ? Vector2.Dot(p / r, Light) : 0;
        Color color;
        if (r > ring_in)
        {
            float t = (r - ring_in) / (outer - ring_in);
            color = Shade(Gold, 0.8f + 0.3f * (t < 0.5f ? -light : light));
        }
        else
        {
            color = Color.Lerp(new Color32(0x3A, 0x2C, 0x1E, 0xFF), new Color32(0x1C, 0x14, 0x0D, 0xFF), Mathf.Clamp01((p.y / ring_in + 1) / 2));
            color = Shade(color, 1 - 0.35f * Smooth((r - (ring_in - 12)) / 12));
            // The crown, scaled from a 64 px drawing centred in the face
            float crown = Crown((p.x / 1.55f) + 32, (p.y / 1.55f) + 33);
            float crown_coverage = Mathf.Clamp01(0.5f - crown * 1.55f);
            color = Color.Lerp(color, Shade(Gold, 1.05f - 0.25f * Mathf.Clamp01((p.y + 30) / 60)), crown_coverage);
        }
        if (Mathf.Abs(r - ring_in) < 1.5f || r > outer - 2)
            color = Shade(color, 0.5f);
        return OverShadow(color, Mathf.Clamp01(outer + 0.5f - r), (new Vector2(x - c - 3, y - c - 5).magnitude - outer) / 8f);
    }

    // A crown in a 64x64 box (y down): band, three points with balls on top
    private static float Crown(float x, float y)
    {
        float band = RoundedBoxAt(x, y, 32, 46, 19, 4, 1.5f);
        float body = Polygon(x, y, new[] { new Vector2(13, 44), new Vector2(13, 22), new Vector2(23, 32), new Vector2(32, 15), new Vector2(41, 32), new Vector2(51, 22), new Vector2(51, 44) });
        float balls = Mathf.Min(Circle(x, y, 13, 19, 3.2f), Mathf.Min(Circle(x, y, 32, 12, 3.6f), Circle(x, y, 51, 19, 3.2f)));
        return Mathf.Min(band, Mathf.Min(body, balls));
    }

    // Card slot: a dark recess with an inner shadow along the top and left, and a thin bronze edge
    private static Color CardSlot(float x, float y)
    {
        const float w = 96, h = 96;
        float d = RoundedBox(x, y, w, h, 10, 1);
        float depth = -d; // distance inside the edge
        Color color = new Color(0.05f, 0.03f, 0.02f, 0.62f);
        // Inner shadow, stronger on the top and left (light from the top left)
        float top_left = 1 - Smooth(Mathf.Min(x - 1, y - 1) / 14f);
        color.a = Mathf.Clamp01(color.a + 0.25f * (1 - Smooth(depth / 10f)) + 0.15f * top_left);
        if (depth < 2.5f)
        {
            float bevel = Mathf.Clamp01((x / w + y / h) / 2); // lighter on the bottom right edge
            color = Color.Lerp(Shade(Bronze, 0.6f), Shade(Bronze, 1.2f), bevel);
            color.a = 0.9f;
        }
        color.a *= Mathf.Clamp01(0.5f - d);
        return color;
    }

    private static float RoundedBoxAt(float x, float y, float cx, float cy, float half_width, float half_height, float radius)
    {
        float qx = Mathf.Abs(x - cx) - (half_width - radius);
        float qy = Mathf.Abs(y - cy) - (half_height - radius);
        return new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - radius;
    }

    // Signed distance to a closed polygon (negative inside)
    private static float Polygon(float x, float y, Vector2[] points)
    {
        Vector2 p = new Vector2(x, y);
        float distance = float.MaxValue;
        bool inside = false;
        for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
        {
            Vector2 a = points[j], b = points[i];
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            distance = Mathf.Min(distance, (p - (a + ab * t)).magnitude);
            if ((a.y > p.y) != (b.y > p.y) && p.x < a.x + (p.y - a.y) / (b.y - a.y) * (b.x - a.x))
                inside = !inside;
        }
        return inside ? -distance : distance;
    }

    private static void WriteColorSliced(string name, int width, int height, ColorAt color_at, Vector4 border)
    {
        Color32[] pixels = new Color32[width * height];
        for (int py = 0; py < height; py++)
            for (int px = 0; px < width; px++)
                pixels[py * width + px] = color_at(px + 0.5f, height - py - 0.5f);
        Save(name, new Texture2D(width, height, TextureFormat.RGBA32, false), pixels, border, BoardFolder);
    }

    private delegate Color ColorAt(float x, float y); // y measured from the top

    private static void WriteColor(string name, int width, int height, ColorAt color_at)
    {
        Color32[] pixels = new Color32[width * height];
        for (int py = 0; py < height; py++)
            for (int px = 0; px < width; px++)
                pixels[py * width + px] = color_at(px + 0.5f, height - py - 0.5f);
        Save(name, new Texture2D(width, height, TextureFormat.RGBA32, false), pixels, Vector4.zero, BoardFolder);
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

    private static void WriteAlpha(string name, int width, int height, Alpha alpha, Vector4 border = default(Vector4), string folder = SpriteFolder)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[width * height];
        for (int py = 0; py < height; py++)
            for (int px = 0; px < width; px++)
            {
                float u = (px + 0.5f) / width, v = (height - py - 0.5f) / height;
                pixels[py * width + px] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(u, v)) * 255));
            }
        Save(name, texture, pixels, border, folder);
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
        // Small shapes stay exact; the big table photos are compressed like any texture
        importer.textureCompression = path.EndsWith(".jpg") ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Uncompressed;
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
        theme.softShadow = AssetDatabase.LoadAssetAtPath<Sprite>(BoardFolder + "/SoftShadow.png");
        EditorUtility.SetDirty(theme);
        AssetDatabase.SaveAssets();
    }
}

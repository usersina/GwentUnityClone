using System;
using System.IO;
using System.Linq;
using Gwent.Rules;
using Gwent.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Gwent.EditorTools
{
    // Draws the UI's simple shapes (rounded panels, pills, circles, gradients, chevrons, icons) as anti-aliased PNGs
    // and wires them into the UI theme (Resources/UITheme.asset) together with the fonts.
    // Shapes are drawn at twice their on-screen size and imported at 200 pixels per unit, so they stay sharp on 4K screens.
    public static class UISpriteGenerator
    {
        public const string SpriteFolder = "Assets/UI/Sprites";
        public const string BoardFolder = "Assets/UI/Board";
        public const string CursorFolder = "Assets/UI/Cursors";
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
            // A pill: fully rounded ends
            Write("Pill", 64, 32, (x, y) => RoundedBox(x, y, 64, 32, 16), new Vector4(16, 16, 16, 16));
            Write("Circle", 64, 64, (x, y) => Circle(x, y, 32, 32, 31), Vector4.zero);
            Write("Ring", 64, 64, (x, y) => Mathf.Abs(Circle(x, y, 32, 32, 28.5f)) - 2.5f, Vector4.zero);
            Write("Diamond", 24, 24, (x, y) => (Mathf.Abs(x - 12) + Mathf.Abs(y - 12) - 11) * 0.7071f, Vector4.zero);

            // Gradients (white, tinted by the Image color): horizontal fades out to the right, vertical is opaque at the top
            // and fades out downwards
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
            TalkIcons();

            GenerateBoard();
            GenerateCursors();

            AssetDatabase.Refresh();
            foreach (string file in Directory.GetFiles(CursorFolder, "*.png"))
                ConfigureCursorImporter(file.Replace('\\', '/'));
            foreach (string folder in new[] { SpriteFolder, BoardFolder })
                foreach (string file in Directory.GetFiles(folder))
                    if (file.EndsWith(".png") || file.EndsWith(".jpg"))
                        ConfigureImporter(file.Replace('\\', '/'), folder == BoardFolder ? 100 : PixelsPerUnit);
            AssetDatabase.Refresh();
            WireTheme();
            Debug.Log("UI sprites and theme regenerated in " + SpriteFolder);
        }

        // The LAN talk window's mute: a speaker sounding (two waves), and silenced (a cross where the waves were)
        internal static void TalkIcons()
        {
            Write("IconSpeaker", 48, 48, (x, y) => Mathf.Min(Speaker(x, y), Mathf.Min(Arc(x, y, 21, 24, 8, 45), Arc(x, y, 21, 24, 14, 45))) - 2f, Vector4.zero);
            Write("IconMuted", 48, 48, (x, y) => Mathf.Min(Speaker(x, y), Mathf.Min(Segment(x, y, 28, 19, 38, 29), Segment(x, y, 38, 19, 28, 29))) - 2f, Vector4.zero);
        }

        // A speaker's outline: its box, and the cone opening to the right
        private static float Speaker(float x, float y)
        {
            float box = Mathf.Min(Mathf.Min(Segment(x, y, 8, 19, 14, 19), Segment(x, y, 8, 29, 14, 29)), Segment(x, y, 8, 19, 8, 29));
            float cone = Mathf.Min(Mathf.Min(Segment(x, y, 14, 19, 21, 12), Segment(x, y, 21, 12, 21, 36)), Segment(x, y, 21, 36, 14, 29));
            return Mathf.Min(box, cone);
        }

        // Distance to an arc of that radius, opening to the right, that many degrees above and below the middle
        private static float Arc(float x, float y, float cx, float cy, float radius, float degrees)
        {
            Vector2 p = new Vector2(x - cx, y - cy);
            float angle = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
            if (Mathf.Abs(angle) <= degrees)
                return Mathf.Abs(p.magnitude - radius);
            float end = degrees * Mathf.Deg2Rad;
            Vector2 a = new Vector2(Mathf.Cos(end), Mathf.Sin(end)) * radius, b = new Vector2(a.x, -a.y);
            return Mathf.Min((p - a).magnitude, (p - b).magnitude);
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
            // GemRed, GemGrey, ScorePlayer and ScoreOpponent are The Witcher 3's own (see Resources/W3/CREDITS.md)
            WriteColor("ScoreTotal", 192, 192, (x, y) => Medallion(x, y, 192, Chalk, ChalkDeep, Silver));
            WriteColor("PortraitFrame", 192, 212, (x, y) => PortraitFrame(x, y, Silver));
            WriteColor("AbilityButton", 192, 192, AbilityButton);
            WriteColor("AbilityButtonReady", 192, 192, AbilityButtonReady);
            WriteColorSliced("CardSlot", 96, 96, CardSlot, new Vector4(24, 24, 24, 24));
            WriteAlpha("SoftShadow", 64, 64, (u, v) => 1 - Smooth((RoundedBox(u * 64, v * 64, 64, 64, 14, 16) + 10) / 22), new Vector4(32, 32, 32, 32), BoardFolder);
            // A glow around a rectangle, clear inside: brightest on the edge of what it frames, fading out over 28 px.
            // Nine-sliced, drawn 32 px larger on every side than its target, so it lights the edge and never veils the art.
            WriteAlpha("GlowRing", 96, 96, (u, v) =>
            {
                float d = RoundedBox(u * 96, v * 96, 96, 96, 3, 32);
                return d >= 0 ? Mathf.Pow(1 - Mathf.Clamp01(d / 28), 2) : 1 - Smooth(-d / 3);
            }, new Vector4(32, 32, 32, 32), BoardFolder);

            foreach (string row in new[] { "close_combat", "range_combat", "siege_combat" })
            {
                Reframe("Assets/Resources/UnitsRows/" + row + ".png", "Row_" + row, RowStyle.Recessed, "RowEmblem_" + row);
                Reframe("Assets/Resources/UnitsRows/" + row + "_selected.png", "Row_" + row + "_selected", RowStyle.Highlighted, "RowEmblem_" + row + "_selected");
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
        // for the hand tray). A unit row's emblem is written on its own (see TakeOutEmblem).
        private static void Reframe(string source, string name, RowStyle style, string emblem = null)
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
            if (emblem != null)
                TakeOutEmblem(pixels, w, h, emblem);
            Save(name, new Texture2D(w, h, TextureFormat.RGBA32, false), pixels, Vector4.zero, BoardFolder);
        }

        // The engraved emblem in the middle of a unit row (a circle around a sword, a bow or a catapult) is written on its
        // own, to lay over an empty row, and the row gets plain wood in its place: the same line of wood further along the
        // grain, its tone matched to the wood on both sides. Cards never show the circle around them.
        private const float EmblemRadius = 58, EmblemFeather = 4;

        private static void TakeOutEmblem(Color32[] pixels, int w, int h, string emblem)
        {
            int size = Mathf.CeilToInt(2 * (EmblemRadius + EmblemFeather));
            int left = (w - size) / 2, bottom = (h - size) / 2;
            const int sample = 6;
            int along = size + 8;
            Color32[] source = (Color32[])pixels.Clone();
            Func<int, int, Color> mean = (from, row) =>
            {
                Color sum = Color.clear;
                for (int x = from; x < from + sample; x++)
                    sum += source[row * w + x];
                return sum / sample;
            };
            Color32[] cut = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                int row = bottom + y;
                // How much lighter or darker the borrowed line is than the wood it continues, at each end
                Color before = mean(left - sample, row) - mean(left - sample + along, row);
                Color after = mean(left + size, row) - mean(left + size + along, row);
                for (int x = 0; x < size; x++)
                {
                    int at = row * w + left + x;
                    float r = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f).magnitude;
                    float inside = 1 - Smooth((r - EmblemRadius) / EmblemFeather);
                    Color original = source[at];
                    Color wood = (Color)source[at + along] + Color.Lerp(before, after, (float)x / size);
                    wood.a = 1;
                    cut[y * size + x] = new Color32(source[at].r, source[at].g, source[at].b, (byte)Mathf.RoundToInt(inside * 255));
                    pixels[at] = Color.Lerp(original, wood, inside);
                }
            }
            Save(emblem, new Texture2D(size, size, TextureFormat.RGBA32, false), cut, Vector4.zero, BoardFolder);
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

        // The same button lit, once the leader is lifted and the crown uses it: an amber face with a cream crown in a pale
        // gold ring, glowing softly (The Witcher 3 lights it blue at that moment; orange and gold here)
        [MenuItem("Gwent/UI/Regenerate Leader Ready Crown")]
        public static void GenerateLeaderReady()
        {
            Directory.CreateDirectory(BoardFolder);
            WriteColor("AbilityButtonReady", 192, 192, AbilityButtonReady);
            AssetDatabase.Refresh();
            ConfigureImporter(BoardFolder + "/AbilityButtonReady.png", 100);
            WireTheme();
        }

        //----------------------------------------The rules page (How to play)----------------------------------------//
        // Each ability's and special card's icon as the game shows it in its description box (Cards/DescriptionIcon: a white
        // disc with a black glyph), set in a bevelled gold ring over a soft shadow, as The Witcher 3's rule cards show them
        public const string RulesFolder = "Assets/Resources/UI/Rules";
        public static readonly string[] RuleMedallions = { "spy", "medic", "muster", "tight_bond", "morale_boost", "commander_horn", "scorch", "agile",
            "biting_frost", "impenetrable_fog", "torrential_rain", "clear_weather", "decoy", "skellige_storm", "mardroeme", "berserker", "avenger" };

        [MenuItem("Gwent/UI/Regenerate Rules Page Art")]
        public static void GenerateRulesArt()
        {
            Directory.CreateDirectory(RulesFolder);
            foreach (string name in RuleMedallions)
                RuleMedallion(name);
            AssetDatabase.Refresh();
            foreach (string name in RuleMedallions)
                ConfigureImporter(RulesFolder + "/" + name + ".png", 100);
            WireTheme();
        }

        private static void RuleMedallion(string name)
        {
            Texture2D source = new Texture2D(2, 2);
            source.LoadImage(File.ReadAllBytes("Assets/Resources/Cards/DescriptionIcon/" + name + ".png"));
            // The icon's disc: centred in its 64 px, its white 29 px in radius; sampled a little inside its edge so none of
            // its antialiased rim shows
            const float icon_x = 32f, icon_y = 32f, icon_radius = 27.5f;
            const int size = 160;
            const float c = size / 2f, disc = 52, outer = 66;
            Color ring_gold = new Color32(0xD8, 0x96, 0x3C, 0xFF);
            Color32[] pixels = new Color32[size * size];
            for (int py = 0; py < size; py++)
                for (int px = 0; px < size; px++)
                {
                    // Texture rows go bottom-up: p is measured with y up, the source sampled the same way
                    Vector2 p = new Vector2(px + 0.5f - c, py + 0.5f - c);
                    float r = p.magnitude;
                    float light = r > 0.001f ? Vector2.Dot(p / r, new Vector2(-0.6f, 0.8f)) : 0; // from the top left
                    Color color;
                    if (r <= disc)
                    {
                        float scale = icon_radius / disc;
                        color = source.GetPixelBilinear((icon_x + p.x * scale) / source.width, (source.height - icon_y + p.y * scale) / source.height);
                        color = Shade(color, 1 - 0.18f * Smooth((r - (disc - 8)) / 8)); // the rim's shadow on the disc
                    }
                    else
                    {
                        float t = (r - disc) / (outer - disc);
                        color = Shade(ring_gold, 0.86f + 0.34f * (t < 0.5f ? -light : light));
                    }
                    if (Mathf.Abs(r - disc) < 1.2f || r > outer - 1.6f)
                        color = Shade(color, 0.42f); // the groove and the outer edge
                    color.a = 1;
                    pixels[py * size + px] = OverShadow(color, Mathf.Clamp01(outer + 0.5f - r), (new Vector2(p.x - 2, p.y + 4).magnitude - outer) / 7f);
                }
            UnityEngine.Object.DestroyImmediate(source);
            Save(name, new Texture2D(size, size, TextureFormat.RGBA32, false), pixels, Vector4.zero, RulesFolder);
        }

        private static Color AbilityButtonReady(float x, float y)
        {
            const float c = 96, outer = 82, ring_in = 72;
            Vector2 p = new Vector2(x - c, y - c);
            float r = p.magnitude;
            float light = r > 0.001f ? Vector2.Dot(p / r, Light) : 0;
            Color color;
            if (r > ring_in)
                color = Color.Lerp(new Color32(0xF0, 0xCB, 0x7A, 0xFF), new Color32(0xFF, 0xF3, 0xD2, 0xFF), 0.5f + 0.4f * light);
            else
            {
                // Lighter at the top, as if lit from above: yellow to deep orange
                color = Color.Lerp(new Color32(0xF6, 0xBE, 0x3E, 0xFF), new Color32(0xC8, 0x5A, 0x14, 0xFF), Mathf.Clamp01((p.y / ring_in + 1) / 2));
                float crown = Crown((p.x / 1.55f) + 32, (p.y / 1.55f) + 33);
                color = Color.Lerp(color, new Color32(0xFF, 0xF8, 0xE8, 0xFF), Mathf.Clamp01(0.5f - crown * 1.55f));
            }
            if (Mathf.Abs(r - ring_in) < 1.2f)
                color = Shade(color, 0.75f);
            float coverage = Mathf.Clamp01(outer + 0.5f - r);
            // The glow around it: a golden halo fading out to the texture's edge
            float glow = Mathf.Clamp01(1 - (r - outer) / (c - outer)) * 0.65f;
            Color halo = new Color(1f, 0.78f, 0.36f, glow * glow);
            return coverage >= 1 ? color : Color.Lerp(halo, color, coverage);
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

        //----------------------------------------Cursors----------------------------------------//
        // Witcher-style pointers, 64 px: a silver arrow, a gold one over things that react to a click, and the silver
        // one with a red "no" badge where a selected card cannot go. The tip is GameCursor's hotspot.
        private static readonly Vector2[] Arrow =
        {
            new Vector2(5, 4), new Vector2(5, 36), new Vector2(12.5f, 29), new Vector2(18, 41),
            new Vector2(23.5f, 38.5f), new Vector2(18, 27), new Vector2(28, 27),
        };
        private static readonly Color SilverLight = new Color32(0xEE, 0xF0, 0xF2, 0xFF);
        private static readonly Color SilverDark = new Color32(0x7A, 0x82, 0x8A, 0xFF);
        private static readonly Color GoldLight = new Color32(0xF6, 0xE0, 0xA6, 0xFF);
        private static readonly Color GoldDark = new Color32(0xA8, 0x7C, 0x36, 0xFF);

        private static void GenerateCursors()
        {
            Directory.CreateDirectory(CursorFolder);
            WriteCursor("CursorDefault", (x, y) => CursorArrow(x, y, SilverLight, SilverDark, false));
            WriteCursor("CursorInteract", (x, y) => CursorArrow(x, y, GoldLight, GoldDark, false));
            WriteCursor("CursorInvalid", (x, y) => CursorArrow(x, y, SilverLight, SilverDark, true));
            WriteCursor("CursorGiveUp", CursorHand);
        }

        // A gold hand laid flat, seen from its back: four fingers up, the thumb out to the left. Over the player's own deck in
        // their turn: laying a hand on the deck gives up the battle (Yu-Gi-Oh's surrender). Its hotspot is the palm
        // (GameCursor.HandHotspot).
        private static readonly Vector2[] Palm =
        {
            new Vector2(16.5f, 31), new Vector2(43.5f, 31), new Vector2(45, 45), new Vector2(40, 55), new Vector2(22, 55), new Vector2(16, 46),
        };

        private static Color CursorHand(float x, float y)
        {
            // Each finger a capsule, a hair apart, so the outline runs between them
            float index = Segment(x, y, 20.5f, 33, 20.5f, 13.5f) - 3.35f;
            float middle = Segment(x, y, 27.3f, 33, 27.3f, 9.5f) - 3.35f;
            float ring = Segment(x, y, 34.1f, 33, 34.1f, 11.5f) - 3.35f;
            float little = Segment(x, y, 40.7f, 34, 41.3f, 17.5f) - 3.1f;
            float thumb = Segment(x, y, 19, 45, 9.5f, 33.5f) - 3.4f;
            float d = Mathf.Min(Polygon(x, y, Palm), Mathf.Min(Mathf.Min(index, middle), Mathf.Min(Mathf.Min(ring, little), thumb)));
            // Gold metal as the gold arrow: lighter towards the top left, a bright bevel inside the edges
            float t = Mathf.Clamp01((x - 10) / 36f * 0.4f + (y - 10) / 45f * 0.6f);
            Color color = Color.Lerp(GoldLight, GoldDark, t);
            if (d > -2.5f && d < -0.8f)
                color = Color.Lerp(color, Color.white, 0.3f);
            if (d > -1.2f)
                color = new Color(0.08f, 0.06f, 0.05f, 1); // dark outline
            // The knuckles' line across the top of the palm
            if (Mathf.Abs(y - 33.5f) < 0.6f && x > 18 && x < 43 && d < -1.5f)
                color = Shade(color, 0.7f);
            float coverage = Mathf.Clamp01(0.5f - d);
            float shadow = Mathf.Min(Polygon(x - 2, y - 3, Palm), Segment(x - 2, y - 3, 27.3f, 33, 27.3f, 9.5f) - 9);
            return OverShadow(color, coverage, (shadow + 1) / 3f);
        }

        private static Color CursorArrow(float x, float y, Color light, Color dark, bool invalid)
        {
            float d = Polygon(x, y, Arrow);
            // Metal: lighter towards the top left, a bright bevel along the edges that face the light
            float t = Mathf.Clamp01((x - 5) / 30f * 0.4f + (y - 4) / 38f * 0.6f);
            Color color = Color.Lerp(light, dark, t);
            if (d > -2.5f && d < -0.8f)
                color = Color.Lerp(color, Color.white, 0.35f);
            if (d > -1.2f)
                color = new Color(0.08f, 0.06f, 0.05f, 1); // dark outline
            float coverage = Mathf.Clamp01(0.5f - d);

            if (invalid)
            {
                // Red "no" badge at the bottom right of the arrow
                float ring = Mathf.Abs(Circle(x, y, 38, 40, 8.5f)) - 2.2f;
                Vector2 a = new Vector2(32.5f, 46), b = new Vector2(43.5f, 34);
                float slash = Segment(x, y, a.x, a.y, b.x, b.y) - 2.2f;
                float disc = Circle(x, y, 38, 40, 11);
                float badge = Mathf.Min(ring, slash);
                if (disc < 0.5f)
                {
                    Color under = Color.Lerp(color, new Color(0.08f, 0.06f, 0.05f, 1), Mathf.Clamp01(0.5f - disc));
                    color = Color.Lerp(under, new Color32(0xD8, 0x3A, 0x30, 0xFF), Mathf.Clamp01(0.5f - badge));
                    coverage = Mathf.Max(coverage, Mathf.Clamp01(0.5f - disc));
                }
            }
            return OverShadow(color, coverage, (Polygon(x - 2, y - 3, Arrow) + 1) / 3f);
        }

        private static void WriteCursor(string name, ColorAt color_at)
        {
            const int size = 64;
            Color32[] pixels = new Color32[size * size];
            for (int py = 0; py < size; py++)
                for (int px = 0; px < size; px++)
                    pixels[py * size + px] = color_at(px + 0.5f, size - py - 0.5f);
            Save(name, new Texture2D(size, size, TextureFormat.RGBA32, false), pixels, Vector4.zero, CursorFolder);
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

        // Hardware cursors: the Cursor texture type (readable, uncompressed, no mipmaps)
        private static void ConfigureCursorImporter(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return;
            importer.textureType = TextureImporterType.Cursor;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.isReadable = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

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
            theme.headingShadow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Fonts/Cinzel SDF - Shadow.mat");

            Func<string, Sprite> sprite = name => AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + "/" + name + ".png");
            theme.panelSprite = sprite("Panel");
            theme.panelOutline = sprite("PanelOutline");
            theme.pill = sprite("Pill");
            theme.circle = sprite("Circle");
            theme.ring = sprite("Ring");
            theme.diamond = sprite("Diamond");
            theme.gradientH = sprite("GradientH");
            theme.gradientV = sprite("GradientV");
            theme.radial = sprite("Radial");
            theme.rule = sprite("Rule");
            theme.chevronLeft = sprite("ChevronLeft");
            theme.chevronRight = sprite("ChevronRight");
            theme.iconSpeaker = sprite("IconSpeaker");
            theme.iconMuted = sprite("IconMuted");
            theme.softShadow = AssetDatabase.LoadAssetAtPath<Sprite>(BoardFolder + "/SoftShadow.png");
            theme.glowRing = AssetDatabase.LoadAssetAtPath<Sprite>(BoardFolder + "/GlowRing.png");
            theme.leaderReady = AssetDatabase.LoadAssetAtPath<Sprite>(BoardFolder + "/AbilityButtonReady.png");
            theme.rowEmblems = new[] { "close_combat", "range_combat", "siege_combat" }.Select(row => AssetDatabase.LoadAssetAtPath<Sprite>(BoardFolder + "/RowEmblem_" + row + ".png")).ToArray();
            theme.gem = AssetDatabase.LoadAssetAtPath<Sprite>(BoardFolder + "/GemRed.png");
            theme.scoreDisc = AssetDatabase.LoadAssetAtPath<Sprite>(BoardFolder + "/ScorePlayer.png");
            theme.scoreTotal = AssetDatabase.LoadAssetAtPath<Sprite>(BoardFolder + "/ScoreTotal.png");
            theme.cursorDefault = AssetDatabase.LoadAssetAtPath<Texture2D>(CursorFolder + "/CursorDefault.png");
            theme.cursorInteract = AssetDatabase.LoadAssetAtPath<Texture2D>(CursorFolder + "/CursorInteract.png");
            theme.cursorInvalid = AssetDatabase.LoadAssetAtPath<Texture2D>(CursorFolder + "/CursorInvalid.png");
            theme.cursorGiveUp = AssetDatabase.LoadAssetAtPath<Texture2D>(CursorFolder + "/CursorGiveUp.png");
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
        }
    }
}

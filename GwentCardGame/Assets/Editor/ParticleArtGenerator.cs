using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Gwent.EditorTools
{
    // The particle effects' art (ParticleFx), drawn from noise, and its materials, in Resources/W3/Particles: Spark (a hot
    // point in a soft glow), Smoke (four billowing puffs, lit from the top left, blended over), Blaze (the same puffs,
    // added on: churning fire, flashes), Fire (four flame tongues, added on), Mist (four soft, wide clouds of fog, near
    // white, blended over), Streak (a falling raindrop's streak, drawn tall and thin, blended over), Glint (a four-pointed
    // sparkle, added on), Flake (a soft snowflake, blended over) and Biting Frost's: Shard (four needles of ice), Snow
    // (four banks of it), Rime (four frosted shrubs), Band (the cold's glow, fading upwards) and Frost (a veil of frost
    // grain and flakes, fading upwards), all blended over and standing on their sheet cell's middle
    // (its lower half the floor below them, so that a particle grows and leans about its foot). The materials use UI shaders (ParticleEffectForUGUI's UI/Additive, Unity's UI/Default), which also brings
    // UI/Additive into the builds.
    public static class ParticleArtGenerator
    {
        public const string Folder = "Assets/Resources/W3/Particles";

        [MenuItem("Gwent/UI/Regenerate Particle Art")]
        public static void Generate()
        {
            Directory.CreateDirectory(Folder);
            Write("Spark", 64, Spark);
            Write("Smoke", 256, (x, y) => Atlas(x, y, 256, Puff));
            Write("Fire", 256, (x, y) => Atlas(x, y, 256, Tongue));
            Write("Mist", 512, (x, y) => Atlas(x, y, 512, Cloud));
            Write("Streak", 64, Streak);
            Write("Glint", 64, Glint);
            Write("Shard", 32, 256, (x, y) => Sheet(x, y, 16, 128, Needle)); // about the size the needles are drawn: their lattice shows
            Write("Snow", 512, 128, (x, y) => Sheet(x, y, 256, 64, Bank));
            float[] rime = Tufts(256);
            Write("Rime", 512, 512, (x, y) => Frosted(rime, 512, x, y));
            Write("Band", 8, 256, (x, y) => Glow(y / 256));
            Write("Frost", 512, 256, Veil);
            Write("Flake", 32, Flake);
            AssetDatabase.Refresh();
            ConfigureImporter("Spark", TextureImporterCompression.Uncompressed); // smooth gradients: no banding
            ConfigureImporter("Smoke", TextureImporterCompression.CompressedHQ);
            ConfigureImporter("Fire", TextureImporterCompression.CompressedHQ);
            ConfigureImporter("Mist", TextureImporterCompression.CompressedHQ);
            ConfigureImporter("Streak", TextureImporterCompression.Uncompressed); // drawn thin: no blocks
            ConfigureImporter("Glint", TextureImporterCompression.Uncompressed);
            ConfigureImporter("Shard", TextureImporterCompression.Uncompressed); // fine lines: no blocks
            ConfigureImporter("Snow", TextureImporterCompression.CompressedHQ);
            ConfigureImporter("Rime", TextureImporterCompression.CompressedHQ);
            ConfigureImporter("Band", TextureImporterCompression.Uncompressed); // a smooth gradient: no banding
            ConfigureImporter("Frost", TextureImporterCompression.CompressedHQ);
            ConfigureImporter("Flake", TextureImporterCompression.Uncompressed);
            WriteMaterial("Spark", "UI/Additive");
            WriteMaterial("Fire", "UI/Additive");
            WriteMaterial("Smoke", "UI/Default");
            WriteMaterial("Blaze", "UI/Additive", "Smoke");
            WriteMaterial("Mist", "UI/Default");
            WriteMaterial("Streak", "UI/Default");
            WriteMaterial("Glint", "UI/Additive");
            WriteMaterial("Shard", "UI/Default");
            WriteMaterial("Snow", "UI/Default");
            WriteMaterial("Rime", "UI/Default");
            WriteMaterial("Band", "UI/Default");
            WriteMaterial("Frost", "UI/Default");
            WriteMaterial("Flake", "UI/Default");
            AssetDatabase.SaveAssets();
        }

        //----------------------------------------Shapes----------------------------------------//
        // A white-hot point in a soft glow (x, y in pixels)
        private static Color Spark(float x, float y)
        {
            float r = new Vector2(x - 32, y - 32).magnitude;
            float a = Mathf.Exp(-(r / 3.2f) * (r / 3.2f)) + 0.45f * Mathf.Exp(-(r / 11f) * (r / 11f));
            return new Color(1, 1, 1, Mathf.Clamp01(a));
        }

        // A puff of smoke (u, v from 0 to 1 in its cell, v up): a ragged cloud, denser and darker in its folds, lit from
        // the top left
        private static Color Puff(float u, float v, int seed)
        {
            float n = Fbm(u * 3.5f + seed * 17.3f, v * 3.5f + seed * 9.1f);
            float d = new Vector2(u - 0.5f, v - 0.5f).magnitude * 2;
            float shape = Smooth(0.25f, 0.92f, d + (n - 0.5f) * 0.6f);
            float folds = Fbm(u * 7f + seed * 3.7f + 40, v * 7f + seed * 5.3f + 40);
            float alpha = shape * (0.3f + 0.7f * n);
            float light = 0.62f + 0.3f * folds + 0.16f * ((0.5f - u) + (v - 0.5f));
            return new Color(light, light, light, Mathf.Clamp01(alpha));
        }

        // A tongue of flame (v up): a rounded base, tapering and swaying towards its tip, soft-edged, hottest in its core
        private static Color Tongue(float u, float v, int seed)
        {
            float sway = (Fbm(v * 2.5f + seed * 11.7f, seed * 3.3f) - 0.5f) * 0.6f * v;
            float x = (u - 0.5f) * 2 - sway;
            float width = (0.55f * Mathf.Pow(1 - v, 0.6f) + 0.02f) * Mathf.Lerp(0.4f, 1f, Mathf.Sqrt(Mathf.Clamp01(v / 0.3f)));
            float inside = Smooth(width * 0.15f, width, Mathf.Abs(x));
            float detail = Fbm(u * 6f + seed * 7.1f, v * 4f - seed * 2.9f);
            float ends = (1 - Smooth(0f, 0.1f, v)) * Smooth(0.5f, 1f, v);
            float alpha = inside * ends * (0.5f + 0.5f * detail);
            return new Color(1, 1, 1, Mathf.Clamp01(alpha * 1.25f));
        }

        // A cloud of fog (v up): a bank half again as wide as it is tall, dense in its middle, its top billowing in rounded
        // lumps, its folds shaded, near white and lighter on top
        private static Color Cloud(float u, float v, int seed)
        {
            float n = Fbm(u * 4f + seed * 13.1f, v * 4f + seed * 7.7f);
            float dx = (u - 0.5f) / 0.45f, dy = (v - 0.5f) / 0.3f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float billow = (n - 0.5f) * Mathf.Lerp(0.5f, 1.1f, Mathf.SmoothStep(0, 1, v)); // lumpy on top, flatter below
            float shape = Smooth(0.45f, 1f, d + billow);
            float folds = Fbm(u * 8f + seed * 2.3f + 60, v * 8f + seed * 4.1f + 60);
            float alpha = shape * (0.6f + 0.4f * n);
            float light = 0.84f + 0.16f * folds + 0.08f * (v - 0.5f);
            return new Color(light, light, light, Mathf.Clamp01(alpha));
        }

        // A raindrop's streak (x, y in pixels, y up), as the eye sees a falling drop: a line filling the width it is drawn
        // at (drawn thin), soft at its sides, its leading (lower) end brightest, its tail fading out
        private static Color Streak(float x, float y)
        {
            float u = x / 64, v = y / 64;
            float across = Mathf.Exp(-((u - 0.5f) / 0.32f) * ((u - 0.5f) / 0.32f));
            float along = (1 - Smooth(0f, 0.12f, v)) * Smooth(0.55f, 1f, v) * Mathf.Lerp(1f, 0.5f, v);
            return new Color(1, 1, 1, Mathf.Clamp01(across * along * 1.1f));
        }

        // A needle of ice (u across, v up, standing on the cell's middle), as The Witcher 3's frost has them: two thin edges
        // closing to its tip, cross-braced between them by two zigzags of a few wide turns crossing each other (a frosted
        // pylon more than a blade), its lines soft, in a frosty halo; bent a little, each its own way
        private static Color Needle(float u, float v, int seed)
        {
            float t = (v - 0.5f) / 0.5f; // 0 at the foot, 1 at the tip
            if (t < 0 || t > 1)
                return new Color(1, 1, 1, 0);
            float half = 0.4f * Mathf.Pow(1 - t, 0.85f + 0.3f * Hash(seed, 2)) + 0.03f;
            float x = (u - 0.5f - 0.06f * (Hash(seed, 1) - 0.5f) * t * t) / half; // -1 and 1 at the edges
            float line = 0.11f / half;                                          // a line's width, likewise
            float edges = Mathf.Exp(-Sq((Mathf.Abs(x) - 0.88f) / line));
            // The zigzag: from edge to edge, its turns closer together towards the tip
            float s = (1 - Mathf.Pow(1 - t, 1.4f)) * (5 + 2 * Hash(seed, 3));
            float across = (s - Mathf.Floor(s)) * 2 - 1;
            if (Mathf.FloorToInt(s) % 2 == 1)
                across = -across;
            float zigzag = Mathf.Max(Mathf.Exp(-Sq((x - across * 0.88f) / (line * 1.5f))), Mathf.Exp(-Sq((x + across * 0.88f) / (line * 1.5f))));
            float halo = 0.3f * Mathf.Exp(-Sq(x / 1.6f));
            float tip = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.9f, 1, t));
            float frost = 0.8f + 0.4f * Noise(u * 20 + seed * 7, v * 60);
            float alpha = Mathf.Clamp01((Mathf.Max(edges, zigzag * 0.7f) + halo) * tip * frost);
            return new Color(1, 1, 1, alpha);
        }

        // A bank of snow (u along, v up, lying on the cell's middle): low, its top crisp, in drifts and small lumps,
        // thinning to nothing at its ends; its crest bright white over a blue-grey shade, grainy, sparkling here and there;
        // below the floor it is all snow, fading out before the cell's edge (or the sheet's next cell would pick it up)
        private static Color Bank(float u, float v, int seed)
        {
            float t = (v - 0.5f) / 0.5f; // 0 at the floor, 1 at the cell's top
            float ends = Mathf.Pow(Mathf.Sin(Mathf.PI * u), 0.4f);
            float top = ends * (0.3f + 0.5f * Fbm(u * 5 + seed * 5.3f, seed * 2.1f) + 0.12f * (Noise(u * 36 + seed * 3, seed) - 0.5f)
                + 0.07f * (Noise(u * 90 + seed * 7, seed + 3) - 0.5f));
            float inside = Mathf.SmoothStep(0, 1, (top - t) / 0.035f);
            float crest = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(top - 0.28f, top - 0.02f, t));
            float grain = 0.86f + 0.14f * Noise(u * 160 + seed * 11, v * 60);
            float sparkle = Hash((int)(u * 256) + seed * 1000, (int)(v * 64)) > 0.985f ? 0.2f : 0;
            Color shade = Color.Lerp(new Color(0.72f, 0.8f, 0.92f), Color.white, crest) * grain;
            float edge = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-0.6f, -0.9f, t));
            return new Color(Mathf.Min(1, shade.r + sparkle), Mathf.Min(1, shade.g + sparkle), Mathf.Min(1, shade.b + sparkle), Mathf.Clamp01(inside * 0.97f * edge));
        }

        // The cold's glow (v up, from the cell's middle): solid over its lowest quarter, then fading smoothly away
        private static Color Glow(float v)
        {
            float t = (v - 0.5f) / 0.5f;
            return new Color(1, 1, 1, t < 0 ? 1 : 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.25f, 0.9f, t)));
        }

        // Four frosted shrubs (a 2 x 2 sheet, each cell size pixels across), drawn stroke by stroke into an alpha map, as
        // The Witcher 3's frost has them: from a foot on the cell's middle a few faint stems rise and fork into twigs, a
        // wisp or two at their ends, frost gathering on them in soft blotches: a mottled, see-through cloud, wider than it
        // is tall
        private static float[] Tufts(int cell)
        {
            float[] alpha = new float[cell * cell * 4];
            for (int variant = 0; variant < 4; variant++)
            {
                // Drawn in a cell of its own (nothing spills into the others), then laid in the sheet
                float[] shrub = new float[cell * cell];
                System.Random random = new System.Random(variant * 7919 + 17);
                int stems = 5 + random.Next(4);
                for (int i = 0; i < stems; i++)
                {
                    float lean = (float)(random.NextDouble() * 2 - 1);
                    Vector2 foot = new Vector2(cell * (0.5f + lean * 0.06f), cell * 0.5f);
                    Branch(shrub, cell, foot, lean * 0.7f, cell * (0.1f + 0.06f * (float)random.NextDouble()), 1f, 3, random);
                }
                int ox = (variant % 2) * cell, oy = (variant / 2) * cell;
                for (int y = 0; y < cell; y++)
                    for (int x = 0; x < cell; x++)
                        alpha[(oy + y) * cell * 2 + ox + x] = shrub[y * cell + x];
            }
            return alpha;
        }

        // A branch of a shrub from a point (pixels) at an angle (radians from the vertical, clockwise), forking at its end
        // into two or three (depth more times); frost gathering on it in clumps, more towards the shrub's crown
        private static void Branch(float[] alpha, int size, Vector2 at, float angle, float length, float width, int depth,
            System.Random random)
        {
            Vector2 end = at;
            Stroke(alpha, size, at, angle, (float)(random.NextDouble() * 2 - 1) * 0.4f, length, width, depth == 3 ? 0.05f : depth == 2 ? 0.14f : 0.24f, random, false, out end);
            int clumps = 1 + depth;
            for (int c = 0; c < clumps; c++)
            {
                Vector2 near = Vector2.Lerp(at, end, 0.4f + 0.6f * (float)random.NextDouble());
                float spread = 2f + 3f * (3 - depth) + 2.5f * (float)random.NextDouble();
                int specks = 5 + random.Next(6);
                for (int k = 0; k < specks; k++)
                {
                    Vector2 off = new Vector2((float)Gauss(random), (float)Gauss(random) * 0.8f) * spread * 1.2f;
                    Splat(alpha, size, near + off, 0.9f + 1.1f * (float)random.NextDouble(), 0.12f + 0.16f * (float)random.NextDouble());
                }
            }
            if (depth <= 0)
            {
                // Wisps fanning from a twig's end
                for (int w = 1 + random.Next(2); w > 0; w--)
                    Stroke(alpha, size, end, angle + ((float)random.NextDouble() * 2 - 1) * 0.9f, ((float)random.NextDouble() * 2 - 1) * 0.5f,
                        size * (0.02f + 0.03f * (float)random.NextDouble()), 0.5f, 0.24f, random, false, out _);
                return;
            }
            int forks = 2 + random.Next(2);
            for (int f = 0; f < forks; f++)
            {
                float turn = ((float)random.NextDouble() * 2 - 1) * 0.7f + (angle > 0 ? 0.15f : -0.15f);
                Branch(alpha, size, end, angle + turn, length * (0.55f + 0.2f * (float)random.NextDouble()), width * 0.7f, depth - 1, random);
            }
        }

        private static double Gauss(System.Random random)
        {
            return System.Math.Sqrt(-2 * System.Math.Log(1 - random.NextDouble())) * System.Math.Cos(2 * System.Math.PI * random.NextDouble());
        }

        // A stroke from a point (pixels), leaving at an angle (radians from the vertical, clockwise) and turning by bend over
        // its length, thinning from width (pixels) to its tip, dabbed on as it goes, beaded with rime here and there; with
        // twigs, short strokes off it. end: where it ends.
        private static void Stroke(float[] alpha, int size, Vector2 at, float angle, float bend, float length, float width,
            float strength, System.Random random, bool twigs, out Vector2 end)
        {
            int steps = Mathf.CeilToInt(length * 2);
            for (int i = 0; i <= steps; i++)
            {
                float k = i / (float)steps;
                float a = angle + bend * k;
                at += new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * (length / steps);
                float radius = Mathf.Lerp(width, width * 0.35f, k);
                Splat(alpha, size, at, radius, strength * Mathf.Lerp(1, 0.5f, k));
                if (random.NextDouble() < 0.05) // rime beading on it
                    Splat(alpha, size, at, radius * 1.8f, strength * 0.9f);
                if (twigs && random.NextDouble() < 0.06 && k > 0.2f)
                {
                    float side = random.NextDouble() < 0.5 ? -1 : 1;
                    Stroke(alpha, size, at, a + side * (0.6f + 0.6f * (float)random.NextDouble()), side * 0.3f,
                        length * (0.12f + 0.18f * (float)random.NextDouble()), radius * 0.8f, strength * 0.8f, random, false, out _);
                }
            }
            end = at;
        }

        // A soft round dab of alpha (laid over what is there)
        private static void Splat(float[] alpha, int size, Vector2 at, float radius, float strength)
        {
            int reach = Mathf.CeilToInt(radius * 2 + 1);
            for (int y = Mathf.Max(0, (int)at.y - reach); y <= Mathf.Min(size - 1, (int)at.y + reach); y++)
                for (int x = Mathf.Max(0, (int)at.x - reach); x <= Mathf.Min(size - 1, (int)at.x + reach); x++)
                {
                    float d = (new Vector2(x + 0.5f, y + 0.5f) - at).magnitude / Mathf.Max(radius, 0.3f);
                    float dab = strength * Mathf.Exp(-d * d);
                    int index = y * size + x;
                    alpha[index] += dab * (1 - alpha[index]);
                }
        }

        // A frosted shrub's pixel: near white, a little pink, its alpha drawn
        private static Color Frosted(float[] alpha, int size, float x, float y)
        {
            float a = alpha[(int)y * size + (int)x];
            return new Color(0.97f, 0.92f, 0.95f, Mathf.Clamp01(a));
        }

        // A veil of frost (x, y in pixels of 512 x 256, y up, lying on the middle): a fine mottled grain, like frost on
        // glass, thicker in some patches than others, strewn with small faint flakes, thickest at the floor and thinning
        // upwards; seen through
        private static Color Veil(float x, float y)
        {
            float t = (y / 256 - 0.5f) / 0.5f;
            float rise = t < 0 ? 1 : Mathf.Pow(1 - Mathf.Min(t, 1), 1.3f);
            float patches = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.35f, 0.7f, Fbm(x / 70f + 20, y / 30f)));
            float grain = Fbm(x / 9f, y / 6f) * (0.6f + 0.4f * Noise(x / 2.5f + 50, y / 2.5f)) * (0.35f + 0.65f * patches);
            // Flakes: in a grid of 7-pixel cells, one now and then, at its own place in its cell and of its own size
            float flakes = 0;
            int cx = Mathf.FloorToInt(x / 7), cy = Mathf.FloorToInt(y / 7);
            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    int ax = cx + i, ay = cy + j;
                    if (Hash(ax * 3 + 1, ay * 5 + 2) > 0.08f + 0.17f * rise)
                        continue;
                    Vector2 at = new Vector2((ax + Hash(ax, ay * 7)) * 7, (ay + Hash(ax * 11, ay)) * 7);
                    float r = 0.5f + 1f * Hash(ax * 13, ay * 17) * Hash(ax * 19, ay * 23);
                    flakes = Mathf.Max(flakes, Mathf.Exp(-Sq((new Vector2(x, y) - at).magnitude / r)));
                }
            return new Color(1, 1, 1, Mathf.Clamp01(rise * (0.55f * grain + 0.35f * flakes)));
        }

        // A snowflake (x, y in pixels): a soft round light, a little brighter in its middle
        private static Color Flake(float x, float y)
        {
            float r = new Vector2(x - 16, y - 16).magnitude;
            return new Color(1, 1, 1, Mathf.Clamp01(0.85f * Mathf.Exp(-Sq(r / 7f)) + 0.3f * Mathf.Exp(-Sq(r / 2.5f))));
        }

        private static float Sq(float x) { return x * x; }

        // A sparkle (x, y in pixels): a white point, its light spilling in a soft glow and four long thin rays
        private static Color Glint(float x, float y)
        {
            float dx = Mathf.Abs(x - 32), dy = Mathf.Abs(y - 32);
            float core = Mathf.Exp(-(dx * dx + dy * dy) / 6f);
            float glow = 0.35f * Mathf.Exp(-(dx * dx + dy * dy) / 60f);
            float rays = Mathf.Exp(-dx / 1.1f) * Mathf.Exp(-dy / 11f) + Mathf.Exp(-dy / 1.1f) * Mathf.Exp(-dx / 11f);
            return new Color(1, 1, 1, Mathf.Clamp01(core + glow + 0.8f * rays));
        }

        // Four variants in a 2 x 2 sheet (ParticleSystem texture sheet animation picks one per particle)
        private static Color Atlas(float x, float y, int size, Func<float, float, int, Color> shape)
        {
            return Sheet(x, y, size / 2, size / 2, shape);
        }

        // Four variants in a 2 x 2 sheet of cells that size (pixels)
        private static Color Sheet(float x, float y, int cellWidth, int cellHeight, Func<float, float, int, Color> shape)
        {
            int cx = (int)x / cellWidth, cy = (int)y / cellHeight;
            return shape((x - cx * cellWidth) / cellWidth, (y - cy * cellHeight) / cellHeight, cy * 2 + cx + 1);
        }

        //----------------------------------------Noise----------------------------------------//
        private static float Hash(int x, int y)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0x7fffffff) / (float)0x7fffffff;
            }
        }

        private static float Noise(float x, float y)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            float bottom = Mathf.Lerp(Hash(xi, yi), Hash(xi + 1, yi), fx);
            float top = Mathf.Lerp(Hash(xi, yi + 1), Hash(xi + 1, yi + 1), fx);
            return Mathf.Lerp(bottom, top, fy);
        }

        private static float Fbm(float x, float y)
        {
            float sum = 0, amplitude = 0.5f, total = 0;
            for (int octave = 0; octave < 5; octave++)
            {
                sum += amplitude * Noise(x, y);
                total += amplitude;
                x *= 2.03f;
                y *= 2.03f;
                amplitude *= 0.5f;
            }
            return sum / total;
        }

        // 1 at from, 0 at to, smoothly
        private static float Smooth(float from, float to, float t)
        {
            float k = Mathf.Clamp01((t - from) / (to - from));
            return 1 - k * k * (3 - 2 * k);
        }

        //----------------------------------------Files----------------------------------------//
        private static void Write(string name, int size, Func<float, float, Color> pixel)
        {
            Write(name, size, size, pixel);
        }

        private static void Write(string name, int width, int height, Func<float, float, Color> pixel)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = pixel(x + 0.5f, y + 0.5f);
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(Folder + "/" + name + ".png", texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        private static void ConfigureImporter(string name, TextureImporterCompression compression)
        {
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(Folder + "/" + name + ".png");
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true; // particles shrink a long way
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = compression;
            importer.SaveAndReimport();
        }

        // Kept when it exists (its GUID stays), its shader and texture (its own, or another's) set again
        private static void WriteMaterial(string name, string shader, string texture = null)
        {
            string path = Folder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = Shader.Find(shader);
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/" + (texture ?? name) + ".png");
            EditorUtility.SetDirty(material);
        }
    }
}

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Gwent.EditorTools
{
    // The particle effects' art (ParticleFx), drawn from noise, and its materials, in Resources/W3/Particles: Spark (a hot
    // point in a soft glow), Smoke (four billowing puffs, lit from the top left, blended over), Blaze (the same puffs,
    // added on: churning fire, flashes) and Fire (four flame tongues, added on). The materials use UI shaders (ParticleEffectForUGUI's UI/Additive, UI/Default), which also
    // brings UI/Additive into the builds.
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
            AssetDatabase.Refresh();
            ConfigureImporter("Spark", TextureImporterCompression.Uncompressed); // smooth gradients: no banding
            ConfigureImporter("Smoke", TextureImporterCompression.CompressedHQ);
            ConfigureImporter("Fire", TextureImporterCompression.CompressedHQ);
            WriteMaterial("Spark", "UI/Additive");
            WriteMaterial("Fire", "UI/Additive");
            WriteMaterial("Smoke", "UI/Default");
            WriteMaterial("Blaze", "UI/Additive", "Smoke");
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

        // Four variants in a 2 x 2 sheet (ParticleSystem texture sheet animation picks one per particle)
        private static Color Atlas(float x, float y, int size, Func<float, float, int, Color> shape)
        {
            int cell = size / 2;
            int cx = (int)x / cell, cy = (int)y / cell;
            return shape((x - cx * cell) / cell, (y - cy * cell) / cell, cy * 2 + cx + 1);
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
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = pixel(x + 0.5f, y + 0.5f);
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

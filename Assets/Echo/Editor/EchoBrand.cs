using System;
using System.IO;
using Echo.Editor.Art;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Echo.Editor
{
    /// <summary>
    /// The visual identity in one place: palette, Rubik type, materials. White silhouettes and cyan echoes on
    /// a bold, hue-shifting background (game spec art direction), with amber for actions and red for the Nemesis.
    /// </summary>
    public static partial class EchoSceneBuilder
    {
        // ---- palette ----
        static readonly Color Background = new Color(0.16f, 0.45f, 0.50f);
        static readonly Color Ink = new Color(0.06f, 0.16f, 0.19f);
        static readonly Color Accent = new Color(1f, 0.79f, 0.29f);
        static readonly Color GhostCyan = new Color(0.45f, 0.95f, 1f);
        static readonly Color NemesisRed = new Color(1f, 0.33f, 0.33f);
        static readonly Color Soft = new Color(1f, 1f, 1f, 0.82f);
        static readonly Color Card = new Color(0f, 0f, 0f, 0.28f);
        static readonly Color Scrim = new Color(0.02f, 0.05f, 0.07f, 0.5f);
        static readonly Color MenuTint = new Color(0.04f, 0.08f, 0.10f, 1f);
        static readonly Color SecondaryButton = new Color(1f, 1f, 1f, 0.14f);
        // Secondary buttons over the game world (title, death screen): dark glass that reads on any world, where the
        // light menu grey washed out against the floor.
        static readonly Color Glass = new Color(0.03f, 0.07f, 0.09f, 0.62f);
        // Settings rows and other list rows: a step up from the menu background, quieter than a button.
        static readonly Color RowSurface = new Color(1f, 1f, 1f, 0.07f);

        // ---- type ----
        const string FontDir = "Assets/Echo/Content/Fonts";
        static TMP_FontAsset _black, _bold, _medium;
        // Time Run art pass phase 7: the era titles' modern (3026) and extruded (10,000 AD) faces.
        static TMP_FontAsset _modernTitle, _extrudedTitle;
        // Badge icons drawn inline (BadgeSprites): boards and profile lines use them.
        static TMP_SpriteAsset _badges;

        // Static atlas: every glyph the game can show, baked at build time so nothing is generated mid-run.
        const string Glyphs = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~£€×•…–—’“”éàèêçÉ";

        // Retro type: Press Start 2P (an 8-px arcade face) for display text and buttons, Pixelify Sans for
        // everything longer. Rubik stays as the last fallback for any glyph the pixel faces lack.
        static void EnsureFonts()
        {
            TMP_FontAsset rubik = FontAsset("Rubik-Bold", 90, 1024);
            // One Pixelify weight for all body text keeps every line on the same pixel grid.
            TMP_FontAsset pixelify = FontAsset("PixelifySans-Regular", 72, 1024, rubik);
            _black = FontAsset("PressStart2P-Regular", 64, 1024, pixelify, rubik);
            _bold = pixelify;
            _medium = pixelify;
            _modernTitle = FontAsset("Rubik-Medium", 90, 1024, rubik);
            _extrudedTitle = FontAsset("Rubik-Black", 90, 1024, rubik);
        }

        // Pixel faces are sampled so each design pixel spans about 8 atlas pixels, so edges stay square and gaps open.
        static TMP_FontAsset FontAsset(string name, int samplingSize, int atlasSize, params TMP_FontAsset[] fallbacks)
        {
            string assetPath = $"{FontDir}/{name} SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null)
            {
                SetFallbacks(existing, fallbacks);
                return existing;
            }

            var font = AssetDatabase.LoadAssetAtPath<Font>($"{FontDir}/{name}.ttf")
                       ?? throw new InvalidOperationException($"font file missing: {FontDir}/{name}.ttf");
            var asset = TMP_FontAsset.CreateFontAsset(font, samplingSize, 9, GlyphRenderMode.SDFAA, atlasSize, atlasSize, AtlasPopulationMode.Dynamic, false);
            asset.name = $"{name} SDF";
            AssetDatabase.CreateAsset(asset, assetPath);
            asset.TryAddCharacters(Glyphs, out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.Log($"[EchoSceneBuilder] {name} lacks: {missing}");

            asset.atlasTextures[0].name = $"{name} Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            asset.material.name = $"{name} Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            SetFallbacks(asset, fallbacks);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        static void SetFallbacks(TMP_FontAsset asset, TMP_FontAsset[] fallbacks)
        {
            asset.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>(fallbacks);
            EditorUtility.SetDirty(asset);
        }

        // ---- materials ----
        const string GlowShader = "Echo/Sprite Glow";

        static Material GlowMaterial(string name, float intensity)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                EnsureFolder(MaterialDir);
                var shader = Shader.Find(GlowShader) ?? throw new InvalidOperationException($"shader {GlowShader} not found");
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetFloat("_Intensity", intensity);
            EditorUtility.SetDirty(m);
            return m;
        }

        // Art fix phase 2: the Far Future's wet floor (Echo/Sprite Ripple), a touch darker than lit, like the layers it mirrors.
        static Material RippleMaterial()
        {
            const string shaderName = "Echo/Sprite Ripple";
            string path = $"{MaterialDir}/FutureRipple.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                EnsureFolder(MaterialDir);
                var shader = Shader.Find(shaderName) ?? throw new InvalidOperationException($"shader {shaderName} not found");
                m = new Material(shader) { name = "FutureRipple" };
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetFloat("_Intensity", 0.85f);
            m.SetFloat("_Amplitude", 0.0012f);   // in the tile's UV: a 16-unit tile shifts a few pixels, a shimmer, not a smear
            m.SetFloat("_Frequency", 4f);
            m.SetFloat("_Speed", 1.6f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Sprite LoadSprite(string path) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new InvalidOperationException($"sprite missing: {path}");

        static Sprite[] RunnerFrames(string character)
        {
            var frames = new Sprite[PixelRig.RunFrames];
            for (int i = 0; i < frames.Length; i++) frames[i] = LoadSprite(EchoArt.CharacterPath(character, i));
            return frames;
        }

        static Sprite[] GhostFrames(string character)
        {
            var frames = new Sprite[PixelRig.RunFrames];
            for (int i = 0; i < frames.Length; i++) frames[i] = LoadSprite(EchoArt.GhostPath(character, i));
            return frames;
        }
    }
}

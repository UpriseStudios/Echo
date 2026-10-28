using System;
using System.Collections.Generic;
using Echo.Editor.Art;
using Echo.Game.Seasons;
using UnityEditor;
using UnityEngine;

namespace Echo.Editor
{
    /// <summary>
    /// Writes the <see cref="SeasonTheme"/> assets (seasons doc) from the art in Editor/Art/PixelSeasons. Rebuilt on
    /// every scene build, like the cosmetic catalog. Seasons phase 1 has one theme, "test", which turns every override on
    /// with placeholder art; it is never in the schedule, only forced from Settings.
    /// </summary>
    public static class SeasonThemes
    {
        public const string Dir = "Assets/Echo/Content/Seasons/Themes";
        public const string SchedulePath = "Assets/Echo/Content/Seasons/seasons.json";

        public static SeasonTheme[] Build()
        {
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Echo/Content/Seasons", "Themes");
            var themes = new List<SeasonTheme> { Christmas(), NewYear(), Test() };
            foreach (SeasonTheme t in themes) EditorUtility.SetDirty(t);
            return themes.ToArray();
        }

        // Seasons doc: falling snow, snow-capped obstacles, ghosts in Santa hats, snowman graves, a red-nosed Nemesis.
        static SeasonTheme Christmas() => Snowy(Load(SeasonIds.Christmas), "Christmas");

        // New Year inside Christmas: the same snow, plus fireworks, and a show when the Daily Haunt board resets.
        static SeasonTheme NewYear()
        {
            SeasonTheme t = Snowy(Load(SeasonIds.NewYear), "New Year");
            t.Fireworks = true;
            t.FireworkSpark = Load<Sprite>(SeasonArt.SparkPath);
            t.FireworkColours = new[] { Hex(0xFFD24A), Hex(0xFF5A8A), Hex(0x8A6AFF), Hex(0x4AD8FF), Hex(0x8AFF6A) };
            t.FireworksPerMinute = 5f;
            t.ResetShowSeconds = 120f;
            t.ResetShowToast = "HAPPY NEW YEAR!";
            t.Stinger = null;
            return t;
        }

        static SeasonTheme Snowy(SeasonTheme t, string title)
        {
            const string art = SeasonIds.Christmas;
            t.Title = title;
            t.PaletteStops = new[] { Hex(0x5A86B0), Hex(0x3E5A8E), Hex(0x2A3A66) };
            t.PaletteBlend = 0.7f;
            t.FarLayer = Sprite(art, SeasonArt.Far);
            t.NearLayer = Sprite(art, SeasonArt.Near);
            t.Effect = SeasonEffect.Falling;
            t.EffectSprites = new[] { Load<Sprite>(SeasonArt.EffectPath(art, 0)), Load<Sprite>(SeasonArt.EffectPath(art, 1)) };
            t.EffectColours = new Color[0];
            t.EffectCount = 56;
            t.EffectFall = 0.7f;
            t.EffectSway = 0.35f;
            t.EffectParallax = 0.25f;
            t.Fireworks = false;
            t.FireworkSpark = null;
            t.FireworkColours = new Color[0];
            t.ResetShowSeconds = 0f;
            t.ResetShowToast = "";
            t.Spike = Sprite(art, SeasonArt.Spike);
            t.Block = Sprite(art, SeasonArt.Block);
            t.GroundTop = Sprite(art, SeasonArt.GroundTop);
            t.Grave = Sprite(art, SeasonArt.Grave);
            t.GraveInColour = true;
            t.Headstone = Sprite(art, SeasonArt.Headstone);
            t.GhostAccessory = Sprite(art, SeasonArt.GhostAccessory);
            t.NemesisAccessory = Sprite(art, SeasonArt.NemesisAccessory);
            t.DeathLine = "";
            t.DeathLineNemesisOnly = false;
            t.Stinger = Load<AudioClip>(PlaceholderAudio.PathFor(PlaceholderAudio.ChristmasStinger));
            return t;
        }

        static SeasonTheme Test()
        {
            const string id = SeasonIds.Test;
            SeasonTheme t = Load(id);
            t.Title = "Test Season";
            t.PaletteStops = new[] { Hex(0x6A3FA0), Hex(0x2F8C8C), Hex(0xA03F7A) };
            t.PaletteBlend = 0.6f;
            t.FarLayer = Sprite(id, SeasonArt.Far);
            t.NearLayer = Sprite(id, SeasonArt.Near);
            t.Effect = SeasonEffect.Falling;
            t.EffectSprites = new[] { Load<Sprite>(SeasonArt.EffectPath(id, 0)), Load<Sprite>(SeasonArt.EffectPath(id, 1)) };
            t.EffectColours = new[] { Hex(0xFF7AC8), Hex(0xFFE36A), Hex(0x6AE0FF), Hex(0x9CFF6A) };
            t.EffectCount = 40;
            t.EffectFall = 1.2f;
            t.EffectSway = 0.3f;
            t.EffectParallax = 0.3f;
            t.Spike = Sprite(id, SeasonArt.Spike);
            t.Block = Sprite(id, SeasonArt.Block);
            t.GroundTop = Sprite(id, SeasonArt.GroundTop);
            t.Grave = Sprite(id, SeasonArt.Grave);
            t.GraveInColour = true;
            t.Headstone = Sprite(id, SeasonArt.Headstone);
            t.GhostAccessory = Sprite(id, SeasonArt.GhostAccessory);
            t.NemesisAccessory = Sprite(id, SeasonArt.NemesisAccessory);
            t.DeathLine = "Test season: every override is on.";
            t.DeathLineNemesisOnly = false;
            t.Stinger = Load<AudioClip>(PlaceholderAudio.PathFor(PlaceholderAudio.TestSeasonStinger));
            return t;
        }

        static SeasonTheme Load(string id)
        {
            string path = $"{Dir}/{id}.asset";
            var theme = AssetDatabase.LoadAssetAtPath<SeasonTheme>(path);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<SeasonTheme>();
                AssetDatabase.CreateAsset(theme, path);
            }
            theme.Id = id;
            return theme;
        }

        static Sprite Sprite(string season, string part) => Load<Sprite>(SeasonArt.PathFor(season, part));

        static T Load<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException($"missing: {path}");

        static Color Hex(uint rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}

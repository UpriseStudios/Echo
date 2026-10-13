using System.IO;
using UnityEngine;

namespace Echo.Editor.Art
{
    /// <summary>
    /// Generates the art and renders review sheets to TestResults/Art: every character's frames and ghost,
    /// and the scenery tiles, scaled up so single pixels are easy to judge.
    /// Batch: -executeMethod Echo.Editor.Art.ArtPreview.Render -quit
    /// </summary>
    public static class ArtPreview
    {
        static readonly Color32 Bg = new Color32(41, 115, 128, 255);
        static readonly Color32 Floor = new Color32(24, 66, 76, 255);
        static readonly Color32 Cyan = new Color32(115, 242, 255, 255);

        public static void Render()
        {
            EchoArt.Generate();
            string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "TestResults", "Art");
            Directory.CreateDirectory(dir);

            // The base cast, then the packs: one sheet would pass the largest texture size.
            Save(CharacterSheet(PixelCharacters.Base).Scaled(6), Path.Combine(dir, "characters.png"));
            var packed = new System.Collections.Generic.List<PixelCharacter>();
            foreach (PixelPack pack in PixelPacks.All) packed.AddRange(pack.Characters);
            Save(CharacterSheet(packed.ToArray()).Scaled(4), Path.Combine(dir, "packs.png"));

            var world = new PixelCanvas(520, 360);
            Fill(world, Bg);
            Stamp(world, PixelWorld.Mountains(), 4, 120, new Color32(52, 140, 156, 255));
            Stamp(world, PixelWorld.Hills(), 4, 40, new Color32(36, 100, 112, 255));
            for (int x = 0; x < 128; x += 32)
            {
                Stamp(world, PixelWorld.GroundFill(), 4 + x, 0, Floor);
                Stamp(world, PixelWorld.GroundTop(), 4 + x, 30, new Color32(50, 120, 132, 255));
            }
            Stamp(world, PixelWorld.Spike(), 150, 38, new Color32(14, 40, 46, 255));
            Stamp(world, PixelWorld.Block(), 180, 50, new Color32(14, 40, 46, 255));
            Stamp(world, PixelWorld.Grave(), 210, 38, new Color32(120, 190, 200, 255));
            Stamp(world, PixelWorld.GhostIcon(), 240, 38, Cyan);
            Stamp(world, PixelWorld.UiFrame(), 270, 38, new Color32(255, 201, 74, 255));
            Save(world.Scaled(3), Path.Combine(dir, "world.png"));
            File.Copy(Path.Combine(Path.GetDirectoryName(Application.dataPath), EchoArt.IconPath), Path.Combine(dir, "app_icon.png"), true);
        }

        // One row per character, top to bottom: four run frames, the jump, then the ghost run frame and ghost jump.
        static PixelCanvas CharacterSheet(PixelCharacter[] characters)
        {
            const int cell = PixelRig.W + 4, rowH = PixelRig.H + 4;
            int cols = PixelRig.RunFrames + 3;
            var sheet = new PixelCanvas(cols * cell, characters.Length * rowH);
            Fill(sheet, Bg);
            for (int r = 0; r < characters.Length; r++)
            {
                PixelCharacter c = characters[characters.Length - 1 - r];
                int y = r * rowH + 2;
                for (int f = 0; f < PixelRig.RunFrames; f++) Stamp(sheet, PixelRig.Frame(c, f), f * cell + 2, y, null);
                Stamp(sheet, PixelRig.Jump(c), PixelRig.RunFrames * cell + 2, y, null);
                Stamp(sheet, PixelRig.Frame(c, 0).Hollow(EchoArt.GhostFill), (PixelRig.RunFrames + 1) * cell + 2, y, Cyan);
                Stamp(sheet, PixelRig.Jump(c).Hollow(EchoArt.GhostFill), (PixelRig.RunFrames + 2) * cell + 2, y, Cyan);
            }
            return sheet;
        }

        static void Fill(PixelCanvas c, Color32 color) => c.Rect(0, 0, c.Width - 1, c.Height - 1, color);

        static void Stamp(PixelCanvas dst, PixelCanvas src, int ox, int oy, Color32? tint)
        {
            for (int y = 0; y < src.Height; y++)
                for (int x = 0; x < src.Width; x++)
                {
                    Color s = src[x, y];
                    if (s.a <= 0f) continue;
                    if (tint.HasValue) s *= (Color)tint.Value;
                    Color d = dst[x + ox, y + oy];
                    dst[x + ox, y + oy] = Color.Lerp(d, new Color(s.r, s.g, s.b, 1f), s.a);
                }
        }

        static void Save(PixelCanvas c, string path)
        {
            var tex = c.ToTexture();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}

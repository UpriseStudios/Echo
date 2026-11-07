using UnityEditor;
using UnityEngine;

namespace Echo.Editor.Art
{
    /// <summary>
    /// World Time Run doc (art pass phase 6): the textures of the Far Far Future's 3D megacity (Far3DScene), generated like
    /// the rest of the game's art: a facade of dark glass panels with a window grid and its lit windows as an emission map,
    /// a ground of dark plating crossed by glowing lanes, a sky from deep indigo to a magenta horizon, the orbital ring's
    /// light strips, and the soft blob shadow cast on the floor by the runner and the ghosts.
    /// </summary>
    public static class Far3DArt
    {
        public const string Dir = "Assets/Echo/Content/Art/Far3D";
        public static string Facade => Dir + "/facade.png";
        public static string Windows => Dir + "/facade_windows.png";
        public static string Ground => Dir + "/ground.png";
        public static string Sky => Dir + "/sky.png";
        public static string Shadow => Dir + "/shadow.png";

        static Color C(uint rgb, float a = 1f) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);
        static float Noise(int x, int y) => (((uint)(x * 73856093) ^ (uint)(y * 19349663)) % 1000u) / 1000f;

        public static void Write()
        {
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Echo/Content/Art", "Far3D");
            EchoArt.Write(Facade, FacadeTexture(lit: false));
            EchoArt.Write(Windows, FacadeTexture(lit: true));
            EchoArt.Write(Ground, GroundTexture());
            EchoArt.Write(Sky, SkyTexture());
            EchoArt.Write(Shadow, ShadowTexture());
        }

        public static void Import()
        {
            foreach (string p in new[] { Facade, Windows, Ground, Sky })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(p);
                importer.textureType = TextureImporterType.Default;
                importer.wrapMode = p == Sky ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            EchoArt.ImportSprite(Shadow, 64f, new Vector2(0.5f, 0.5f), default, TextureWrapMode.Clamp, FilterMode.Bilinear);
        }

        // 256 x 512: one tile is 20 x 40 units of facade (Far3DScene.FacadeTile*), 16 x 32 windows of 16 px, so its windows
        // stay one size on every tower and stay sharp close up.
        // Art fix phase 3: mostly dark. About one window in five is lit, in runs along a floor (an office still working)
        // rather than an even speckle; every fourth floor is a darker band and a mullion every four windows, so a facade
        // reads as a building, and mipmapping turns a distant one into a dim glow.

        static bool Lit(int col, int row) => Noise(col / 3 + 11, row) > 0.55f && Noise(col, row + 5) > 0.35f;

        static PixelCanvas FacadeTexture(bool lit)
        {
            var c = new HdCanvas(256, 512);
            c.Fill((x, y) =>
            {
                int col = x / 16, row = y / 16;
                bool frame = x % 16 < 2 || y % 16 < 3;
                bool mullion = x % 64 < 3;
                bool band = row % 4 == 3;
                bool on = !band && Lit(col, row);
                if (lit)
                {
                    if (frame || mullion || !on) return new Color(0f, 0f, 0f, 1f);
                    Color w = Noise(col + 7, row) > 0.75f ? C(0x8ADCFF) : C(0xFFC88A);
                    return w * (0.65f + 0.35f * Noise(col + 3, row + 9));   // some brighter than others
                }
                // Light enough for the moonlight to shade the faces: one wall lit, the other in shade, the roofs catching it.
                Color glass = Color.Lerp(C(0x26304C), C(0x38445E), Noise(col, row + 40) * 0.6f + y / 1280f);
                if (band) glass = C(0x1A2034);
                return mullion ? C(0x4A5670) : frame ? C(0x30394F) : on ? Color.Lerp(glass, C(0x6A7898), 0.35f) : glass;
            });
            return c.ToPixelCanvas();
        }

        /// <summary>The share of the facade's windows that are lit (tests check it stays 15-25%).</summary>
        public static float MeasuredLitShare()
        {
            int lit = 0, windows = 0;
            for (int row = 0; row < 32; row++)
                for (int col = 0; col < 16; col++)
                {
                    windows++;
                    if (row % 4 != 3 && Lit(col, row)) lit++;
                }
            return lit / (float)windows;
        }

        // The canyon's street: dark plating, lanes running along the canyon (v), a faint cross street each tile.
        static PixelCanvas GroundTexture()
        {
            var c = new HdCanvas(256, 256);
            c.Fill((x, y) => Noise(x / 16, y / 16) > 0.5f ? C(0x080B16) : C(0x0B0F1C));
            for (int k = 0; k < 256; k++)
                for (int d = 0; d < 5; d++)
                {
                    float a = d == 0 ? 0.9f : 0.3f * (1f - d / 5f);
                    foreach (int lane in new[] { 40, 104, 152, 216 }) { c.Add(lane + d, k, C(0x3AF0FF), a * 0.8f); c.Add(lane - d, k, C(0x3AF0FF), a * 0.8f); }
                    c.Add(128 + d, k, C(0xFF3AD0), a);
                    c.Add(k, d, C(0x3AF0FF), a * 0.3f);
                }
            return c.ToPixelCanvas();
        }

        /// <summary>Where the horizon is up the sky texture (Far3DScene.HorizonV, which places it at the camera's eye level).</summary>
        public const float HorizonV = Echo.Game.Views.Far3DScene.HorizonV;

        /// <summary>The exponential fog's colour, which the sky's horizon matches, so the fogged city melts into it.</summary>
        public static readonly Color FogColour = new Color(0.30f, 0.13f, 0.38f);

        // 256 x 512: below the horizon the fog's colour; a magenta glow just above it, fading through violet to a near-black
        // top with stars.
        static PixelCanvas SkyTexture()
        {
            var c = new HdCanvas(256, 512);
            c.Fill((x, y) =>
            {
                float v = y / 511f;
                if (v < HorizonV) return FogColour;
                float t = (v - HorizonV) / (1f - HorizonV);
                Color glow = Color.Lerp(Color.Lerp(FogColour, C(0x8A3A8E), Mathf.SmoothStep(0f, 0.05f, t)), C(0x2E1A5A), Mathf.SmoothStep(0.03f, 0.3f, t));
                return Color.Lerp(glow, C(0x05060F), Mathf.SmoothStep(0.25f, 0.85f, t));
            });
            for (int k = 0; k < 260; k++)
            {
                int x = (int)(Noise(k, 61) * 256), y = (int)(512 * (HorizonV + 0.2f + 0.8f * (1f - HorizonV - 0.2f) * Noise(k, 62)));
                float b = 0.3f + 0.7f * Noise(k, 63);
                c.Blend(x, y, new Color(0.9f, 0.92f, 1f, b));
            }
            return c.ToPixelCanvas();
        }

        // A soft dark ellipse, 64 x 16: the blob shadow under a figure.
        static PixelCanvas ShadowTexture()
        {
            var c = new HdCanvas(64, 16);
            c.Fill((x, y) =>
            {
                float u = (x - 31.5f) / 31.5f, v = (y - 7.5f) / 7.5f;
                float d = Mathf.Sqrt(u * u + v * v);
                return new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d) * 1.6f);
            });
            return c.ToPixelCanvas();
        }
    }
}

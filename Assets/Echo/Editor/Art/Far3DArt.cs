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
        public static string Ring => Dir + "/ring.png";
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
            EchoArt.Write(Ring, RingTexture());
            EchoArt.Write(Shadow, ShadowTexture());
        }

        public static void Import()
        {
            foreach (string p in new[] { Facade, Windows, Ground, Sky, Ring })
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

        // 128 x 256: one tile is 24 x 48 units of facade, so its windows stay one size on every tower.
        static PixelCanvas FacadeTexture(bool lit)
        {
            var c = new HdCanvas(128, 256);
            c.Fill((x, y) =>
            {
                int col = x / 8, row = y / 10;
                bool frame = x % 8 == 0 || y % 10 == 0 || x % 32 == 0;
                bool on = Noise(col, row) > 0.84f;   // about one window in six lit
                if (lit) return frame || !on ? new Color(0f, 0f, 0f, 1f) : Noise(col + 7, row) > 0.7f ? C(0x9AE8FF) : C(0xFFD49A);
                Color glass = Color.Lerp(C(0x121A30), C(0x1E2A48), y / 255f);
                return frame ? C(0x3A4664) : on ? Color.Lerp(glass, C(0x5A6A8A), 0.3f) : glass;
            });
            return c.ToPixelCanvas();
        }

        static PixelCanvas GroundTexture()
        {
            var c = new HdCanvas(256, 256);
            c.Fill((x, y) => Noise(x / 16, y / 16) > 0.5f ? C(0x0A0E1E) : C(0x0E1426));
            for (int k = 0; k < 256; k++)
                for (int d = 0; d < 6; d++)
                {
                    float a = d == 0 ? 1f : 0.35f * (1f - d / 6f);
                    c.Add(k, (128 + d) % 256, C(0x3AF0FF), a);
                    c.Add((64 + d) % 256, k, C(0xFF3AD0), a * 0.7f);
                }
            return c.ToPixelCanvas();
        }

        static PixelCanvas SkyTexture()
        {
            var c = new HdCanvas(16, 256);
            c.Fill((x, y) =>
            {
                float t = y / 255f;
                Color low = Color.Lerp(C(0xC0408A), C(0x4A1E6A), Mathf.SmoothStep(0f, 1f, t * 2.5f));
                return Color.Lerp(low, C(0x070818), Mathf.SmoothStep(0.3f, 1f, t));
            });
            return c.ToPixelCanvas();
        }

        static PixelCanvas RingTexture()
        {
            var c = new HdCanvas(64, 16);
            c.Fill((x, y) => y == 7 || y == 8 || (x % 16 < 3 && y > 2 && y < 13) ? C(0x7AF4FF) : C(0x1A2440));
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

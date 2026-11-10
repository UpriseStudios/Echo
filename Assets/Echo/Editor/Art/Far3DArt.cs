using UnityEditor;
using UnityEngine;

namespace Echo.Editor.Art
{
    /// <summary>
    /// The textures of the Far Far Future's 3D city (Far3DScene, "The Infinite City"), generated like the rest of the game's
    /// art: a subtle panelled surface for the megastructures (large plates and faint seams, no windows), a dark ground with
    /// faint lines running to the horizon, a sky from the haze's violet at the horizon through midnight blue to a near-black
    /// top with stars, and the soft blob shadow cast on the floor by the runner and the ghosts.
    /// </summary>
    public static class Far3DArt
    {
        public const string Dir = "Assets/Echo/Content/Art/Far3D";
        public static string Panels => Dir + "/panels.png";
        public static string Ground => Dir + "/ground.png";
        public static string Sky => Dir + "/sky.png";
        public static string Shadow => Dir + "/shadow.png";

        static Color C(uint rgb, float a = 1f) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);
        static float Noise(int x, int y) => (((uint)(x * 73856093) ^ (uint)(y * 19349663)) % 1000u) / 1000f;

        public static void Write()
        {
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Echo/Content/Art", "Far3D");
            // The window facades of the earlier city are gone with it.
            foreach (string old in new[] { Dir + "/facade.png", Dir + "/facade_windows.png" })
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(old) != null) AssetDatabase.DeleteAsset(old);
            EchoArt.Write(Panels, PanelTexture());
            EchoArt.Write(Ground, GroundTexture());
            EchoArt.Write(Sky, SkyTexture());
            EchoArt.Write(Shadow, ShadowTexture());
        }

        public static void Import()
        {
            foreach (string p in new[] { Panels, Ground, Sky })
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

        // 256 x 256 for 60 x 60 units of surface (Far3DScene.PanelTile*): big plates a touch different in tone, faint seams
        // between them and a finer horizontal course every so often, so a surface reads as built at an impossible scale
        // without a single window. Near white: the materials tint it.
        static PixelCanvas PanelTexture()
        {
            var c = new HdCanvas(256, 256);
            c.Fill((x, y) =>
            {
                int px = x / 64, py = y / 32;
                float plate = 0.86f + 0.1f * Noise(px + 3, py + 7);
                bool seam = x % 64 < 2 || y % 32 < 2;
                bool course = y % 128 < 4;
                float v = seam ? 0.62f : course ? 0.74f : plate;
                return new Color(v, v, v, 1f);
            });
            return c.ToPixelCanvas();
        }

        // The ground far below: dark, with faint lines running along the path (v) towards the horizon.
        static PixelCanvas GroundTexture()
        {
            var c = new HdCanvas(256, 256);
            c.Fill((x, y) => Noise(x / 32, y / 32) > 0.5f ? C(0x07081A) : C(0x090A1E));
            for (int k = 0; k < 256; k++)
                for (int d = 0; d < 3; d++)
                {
                    float a = d == 0 ? 0.18f : 0.06f * (1f - d / 3f);
                    foreach (int lane in new[] { 64, 192 }) { c.Add(lane + d, k, C(0x3AA8D0), a); c.Add(lane - d, k, C(0x3AA8D0), a); }
                }
            return c.ToPixelCanvas();
        }

        /// <summary>Where the horizon is up the sky texture (Far3DScene.HorizonV, which places it at the camera's eye level).</summary>
        public const float HorizonV = Echo.Game.Views.Far3DScene.HorizonV;

        /// <summary>The haze's colour, which the sky's horizon matches, so the far city melts into it.</summary>
        public static Color FogColour => Echo.Game.Views.Far3DScene.FogColour;

        // 256 x 512: below the horizon the haze's colour; just above it a soft violet glow, rising through midnight blue to a
        // near-black top with faint stars.
        static PixelCanvas SkyTexture()
        {
            var c = new HdCanvas(256, 512);
            c.Fill((x, y) =>
            {
                float v = y / 511f;
                if (v < HorizonV) return FogColour;
                float t = (v - HorizonV) / (1f - HorizonV);
                Color low = Color.Lerp(Color.Lerp(FogColour, C(0x4A3A7E), Mathf.SmoothStep(0f, 0.06f, t)), C(0x1A1C48), Mathf.SmoothStep(0.04f, 0.35f, t));
                return Color.Lerp(low, C(0x04050F), Mathf.SmoothStep(0.3f, 0.95f, t));
            });
            for (int k = 0; k < 220; k++)
            {
                int x = (int)(Noise(k, 61) * 256), y = (int)(512 * (HorizonV + 0.25f + (1f - HorizonV - 0.25f) * Noise(k, 62)));
                float b = 0.2f + 0.6f * Noise(k, 63);
                c.Blend(x, y, new Color(0.85f, 0.9f, 1f, b));
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

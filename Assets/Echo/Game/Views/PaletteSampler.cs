using UnityEngine;

namespace Echo.Game.Views
{
    /// <summary>Background hue by distance (tech spec 11), plus the darker shades for ground and obstacles.</summary>
    public static class PaletteSampler
    {
        public const float MetresPerStop = 500f;
        static readonly Color Fallback = new Color(0.16f, 0.45f, 0.50f);

        /// <summary>Blends between stops every 500 m and holds the last one after that.</summary>
        public static Color Sample(Color[] stops, float metres)
        {
            if (stops == null || stops.Length == 0) return Fallback;
            float t = Mathf.Max(0f, metres) / MetresPerStop;
            int i = Mathf.Min((int)t, stops.Length - 1);
            if (i >= stops.Length - 1) return stops[stops.Length - 1];
            return Color.Lerp(stops[i], stops[i + 1], t - i);
        }

        /// <summary>Same hue, lower value: "one shade darker than the background" (game spec).</summary>
        public static Color Darker(Color c, float factor)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            return Color.HSVToRGB(h, Mathf.Clamp01(s * 1.05f), v * factor);
        }
    }
}

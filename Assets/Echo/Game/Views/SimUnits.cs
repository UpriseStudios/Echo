using Echo.Core.Sim;
using UnityEngine;

namespace Echo.Game.Views
{
    /// <summary>Sub-units (int, 1000 per unit) to world units (float). Views only; never fed back to the sim.</summary>
    public static class SimUnits
    {
        public const float PerUnit = 1000f;

        public static float ToWorld(int sub) => sub / PerUnit;

        /// <summary>
        /// The grid views snap to: the art's 32 px per unit, or a much finer one while the Far Future renders at full
        /// resolution (World Time Run doc, phase 3), so things glide there instead of stepping a whole art pixel.
        /// </summary>
        public static float SnapPerUnit = CameraRig.PixelsPerUnit;

        /// <summary>Rounds a world coordinate to the grid the world is rendered on (32 px per unit).</summary>
        public static float Pixel(float v) => Mathf.Round(v * SnapPerUnit) / SnapPerUnit;

        public static Vector2 Lerp(in RunnerState from, in RunnerState to, float alpha) => new Vector2(
            Mathf.LerpUnclamped(from.X, to.X, alpha) / PerUnit,
            Mathf.LerpUnclamped(from.Y, to.Y, alpha) / PerUnit);
    }
}

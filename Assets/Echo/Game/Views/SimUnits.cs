using Echo.Core.Sim;
using UnityEngine;

namespace Echo.Game.Views
{
    /// <summary>Sub-units (int, 1000 per unit) to world units (float). Views only; never fed back to the sim.</summary>
    public static class SimUnits
    {
        public const float PerUnit = 1000f;

        public static float ToWorld(int sub) => sub / PerUnit;

        /// <summary>Rounds a world coordinate to the 32-px-per-unit pixel grid the world is rendered on.</summary>
        public static float Pixel(float v) => Mathf.Round(v * CameraRig.PixelsPerUnit) / CameraRig.PixelsPerUnit;

        public static Vector2 Lerp(in RunnerState from, in RunnerState to, float alpha) => new Vector2(
            Mathf.LerpUnclamped(from.X, to.X, alpha) / PerUnit,
            Mathf.LerpUnclamped(from.Y, to.Y, alpha) / PerUnit);
    }
}

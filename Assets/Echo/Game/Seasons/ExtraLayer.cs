using System;
using UnityEngine;

namespace Echo.Game.Seasons
{
    /// <summary>
    /// A backdrop's extra parallax layer (World Time Run doc, phase 3: Today's distant skyline and window light, the Far
    /// Future's sky and moons), drawn by BackgroundView behind the floor and everything on it.
    /// </summary>
    [Serializable]
    public sealed class ExtraLayer
    {
        public Sprite Sprite;
        [Tooltip("0 stays with the camera (a sky), 1 moves with the course; the far layer is 0.15, the near 0.4.")]
        public float Parallax;
        [Tooltip("Sorting order: -30 the sky, -22 behind the far layer, -20 between far and near, -18 in front of the near.")]
        public int Order;
        [Tooltip("White shows the layer in its own colours.")]
        public Color Tint = Color.white;
    }
}

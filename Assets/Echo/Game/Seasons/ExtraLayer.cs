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
        [Tooltip("Art pass phase 3: frames drawn over the layer at one spot in every tile (the windmill's turning sails).")]
        public Sprite[] Frames = new Sprite[0];
        [Tooltip("Where the frames go, in units from the tile's bottom-left.")]
        public Vector2 FramesAt;
        public float FramesPerSecond = 6f;
        [Tooltip("Art pass phase 4: fades in between these distances in metres (Today's dusk sky and window lights); equal: always shown.")]
        public float FadeFrom, FadeTo;
        [Tooltip("Vehicles driving along the layer (Today's flyover), half each way, at TrafficY units up.")]
        public Sprite[] Traffic = new Sprite[0];
        public float TrafficY;
        public float TrafficSpeed = 3f;
        public int TrafficCount;
        [Tooltip("Art fix phase 2: an emissive layer (neon, lit windows, signs), drawn through the HDR glow material so the bloom catches it; with reduced effects it is drawn plainly.")]
        public bool Glow;
    }
}

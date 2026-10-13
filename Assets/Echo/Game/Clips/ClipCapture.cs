using System;
using UnityEngine;

namespace Echo.Game.Clips
{
    /// <summary>
    /// What a clip sees (a user revision: the clip editor). The world camera renders into this 9:16 texture at the
    /// pixel-perfect size, 288 x 512, which the editor shows as its preview, so the clip is exactly what the player
    /// framed. Each captured frame is read back as RGBA, top row first, with the score and ghost count stamped on
    /// (game spec: the share clip's overlay).
    /// </summary>
    public sealed class ClipCapture : IDisposable
    {
        public const int Width = 288, Height = 512;

        const uint Cyan = 0x73F2FF, White = 0xFFFFFF, Gold = 0xFFC94A, Shadow = 0x080A12;

        readonly RenderTexture _target;
        readonly Texture2D _readback;
        bool _disposed;

        /// <summary>The latest frame: RGBA, Width x Height, top row first.</summary>
        public byte[] Frame { get; } = new byte[Width * Height * 4];

        public RenderTexture Target => _target;

        public ClipCapture()
        {
            _target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                filterMode = FilterMode.Point,
                name = "ClipCapture",
            };
            _target.Create();
            _readback = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false);
        }

        /// <summary>Reads what the camera last drew into the target, and stamps the overlay on it.</summary>
        public void Read(int score, int ghosts, bool newBest)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = _target;
            _readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
            _readback.Apply(false);
            RenderTexture.active = previous;

            var pixels = _readback.GetPixelData<byte>(0);
            const int row = Width * 4;
            for (int y = 0; y < Height; y++)
                Unity.Collections.NativeArray<byte>.Copy(pixels, (Height - 1 - y) * row, Frame, y * row, row);   // its first row is the bottom
            Stamp(Frame, score, ghosts, newBest);
        }

        /// <summary>The overlay: ECHO top left, the score top right, the ghost count under it, NEW BEST! when it was one.</summary>
        public static void Stamp(byte[] rgba, int score, int ghosts, bool newBest)
        {
            const int margin = 8;
            ClipFont.Draw(rgba, Width, Height, "ECHO", margin, margin, 3, Cyan, Shadow);
            string points = score.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
            ClipFont.Draw(rgba, Width, Height, points, Width - margin - ClipFont.Measure(points, 3), margin, 3, White, Shadow);
            string crowd = ghosts == 1 ? "1 GHOST" : $"{ghosts} GHOSTS";
            ClipFont.Draw(rgba, Width, Height, crowd, Width - margin - ClipFont.Measure(crowd, 2), margin + 28, 2, Cyan, Shadow);
            if (newBest) ClipFont.Draw(rgba, Width, Height, "NEW BEST!", margin, margin + 28, 2, Gold, Shadow);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _target.Release();
            UnityEngine.Object.Destroy(_target);
            UnityEngine.Object.Destroy(_readback);
        }
    }
}

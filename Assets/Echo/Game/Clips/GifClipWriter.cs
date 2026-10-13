using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Echo.Game.Clips
{
    /// <summary>Collects a clip's frames as 15-bit colour and writes the looping GIF off the main thread.</summary>
    public sealed class GifClipWriter
    {
        public const int TicksPerFrame = 5;   // 12 frames a second
        public const int DelayCs = 8;
        public const int Scale = 2;

        readonly List<ushort[]> _frames = new List<ushort[]>();
        readonly int _width, _height;

        public GifClipWriter(int width, int height)
        {
            _width = width;
            _height = height;
        }

        public int FrameCount => _frames.Count;

        /// <param name="rgba">One frame, RGBA, top row first.</param>
        public void AddFrame(byte[] rgba)
        {
            var frame = new ushort[_width * _height];
            for (int i = 0; i < frame.Length; i++) frame[i] = GifEncoder.Pack(rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2]);
            _frames.Add(frame);
        }

        public Task SaveAsync(string path)
        {
            var frames = _frames.ToArray();
            int w = _width, h = _height;
            return Task.Run(() => File.WriteAllBytes(path, GifEncoder.Encode(frames, w, h, Scale, DelayCs)));
        }
    }
}

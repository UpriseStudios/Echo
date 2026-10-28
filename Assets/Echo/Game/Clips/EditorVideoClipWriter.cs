#if UNITY_EDITOR
using System.IO;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEditor.Media;
using UnityEngine;

namespace Echo.Game.Clips
{
    /// <summary>In the editor (and its tests) video clips are written by Unity's own MediaEncoder: a real MP4.</summary>
    public sealed class EditorVideoClipWriter : IVideoClipWriter
    {
        MediaEncoder _encoder;
        NativeArray<byte> _frame;
        byte[] _scaled;
        readonly System.Diagnostics.Stopwatch _scaleTime = new System.Diagnostics.Stopwatch(), _encodeTime = new System.Diagnostics.Stopwatch();
        int _frames;
        int _width, _height, _scale, _channels;

        public int Scale => _scale;
        public int Fps { get; private set; }
        string _path;
        bool _hasAudio;

        public bool Begin(string path, int width, int height, int scale, int fps, int sampleRate, int channels)
        {
            _path = path;
            _width = width;
            _height = height;
            _scale = scale;
            Fps = fps;
            _channels = channels;
            int w = width * scale, h = height * scale;
            if (File.Exists(path)) File.Delete(path);
            var video = new VideoTrackAttributes
            {
                frameRate = new MediaRational(fps),
                width = (uint)w,
                height = (uint)h,
                includeAlpha = false,
                bitRateMode = UnityEditor.VideoBitrateMode.High,
            };
            var audio = new AudioTrackAttributes
            {
                sampleRate = new MediaRational(sampleRate),
                channelCount = (ushort)channels,
                language = "en",
            };
            _encoder = sampleRate > 0 && channels > 0 ? new MediaEncoder(path, video, audio) : new MediaEncoder(path, video);
            _hasAudio = sampleRate > 0 && channels > 0;
            _scaled = new byte[w * h * 4];
            _frame = new NativeArray<byte>(w * h * 4, Allocator.Persistent);
            return true;
        }

        public void AddFrame(byte[] rgba)
        {
            _scaleTime.Start();
            int w = _width * _scale, rowBytes = w * 4;
            // Each source row is widened once, then copied for the other rows it covers. The encoder's first row is
            // the bottom one, like a texture's.
            for (int sy = 0; sy < _height; sy++)
            {
                int src = sy * _width * 4;
                int first = (_height - 1 - sy) * _scale * rowBytes;
                for (int x = 0, d = first; x < _width; x++)
                {
                    int s = src + x * 4;
                    for (int k = 0; k < _scale; k++, d += 4)
                    {
                        _scaled[d] = rgba[s];
                        _scaled[d + 1] = rgba[s + 1];
                        _scaled[d + 2] = rgba[s + 2];
                        _scaled[d + 3] = 255;
                    }
                }
                for (int k = 1; k < _scale; k++) System.Buffer.BlockCopy(_scaled, first, _scaled, first + k * rowBytes, rowBytes);
            }
            NativeArray<byte>.Copy(_scaled, _frame);
            _scaleTime.Stop();
            _encodeTime.Start();
            _encoder.AddFrame(w, _height * _scale, rowBytes, TextureFormat.RGBA32, _frame);
            _encodeTime.Stop();
            _frames++;
        }

        public void AddAudio(float[] interleaved, int frames)
        {
            if (frames <= 0 || !_hasAudio) return;
            using var samples = new NativeArray<float>(frames * _channels, Allocator.Temp);
            NativeArray<float>.Copy(interleaved, samples, frames * _channels);
            _encoder.AddSamples(samples);
        }

        public Task<bool> FinishAsync()
        {
            _encoder.Dispose();
            if (_frame.IsCreated) _frame.Dispose();
            Debug.Log($"[Clips] editor video: {_frames} frames, widening {_scaleTime.ElapsedMilliseconds / System.Math.Max(1, _frames)} ms and encoding {_encodeTime.ElapsedMilliseconds / System.Math.Max(1, _frames)} ms a frame");
            return Task.FromResult(File.Exists(_path));
        }
    }
}
#endif

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
        Texture2D _texture;
        byte[] _scaled;
        int _width, _height, _scale, _channels;
        string _path;
        bool _hasAudio;

        public bool Begin(string path, int width, int height, int scale, int fps, int sampleRate, int channels)
        {
            _path = path;
            _width = width;
            _height = height;
            _scale = scale;
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
            _texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            _scaled = new byte[w * h * 4];
            return true;
        }

        public void AddFrame(byte[] rgba)
        {
            int w = _width * _scale, h = _height * _scale;
            for (int y = 0; y < h; y++)
            {
                int src = (_height - 1 - y / _scale) * _width;   // the texture's first row is the bottom
                int dst = y * w;
                for (int x = 0; x < w; x++)
                {
                    int s = (src + x / _scale) * 4, d = (dst + x) * 4;
                    _scaled[d] = rgba[s];
                    _scaled[d + 1] = rgba[s + 1];
                    _scaled[d + 2] = rgba[s + 2];
                    _scaled[d + 3] = 255;
                }
            }
            _texture.SetPixelData(_scaled, 0);
            _texture.Apply(false);
            _encoder.AddFrame(_texture);
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
            Object.Destroy(_texture);
            return Task.FromResult(File.Exists(_path));
        }
    }
}
#endif

using UnityEngine;

namespace Echo.Game.Clips
{
    /// <summary>
    /// Records what the player hears for a video clip (a user revision: music and sound effects). It sits beside the
    /// AudioListener, so OnAudioFilterRead sees the final mix; it only reads it, never changes it. Samples are kept
    /// in a buffer on the audio thread and taken on the main thread each frame. Stereo out, whatever the device mix.
    /// </summary>
    [RequireComponent(typeof(AudioListener))]
    public sealed class ClipAudioTap : MonoBehaviour
    {
        public const int Channels = 2;

        readonly object _lock = new object();
        float[] _buffer = new float[0];
        int _frames;
        volatile bool _recording;

        public static int SampleRate => AudioSettings.outputSampleRate;

        public void Begin()
        {
            lock (_lock)
            {
                // Two seconds of room: drained every frame, so it never fills unless the game stalls.
                if (_buffer.Length < SampleRate * Channels * 2) _buffer = new float[SampleRate * Channels * 2];
                _frames = 0;
            }
            _recording = true;
        }

        public void End() => _recording = false;

        /// <summary>Moves what has been heard since the last call into <paramref name="into"/>; returns the frame count.</summary>
        public int Drain(ref float[] into)
        {
            lock (_lock)
            {
                int samples = _frames * Channels;
                if (into == null || into.Length < samples) into = new float[Mathf.Max(samples, 1024)];
                System.Array.Copy(_buffer, into, samples);
                int frames = _frames;
                _frames = 0;
                return frames;
            }
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (!_recording || channels <= 0) return;
            lock (_lock)
            {
                int frames = data.Length / channels;
                int room = _buffer.Length / Channels - _frames;
                if (frames > room) frames = room;
                int o = _frames * Channels;
                for (int f = 0; f < frames; f++)
                {
                    int i = f * channels;
                    float left = data[i], right = channels > 1 ? data[i + 1] : left;
                    _buffer[o++] = left;
                    _buffer[o++] = right;
                }
                _frames += frames;
            }
        }
    }
}

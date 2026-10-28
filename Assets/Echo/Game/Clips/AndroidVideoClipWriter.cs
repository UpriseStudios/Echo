#if UNITY_ANDROID && !UNITY_EDITOR
using System.Threading.Tasks;
using UnityEngine;

namespace Echo.Game.Clips
{
    /// <summary>
    /// Android video clips through our own small Java class (Assets/Plugins/Android/EchoClipEncoder.java), which
    /// drives the phone's hardware H.264 and AAC encoders (MediaCodec) and MediaMuxer. Sound goes over as 16-bit PCM.
    /// </summary>
    public sealed class AndroidVideoClipWriter : IVideoClipWriter
    {
        AndroidJavaObject _encoder;
        // Java byte arrays are signed: sbyte[] goes over JNI as byte[] without a conversion warning.
        sbyte[] _pcm = new sbyte[0];
        sbyte[] _frame = new sbyte[0];
        int _channels;

        public int Scale { get; private set; }
        public int Fps { get; private set; }

        public bool Begin(string path, int width, int height, int scale, int fps, int sampleRate, int channels)
        {
            _channels = channels;
            try
            {
                _encoder = new AndroidJavaObject("com.uprise.echo.EchoClipEncoder");
                bool begun = _encoder.Call<bool>("begin", path, width, height, scale, fps, sampleRate, channels);
                if (begun)
                {
                    Scale = _encoder.Call<int>("getScale");
                    Fps = _encoder.Call<int>("getFps");
                }
                return begun;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Clips] video encoder unavailable: " + e.Message);
                return false;
            }
        }

        public void AddFrame(byte[] rgba)
        {
            if (_encoder == null) return;
            if (_frame.Length != rgba.Length) _frame = new sbyte[rgba.Length];
            System.Buffer.BlockCopy(rgba, 0, _frame, 0, rgba.Length);
            _encoder.Call("addFrame", _frame);
        }

        public void AddAudio(float[] interleaved, int frames)
        {
            if (_encoder == null || frames <= 0) return;
            int count = frames * _channels;
            if (_pcm.Length < count * 2) _pcm = new sbyte[count * 2];
            for (int i = 0; i < count; i++)
            {
                int v = (int)(Mathf.Clamp(interleaved[i], -1f, 1f) * 32767f);
                _pcm[i * 2] = unchecked((sbyte)v);
                _pcm[i * 2 + 1] = unchecked((sbyte)(v >> 8));
            }
            _encoder.Call("addAudio", _pcm, count * 2);
        }

        public async Task<bool> FinishAsync()
        {
            if (_encoder == null) return false;
            _encoder.Call("finish");
            // The Java side drains and closes the file on its own thread; 1 done, -1 failed.
            int state;
            while ((state = _encoder.Call<int>("state")) == 0) await Task.Yield();
            _encoder.Dispose();
            _encoder = null;
            return state == 1;
        }
    }
}
#endif

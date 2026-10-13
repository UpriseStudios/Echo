#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Echo.Game.Clips
{
    /// <summary>
    /// iOS video clips through our own small AVFoundation writer (Assets/Plugins/iOS/EchoClipEncoder.mm): H.264 and
    /// AAC into an MP4 with AVAssetWriter.
    /// </summary>
    public sealed class IosVideoClipWriter : IVideoClipWriter
    {
        [DllImport("__Internal")] [return: MarshalAs(UnmanagedType.I1)] static extern bool EchoClip_Begin(string path, int width, int height, int scale, int fps, int sampleRate, int channels);
        [DllImport("__Internal")] static extern void EchoClip_AddFrame(byte[] rgba);
        [DllImport("__Internal")] static extern void EchoClip_AddAudio(float[] interleaved, int frames);
        [DllImport("__Internal")] static extern void EchoClip_Finish();
        [DllImport("__Internal")] static extern int EchoClip_State();

        bool _begun;

        public bool Begin(string path, int width, int height, int scale, int fps, int sampleRate, int channels) =>
            _begun = EchoClip_Begin(path, width, height, scale, fps, sampleRate, channels);

        public void AddFrame(byte[] rgba)
        {
            if (_begun) EchoClip_AddFrame(rgba);
        }

        public void AddAudio(float[] interleaved, int frames)
        {
            if (_begun && frames > 0) EchoClip_AddAudio(interleaved, frames);
        }

        public async Task<bool> FinishAsync()
        {
            if (!_begun) return false;
            EchoClip_Finish();
            int state;
            while ((state = EchoClip_State()) == 0) await Task.Yield();
            return state == 1;
        }
    }
}
#endif

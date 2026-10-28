using System.Threading.Tasks;

namespace Echo.Game.Clips
{
    /// <summary>
    /// Writes a clip as an H.264 MP4 with AAC sound (a user revision: video clips for TikTok and YouTube), using the
    /// platform's own encoder: MediaCodec on Android, AVFoundation on iOS, Unity's MediaEncoder in the editor.
    /// Frames arrive at a constant rate in order; sound arrives as it was heard. No package is involved.
    /// </summary>
    public interface IVideoClipWriter
    {
        /// <param name="width">The frame size given to AddFrame; the video is <paramref name="scale"/> times larger.</param>
        /// <param name="sampleRate">Game audio rate; 0 for no sound track.</param>
        bool Begin(string path, int width, int height, int scale, int fps, int sampleRate, int channels);

        /// <summary>
        /// The scale and frame rate in use after Begin: a phone whose encoder can't do what was asked steps down
        /// (x3, then 30 fps), and frames must then be sent at this rate.
        /// </summary>
        int Scale { get; }
        int Fps { get; }

        /// <param name="rgba">One frame, RGBA, top row first.</param>
        void AddFrame(byte[] rgba);

        /// <param name="interleaved">Float samples in [-1, 1], <paramref name="frames"/> x channels of them.</param>
        void AddAudio(float[] interleaved, int frames);

        /// <summary>Finishes the file; true when it was written.</summary>
        Task<bool> FinishAsync();
    }
}

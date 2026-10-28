namespace Echo.Game.Clips
{
    /// <summary>The video writer for this platform, or null where there is none (video clips are then not offered).</summary>
    public static class VideoClipWriters
    {
        public const int Fps = 30;
        public const int TicksPerFrame = 2;
        public const int Scale = 3;   // 288 x 512 world pixels make an 864 x 1536 video, 9:16

        // The Vertical preset (a user choice): x4 keeps every pixel square at 1152 x 2048, at the sim's own 60 fps.
        public const int VerticalScale = 4;
        public const int VerticalFps = 60;

        public static bool Supported =>
#if UNITY_EDITOR || UNITY_ANDROID || UNITY_IOS
            true;
#else
            false;
#endif

        public static IVideoClipWriter Create()
        {
#if UNITY_EDITOR
            return new EditorVideoClipWriter();
#elif UNITY_ANDROID
            return new AndroidVideoClipWriter();
#elif UNITY_IOS
            return new IosVideoClipWriter();
#else
            return null;
#endif
        }
    }
}

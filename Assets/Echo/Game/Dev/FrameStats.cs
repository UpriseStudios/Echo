namespace Echo.Game.Dev
{
    /// <summary>
    /// Frame times for the Time Run benchmark (World Time Run doc, phase 3: the frame rate in each era with 30 ghosts):
    /// the average and the slowest 1% from a histogram of whole milliseconds, so adding a frame allocates nothing.
    /// </summary>
    public sealed class FrameStats
    {
        const int Buckets = 101;   // 0-99 ms, and 100 ms or slower
        readonly int[] _ms = new int[Buckets];
        double _seconds;

        public int Frames { get; private set; }
        public int MostGhostsOnScreen { get; private set; }

        public void Add(float seconds, int ghostsOnScreen)
        {
            Frames++;
            _seconds += seconds;
            int ms = (int)(seconds * 1000f);
            _ms[ms < 0 ? 0 : ms >= Buckets ? Buckets - 1 : ms]++;
            if (ghostsOnScreen > MostGhostsOnScreen) MostGhostsOnScreen = ghostsOnScreen;
        }

        public float AverageFps => _seconds > 0 ? (float)(Frames / _seconds) : 0f;

        /// <summary>The frame rate of the slowest <paramref name="fraction"/> of frames (1% low).</summary>
        public float LowFps(float fraction = 0.01f)
        {
            if (Frames == 0) return 0f;
            int need = System.Math.Max(1, (int)(Frames * fraction)), seen = 0;
            for (int ms = Buckets - 1; ms >= 0; ms--)
            {
                seen += _ms[ms];
                if (seen >= need) return 1000f / (ms + 1);
            }
            return 0f;
        }
    }
}

using Echo.Core.Sim;

namespace Echo.Core.Courses
{
    /// <summary>Block whose bottom follows an integer triangle wave on the run tick (tech spec 6).</summary>
    public readonly struct Mover
    {
        public readonly int X;
        public readonly int W;
        public readonly int H;
        public readonly int YMin;
        public readonly int YMax;
        public readonly int Period;
        public readonly int Phase;

        public Mover(int x, int w, int h, int yMin, int yMax, int period, int phase)
        {
            X = x;
            W = w;
            H = h;
            YMin = yMin;
            YMax = yMax;
            Period = period;
            Phase = phase;
        }

        public int BottomAt(int tick)
        {
            int p = (tick + Phase) % Period;
            int half = Period / 2;
            return YMin + (YMax - YMin) * (p < half ? p : Period - p) / half;
        }

        public Aabb BoxAt(int tick)
        {
            int y = BottomAt(tick);
            return new Aabb(X, y, X + W, y + H);
        }
    }
}

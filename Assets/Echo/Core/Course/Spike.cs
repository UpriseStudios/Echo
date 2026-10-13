using Echo.Core.Sim;

namespace Echo.Core.Courses
{
    /// <summary>Hazard from the floor (y = 0) up to H, chunk-local sub-units.</summary>
    public readonly struct Spike
    {
        public readonly int X;
        public readonly int W;
        public readonly int H;

        public Spike(int x, int w, int h)
        {
            X = x;
            W = w;
            H = h;
        }

        public Aabb Box => new Aabb(X, 0, X + W, H);
    }
}

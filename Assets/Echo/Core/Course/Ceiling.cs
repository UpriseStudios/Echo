using Echo.Core.Sim;

namespace Echo.Core.Courses
{
    /// <summary>Hazard from Y upwards, chunk-local sub-units.</summary>
    public readonly struct Ceiling
    {
        public readonly int X;
        public readonly int W;
        public readonly int Y;

        public Ceiling(int x, int w, int y)
        {
            X = x;
            W = w;
            Y = y;
        }

        public Aabb Box => new Aabb(X, Y, X + W, int.MaxValue);
    }
}

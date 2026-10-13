namespace Echo.Core.Courses
{
    /// <summary>Missing floor over [X, X + W), chunk-local sub-units.</summary>
    public readonly struct Gap
    {
        public readonly int X;
        public readonly int W;

        public Gap(int x, int w)
        {
            X = x;
            W = w;
        }

        public bool Contains(int localX) => localX >= X && localX < X + W;
    }
}

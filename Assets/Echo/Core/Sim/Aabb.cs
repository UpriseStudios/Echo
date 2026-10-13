namespace Echo.Core.Sim
{
    /// <summary>Integer box with exclusive max edges; touching edges do not overlap.</summary>
    public readonly struct Aabb
    {
        public readonly int MinX;
        public readonly int MinY;
        public readonly int MaxX;
        public readonly int MaxY;

        public Aabb(int minX, int minY, int maxX, int maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public static Aabb FromBottomCentre(int x, int y, int w, int h)
        {
            int minX = x - w / 2;
            return new Aabb(minX, y, minX + w, y + h);
        }

        public bool Overlaps(in Aabb o) =>
            MinX < o.MaxX && o.MinX < MaxX && MinY < o.MaxY && o.MinY < MaxY;

        public Aabb Offset(int dx, int dy) => new Aabb(MinX + dx, MinY + dy, MaxX + dx, MaxY + dy);

        /// <summary>Largest per-axis gap between the boxes; 0 when they overlap or touch.</summary>
        public int GapTo(in Aabb o)
        {
            int dx = System.Math.Max(0, System.Math.Max(o.MinX - MaxX, MinX - o.MaxX));
            int dy = System.Math.Max(0, System.Math.Max(o.MinY - MaxY, MinY - o.MaxY));
            return System.Math.Max(dx, dy);
        }
    }
}

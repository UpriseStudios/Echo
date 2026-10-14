using System.Collections.Generic;
using Echo.Core.Sim;

namespace Echo.Core.Courses
{
    /// <summary>
    /// Endless chain of chunks generated lazily from a seed (tech spec 6). Chunk i depends only on
    /// the seed and i, so the player and every ghost see the same course whoever queries first.
    /// The floor is flat at y = 0; gaps are missing floor. x &lt; 0 is plain floor behind the start line.
    /// </summary>
    public sealed class Course
    {
        // tech spec 6: keep 3 chunks generated beyond the one being queried.
        public const int LookaheadChunks = 3;

        readonly ChunkLibrary _library;
        readonly IReadOnlyList<Chunk> _opening;
        readonly List<Chunk> _chunks = new List<Chunk>(256);
        readonly List<int> _starts = new List<int>(256);
        Xorshift32 _rng;
        int _endX;

        public Course(ChunkLibrary library, uint seed)
        {
            _library = library;
            Seed = seed;
            _rng = new Xorshift32(seed);
        }

        /// <summary>
        /// How to play (a user revision): a course that opens with the given chunks instead of the tutorial ones, then
        /// carries on as the seed would. Only the how-to-play clips use it; real runs never do.
        /// </summary>
        public Course(ChunkLibrary library, IReadOnlyList<Chunk> opening, uint seed) : this(library, seed)
        {
            _opening = opening;
        }

        public uint Seed { get; }
        public int GeneratedChunkCount => _chunks.Count;

        public Chunk ChunkAt(int index)
        {
            EnsureChunkCount(index + 1);
            return _chunks[index];
        }

        public int ChunkStartX(int index)
        {
            EnsureChunkCount(index + 1);
            return _starts[index];
        }

        /// <summary>Index of the chunk containing x, or -1 behind the start line.</summary>
        public int ChunkIndexAt(int x)
        {
            if (x < 0) return -1;
            while (_endX <= x) Generate();
            int i = FindIndex(x);
            EnsureChunkCount(i + 1 + LookaheadChunks);
            return i;
        }

        public bool HasFloorAt(int x)
        {
            int i = ChunkIndexAt(x);
            if (i < 0) return true;
            Gap[] gaps = _chunks[i].Gaps;
            int local = x - _starts[i];
            for (int g = 0; g < gaps.Length; g++)
                if (gaps[g].Contains(local)) return false;
            return true;
        }

        public bool OverlapsHazard(in Aabb box, int tick)
        {
            int first = ChunkIndexAt(box.MinX);
            int last = ChunkIndexAt(box.MaxX - 1);
            if (first < 0) first = 0;
            for (int i = first; i <= last; i++)
                if (ChunkOverlaps(_chunks[i], box.Offset(-_starts[i], 0), tick)) return true;
            return false;
        }

        static bool ChunkOverlaps(Chunk c, in Aabb local, int tick)
        {
            for (int i = 0; i < c.Spikes.Length; i++)
                if (local.Overlaps(c.Spikes[i].Box)) return true;
            for (int i = 0; i < c.Ceilings.Length; i++)
                if (local.Overlaps(c.Ceilings[i].Box)) return true;
            for (int i = 0; i < c.Movers.Length; i++)
                if (local.Overlaps(c.Movers[i].BoxAt(tick))) return true;
            return false;
        }

        int FindIndex(int x)
        {
            int lo = 0, hi = _starts.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) >> 1;
                if (_starts[mid] <= x) lo = mid;
                else hi = mid - 1;
            }
            return lo;
        }

        void EnsureChunkCount(int count)
        {
            while (_chunks.Count < count) Generate();
        }

        void Generate()
        {
            int index = _chunks.Count;
            IReadOnlyList<Chunk> opening = _opening ?? _library.Tutorial;
            Chunk chunk = index < opening.Count
                ? opening[index]
                : Pick(TierSchedule.MaxTierAt(_endX));
            _chunks.Add(chunk);
            _starts.Add(_endX);
            _endX += chunk.Length;
        }

        // tech spec 6: any chunk with tier <= max tier, chunks at the max tier weighted 2x.
        Chunk Pick(int maxTier)
        {
            Chunk[] pool = _library.Pool;
            int total = 0;
            for (int i = 0; i < pool.Length; i++) total += Weight(pool[i].Tier, maxTier);

            int r = _rng.NextInt(total);
            for (int i = 0; i < pool.Length; i++)
            {
                r -= Weight(pool[i].Tier, maxTier);
                if (r < 0) return pool[i];
            }
            return pool[pool.Length - 1];
        }

        static int Weight(int tier, int maxTier) => tier > maxTier ? 0 : tier == maxTier ? 2 : 1;
    }
}

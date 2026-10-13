using Echo.Core.Sim;

namespace Echo.Core.Courses
{
    /// <summary>
    /// FNV-1a over the parsed chunk library, every SimConfig value and the tier schedule (tech spec 6).
    /// Hashing parsed values rather than raw text means whitespace and line-ending changes don't discard ghosts.
    /// </summary>
    public static class ContentVersion
    {
        public static string Compute(ChunkLibrary library, in SimConfig cfg) => ComputeHash(library, cfg).ToString("x8");

        public static uint ComputeHash(ChunkLibrary library, in SimConfig cfg)
        {
            uint h = Fnv1a.OffsetBasis;

            Chunk[] all = library.All;
            h = Fnv1a.Add(h, all.Length);
            for (int i = 0; i < all.Length; i++) h = AddChunk(h, all[i]);

            var values = new int[SimConfig.ValueCount];
            cfg.WriteValues(values);
            for (int i = 0; i < values.Length; i++) h = Fnv1a.Add(h, values[i]);

            for (int t = 0; t <= TierSchedule.MaxTier; t++) h = Fnv1a.Add(h, TierSchedule.UnlockXFor(t));
            return h;
        }

        static uint AddChunk(uint h, Chunk c)
        {
            h = Fnv1a.Add(h, c.Id);
            h = Fnv1a.Add(h, c.Tier);
            h = Fnv1a.Add(h, c.Length);

            h = Fnv1a.Add(h, c.Gaps.Length);
            foreach (var g in c.Gaps) { h = Fnv1a.Add(h, g.X); h = Fnv1a.Add(h, g.W); }

            h = Fnv1a.Add(h, c.Spikes.Length);
            foreach (var s in c.Spikes) { h = Fnv1a.Add(h, s.X); h = Fnv1a.Add(h, s.W); h = Fnv1a.Add(h, s.H); }

            h = Fnv1a.Add(h, c.Ceilings.Length);
            foreach (var cl in c.Ceilings) { h = Fnv1a.Add(h, cl.X); h = Fnv1a.Add(h, cl.W); h = Fnv1a.Add(h, cl.Y); }

            h = Fnv1a.Add(h, c.Movers.Length);
            foreach (var m in c.Movers)
            {
                h = Fnv1a.Add(h, m.X); h = Fnv1a.Add(h, m.W); h = Fnv1a.Add(h, m.H);
                h = Fnv1a.Add(h, m.YMin); h = Fnv1a.Add(h, m.YMax);
                h = Fnv1a.Add(h, m.Period); h = Fnv1a.Add(h, m.Phase);
            }
            return h;
        }
    }
}

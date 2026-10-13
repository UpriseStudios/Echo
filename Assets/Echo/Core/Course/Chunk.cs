using System;

namespace Echo.Core.Courses
{
    /// <summary>One hand-made course segment, parsed from chunk JSON (tech spec 6). All x values are chunk-local.</summary>
    public sealed class Chunk
    {
        public readonly string Id;
        public readonly int Tier;
        public readonly int Length;
        public readonly Gap[] Gaps;
        public readonly Spike[] Spikes;
        public readonly Ceiling[] Ceilings;
        public readonly Mover[] Movers;

        public Chunk(string id, int tier, int length, Gap[] gaps, Spike[] spikes, Ceiling[] ceilings, Mover[] movers)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Tier = tier;
            Length = length;
            Gaps = gaps ?? Array.Empty<Gap>();
            Spikes = spikes ?? Array.Empty<Spike>();
            Ceilings = ceilings ?? Array.Empty<Ceiling>();
            Movers = movers ?? Array.Empty<Mover>();
        }
    }
}

using System;
using System.Collections.Generic;

namespace Echo.Core.Courses
{
    /// <summary>
    /// All chunks, sorted by id (ordinal) so selection never depends on file or load order.
    /// Tier 0 chunks are the fixed tutorial; tiers 1+ form the random pool (tech spec 6).
    /// </summary>
    public sealed class ChunkLibrary
    {
        public readonly Chunk[] All;
        public readonly Chunk[] Tutorial;
        public readonly Chunk[] Pool;

        public ChunkLibrary(IEnumerable<Chunk> chunks)
        {
            if (chunks == null) throw new ArgumentNullException(nameof(chunks));
            var all = new List<Chunk>(chunks);
            all.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));

            var tutorial = new List<Chunk>();
            var pool = new List<Chunk>();
            for (int i = 0; i < all.Count; i++)
            {
                if (i > 0 && all[i].Id == all[i - 1].Id)
                    throw new ChunkFormatException($"duplicate chunk id \"{all[i].Id}\"");
                (all[i].Tier == 0 ? tutorial : pool).Add(all[i]);
            }

            if (!pool.Exists(c => c.Tier == 1))
                throw new ChunkFormatException("library needs at least one tier 1 chunk to follow the tutorial");

            All = all.ToArray();
            Tutorial = tutorial.ToArray();
            Pool = pool.ToArray();
        }

        public static ChunkLibrary FromJson(IEnumerable<string> jsonFiles) =>
            new ChunkLibrary(ChunkParser.ParseAll(jsonFiles));
    }
}

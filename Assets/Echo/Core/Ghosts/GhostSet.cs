using System;
using System.Collections.Generic;

namespace Echo.Core.Ghosts
{
    /// <summary>The saved ghosts, kept in ascending RunIndex order (oldest first).</summary>
    public sealed class GhostSet
    {
        readonly List<GhostRecord> _ghosts = new List<GhostRecord>(32);

        public int Count => _ghosts.Count;
        public GhostRecord this[int index] => _ghosts[index];
        public IReadOnlyList<GhostRecord> All => _ghosts;

        /// <summary>Derived from kills on demand, so it always reflects the latest death (tech spec 7).</summary>
        public GhostRecord Nemesis => NemesisRules.Select(_ghosts);

        public bool Contains(GhostRecord g) => _ghosts.Contains(g);

        public void Add(GhostRecord g)
        {
            if (g == null) throw new ArgumentNullException(nameof(g));
            int i = _ghosts.Count;
            while (i > 0 && _ghosts[i - 1].RunIndex > g.RunIndex) i--;
            if (i > 0 && _ghosts[i - 1].RunIndex == g.RunIndex)
                throw new InvalidOperationException($"ghost for run {g.RunIndex} already exists");
            _ghosts.Insert(i, g);
        }

        public bool Remove(GhostRecord g) => _ghosts.Remove(g);

        public void Clear() => _ghosts.Clear();

        /// <summary>Removes the oldest ghosts until at most <paramref name="cap"/> remain. Returns the evicted ones.</summary>
        public List<GhostRecord> ApplyCap(int cap)
        {
            var evicted = new List<GhostRecord>();
            GhostRecord nemesis = Nemesis;
            while (_ghosts.Count > cap)
            {
                // tech spec 7: the Nemesis is exempt from the cap; the oldest other ghost goes instead.
                int victim = _ghosts[0] == nemesis ? 1 : 0;
                evicted.Add(_ghosts[victim]);
                _ghosts.RemoveAt(victim);
            }
            return evicted;
        }
    }
}

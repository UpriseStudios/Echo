using System.Collections.Generic;

namespace Echo.Core.Modes
{
    /// <summary>
    /// The Community Graveyard (modes doc): one 50 m stretch of the week's course, how many runs have died there, and
    /// the first few of those runs (by their keys), from which the day's ghosts are chosen.
    /// </summary>
    public sealed class SectionTally
    {
        public int Index;
        public int Deaths;
        public List<string> Runs = new List<string>();
    }
}

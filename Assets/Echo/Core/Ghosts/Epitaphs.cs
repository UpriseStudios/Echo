using System.Globalization;
using Echo.Core.Rules;

namespace Echo.Core.Ghosts
{
    /// <summary>
    /// The short line on a gravestone (skins addendum), e.g. "Run #12 · jumped too early" or "Killed by The
    /// Seventh". Picked from the record alone, so the same grave always says the same thing.
    /// </summary>
    public static class Epitaphs
    {
        static readonly string[] Falls = { "jumped too early", "fell short", "took the long way down", "misjudged it" };
        static readonly string[] Hazards = { "met a spike", "should have jumped", "found the sharp end", "spiked" };
        static readonly string[] Copycats = { "met itself", "copied to death", "out-copied", "its own worst enemy" };
        static readonly string[] Unknown = { "rests here", "gone but not forgotten" };

        public static string For(GhostRecord record)
        {
            string line = Line(record);
            return KilledByAGhost(record) ? line : Title(record) + " · " + line;
        }

        /// <summary>"Run #12", the name a headstone carries.</summary>
        public static string Title(GhostRecord record) => "Run #" + record.RunIndex.ToString(CultureInfo.InvariantCulture);

        /// <summary>The epitaph without the run number, e.g. "jumped too early" or "Killed by The Seventh".</summary>
        public static string Line(GhostRecord record)
        {
            if (KilledByAGhost(record)) return "Killed by " + NemesisNames.For(record.KillerRunIndex);
            string[] lines = record.Cause == DeathCause.Fall ? Falls
                : record.Cause == DeathCause.Hazard ? Hazards
                : record.Cause == DeathCause.Copycat ? Copycats
                : Unknown;
            int pick = ((record.RunIndex * 7 + record.DeathTick) % lines.Length + lines.Length) % lines.Length;
            return lines[pick];
        }

        static bool KilledByAGhost(GhostRecord record) => record.Cause == DeathCause.Ghost && record.KillerRunIndex > 0;
    }
}

using System.Collections.Generic;

namespace Echo.Core.Ghosts
{
    public static class NemesisRules
    {
        public const int MinKills = 2;

        // tech spec 7: most kills, minimum 2; ties go to the most recent killer (highest LastKillRun).
        public static GhostRecord Select(IReadOnlyList<GhostRecord> ghosts)
        {
            GhostRecord best = null;
            for (int i = 0; i < ghosts.Count; i++)
            {
                GhostRecord g = ghosts[i];
                if (g.Kills < MinKills) continue;
                if (best == null || g.Kills > best.Kills || (g.Kills == best.Kills && g.LastKillRun > best.LastKillRun))
                    best = g;
            }
            return best;
        }
    }
}

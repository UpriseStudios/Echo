using Echo.Core.Ghosts;
using Echo.Core.Rules;

namespace Echo.Game.Flow
{
    /// <summary>Text for the death screen's killer line (tech spec 14).</summary>
    public static class DeathScreenText
    {
        public static string KillerLine(RunSimulation run, GhostRecord nemesis)
        {
            if (run.Cause == DeathCause.Copycat) return "Your Copycat got you";
            if (run.Cause != DeathCause.Ghost || run.Killer == null) return "";
            GhostRecord k = run.Killer;
            return k == nemesis
                ? $"{NemesisNames.For(k.RunIndex)} got you again ({k.Kills})"
                : $"Killed by: run #{k.RunIndex}";
        }
    }
}

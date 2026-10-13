using Echo.Core.Rules;

namespace Echo.Core.Challenges
{
    /// <summary>Turns a finished run into a challenge to send. The sender's name and the content version are added when it is sent.</summary>
    public static class ChallengeRuns
    {
        public static Challenge FromRun(RunSimulation run) => new Challenge
        {
            Seed = run.Course.Seed,
            SpeedPct = run.SpeedPct,
            Distance = run.Distance,
            Record = run.ToGhostRecord(),
        };

        /// <summary>A saved reel sent as a challenge: its own course, speed and content version.</summary>
        public static Challenge FromReel(Echo.Core.Reels.Reel reel) => new Challenge
        {
            Seed = reel.Seed,
            SpeedPct = reel.SpeedPct,
            Distance = reel.Distance,
            ContentVersion = reel.ContentVersion,
            Record = reel.Run.Clone(),
        };

        /// <summary>
        /// Any finished run can be sent except a duel: a challenge run too, which is how "send one back" works (it was
        /// played on the challenger's course, so the reply races them on their own course).
        /// </summary>
        public static bool CanSend(RunSimulation run) =>
            run != null && run.IsDead && !run.Options.Duel && run.Distance > 0;
    }
}

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

        /// <summary>
        /// A reel sent as a full challenge (balance addendum): its course, speed, content version and the ghosts its
        /// run started with. A reel holds everything a fair challenge needs.
        /// </summary>
        public static Challenge FromReel(Echo.Core.Reels.Reel reel) => new Challenge
        {
            Seed = reel.Seed,
            SpeedPct = reel.SpeedPct,
            Distance = reel.Distance,
            Score = reel.Score,
            ContentVersion = reel.ContentVersion,
            Record = reel.Run.Clone(),
            Snapshot = reel,
        };

        /// <summary>A full challenge carries no more ghosts than the cap allows.</summary>
        public static bool CanSendFull(Echo.Core.Reels.Reel reel) =>
            reel != null && !reel.Options.Duel && !reel.Options.Daily && reel.Ghosts.Count <= Challenge.MaxGhosts;

        /// <summary>
        /// Any finished run can be sent except a duel: a challenge run too, which is how "send one back" works (it was
        /// played on the challenger's course, so the reply races them on their own course).
        /// </summary>
        public static bool CanSend(RunSimulation run) =>
            run != null && run.IsDead && !run.Options.Duel && !run.Options.Daily && run.Distance > 0;
    }
}

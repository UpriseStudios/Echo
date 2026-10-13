using Echo.Core.Courses;
using Echo.Core.Net;
using Echo.Core.Sim;

namespace Echo.Core.Reels
{
    /// <summary>
    /// Plays a run back to its end and reports what happened (version 2.0). The game and the Cloud Code module call
    /// this same code, so a run replays to the same distance on both. A run that never ends stops at the tick limit.
    /// </summary>
    public static class RunReplay
    {
        public static ReplayResult Replay(Reel reel, Course course, in SimConfig cfg)
        {
            var player = new ReelPlayer(reel, course, cfg);
            while (!player.Finished && player.Run.Tick < ReelPacket.MaxTicks)
            {
                if (player.ContinueDue) player.Continue();
                player.Step();
            }
            var run = player.Run;
            return new ReplayResult(run.Distance, run.Score, run.DeathTick, run.NearMisses, run.Cause, player.Finished);
        }
    }
}

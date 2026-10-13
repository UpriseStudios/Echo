using Echo.Core.Ghosts;
using Echo.Core.Reels;
using Echo.Core.Rules;

namespace Echo.Core.Net
{
    /// <summary>
    /// A run as it travels to the server (version 2.0): a <see cref="Reel"/>, which already holds everything a replay
    /// needs (inputs only, the ghosts at its start, its options, speed and seed, and the ticks of Shield, Phase and
    /// Continue). The client's claimed score and distance travel too, but the server works its own out by replaying.
    /// Decoding refuses anything malformed or past the limits, so a hostile payload never reaches the simulation.
    /// </summary>
    public static class ReelPacket
    {
        // 2 (version 2.0, phase 4): the daily option, and each ghost's leader flag and name.
        const byte Format = 2;

        /// <summary>Limits checked before any replay (Daily Haunt doc: sanity limits on taps, ticks and size).</summary>
        public const int MaxGhosts = 40;
        public const int MaxTicks = 216_000;      // an hour of play at 60 Hz
        public const int MaxChars = 512 * 1024;

        public static string Encode(Reel reel)
        {
            var w = new WireWriter();
            w.Byte(Format);
            w.Text(reel.ContentVersion);
            w.UInt32(reel.Seed);
            w.Var((uint)reel.SpeedPct);
            w.Var((uint)reel.RunIndex);
            RunOptions o = reel.Options;
            w.Byte((byte)((o.Bounties ? 1 : 0) | (o.Challenge ? 2 : 0) | (o.Duel ? 4 : 0) | (o.Daily ? 8 : 0)));
            w.Var((uint)o.Twist);
            w.Int(o.TwistWeek);
            w.Int(reel.ShieldTick);
            w.Int(reel.PhaseTick);
            w.Int(reel.ContinueTick);
            w.Bool(reel.ExtraLife);
            w.Var((uint)reel.Score);
            w.Var((uint)reel.Distance);
            w.Var((uint)reel.NearMisses);
            w.Var((uint)reel.GhostsAtStart);
            w.Bool(reel.NewBest);
            w.Text(reel.SavedOn);
            Record(w, reel.Run);
            w.Var((uint)reel.Ghosts.Count);
            foreach (GhostRecord g in reel.Ghosts) Record(w, g);
            return w.ToBase64Url();
        }

        public static bool TryDecode(string payload, out Reel reel)
        {
            reel = null;
            if (!WireReader.TryOpen(payload, MaxChars, MaxTicks, out WireReader r)) return false;
            if (!r.Byte(out byte format) || format != Format) return false;
            var x = new Reel();
            if (!r.Text(out x.ContentVersion) || !r.UInt32(out x.Seed)) return false;
            if (!r.Count(1000, out x.SpeedPct) || x.SpeedPct == 0 || !r.Count(int.MaxValue, out x.RunIndex)) return false;
            if (!r.Byte(out byte flags) || flags > 15 || !r.Count(16, out int twist) || !r.Int(out int twistWeek)) return false;
            x.Options = new RunOptions
            {
                Bounties = (flags & 1) != 0,
                Challenge = (flags & 2) != 0,
                Duel = (flags & 4) != 0,
                Daily = (flags & 8) != 0,
                Twist = (Twist)twist,
                TwistWeek = twistWeek,
            };
            if (!System.Enum.IsDefined(typeof(Twist), x.Options.Twist)) return false;
            if (!r.Int(out x.ShieldTick) || !r.Int(out x.PhaseTick) || !r.Int(out x.ContinueTick) || !r.Bool(out x.ExtraLife)) return false;
            if (!InTicks(x.ShieldTick) || !InTicks(x.PhaseTick) || !InTicks(x.ContinueTick)) return false;
            if (!r.Count(int.MaxValue, out x.Score) || !r.Count(int.MaxValue, out x.Distance)) return false;
            if (!r.Count(int.MaxValue, out x.NearMisses) || !r.Count(MaxGhosts, out x.GhostsAtStart)) return false;
            if (!r.Bool(out x.NewBest) || !r.Text(out x.SavedOn)) return false;
            if (!Record(r, out x.Run)) return false;
            if (!r.Count(MaxGhosts, out int ghosts)) return false;
            int lastRunIndex = 0;
            for (int i = 0; i < ghosts; i++)
            {
                if (!Record(r, out GhostRecord g)) return false;
                // Oldest first and no repeats, as a GhostSet needs them.
                if (g.RunIndex <= lastRunIndex) return false;
                lastRunIndex = g.RunIndex;
                x.Ghosts.Add(g);
            }
            if (!r.AtEnd) return false;
            reel = x;
            return true;
        }

        static bool InTicks(int tick) => tick >= -1 && tick <= MaxTicks;

        static void Record(WireWriter w, GhostRecord g)
        {
            w.Var((uint)g.RunIndex);
            w.Text(g.SkinId);
            w.Var((uint)g.SpeedPct);
            w.Var((uint)g.DeathTick);
            w.Int(g.DeathX);
            w.Var((uint)g.Cause);
            w.Var((uint)g.KillerRunIndex);
            w.Var((uint)g.Kills);
            w.Var((uint)g.LastKillRun);
            w.Bool(g.Leader);
            w.Text(g.Name);
            w.Ticks(g.TapTicks);
            w.Ticks(g.ReleaseTicks);
            w.Ticks(g.ReviveTicks);
        }

        static bool Record(WireReader r, out GhostRecord g)
        {
            g = new GhostRecord();
            if (!r.Count(int.MaxValue, out g.RunIndex) || !r.Text(out g.SkinId)) return false;
            if (!r.Count(1000, out g.SpeedPct) || g.SpeedPct == 0 || !r.Count(MaxTicks, out g.DeathTick) || !r.Int(out g.DeathX)) return false;
            if (!r.Count((int)DeathCause.Copycat, out int cause)) return false;
            g.Cause = (DeathCause)cause;
            if (!r.Count(int.MaxValue, out g.KillerRunIndex) || !r.Count(int.MaxValue, out g.Kills) || !r.Count(int.MaxValue, out g.LastKillRun)) return false;
            if (!r.Bool(out g.Leader) || !r.Text(out g.Name)) return false;
            return r.Ticks(out g.TapTicks) && r.Ticks(out g.ReleaseTicks) && r.Ticks(out g.ReviveTicks);
        }
    }
}

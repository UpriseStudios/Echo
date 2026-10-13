using System.Collections.Generic;

namespace Echo.Core.Ghosts
{
    /// <summary>Collects the tap ticks, release ticks, revive ticks and final death tick of the current run (tech spec 7).</summary>
    public sealed class RunRecorder
    {
        // Sized for 15 minutes of tapping twice a second, so the list never grows (and allocates) mid-run (tech spec 18).
        readonly List<int> _taps = new List<int>(2048);
        readonly List<int> _revives = new List<int>(16);
        readonly List<int> _releases = new List<int>(2048);

        public int DeathTick { get; private set; } = -1;
        public int TapCount => _taps.Count;
        public int ReviveCount => _revives.Count;

        public void Reset()
        {
            _taps.Clear();
            _revives.Clear();
            _releases.Clear();
            DeathTick = -1;
        }

        public void RecordTap(int tick)
        {
            if (_taps.Count > 0 && _taps[_taps.Count - 1] >= tick) return;
            _taps.Add(tick);
        }

        public void RecordRevive(int tick) => _revives.Add(tick);

        public void RecordRelease(int tick)
        {
            if (_releases.Count > 0 && _releases[_releases.Count - 1] >= tick) return;
            _releases.Add(tick);
        }

        public void RecordDeath(int tick) => DeathTick = tick;

        public GhostRecord ToRecord(int runIndex, int speedPct) => new GhostRecord
        {
            RunIndex = runIndex,
            TapTicks = _taps.ToArray(),
            ReviveTicks = _revives.ToArray(),
            ReleaseTicks = _releases.ToArray(),
            DeathTick = DeathTick,
            SpeedPct = speedPct,
        };
    }
}

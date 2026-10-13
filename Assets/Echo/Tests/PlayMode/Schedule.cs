using System.Collections.Generic;

namespace Echo.Tests.PlayMode
{
    /// <summary>A scripted finger: a tap and whether the finger is down on each tick. Test support only.</summary>
    internal sealed class Schedule
    {
        public readonly bool[] Taps, Held;

        public Schedule(int ticks)
        {
            Taps = new bool[ticks];
            Held = new bool[ticks];
        }

        public int Length => Taps.Length;
        public bool TapAt(int t) => t < Taps.Length && Taps[t];
        public bool HeldAt(int t) => t < Held.Length && Held[t];

        public int[] TapTicks
        {
            get
            {
                var list = new List<int>();
                for (int t = 0; t < Taps.Length; t++) if (Taps[t]) list.Add(t);
                return list.ToArray();
            }
        }

        /// <summary>The ticks the finger lifts, as RunRecorder records them for a ghost.</summary>
        public int[] ReleaseTicks
        {
            get
            {
                var list = new List<int>();
                for (int t = 0; t < Taps.Length; t++)
                    if ((Taps[t] || (t > 0 && Held[t - 1])) && !Held[t]) list.Add(t);
                return list.ToArray();
            }
        }
    }
}

using Echo.Core.Sim;

namespace Echo.Core.Rules
{
    /// <summary>One Copycat encounter (weekly twist). Views read it; only RunSimulation writes it.</summary>
    public struct CopycatState
    {
        /// <summary>Planned: the encounter below is scheduled. Active: it is on the course, running at the player.</summary>
        public bool Planned;
        public bool Active;

        /// <summary>The chunk at whose start the player meets it.</summary>
        public int Chunk;

        /// <summary>The tick it appears and starts copying taps, the tick it reaches the player, and the tick it leaves.</summary>
        public int CopyFrom;
        public int MeetTick;
        public int EndTick;

        /// <summary>Its world x when it appears. It runs left, so its x is StartX - Body.X.</summary>
        public int StartX;

        /// <summary>Its own run on a flat phantom floor, replaying the player's taps Copycat.DelayTicks late.</summary>
        public RunnerState Body;
        public RunnerState PrevBody;

        /// <summary>The player got past it; counted once as a near-miss.</summary>
        public bool Passed;

        public int X => StartX - Body.X;
        public int PrevX => StartX - PrevBody.X;
    }
}

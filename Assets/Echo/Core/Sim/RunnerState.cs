namespace Echo.Core.Sim
{
    /// <summary>Complete state of one runner, player or ghost (tech spec 5). Bottom-centre anchored.</summary>
    public struct RunnerState
    {
        public int X;
        public int Y;
        public int Vy;
        public int LastSafeX;
        public int Coyote;
        public int Buffer;
        public int Invuln;
        public int AirJumpsLeft;

        /// <summary>Ticks since the current jump started, counting up to SimConfig.HopHoldTicks and stopping there.</summary>
        public int JumpAge;
        public bool Grounded;
        public bool Alive;

        public static RunnerState AtStart() => new RunnerState { Grounded = true, Alive = true };

        /// <summary>FNV-1a over every field, for determinism checks.</summary>
        public uint Hash()
        {
            uint h = Fnv1a.OffsetBasis;
            h = Fnv1a.Add(h, X);
            h = Fnv1a.Add(h, Y);
            h = Fnv1a.Add(h, Vy);
            h = Fnv1a.Add(h, LastSafeX);
            h = Fnv1a.Add(h, Coyote);
            h = Fnv1a.Add(h, Buffer);
            h = Fnv1a.Add(h, Invuln);
            h = Fnv1a.Add(h, AirJumpsLeft);
            h = Fnv1a.Add(h, JumpAge);
            h = Fnv1a.Add(h, Grounded ? 1 : 0);
            h = Fnv1a.Add(h, Alive ? 1 : 0);
            return h;
        }
    }
}

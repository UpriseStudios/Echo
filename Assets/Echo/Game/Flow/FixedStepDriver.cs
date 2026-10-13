namespace Echo.Game.Flow
{
    /// <summary>
    /// Turns frame time into whole 60 Hz sim ticks (tech spec 3), at most 5 per frame.
    /// Time beyond the cap is dropped rather than replayed later.
    /// </summary>
    public sealed class FixedStepDriver
    {
        public const int TickRate = 60;
        public const int MaxTicksPerFrame = 5;
        const double TickSeconds = 1.0 / TickRate;

        double _accumulator;

        /// <summary>How far between the last two ticks the current frame is, 0..1, for view interpolation.</summary>
        public float Alpha => (float)(_accumulator / TickSeconds);

        public int Advance(double deltaSeconds)
        {
            if (deltaSeconds > 0) _accumulator += deltaSeconds;
            int ticks = (int)(_accumulator / TickSeconds);
            if (ticks > MaxTicksPerFrame)
            {
                ticks = MaxTicksPerFrame;
                _accumulator = 0;
                return ticks;
            }
            _accumulator -= ticks * TickSeconds;
            return ticks;
        }

        public void Reset() => _accumulator = 0;
    }
}

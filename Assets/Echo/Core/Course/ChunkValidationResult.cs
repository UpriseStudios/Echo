namespace Echo.Core.Courses
{
    public readonly struct ChunkValidationResult
    {
        public readonly bool Clearable;
        public readonly int FailedSpeed;
        public readonly int FailedPhase;
        public readonly int FailedEntryX;
        public readonly int StatesExplored;

        public ChunkValidationResult(bool clearable, int failedSpeed, int failedPhase, int failedEntryX, int statesExplored)
        {
            Clearable = clearable;
            FailedSpeed = failedSpeed;
            FailedPhase = failedPhase;
            FailedEntryX = failedEntryX;
            StatesExplored = statesExplored;
        }

        public override string ToString() => Clearable
            ? $"clearable ({StatesExplored} states)"
            : $"NOT clearable at speed {FailedSpeed} sub/tick, entry x {FailedEntryX}, entry phase {FailedPhase}";
    }
}

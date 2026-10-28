namespace Echo.Core.Modes
{
    /// <summary>
    /// When a mode runs (modes doc: a switch and UTC start and end), in whole seconds since 1970 so Core needs no clock.
    /// A mode may have several windows (One Life: Christmas Day, then Sundays).
    /// </summary>
    public sealed class ModeWindow
    {
        public string Id = "";
        public bool Enabled;

        /// <summary>Open from Start (inclusive) to End (exclusive).</summary>
        public long Start, End;

        /// <summary>The card stays on the Modes screen until then after closing (a result to reveal); 0 for not.</summary>
        public long ShowUntil;

        /// <summary>Open only on these UTC weekdays inside the window: bit 0 Sunday to bit 6 Saturday; 0 for every day.</summary>
        public int Weekdays;
    }
}

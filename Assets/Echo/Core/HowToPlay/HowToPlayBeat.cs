namespace Echo.Core.HowToPlay
{
    /// <summary>A caption over a how-to-play clip, between two ticks of its run, optionally in slow motion.</summary>
    public sealed class HowToPlayBeat
    {
        public int From;
        public int To;
        public string Text = "";
        public bool SlowMo;

        public bool ActiveAt(int tick) => tick >= From && tick < To;
    }
}

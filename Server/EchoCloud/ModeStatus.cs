namespace EchoCloud
{
    /// <summary>One mode on the Modes screen: open now, and the seconds until it closes (or opens; -1 never).</summary>
    public sealed class ModeStatus
    {
        public string Id { get; set; } = "";
        public bool Open { get; set; }
        public long SecondsLeft { get; set; }
    }
}

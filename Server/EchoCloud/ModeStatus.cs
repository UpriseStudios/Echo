namespace EchoCloud
{
    /// <summary>One mode on the Modes screen: open now, and the seconds until it closes (or opens; -1 never).</summary>
    public sealed class ModeStatus
    {
        public string Id { get; set; } = "";
        public bool Open { get; set; }
        public long SecondsLeft { get; set; }
        /// <summary>Ghost Town: how many are playing it (today, this week or this season by mode); -1 unknown.</summary>
        public int Players { get; set; } = -1;
    }
}

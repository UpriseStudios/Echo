namespace Echo.Services.Backend
{
    /// <summary>One mode on show (mirrors Server/EchoCloud/ModeStatus): open now, and seconds until it closes or opens.</summary>
    public sealed class ModeState
    {
        public string Id = "";
        public bool Open;

        /// <summary>Open: until it closes. Closed: until it opens again, or -1 if it won't.</summary>
        public long SecondsLeft;

        /// <summary>Ghost Town: how many are playing it (today, this week or this season by mode); -1 unknown.</summary>
        public int Players = -1;
    }
}

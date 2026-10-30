namespace Echo.Services.Backend
{
    /// <summary>A recent Ghost Duel (mirrors Server/EchoCloud/RankedResultEntry).</summary>
    public sealed class RankedResultRow
    {
        public string Opponent = "";

        /// <summary>"won", "lost" or "draw".</summary>
        public string Outcome = "";
        public int Change, Distance, OpponentDistance;
    }
}

namespace EchoCloud
{
    /// <summary>The answer to DeleteMyData: the player's online data is gone (the game then deletes their account).</summary>
    public sealed class DeleteMyDataResponse
    {
        public bool Ok { get; set; }
        public string Error { get; set; } = "";

        /// <summary>
        /// Today's live leaderboard score was removed (or there was none). False when the server has no Leaderboards
        /// admin key; archived boards can't be changed by anyone and keep only the random player id, the
        /// auto-generated name and the distance.
        /// </summary>
        public bool LiveScoreDeleted { get; set; }

        /// <summary>Why the live score stayed: "no-admin-key", or the admin API's HTTP status. Empty when it went.</summary>
        public string LiveScoreNote { get; set; } = "";
    }
}

namespace Echo.Services.Backend
{
    /// <summary>The answer to Settings › Delete online data (mirrors Server/EchoCloud/DeleteMyDataResponse).</summary>
    public sealed class DeleteOnlineDataResult
    {
        public bool Ok;

        /// <summary>"offline" when the server couldn't be reached, or the server's reason.</summary>
        public string Error = "";

        /// <summary>Today's live leaderboard score went too (false when the server has no admin key).</summary>
        public bool LiveScoreDeleted;

        public static DeleteOnlineDataResult Failed(string error) => new DeleteOnlineDataResult { Error = error };
    }
}

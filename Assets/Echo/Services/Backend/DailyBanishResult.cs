namespace Echo.Services.Backend
{
    /// <summary>The server's answer to banishing one of your own daily ghosts (mirrors Server/EchoCloud/BanishDailyGhostResponse).</summary>
    public sealed class DailyBanishResult
    {
        public bool Ok;

        /// <summary>"offline", "wrong-day", "none-left", "not-yours" or "disabled".</summary>
        public string Error = "";
        public int BanishesLeft;

        public static DailyBanishResult Failed(string error) => new DailyBanishResult { Error = error };
    }
}

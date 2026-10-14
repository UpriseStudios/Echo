namespace EchoCloud
{
    /// <summary>The answer to BanishDailyGhost: one of the player's own daily ghosts is gone.</summary>
    public sealed class BanishDailyGhostResponse
    {
        public bool Ok { get; set; }

        /// <summary>"wrong-day", "none-left", "not-yours" (not one of your own ghosts, or a leader), "disabled".</summary>
        public string Error { get; set; } = "";
        public int BanishesLeft { get; set; }
    }
}

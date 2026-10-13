namespace Echo.Game.Flow
{
    /// <summary>Game flow states (tech spec 9).</summary>
    public enum GameState
    {
        Boot,
        Title,
        Running,
        Dying,
        DeathScreen,
        Paused,
        Menu,
        Consent,
        /// <summary>A saved reel playing back (highlights reel, a user revision).</summary>
        Replay,
    }
}

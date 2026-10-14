namespace Echo.Services.Ads
{
    /// <summary>Rewarded placement ids (tech spec 15). Both are death-screen only.</summary>
    public static class AdPlacements
    {
        public const string BanishGhost = "banish_ghost";
        public const string Continue = "continue";

        /// <summary>Balance addendum: clears the 10 deadliest ghosts at a full graveyard. Death screen only.</summary>
        public const string BanishTen = "banish_ten";

        /// <summary>Watched from the Skins screen towards the ad-unlocked characters (never during a run).</summary>
        public const string UnlockCharacter = "unlock_character";

        /// <summary>Earns a power-up (a user revision): from the title, the death screen or the Skins screen, never during a run.</summary>
        public const string PowerUp = "power_up";

        /// <summary>Daily Haunt (a user decision): banishes one of your own daily ghosts, 3 a day. Daily death screen only.</summary>
        public const string DailyBanish = "daily_banish";

        /// <summary>Daily Haunt: watch someone else's run on today's board, from the lobby.</summary>
        public const string WatchRun = "watch_run";
    }
}

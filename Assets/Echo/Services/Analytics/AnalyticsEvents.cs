namespace Echo.Services.Analytics
{
    /// <summary>Event names from tech spec 15. Each needs a matching custom event schema in the UGS dashboard.</summary>
    public static class AnalyticsEvents
    {
        public const string RunStart = "run_start";
        public const string RunEnd = "run_end";
        public const string NewBest = "new_best";
        public const string BanishOffered = "banish_offered";
        public const string BanishUsed = "banish_used";
        public const string ContinueOffered = "continue_offered";
        public const string ContinueUsed = "continue_used";
        public const string BanishTenOffered = "banish_ten_offered";   // balance addendum
        public const string BanishTenUsed = "banish_ten_used";
        public const string DailyOpened = "daily_opened";             // version 2.0, the Daily Haunt
        public const string DailyRunEnd = "daily_run_end";
        public const string DailyNewBest = "daily_new_best";
        public const string DailySubmitFailed = "daily_submit_failed";
        public const string DailyBanishUsed = "daily_banish_used";
        public const string DailyRunWatched = "daily_run_watched";
        public const string IapPurchase = "iap_purchase";
        public const string UnlockAdWatched = "unlock_ad_watched";   // addition: ad-unlocked characters
        // Value-adds doc.
        public const string BountyClaimed = "bounty_claimed";
        public const string TwistActive = "twist_active";
        public const string DuelOffered = "duel_offered";
        public const string DuelResult = "duel_result";
        public const string GalleryViewed = "gallery_viewed";
        public const string ChallengeSent = "challenge_sent";
        public const string ChallengeOpened = "challenge_opened";
        public const string ChallengeResult = "challenge_result";
        public const string PowerUpEarned = "power_up_earned";
        public const string PowerUpUsed = "power_up_used";
        public const string SessionStart = "session_start";
        public const string SessionEnd = "session_end";
    }
}

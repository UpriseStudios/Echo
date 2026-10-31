using System.Threading.Tasks;

namespace Echo.Services.PlayGames
{
    /// <summary>
    /// Google Play Games Services v2 (Play's Level Up guidelines): sign-in, achievements, Game Stats and one Saved
    /// Game holding the save. Real only on Android once Play Games is configured (StoreListing/PlayGames/README.md);
    /// everywhere else a mock that is not available, so the game behaves as before.
    /// </summary>
    public interface IPlayGamesService
    {
        /// <summary>Play Games can be used on this device (Android, configured).</summary>
        bool Available { get; }
        bool SignedIn { get; }
        string Status { get; }

        /// <summary>Signs in: silently at start, or with Play Games' own sign-in screen when the player asks.</summary>
        Task<bool> SignInAsync(bool interactive);

        /// <summary>A one-use code for Unity Authentication's Play Games sign-in; null when unavailable.</summary>
        Task<string> GetServerAuthCodeAsync();

        /// <summary>Unlocks an Echo achievement on Play (counted ones are set to at least <paramref name="progress"/> steps).</summary>
        void ReportAchievement(string achievementId, int progress, int target);

        /// <summary>Game Stats: one finished run.</summary>
        void RecordRun(int distance, int score, int ghostsPassed, int nearMisses);

        /// <summary>Game Stats: the progression stat (best distance in metres).</summary>
        void RecordBestDistance(int metres);

        /// <summary>Sends buffered Game Stats events.</summary>
        void FlushEvents();

        /// <summary>The Saved Game's bytes; null when there is none or it couldn't be read.</summary>
        Task<byte[]> LoadCloudSaveAsync();

        /// <summary>Writes the Saved Game. Runs played is its play time, so Play keeps the one with most runs.</summary>
        Task<bool> WriteCloudSaveAsync(byte[] data, string description, int runsPlayed);

        void ShowAchievements();
    }
}

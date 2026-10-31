using System;

namespace Echo.Services.PlayGames
{
    /// <summary>An Echo achievement id and its Play Games id (written by Echo > Play Games > Import Resources).</summary>
    [Serializable]
    public sealed class PlayGamesAchievement
    {
        public string EchoId;
        public string PlayId;
    }
}

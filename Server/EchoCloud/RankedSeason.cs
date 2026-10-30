using System.Collections.Generic;

namespace EchoCloud
{
    /// <summary>A Ghost Duels season (modes doc): its number, UTC start and end (seconds since 1970) and its 10 arena seeds.</summary>
    public sealed class RankedSeason
    {
        public int Number { get; set; }
        public long Start { get; set; }
        public long End { get; set; }
        public List<uint> Arenas { get; set; } = new List<uint>();

        public bool Holds(long now) => now >= Start && now < End;

        /// <summary>The season's leaderboard, ranking players by rating (Server/Config/duel_season_N.lb).</summary>
        public string Board => "duel_season_" + Number;
    }
}

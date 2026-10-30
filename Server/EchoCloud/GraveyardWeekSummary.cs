namespace EchoCloud
{
    /// <summary>The Community Graveyard's end-of-week summary (modes doc): the deadliest spot, the champion and how you did.</summary>
    public sealed class GraveyardWeekSummary
    {
        /// <summary>The week's Monday (yyyy-MM-dd).</summary>
        public string Week { get; set; } = "";

        /// <summary>The deadliest 50 m section: where it starts, and how many runners died in it.</summary>
        public int DeadliestMetres { get; set; }
        public int DeadliestDeaths { get; set; }

        /// <summary>Deaths in the whole week.</summary>
        public int Size { get; set; }
        public string Champion { get; set; } = "";
        public int ChampionTotal { get; set; }
        public int Rank { get; set; }
        public int Total { get; set; }
        public int DaysPlayed { get; set; }
    }
}

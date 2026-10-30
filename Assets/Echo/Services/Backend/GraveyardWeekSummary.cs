namespace Echo.Services.Backend
{
    /// <summary>Last week's Community Graveyard (mirrors Server/EchoCloud/GraveyardWeekSummary).</summary>
    public sealed class GraveyardWeekSummary
    {
        public string Week = "";
        public int DeadliestMetres, DeadliestDeaths, Size;
        public string Champion = "";
        public int ChampionTotal, Rank, Total, DaysPlayed;
    }
}

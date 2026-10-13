namespace Echo.Services.Backend
{
    /// <summary>A line of the daily lobby's board (mirrors Server/EchoCloud/DailyBoardEntry).</summary>
    public sealed class DailyBoardRow
    {
        public int Rank;
        public string Name = "";
        public int Distance;
        public bool IsYou;
    }
}

namespace Echo.Services.Backend
{
    /// <summary>A line of the daily lobby's board (mirrors Server/EchoCloud/DailyBoardEntry).</summary>
    public sealed class DailyBoardRow
    {
        public int Rank;
        public string Name = "";
        public int Distance;
        public bool IsYou;

        /// <summary>Badges beside the name (modes doc; a user decision).</summary>
        public System.Collections.Generic.List<string> Badges = new System.Collections.Generic.List<string>();
    }
}

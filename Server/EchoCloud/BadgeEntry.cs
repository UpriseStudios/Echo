namespace EchoCloud
{
    /// <summary>A badge shown beside a player's name on boards and in their profile, until a time (seconds since 1970 UTC).</summary>
    public sealed class BadgeEntry
    {
        public string Id { get; set; } = "";
        public long Until { get; set; }
    }
}

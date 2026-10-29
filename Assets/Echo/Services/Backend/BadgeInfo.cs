namespace Echo.Services.Backend
{
    /// <summary>A badge the server has granted, showing until a time (seconds since 1970 UTC).</summary>
    public sealed class BadgeInfo
    {
        public string Id = "";
        public long Until;
    }
}

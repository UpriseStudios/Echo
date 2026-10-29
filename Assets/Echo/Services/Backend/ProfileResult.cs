using System.Collections.Generic;

namespace Echo.Services.Backend
{
    /// <summary>The player's online profile (mirrors Server/EchoCloud/ProfileResponse): server-granted rewards and their side.</summary>
    public sealed class ProfileResult
    {
        public bool Ok;
        public string Error = "";
        public List<string> Cosmetics = new List<string>();
        public List<BadgeInfo> Badges = new List<BadgeInfo>();
        public string Team = "";

        public static ProfileResult Failed(string error) => new ProfileResult { Error = error };
    }
}

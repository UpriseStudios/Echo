using System.Collections.Generic;

namespace EchoCloud
{
    /// <summary>
    /// The player's online profile (modes doc; a user decision: shown at the top of Stats): rewards the server has
    /// granted (cosmetics and badges, kept for good) and the event team they're on now.
    /// </summary>
    public sealed class ProfileResponse
    {
        public bool Ok { get; set; }
        public string Error { get; set; } = "";
        public List<string> Cosmetics { get; set; } = new List<string>();

        /// <summary>Badges still showing.</summary>
        public List<BadgeEntry> Badges { get; set; } = new List<BadgeEntry>();

        /// <summary>The Naughty vs Nice side for the event on show, or "".</summary>
        public string Team { get; set; } = "";
    }
}

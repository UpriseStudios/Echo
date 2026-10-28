using System.Collections.Generic;

namespace EchoCloud
{
    /// <summary>GetModes's answer: the server's clock and the modes on show (switched on and inside their dates).</summary>
    public sealed class GetModesResponse
    {
        public bool Ok { get; set; }
        public string Error { get; set; } = "";

        /// <summary>The server's time, seconds since 1970 UTC.</summary>
        public long Now { get; set; }
        public List<ModeStatus> Modes { get; set; } = new List<ModeStatus>();
    }
}

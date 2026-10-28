using System.Collections.Generic;

namespace Echo.Services.Backend
{
    /// <summary>The server's answer to GetModes (mirrors Server/EchoCloud/GetModesResponse).</summary>
    public sealed class ModesResult
    {
        public bool Ok;

        /// <summary>"offline".</summary>
        public string Error = "";

        /// <summary>The server's clock, seconds since 1970 UTC.</summary>
        public long Now;
        public List<ModeState> Modes = new List<ModeState>();

        public static ModesResult Failed(string error) => new ModesResult { Error = error };
    }
}

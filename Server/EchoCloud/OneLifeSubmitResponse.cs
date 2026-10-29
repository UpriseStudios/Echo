namespace EchoCloud
{
    /// <summary>SubmitOneLife: the replayed distance and where it ranks.</summary>
    public sealed class OneLifeSubmitResponse
    {
        public bool Ok { get; set; }

        /// <summary>"token" (wrong or already used), "malformed", "content-version", "options", "seed", "ghosts", "unfinished".</summary>
        public string Error { get; set; } = "";
        public int Distance { get; set; }
        public int Rank { get; set; }

        /// <summary>It came in after its day's board had closed (the game was closed mid-run): kept, not ranked.</summary>
        public bool BoardClosed { get; set; }
        public bool Abandoned { get; set; }
    }
}

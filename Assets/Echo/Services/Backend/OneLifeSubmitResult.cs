namespace Echo.Services.Backend
{
    /// <summary>SubmitOneLife's answer (mirrors Server/EchoCloud/OneLifeSubmitResponse).</summary>
    public sealed class OneLifeSubmitResult
    {
        public bool Ok;

        /// <summary>"token" (already in, or not this run's), "offline", or a rule broken.</summary>
        public string Error = "";
        public int Distance, Rank;
        public bool BoardClosed, Abandoned;

        public static OneLifeSubmitResult Failed(string error) => new OneLifeSubmitResult { Error = error };
    }
}

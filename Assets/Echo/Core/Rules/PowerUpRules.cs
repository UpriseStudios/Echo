namespace Echo.Core.Rules
{
    /// <summary>The numbers behind the power-ups (a user revision).</summary>
    public static class PowerUpRules
    {
        /// <summary>A player holds up to this many of each, and uses at most one of each per run.</summary>
        public const int MaxHeld = 2;

        /// <summary>Phase keeps every ghost (and the Copycat) harmless for this many metres.</summary>
        public const int PhaseMetres = 100;

        /// <summary>After the Shield breaks, ghosts are harmless this long, so the player can get clear of the one that hit.</summary>
        public const int ShieldGraceTicks = 45;

        public static readonly PowerUp[] All = { PowerUp.Shield, PowerUp.Phase, PowerUp.Lantern, PowerUp.ExtraLife };

        public static string Name(PowerUp p) => p switch
        {
            PowerUp.Shield => "Ghost Shield",
            PowerUp.Phase => "Phase",
            PowerUp.Lantern => "Ghost Lantern",
            _ => "Extra 1-UP",
        };

        public static string Description(PowerUp p) => p switch
        {
            PowerUp.Shield => "Survive one ghost touch",
            PowerUp.Phase => "Run through ghosts for 100 m",
            PowerUp.Lantern => "See every ghost's path ahead",
            _ => "Start with a life, used automatically",
        };
    }
}

namespace Echo.Game.World
{
    /// <summary>What the scenery looks like at one distance: the biome, how far each change has blended in, and night.</summary>
    public struct EnvironmentState
    {
        public Biome Biome;

        /// <summary>0 in the Graveyard, 1 once the Castle has fully faded in (and through Space).</summary>
        public float Castle;

        /// <summary>0 before Space, 1 once it has fully faded in.</summary>
        public float Space;

        /// <summary>0 by day, 1 at full night. Night falls during the Castle and stays through Space.</summary>
        public float Night;

        /// <summary>How much of the Graveyard is left: 1 at the start, 0 once the Castle is in.</summary>
        public float Graveyard => 1f - Castle;
    }
}

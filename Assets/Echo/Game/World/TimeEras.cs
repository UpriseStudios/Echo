namespace Echo.Game.World
{
    /// <summary>
    /// World: Time Run (World Time Run doc): one era every 250 m, the last carrying on for the rest of the run. The era is
    /// read from the simulation's distance, so a replay, a challenge and a ghost see the same era in the same place.
    /// Visual only: nothing here reaches the simulation.
    /// </summary>
    public static class TimeEras
    {
        public const int EraMetres = 250;

        /// <summary>The era at <paramref name="metres"/> along the course, among <paramref name="count"/> eras (-1 with none).</summary>
        public static int At(int metres, int count)
        {
            if (count <= 0) return -1;
            int era = metres / EraMetres;
            return era < 0 ? 0 : era >= count ? count - 1 : era;
        }
    }
}

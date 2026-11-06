namespace Echo.Game.World
{
    /// <summary>
    /// World: Time Run (World Time Run doc, art pass phase 1): five eras by distance, 300 m each (Dino Land, Medieval,
    /// Today, the Far Future, then the Far Far Future for the rest of the run). The era is read from the simulation's
    /// distance, so a replay, a challenge and a ghost see the same era in the same place. The next era is warmed 240 m
    /// into each one. Visual only: nothing here reaches the simulation.
    /// </summary>
    public static class TimeEras
    {
        public const int EraMetres = 300;
        public const int PreloadMetres = 240;

        /// <summary>The Far Future's index: it and the era after it count as "the future" (Paradox).</summary>
        public const int FarFuture = 3;

        /// <summary>The era at <paramref name="metres"/> along the course, among <paramref name="count"/> eras (-1 with none).</summary>
        public static int At(int metres, int count)
        {
            if (count <= 0) return -1;
            int era = metres / EraMetres;
            return era < 0 ? 0 : era >= count ? count - 1 : era;
        }

        /// <summary>The era to warm at <paramref name="metres"/>: the next one from 240 m into an era, else -1.</summary>
        public static int PreloadAt(int metres, int count)
        {
            int era = At(metres, count);
            if (era < 0 || era >= count - 1) return -1;
            return metres - era * EraMetres >= PreloadMetres ? era + 1 : -1;
        }
    }
}

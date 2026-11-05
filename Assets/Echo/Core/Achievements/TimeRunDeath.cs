namespace Echo.Core.Achievements
{
    /// <summary>
    /// A death in the Time Run world (World Time Run doc, phase 4), as the game saw it: where the run had got to, which
    /// era it fell in, what killed it and the most ghosts on screen at once in Dino Land. Eras are looks, so the game
    /// works them out; the rules only count.
    /// </summary>
    public struct TimeRunDeath
    {
        public int Distance;
        public bool InDinoLand;
        public bool InFarFuture;
        /// <summary>Killed by one of your own ghosts (not a Daily Haunt leader, not the Copycat).</summary>
        public bool ByOwnGhost;
        public int MostGhostsOnScreenInDinoLand;
    }
}

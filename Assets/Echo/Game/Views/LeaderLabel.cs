namespace Echo.Game.Views
{
    /// <summary>
    /// Daily Haunt doc: one name label, on the next leader ghost ahead of the player (the one to catch). Leaders already
    /// passed keep running unlabelled; with none ahead, no label.
    /// </summary>
    public static class LeaderLabel
    {
        /// <returns>The index of the named leader nearest ahead of playerX, or -1.</returns>
        public static int NextAhead(float playerX, float[] x, bool[] named, int count)
        {
            int best = -1;
            for (int i = 0; i < count; i++)
                if (named[i] && x[i] > playerX && (best < 0 || x[i] < x[best])) best = i;
            return best;
        }
    }
}

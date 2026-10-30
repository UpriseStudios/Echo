namespace Echo.Game.Seasons
{
    /// <summary>A season's background effects layer (seasons doc), always drawn behind the floor and everything on it.</summary>
    public enum SeasonEffect
    {
        None,

        /// <summary>Sprites drifting down across the view with a little sway: snow, leaves, confetti.</summary>
        Falling,

        /// <summary>A user revision: sprites drifting sideways low across the view with a slow bob: fog, will-o'-wisps.</summary>
        Drifting,
    }
}

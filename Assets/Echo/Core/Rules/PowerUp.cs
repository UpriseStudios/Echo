namespace Echo.Core.Rules
{
    /// <summary>
    /// Power-ups (a user revision), earned by rewarded ads outside a run and used during one. None changes how the
    /// runner moves, so a powered run still replays exactly as a ghost: the Shield and Phase only change whether a
    /// ghost touch kills, the Lantern is view-only and the Extra 1-UP is the usual recorded revive.
    /// </summary>
    public enum PowerUp
    {
        Shield,
        Phase,
        Lantern,
        ExtraLife,
    }
}

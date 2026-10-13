namespace Echo.Core.Rules
{
    /// <summary>
    /// Weekly Twists (value-adds doc). Copycat was redesigned head-on (see Copycat): an echo repeating your taps
    /// 0.5 s late at your own speed either trailed you forever (harmless) or sat on you (instant death).
    /// </summary>
    public enum Twist
    {
        None,
        /// <summary>Ghosts stay invisible until about 0.5 s before they reach you. Rendering only.</summary>
        ShadowWeek,
        /// <summary>The course runs right to left. Rendering only.</summary>
        Mirror,
        /// <summary>Only a small radius around the player is lit. Rendering only.</summary>
        LightsOut,
        /// <summary>Ghosts are harmless until the run reaches 300 m. A rule change.</summary>
        CrowdControl,
        /// <summary>Ghosts are 1.5 times the size, hitbox included. A rule change.</summary>
        GiantGhosts,
        /// <summary>Every few chunks a mirror-you runs at you from ahead, copying your taps 0.5 s late. A rule change.</summary>
        Copycat,
    }
}

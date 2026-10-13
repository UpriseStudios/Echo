using Echo.Core.Rules;
using UnityEngine;

namespace Echo.Game.Flow
{
    /// <summary>
    /// One switch per value-add feature (value-adds doc: each ships behind its own flag so it can go out as a
    /// separate update). Created once by the scene build with everything on, then never overwritten.
    /// </summary>
    [CreateAssetMenu(menuName = "Echo/Feature Flags", fileName = "FeatureFlags")]
    public sealed class FeatureFlags : ScriptableObject
    {
        [Header("Phase C")]
        public bool GhostBounties = true;
        public bool WeeklyTwists = true;
        public bool RoguesGallery = true;
        public bool NemesisDuel = true;

        [Tooltip("Pumpkin unlocks for free if the game is played during Halloween week (25-31 October).")]
        public bool HalloweenPumpkin = true;

        [Header("Phase D")]
        public bool GhostChallenges = true;
        public bool ReplayTheatre = true;
        public bool ThemedBiomes = true;

        [Header("Testing")]
        [Tooltip("Force a twist instead of this week's (for checking each one).")]
        public bool OverrideTwist;
        public Twist Twist = Twist.None;
    }
}

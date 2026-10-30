using UnityEngine;

namespace Echo.Game.Views
{
    /// <summary>
    /// Keeps what the player must read (spikes, blocks, the floor and its gaps) apart from the sky behind it (a user
    /// revision: device testing found spikes invisible in Neon Night, and gaps hard to pick out in places). A colour too
    /// close in brightness to the sky is pushed towards white on a dark sky, or towards black on a light one, until the
    /// two differ by at least the minimum. Pure arithmetic on structs, so it allocates nothing per frame.
    /// </summary>
    public static class ColourContrast
    {
        /// <summary>The least brightness difference between a hazard and the sky.</summary>
        public const float HazardMin = 0.32f;

        /// <summary>The least brightness difference between the floor and the sky, so gaps show.</summary>
        public const float FloorMin = 0.22f;

        /// <summary>Perceived brightness (Rec. 709 weights), 0 to 1.</summary>
        public static float Luminance(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        /// <summary><paramref name="c"/>, moved away from <paramref name="against"/>'s brightness until they differ by <paramref name="min"/>.</summary>
        public static Color Apart(Color c, Color against, float min)
        {
            float la = Luminance(against);
            if (Mathf.Abs(Luminance(c) - la) >= min) return c;
            Color target = la < 0.5f ? Color.white : Color.black;
            for (int step = 1; step <= 10; step++)
            {
                Color moved = Color.Lerp(c, target, step / 10f);
                if (Mathf.Abs(Luminance(moved) - la) >= min)
                {
                    moved.a = c.a;
                    return moved;
                }
            }
            target.a = c.a;
            return target;
        }
    }
}

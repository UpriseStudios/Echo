using UnityEngine;

namespace Echo.Game.World
{
    /// <summary>
    /// Themed Biomes (value-adds doc): Graveyard 0-300 m, Castle 300-700 m, Space from 700 m, each blending in over
    /// BlendMetres. Night falls part-way through the Castle (a user revision: some characters react to it) and Space
    /// is always night. Visual only: the course and its obstacles never change.
    /// </summary>
    public static class Biomes
    {
        public const float CastleFrom = 300f;
        public const float SpaceFrom = 700f;
        public const float BlendMetres = 40f;
        public const float DuskFrom = 420f;
        public const float NightFrom = 580f;

        public static EnvironmentState At(float metres)
        {
            float castle = Blend(metres, CastleFrom);
            float space = Blend(metres, SpaceFrom);
            float night = Mathf.Max(Smooth(Mathf.InverseLerp(DuskFrom, NightFrom, metres)), space);
            return new EnvironmentState
            {
                Biome = metres >= SpaceFrom ? Biome.Space : metres >= CastleFrom ? Biome.Castle : Biome.Graveyard,
                Castle = castle,
                Space = space,
                Night = night,
            };
        }

        /// <summary>The name shown as the run enters a biome.</summary>
        public static string Name(Biome biome) => biome switch
        {
            Biome.Castle => "THE CASTLE",
            Biome.Space => "SPACE",
            _ => "THE GRAVEYARD",
        };

        // Blends in over the BlendMetres before the boundary, so the new biome is complete when it is announced.
        static float Blend(float metres, float from) => Smooth(Mathf.InverseLerp(from - BlendMetres, from, metres));

        static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}

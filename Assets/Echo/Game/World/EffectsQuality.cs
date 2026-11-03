using Echo.Game.Persistence;
using UnityEngine;

namespace Echo.Game.World
{
    /// <summary>
    /// World Time Run doc, phase 3: full effects (bloom, and the Far Future's lights) or the reduced version (no bloom,
    /// fewer lights). The player's choice wins once made (Settings › Full effects); until then weaker phones, and phones
    /// on which the Far Future ran slow, get the reduced version.
    /// </summary>
    public static class EffectsQuality
    {
        public const int WeakMemoryMb = 3500;
        public const int WeakCores = 6;

        public static bool Full(SettingsSaveData s, bool weakDevice) => s.EffectsChosen ? s.Glow : !(weakDevice || s.EffectsReduced);

        /// <summary>Under about 3.5 GB of memory or fewer than 6 cores: a budget phone (0 means unknown).</summary>
        public static bool IsWeak(int memoryMb, int cores) => (memoryMb > 0 && memoryMb < WeakMemoryMb) || (cores > 0 && cores < WeakCores);

        public static bool ThisDeviceIsWeak() => !Application.isEditor && IsWeak(SystemInfo.systemMemorySize, SystemInfo.processorCount);
    }
}

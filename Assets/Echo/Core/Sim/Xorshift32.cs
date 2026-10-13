using System;

namespace Echo.Core.Sim
{
    /// <summary>The only RNG allowed in Core (tech spec 3). Marsaglia xorshift32 (13, 17, 5).</summary>
    public struct Xorshift32
    {
        // xorshift has a fixed point at 0, so a zero seed is replaced.
        const uint ZeroSeedReplacement = 0x9E3779B9u;

        uint _state;

        public Xorshift32(uint seed)
        {
            _state = seed == 0 ? ZeroSeedReplacement : seed;
        }

        public uint State => _state;

        public uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>Returns 0 &lt;= n &lt; maxExclusive. Modulo bias is negligible for small ranges and deterministic.</summary>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            return (int)(NextUInt() % (uint)maxExclusive);
        }
    }
}

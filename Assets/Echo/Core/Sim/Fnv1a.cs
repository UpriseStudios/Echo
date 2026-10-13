namespace Echo.Core.Sim
{
    /// <summary>32-bit FNV-1a, fed little-endian ints and UTF-16 strings.</summary>
    public static class Fnv1a
    {
        public const uint OffsetBasis = 2166136261u;
        const uint Prime = 16777619u;

        public static uint AddByte(uint h, byte b) => (h ^ b) * Prime;

        public static uint Add(uint h, int value)
        {
            uint v = (uint)value;
            h = AddByte(h, (byte)v);
            h = AddByte(h, (byte)(v >> 8));
            h = AddByte(h, (byte)(v >> 16));
            return AddByte(h, (byte)(v >> 24));
        }

        public static uint Add(uint h, string value)
        {
            h = Add(h, value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                h = AddByte(h, (byte)c);
                h = AddByte(h, (byte)(c >> 8));
            }
            return h;
        }
    }
}

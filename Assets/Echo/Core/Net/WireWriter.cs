using System;
using System.Collections.Generic;
using System.Text;

namespace Echo.Core.Net
{
    /// <summary>
    /// Writes the compact binary used between the game and its server (version 2.0): unsigned varints, zigzag for
    /// signed values, length-prefixed UTF-8 text and ascending tick lists stored as gaps. The same source compiles
    /// into the game and the Cloud Code module, so both sides read and write identically.
    /// </summary>
    public sealed class WireWriter
    {
        public const int MaxText = 64;

        readonly List<byte> _bytes = new List<byte>(1024);

        public int Length => _bytes.Count;

        public void Byte(byte v) => _bytes.Add(v);

        public void Bool(bool v) => _bytes.Add(v ? (byte)1 : (byte)0);

        public void Var(uint v)
        {
            while (v >= 0x80)
            {
                _bytes.Add((byte)(v | 0x80));
                v >>= 7;
            }
            _bytes.Add((byte)v);
        }

        public void Int(int v) => Var((uint)((v << 1) ^ (v >> 31)));

        public void UInt32(uint v)
        {
            for (int i = 0; i < 4; i++) _bytes.Add((byte)(v >> (8 * i)));
        }

        public void Text(string s)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(s ?? "");
            int n = Math.Min(bytes.Length, MaxText);
            Var((uint)n);
            for (int i = 0; i < n; i++) _bytes.Add(bytes[i]);
        }

        // Ticks are ascending, so each is stored as the gap from the one before.
        public void Ticks(int[] ticks)
        {
            ticks = ticks ?? Array.Empty<int>();
            Var((uint)ticks.Length);
            int last = 0;
            foreach (int t in ticks)
            {
                Var((uint)Math.Max(0, t - last));
                last = Math.Max(last, t);
            }
        }

        /// <summary>The bytes with a 16-bit checksum on the end, as base64url (no padding).</summary>
        public string ToBase64Url()
        {
            var data = new byte[_bytes.Count + 2];
            _bytes.CopyTo(data);
            ushort sum = Checksum(data, _bytes.Count);
            data[data.Length - 2] = (byte)sum;
            data[data.Length - 1] = (byte)(sum >> 8);
            return Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        // FNV-1a folded to 16 bits: catches a cut-off or mangled payload, not tampering (the server replays for that).
        internal static ushort Checksum(byte[] b, int count)
        {
            uint h = 2166136261u;
            for (int i = 0; i < count; i++) h = (h ^ b[i]) * 16777619u;
            return (ushort)(h ^ (h >> 16));
        }
    }
}

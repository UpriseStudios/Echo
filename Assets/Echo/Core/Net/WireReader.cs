using System;
using System.Text;

namespace Echo.Core.Net
{
    /// <summary>Reads what <see cref="WireWriter"/> wrote. Never throws on bad input: every read reports success.</summary>
    public sealed class WireReader
    {
        readonly byte[] _data;
        readonly int _end;
        readonly int _maxTicks;

        public int Pos { get; private set; }
        public bool AtEnd => Pos == _end;

        WireReader(byte[] data, int end, int maxTicks)
        {
            _data = data;
            _end = end;
            _maxTicks = maxTicks;
        }

        /// <summary>
        /// Opens a base64url payload whose checksum matches. Payloads longer than <paramref name="maxChars"/> are
        /// refused before decoding, so an oversized one costs nothing.
        /// </summary>
        public static bool TryOpen(string payload, int maxChars, int maxTicks, out WireReader reader)
        {
            reader = null;
            if (string.IsNullOrEmpty(payload) || payload.Length > maxChars) return false;
            byte[] data;
            try
            {
                string b64 = payload.Replace('-', '+').Replace('_', '/');
                b64 += new string('=', (4 - b64.Length % 4) % 4);
                data = Convert.FromBase64String(b64);
            }
            catch (FormatException)
            {
                return false;
            }
            if (data.Length < 3) return false;
            ushort sum = (ushort)(data[data.Length - 2] | data[data.Length - 1] << 8);
            if (sum != WireWriter.Checksum(data, data.Length - 2)) return false;
            reader = new WireReader(data, data.Length - 2, maxTicks);
            return true;
        }

        public bool Byte(out byte v)
        {
            v = 0;
            if (Pos >= _end) return false;
            v = _data[Pos++];
            return true;
        }

        public bool Bool(out bool v)
        {
            v = false;
            if (!Byte(out byte b) || b > 1) return false;
            v = b == 1;
            return true;
        }

        public bool Var(out uint v)
        {
            v = 0;
            for (int shift = 0; shift < 35; shift += 7)
            {
                if (Pos >= _end) return false;
                byte x = _data[Pos++];
                v |= (uint)(x & 0x7F) << shift;
                if (x < 0x80) return true;
            }
            return false;
        }

        /// <summary>A non-negative int no larger than <paramref name="max"/>.</summary>
        public bool Count(int max, out int v)
        {
            v = 0;
            if (!Var(out uint u) || u > (uint)max) return false;
            v = (int)u;
            return true;
        }

        public bool Int(out int v)
        {
            v = 0;
            if (!Var(out uint u)) return false;
            v = (int)(u >> 1) ^ -(int)(u & 1);
            return true;
        }

        public bool UInt32(out uint v)
        {
            v = 0;
            if (Pos + 4 > _end) return false;
            for (int i = 0; i < 4; i++) v |= (uint)_data[Pos++] << (8 * i);
            return true;
        }

        public bool Text(out string s)
        {
            s = "";
            if (!Var(out uint n) || n > WireWriter.MaxText || Pos + n > _end) return false;
            s = Encoding.UTF8.GetString(_data, Pos, (int)n);
            Pos += (int)n;
            return true;
        }

        public bool Ticks(out int[] ticks)
        {
            ticks = Array.Empty<int>();
            if (!Var(out uint n) || n > (uint)_maxTicks || n > (uint)(_end - Pos)) return false;
            var list = new int[n];
            long t = 0;
            for (int i = 0; i < n; i++)
            {
                if (!Var(out uint gap)) return false;
                t += gap;
                if (t > _maxTicks) return false;
                list[i] = (int)t;
            }
            ticks = list;
            return true;
        }
    }
}

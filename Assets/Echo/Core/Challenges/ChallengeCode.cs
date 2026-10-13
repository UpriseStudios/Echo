using System;
using System.Collections.Generic;
using System.Text;
using Echo.Core.Ghosts;
using Echo.Core.Rules;

namespace Echo.Core.Challenges
{
    /// <summary>
    /// Packs a challenge into a short URL-safe code, and the link that carries it (echoghost://challenge/CODE).
    /// Ghost Challenges have no backend, so the run itself rides in the link: a format byte, the course and run
    /// details, then tap, release and revive ticks as varint gaps, then a 16-bit checksum, all base64url.
    /// Decoding never throws: anything malformed, cut off or mistyped is simply not a challenge.
    /// </summary>
    public static class ChallengeCode
    {
        public const string Scheme = "echoghost";
        public const string LinkPrefix = Scheme + "://challenge/";
        const byte Format = 1;
        const int MaxTicks = 200_000;
        const int MaxText = 64;

        public static string Link(Challenge c) => LinkPrefix + Encode(c);

        public static string Encode(Challenge c)
        {
            var b = new List<byte>(256) { Format };
            Text(b, c.ContentVersion);
            for (int i = 0; i < 4; i++) b.Add((byte)(c.Seed >> (8 * i)));
            Var(b, (uint)Math.Max(1, c.SpeedPct));
            Var(b, (uint)Math.Max(0, c.Distance));
            Text(b, Truncate(c.Name ?? "", Challenge.MaxNameLength));
            GhostRecord r = c.Record;
            Text(b, r.SkinId ?? "");
            Var(b, (uint)Math.Max(0, r.RunIndex));
            Var(b, (uint)Math.Max(0, r.DeathTick));
            Var(b, ZigZag(r.DeathX));
            Var(b, (uint)r.Cause);
            Ticks(b, r.TapTicks);
            Ticks(b, r.ReleaseTicks);
            Ticks(b, r.ReviveTicks);
            ushort sum = Checksum(b, b.Count);
            b.Add((byte)sum);
            b.Add((byte)(sum >> 8));
            return Convert.ToBase64String(b.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        /// <summary>
        /// Finds a challenge in anything the player might open or paste: the link on its own, a whole share message
        /// containing it, or just the code.
        /// </summary>
        public static bool TryParse(string text, out Challenge challenge)
        {
            challenge = null;
            if (string.IsNullOrEmpty(text)) return false;
            int at = text.IndexOf(LinkPrefix, StringComparison.OrdinalIgnoreCase);
            string code = at >= 0 ? text.Substring(at + LinkPrefix.Length) : text.Trim();
            int end = 0;
            while (end < code.Length && IsCodeChar(code[end])) end++;
            return TryDecode(code.Substring(0, end), out challenge);
        }

        public static bool TryDecode(string code, out Challenge challenge)
        {
            challenge = null;
            if (string.IsNullOrEmpty(code) || code.Length > 400_000) return false;
            byte[] data;
            try
            {
                string b64 = code.Replace('-', '+').Replace('_', '/');
                b64 += new string('=', (4 - b64.Length % 4) % 4);
                data = Convert.FromBase64String(b64);
            }
            catch (FormatException)
            {
                return false;
            }
            if (data.Length < 3 || data[0] != Format) return false;
            ushort sum = (ushort)(data[data.Length - 2] | data[data.Length - 1] << 8);
            if (sum != Checksum(data, data.Length - 2)) return false;

            var reader = new Reader(data, data.Length - 2) { Pos = 1 };
            var c = new Challenge();
            var r = new GhostRecord();
            if (!reader.Text(out c.ContentVersion) || !reader.Bytes(4, out c.Seed)) return false;
            if (!reader.Var(out uint speed) || !reader.Var(out uint distance) || !reader.Text(out c.Name) || !reader.Text(out string skin)) return false;
            if (!reader.Var(out uint runIndex) || !reader.Var(out uint deathTick) || !reader.Var(out uint deathX) || !reader.Var(out uint cause)) return false;
            if (!reader.Ticks(out r.TapTicks) || !reader.Ticks(out r.ReleaseTicks) || !reader.Ticks(out r.ReviveTicks)) return false;
            if (reader.Pos != reader.End || speed == 0 || speed > 1000 || deathTick > MaxTicks || cause > (uint)DeathCause.Copycat) return false;

            c.SpeedPct = (int)speed;
            c.Distance = (int)distance;
            c.Name = Truncate(c.Name, Challenge.MaxNameLength);
            r.SkinId = skin;
            r.RunIndex = (int)runIndex;
            r.DeathTick = (int)deathTick;
            r.DeathX = UnZigZag(deathX);
            r.Cause = (DeathCause)cause;
            r.SpeedPct = c.SpeedPct;
            c.Record = r;
            challenge = c;
            return true;
        }

        static bool IsCodeChar(char ch) =>
            ch >= 'A' && ch <= 'Z' || ch >= 'a' && ch <= 'z' || ch >= '0' && ch <= '9' || ch == '-' || ch == '_';

        static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max);

        static void Text(List<byte> b, string s)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(s ?? "");
            int n = Math.Min(bytes.Length, MaxText);
            Var(b, (uint)n);
            for (int i = 0; i < n; i++) b.Add(bytes[i]);
        }

        // Ticks are ascending, so each is stored as the gap from the one before.
        static void Ticks(List<byte> b, int[] ticks)
        {
            ticks = ticks ?? Array.Empty<int>();
            Var(b, (uint)ticks.Length);
            int last = 0;
            foreach (int t in ticks)
            {
                Var(b, (uint)Math.Max(0, t - last));
                last = Math.Max(last, t);
            }
        }

        static void Var(List<byte> b, uint v)
        {
            while (v >= 0x80)
            {
                b.Add((byte)(v | 0x80));
                v >>= 7;
            }
            b.Add((byte)v);
        }

        static uint ZigZag(int v) => (uint)((v << 1) ^ (v >> 31));
        static int UnZigZag(uint v) => (int)(v >> 1) ^ -(int)(v & 1);

        // FNV-1a folded to 16 bits: catches a mistyped or cut-off code, not tampering.
        static ushort Checksum(IReadOnlyList<byte> b, int count)
        {
            uint h = 2166136261u;
            for (int i = 0; i < count; i++) h = (h ^ b[i]) * 16777619u;
            return (ushort)(h ^ (h >> 16));
        }

        sealed class Reader
        {
            readonly byte[] _data;
            public readonly int End;
            public int Pos;

            public Reader(byte[] data, int end)
            {
                _data = data;
                End = end;
            }

            public bool Var(out uint v)
            {
                v = 0;
                for (int shift = 0; shift < 35; shift += 7)
                {
                    if (Pos >= End) return false;
                    byte x = _data[Pos++];
                    v |= (uint)(x & 0x7F) << shift;
                    if (x < 0x80) return true;
                }
                return false;
            }

            public bool Bytes(int n, out uint v)
            {
                v = 0;
                if (Pos + n > End) return false;
                for (int i = 0; i < n; i++) v |= (uint)_data[Pos++] << (8 * i);
                return true;
            }

            public bool Text(out string s)
            {
                s = "";
                if (!Var(out uint n) || n > MaxText || Pos + n > End) return false;
                s = Encoding.UTF8.GetString(_data, Pos, (int)n);
                Pos += (int)n;
                return true;
            }

            public bool Ticks(out int[] ticks)
            {
                ticks = Array.Empty<int>();
                if (!Var(out uint n) || n > MaxTicks) return false;
                var list = new int[n];
                long t = 0;
                for (int i = 0; i < n; i++)
                {
                    if (!Var(out uint gap)) return false;
                    t += gap;
                    if (t > MaxTicks) return false;
                    list[i] = (int)t;
                }
                ticks = list;
                return true;
            }
        }
    }
}

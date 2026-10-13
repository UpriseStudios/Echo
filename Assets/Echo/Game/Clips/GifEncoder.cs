using System;
using System.Collections.Generic;
using System.IO;

namespace Echo.Game.Clips
{
    /// <summary>
    /// Highlights reel clips (a user revision; game spec: the share clip): an animated GIF89a from frames of 15-bit
    /// colour, written in plain C# so no package is needed and it can run off the main thread. Pixel art has few
    /// colours, so one global palette of the 256 most used fits; any others map to their nearest. Each pixel is
    /// written <c>scale</c> times across and down so the art stays sharp where apps resize it.
    /// </summary>
    public static class GifEncoder
    {
        /// <summary>A 15-bit colour (5 bits each, red high) from 8-bit channels.</summary>
        public static ushort Pack(byte r, byte g, byte b) => (ushort)(((r >> 3) << 10) | ((g >> 3) << 5) | (b >> 3));

        /// <param name="frames">Each frame width x height, top row first.</param>
        /// <param name="delayCs">Time per frame in hundredths of a second.</param>
        public static byte[] Encode(IReadOnlyList<ushort[]> frames, int width, int height, int scale, int delayCs)
        {
            if (frames == null || frames.Count == 0) throw new ArgumentException("no frames", nameof(frames));
            if (width <= 0 || height <= 0 || scale <= 0) throw new ArgumentException("bad size");
            int outW = width * scale, outH = height * scale;
            if (outW > 65535 || outH > 65535) throw new ArgumentException("too large");

            BuildPalette(frames, out ushort[] palette, out byte[] lookup);
            var s = new MemoryStream();
            Ascii(s, "GIF89a");
            U16(s, outW);
            U16(s, outH);
            s.WriteByte(0xF7);  // global colour table, 8 bits per channel, 256 entries
            s.WriteByte(0);
            s.WriteByte(0);
            for (int i = 0; i < 256; i++)
            {
                ushort c = i < palette.Length ? palette[i] : (ushort)0;
                s.WriteByte(Expand(c >> 10));
                s.WriteByte(Expand(c >> 5));
                s.WriteByte(Expand(c));
            }
            // Loop forever.
            s.WriteByte(0x21); s.WriteByte(0xFF); s.WriteByte(11);
            Ascii(s, "NETSCAPE2.0");
            s.WriteByte(3); s.WriteByte(1); U16(s, 0); s.WriteByte(0);

            var lzw = new Lzw(s);
            foreach (ushort[] frame in frames)
            {
                s.WriteByte(0x21); s.WriteByte(0xF9); s.WriteByte(4);
                s.WriteByte(0x04);  // disposal: leave in place; no transparency
                U16(s, delayCs);
                s.WriteByte(0); s.WriteByte(0);
                s.WriteByte(0x2C);
                U16(s, 0); U16(s, 0); U16(s, outW); U16(s, outH);
                s.WriteByte(0);
                s.WriteByte(8);  // LZW minimum code size
                lzw.Begin();
                for (int y = 0; y < outH; y++)
                {
                    int row = (y / scale) * width;
                    for (int x = 0; x < outW; x++) lzw.Add(lookup[frame[row + x / scale]]);
                }
                lzw.End();
            }
            s.WriteByte(0x3B);
            return s.ToArray();
        }

        // The most used colours across every frame, and a map from every 15-bit colour seen to its palette index.
        static void BuildPalette(IReadOnlyList<ushort[]> frames, out ushort[] palette, out byte[] lookup)
        {
            var counts = new int[32768];
            foreach (ushort[] frame in frames)
                foreach (ushort c in frame) counts[c & 0x7FFF]++;
            var used = new List<int>();
            for (int c = 0; c < counts.Length; c++)
                if (counts[c] > 0) used.Add(c);
            // Most used first; ties by colour so the palette is the same every time.
            used.Sort((a, b) => counts[b] != counts[a] ? counts[b].CompareTo(counts[a]) : a.CompareTo(b));
            int n = Math.Min(256, used.Count);
            palette = new ushort[n];
            for (int i = 0; i < n; i++) palette[i] = (ushort)used[i];
            lookup = new byte[32768];
            for (int i = 0; i < n; i++) lookup[palette[i]] = (byte)i;
            for (int k = n; k < used.Count; k++) lookup[used[k]] = Nearest(palette, used[k]);
        }

        static byte Nearest(ushort[] palette, int c)
        {
            int r = c >> 10, g = (c >> 5) & 31, b = c & 31, best = 0, bestD = int.MaxValue;
            for (int i = 0; i < palette.Length; i++)
            {
                int p = palette[i];
                int dr = r - (p >> 10), dg = g - ((p >> 5) & 31), db = b - (p & 31);
                int d = 3 * dr * dr + 4 * dg * dg + 2 * db * db;
                if (d < bestD) { bestD = d; best = i; }
            }
            return (byte)best;
        }

        static byte Expand(int five)
        {
            five &= 31;
            return (byte)((five << 3) | (five >> 2));
        }

        static void U16(Stream s, int v)
        {
            s.WriteByte((byte)(v & 0xFF));
            s.WriteByte((byte)((v >> 8) & 0xFF));
        }

        static void Ascii(Stream s, string text)
        {
            foreach (char ch in text) s.WriteByte((byte)ch);
        }

        // Variable-length LZW with 8-bit pixels and codes up to 12 bits, written as GIF sub-blocks of 255 bytes.
        // The open-addressed table (5003 slots) is the classic GIF compressor's.
        sealed class Lzw
        {
            const int Bits = 12, MaxMaxCode = 1 << Bits, HSize = 5003, HShift = 4;
            const int InitBits = 9, ClearCode = 256, EofCode = 257;

            readonly Stream _out;
            readonly int[] _htab = new int[HSize];
            readonly int[] _codetab = new int[HSize];
            readonly byte[] _packet = new byte[255];
            int _packetLength;
            int _nBits, _maxCode, _freeEnt, _ent;
            bool _clearFlag, _started;
            int _accum, _accumBits;

            public Lzw(Stream output) => _out = output;

            public void Begin()
            {
                _nBits = InitBits;
                _maxCode = (1 << _nBits) - 1;
                _freeEnt = ClearCode + 2;
                _clearFlag = false;
                _accum = 0;
                _accumBits = 0;
                _packetLength = 0;
                _started = false;
                ClearTable();
                Output(ClearCode);
            }

            public void Add(byte pixel)
            {
                if (!_started)
                {
                    _ent = pixel;
                    _started = true;
                    return;
                }
                int c = pixel;
                int fcode = (c << Bits) + _ent;
                int i = (c << HShift) ^ _ent;
                if (_htab[i] == fcode)
                {
                    _ent = _codetab[i];
                    return;
                }
                if (_htab[i] >= 0)
                {
                    int disp = i == 0 ? 1 : HSize - i;
                    do
                    {
                        i -= disp;
                        if (i < 0) i += HSize;
                        if (_htab[i] == fcode)
                        {
                            _ent = _codetab[i];
                            return;
                        }
                    } while (_htab[i] >= 0);
                }
                Output(_ent);
                _ent = c;
                if (_freeEnt < MaxMaxCode)
                {
                    _codetab[i] = _freeEnt++;
                    _htab[i] = fcode;
                }
                else
                {
                    ClearTable();
                    _freeEnt = ClearCode + 2;
                    _clearFlag = true;
                    Output(ClearCode);
                }
            }

            public void End()
            {
                if (_started) Output(_ent);
                Output(EofCode);
                FlushPacket();
                _out.WriteByte(0);  // block terminator
            }

            void ClearTable()
            {
                for (int i = 0; i < HSize; i++) _htab[i] = -1;
            }

            void Output(int code)
            {
                _accum |= code << _accumBits;
                _accumBits += _nBits;
                while (_accumBits >= 8)
                {
                    Byte((byte)(_accum & 0xFF));
                    _accum >>= 8;
                    _accumBits -= 8;
                }
                if (_freeEnt > _maxCode || _clearFlag)
                {
                    if (_clearFlag)
                    {
                        _nBits = InitBits;
                        _maxCode = (1 << _nBits) - 1;
                        _clearFlag = false;
                    }
                    else
                    {
                        _nBits++;
                        _maxCode = _nBits == Bits ? MaxMaxCode : (1 << _nBits) - 1;
                    }
                }
                if (code == EofCode)
                {
                    while (_accumBits > 0)
                    {
                        Byte((byte)(_accum & 0xFF));
                        _accum >>= 8;
                        _accumBits -= 8;
                    }
                    _accumBits = 0;
                    _accum = 0;
                }
            }

            void Byte(byte b)
            {
                _packet[_packetLength++] = b;
                if (_packetLength == 255) FlushPacket();
            }

            void FlushPacket()
            {
                if (_packetLength == 0) return;
                _out.WriteByte((byte)_packetLength);
                _out.Write(_packet, 0, _packetLength);
                _packetLength = 0;
            }
        }
    }
}

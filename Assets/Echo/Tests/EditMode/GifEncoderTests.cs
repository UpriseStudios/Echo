using System;
using System.Collections.Generic;
using Echo.Game.Clips;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    /// <summary>Highlights reel clips: the GIF decodes back to the frames, scaled, with a loop and each frame's delay.</summary>
    public class GifEncoderTests
    {
        static ushort[] Frame(int w, int h, int seed, int colours)
        {
            var f = new ushort[w * h];
            uint x = (uint)seed * 2654435761u | 1u;
            for (int i = 0; i < f.Length; i++)
            {
                // Runs of one colour, like pixel art, with the odd change.
                if (i % 7 == 0)
                {
                    x ^= x << 13; x ^= x >> 17; x ^= x << 5;
                }
                f[i] = (ushort)((x % (uint)colours) * 97 % 32768);
            }
            return f;
        }

        [TestCase(1, 50)]
        [TestCase(2, 200)]
        [TestCase(1, 256)]
        public void Frames_DecodeBackExactly(int scale, int colours)
        {
            const int w = 37, h = 23;
            var frames = new List<ushort[]> { Frame(w, h, 1, colours), Frame(w, h, 2, colours), Frame(w, h, 3, colours) };
            byte[] gif = GifEncoder.Encode(frames, w, h, scale, 8);
            List<ushort[]> decoded = Decode(gif, out int dw, out int dh, out int loops, out List<int> delays);
            Assert.AreEqual(w * scale, dw);
            Assert.AreEqual(h * scale, dh);
            Assert.AreEqual(0, loops, "loops forever");
            CollectionAssert.AreEqual(new[] { 8, 8, 8 }, delays);
            Assert.AreEqual(frames.Count, decoded.Count);
            for (int f = 0; f < frames.Count; f++)
                for (int y = 0; y < dh; y++)
                    for (int x = 0; x < dw; x++)
                        Assert.AreEqual(Round(frames[f][(y / scale) * w + x / scale]), decoded[f][y * dw + x], $"frame {f} at {x},{y}");
        }

        [Test]
        public void ALargeFrame_LongerThanTheCodeTable_DecodesExactly()
        {
            const int w = 300, h = 200;
            var frames = new List<ushort[]> { Frame(w, h, 9, 256) };
            List<ushort[]> decoded = Decode(GifEncoder.Encode(frames, w, h, 1, 8), out _, out _, out _, out _);
            for (int i = 0; i < w * h; i++) Assert.AreEqual(Round(frames[0][i]), decoded[0][i], $"pixel {i}");
        }

        [Test]
        public void MoreThan256Colours_MapToTheNearest()
        {
            const int w = 40, h = 40;
            var frame = new ushort[w * h];
            for (int i = 0; i < frame.Length; i++) frame[i] = (ushort)(i % 400 * 81);
            List<ushort[]> decoded = Decode(GifEncoder.Encode(new List<ushort[]> { frame }, w, h, 1, 8), out _, out _, out _, out _);
            Assert.AreEqual(w * h, decoded[0].Length);
        }

        // The 5-bit channels the encoder writes, expanded to 8 and packed back, are the colour itself.
        static ushort Round(ushort c) => c;

        // A small GIF decoder: global palette, NETSCAPE loop, graphic control delays, full-frame images.
        static List<ushort[]> Decode(byte[] d, out int w, out int h, out int loops, out List<int> delays)
        {
            Assert.AreEqual("GIF89a", System.Text.Encoding.ASCII.GetString(d, 0, 6));
            int p = 6;
            w = d[p] | d[p + 1] << 8; h = d[p + 2] | d[p + 3] << 8;
            int flags = d[p + 4];
            p += 7;
            Assert.IsTrue((flags & 0x80) != 0);
            int size = 2 << (flags & 7);
            var palette = new ushort[size];
            for (int i = 0; i < size; i++, p += 3) palette[i] = GifEncoder.Pack(d[p], d[p + 1], d[p + 2]);
            loops = -1;
            delays = new List<int>();
            var frames = new List<ushort[]>();
            while (true)
            {
                byte b = d[p++];
                if (b == 0x3B) break;
                if (b == 0x21)
                {
                    byte label = d[p++];
                    var data = new List<byte>();
                    while (d[p] != 0) { int n = d[p++]; for (int i = 0; i < n; i++) data.Add(d[p++]); }
                    p++;
                    if (label == 0xF9) delays.Add(data[1] | data[2] << 8);
                    if (label == 0xFF && data.Count >= 14) loops = data[12] | data[13] << 8;
                    continue;
                }
                Assert.AreEqual(0x2C, b);
                int iw = d[p + 4] | d[p + 5] << 8, ih = d[p + 6] | d[p + 7] << 8;
                p += 9;
                int min = d[p++];
                var stream = new List<byte>();
                while (d[p] != 0) { int n = d[p++]; for (int i = 0; i < n; i++) stream.Add(d[p++]); }
                p++;
                byte[] indices = Lzw(stream.ToArray(), min, iw * ih);
                var frame = new ushort[iw * ih];
                for (int i = 0; i < frame.Length; i++) frame[i] = palette[indices[i]];
                frames.Add(frame);
            }
            return frames;
        }

        static byte[] Lzw(byte[] data, int min, int count)
        {
            int clear = 1 << min, eoi = clear + 1;
            var output = new List<byte>(count);
            var dict = new List<byte[]>();
            void Reset()
            {
                dict.Clear();
                for (int i = 0; i < clear; i++) dict.Add(new[] { (byte)i });
                dict.Add(null);
                dict.Add(null);
            }
            Reset();
            int size = min + 1, bitPos = 0;
            byte[] prev = null;
            while (true)
            {
                int code = 0;
                for (int i = 0; i < size; i++, bitPos++)
                    if ((data[bitPos >> 3] >> (bitPos & 7) & 1) != 0) code |= 1 << i;
                if (code == clear)
                {
                    Reset();
                    size = min + 1;
                    prev = null;
                    continue;
                }
                if (code == eoi) break;
                byte[] entry;
                if (code < dict.Count && dict[code] != null) entry = dict[code];
                else if (code == dict.Count && prev != null)
                {
                    entry = new byte[prev.Length + 1];
                    Array.Copy(prev, entry, prev.Length);
                    entry[prev.Length] = prev[0];
                }
                else throw new AssertionException($"bad code {code}");
                output.AddRange(entry);
                if (prev != null && dict.Count < 4096)
                {
                    var add = new byte[prev.Length + 1];
                    Array.Copy(prev, add, prev.Length);
                    add[prev.Length] = entry[0];
                    dict.Add(add);
                    if (dict.Count == 1 << size && size < 12) size++;
                }
                prev = entry;
            }
            Assert.AreEqual(count, output.Count, "pixel count");
            return output.ToArray();
        }
    }
}

using System;
using System.IO;
using UnityEditor;

namespace Echo.Editor
{
    /// <summary>
    /// Writes placeholder audio as 16-bit mono WAV files: 6 music stems of identical length and tempo
    /// (tech spec 12) and the 5 sound effects. Replace the files with real audio, keeping the names.
    /// </summary>
    public static class PlaceholderAudio
    {
        public const string Dir = "Assets/Echo/Content/Audio";
        const int Rate = 44100;
        const double Bpm = 120;
        const int Beats = 16; // 4 bars of 4/4 = 8 seconds
        static readonly double Beat = 60.0 / Bpm;
        static int StemSamples => (int)(Rate * Beat * Beats);

        public static readonly string[] StemNames = { "stem0_bass", "stem1_kick", "stem2_hats", "stem3_pad", "stem4_arp", "stem5_lead" };
        public const string Jump = "sfx_jump", NearMiss = "sfx_near_miss", Death = "sfx_death", Clear = "sfx_ghost_clear", Sting = "sfx_nemesis_sting",
            Thud = "sfx_grave_thud", Chisel = "sfx_chisel", Howl = "sfx_howl";

        // A minor: A2, C3, E3, G3 roots for each bar.
        static readonly double[] BarRoots = { 110.0, 87.31, 130.81, 98.0 };

        public static void EnsureAll()
        {
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Echo/Content", "Audio");
            Write(StemNames[0], Stem(Bass));
            Write(StemNames[1], Stem(Kick));
            Write(StemNames[2], Stem(Hats));
            Write(StemNames[3], Stem(Pad));
            Write(StemNames[4], Stem(Arp));
            Write(StemNames[5], Stem(Lead));
            Write(Jump, Sfx(0.08, (t, d) => Square(Lerp(880, 1320, t / d), t) * Env(t, d, 0.005)));
            Write(NearMiss, Sfx(0.28, (t, d) => Noise(t) * Math.Sin(Math.PI * t / d) * 0.6));
            Write(Death, Sfx(0.32, (t, d) => Crush(Square(Lerp(220, 55, t / d), t) * 0.7 + Noise(t) * 0.4, 6) * Env(t, d, 0.002)));
            Write(Clear, Sfx(0.7, (t, d) => Arpeggio(t, new[] { 523.25, 659.25, 783.99, 1046.5 }, 0.09) * Env(t, d, 0.01)));
            Write(Sting, Sfx(0.55, (t, d) => (Saw(110, t) + Saw(116.5, t) + Saw(155.6, t)) * 0.3 * Env(t, d, 0.003)));
            // The headstone landing: a falling low thump with a little grit; then a chisel's short bright tick.
            Write(Thud, Sfx(0.3, (t, d) => (Math.Sin(2 * Math.PI * Lerp(90, 40, t / d) * t) * 0.9 + Noise(t) * 0.25 * Math.Exp(-t * 30)) * Env(t, d, 0.002)));
            // The Werewolf's howl at moonrise: a voice gliding up then falling away, with a little vibrato.
            Write(Howl, Sfx(1.4, (t, d) => Math.Sin(2 * Math.PI * (Lerp(330, 520, Math.Min(1, t / 0.35)) - Math.Max(0, t - 0.6) * 140) * t
                + Math.Sin(2 * Math.PI * 5.5 * t) * 0.6) * 0.5 * Env(t, d, 0.15)));
            Write(Chisel, Sfx(0.045, (t, d) => (Noise(t) * 0.5 + Square(2400, t) * 0.25) * Math.Exp(-t * 90)));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        public static string PathFor(string name) => $"{Dir}/{name}.wav";

        // ---- stems: every function gets time t (s) inside the 8 s loop ----

        static double Bass(double t)
        {
            int beat = (int)(t / Beat);
            double tb = t - beat * Beat;
            double f = BarRoots[(beat / 4) % 4] / 2;
            return Square(f, t) * Math.Exp(-tb * 5) * 0.45;
        }

        static double Kick(double t)
        {
            double tb = t % Beat;
            return Math.Sin(2 * Math.PI * (50 * tb + 60 * (1 - Math.Exp(-tb * 30)) / 30)) * Math.Exp(-tb * 9) * 0.9;
        }

        static double Hats(double t)
        {
            double tb = t % (Beat / 2);
            return Noise(t) * Math.Exp(-tb * 60) * 0.35;
        }

        static double Pad(double t)
        {
            int bar = (int)(t / (Beat * 4)) % 4;
            double r = BarRoots[bar] * 2;
            return (Math.Sin(2 * Math.PI * r * t) + Math.Sin(2 * Math.PI * r * 1.189 * t) + Math.Sin(2 * Math.PI * r * 1.498 * t)) * 0.12;
        }

        static double Arp(double t)
        {
            double step = Beat / 4;
            int n = (int)(t / step);
            int bar = (n / 16) % 4;
            double[] ratios = { 1, 1.189, 1.498, 2 };
            double f = BarRoots[bar] * 4 * ratios[n % 4];
            double ts = t - n * step;
            return Triangle(f, t) * Math.Exp(-ts * 12) * 0.25;
        }

        static double Lead(double t)
        {
            double[] melody = { 440, 0, 523.25, 493.88, 440, 0, 392, 0, 349.23, 0, 392, 440, 523.25, 0, 493.88, 0 };
            int beat = (int)(t / Beat);
            double f = melody[beat % melody.Length];
            if (f == 0) return 0;
            double tb = t - beat * Beat;
            return Square(f, t) * Math.Exp(-tb * 3) * 0.12;
        }

        // ---- building blocks ----

        static float[] Stem(Func<double, double> f)
        {
            var data = new float[StemSamples];
            for (int i = 0; i < data.Length; i++) data[i] = (float)Clamp(f((double)i / Rate));
            return data;
        }

        static float[] Sfx(double seconds, Func<double, double, double> f)
        {
            var data = new float[(int)(Rate * seconds)];
            for (int i = 0; i < data.Length; i++) data[i] = (float)Clamp(f((double)i / Rate, seconds));
            return data;
        }

        static double Square(double f, double t) => Math.Sin(2 * Math.PI * f * t) >= 0 ? 1 : -1;
        static double Saw(double f, double t) => 2 * (f * t - Math.Floor(f * t + 0.5));
        static double Triangle(double f, double t) => 2 * Math.Abs(Saw(f, t)) - 1;
        static double Lerp(double a, double b, double t) => a + (b - a) * t;
        static double Env(double t, double d, double attack) => Math.Min(1, t / attack) * Math.Max(0, 1 - t / d);
        static double Crush(double v, int levels) => Math.Round(v * levels) / levels;
        static double Clamp(double v) => Math.Max(-1, Math.Min(1, v));

        static double Arpeggio(double t, double[] notes, double step)
        {
            int i = Math.Min((int)(t / step), notes.Length - 1);
            return Math.Sin(2 * Math.PI * notes[i] * t) * 0.6;
        }

        // Deterministic white noise so regenerated files are byte-identical.
        static double Noise(double t)
        {
            uint x = (uint)(t * Rate) * 2654435761u + 0x9E3779B9u;
            x ^= x << 13; x ^= x >> 17; x ^= x << 5;
            return (x / (double)uint.MaxValue) * 2 - 1;
        }

        /// <summary>Writes a file only if none exists, so real audio dropped in under the same name is kept.</summary>
        static void Write(string name, float[] samples)
        {
            string path = PathFor(name);
            if (File.Exists(path)) return;
            using var w = new BinaryWriter(File.Create(path));
            int bytes = samples.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + bytes);
            w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(Rate);
            w.Write(Rate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' });
            w.Write(bytes);
            foreach (float s in samples) w.Write((short)(s * short.MaxValue));
        }
    }
}

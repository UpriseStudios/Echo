using System;

namespace Echo.Editor
{
    /// <summary>
    /// Seasonal music (a user revision on the seasons doc, which had only a stinger): a carol as six stems like the main
    /// tune (tech spec 12: stem 0 always plays, the rest join as ghosts fill the screen), so the melody is stem 0 and is
    /// always heard. Two carols, both public domain: the chorus of "Jingle Bells" (James Lord Pierpont, 1857) for
    /// Christmas and "Auld Lang Syne" (traditional) for New Year. Written by PlaceholderAudio like the rest of the
    /// generated audio, only when missing, so a real recording under the same names is kept.
    /// </summary>
    public sealed class CarolMusic
    {
        public const int Rate = 44100;

        /// <summary>Stems: 0 the melody over a light bass, 1 sleigh bells, 2 a soft kick, 3 chords, 4 glockenspiel, 5 a harmony.</summary>
        public readonly string[] StemNames;
        readonly double _beat;
        readonly int _beats;
        readonly (double f, double beats)[] _melody;
        readonly double[] _roots;    // one chord a bar
        readonly double[] _thirds;   // 1.26 major, 1.189 minor

        CarolMusic(string prefix, double bpm, (double, double)[] melody, double[] roots, double[] thirds)
        {
            StemNames = new[] { prefix + "_stem0_melody", prefix + "_stem1_bells", prefix + "_stem2_kick", prefix + "_stem3_chords",
                prefix + "_stem4_glock", prefix + "_stem5_harmony" };
            _beat = 60.0 / bpm;
            _melody = melody;
            _roots = roots;
            _thirds = thirds;
            double beats = 0;
            foreach (var (_, b) in melody) beats += b;
            _beats = (int)Math.Round(beats);
            if (_beats != roots.Length * 4 || roots.Length != thirds.Length) throw new ArgumentException("a chord a bar, 4 beats a bar");
        }

        public int Samples => (int)(Rate * _beat * _beats);
        public double Seconds => _beat * _beats;

        public Func<double, double>[] Stems => new Func<double, double>[] { MelodyAndBass, Bells, Kick, Chords, Glock, Harmony };

        // ---- the carols ----

        const double C4 = 261.63, D4 = 293.66, E4 = 329.63, F4 = 349.23, G4 = 392.00, A4 = 440.00, C5 = 523.25, D5 = 587.33;
        const double Maj = 1.26, Min = 1.189;

        /// <summary>
        /// "Jingle bells, jingle bells, jingle all the way; oh what fun it is to ride in a one-horse open sleigh, hey!" in
        /// C major, brisk: 8 bars at 132 bpm. Chords C C C C F C D G.
        /// </summary>
        public static readonly CarolMusic JingleBells = new CarolMusic("xmas", 132, new (double, double)[]
        {
            (E4, 1), (E4, 1), (E4, 2),
            (E4, 1), (E4, 1), (E4, 2),
            (E4, 1), (G4, 1), (C4, 1.5), (D4, 0.5),
            (E4, 4),
            (F4, 1), (F4, 1), (F4, 1.5), (F4, 0.5),
            (F4, 1), (E4, 1), (E4, 1), (E4, 0.5), (E4, 0.5),
            (E4, 1), (D4, 1), (D4, 1), (E4, 1),
            (D4, 2), (G4, 2),
        }, new[] { 130.81, 130.81, 130.81, 130.81, 174.61, 130.81, 146.83, 196.0 }, new[] { Maj, Maj, Maj, Maj, Maj, Maj, Maj, Maj });

        /// <summary>
        /// "Should auld acquaintance be forgot, and never brought to mind? Should auld acquaintance be forgot, and auld lang
        /// syne?" in F major, gently: 8 bars at 96 bpm, the pick-up "Should" on the last beat so the loop flows on. Chords
        /// F C F Bb F C Dm F.
        /// </summary>
        public static readonly CarolMusic AuldLangSyne = new CarolMusic("newyear", 96, new (double, double)[]
        {
            (F4, 1.5), (F4, 0.5), (F4, 1), (A4, 1),
            (G4, 1.5), (F4, 0.5), (G4, 1), (A4, 1),
            (F4, 1.5), (F4, 0.5), (A4, 1), (C5, 1),
            (D5, 3), (D5, 1),
            (C5, 1.5), (A4, 0.5), (A4, 1), (F4, 1),
            (G4, 1.5), (F4, 0.5), (G4, 1), (A4, 1),
            (F4, 1.5), (D4, 0.5), (D4, 1), (C4, 1),
            (F4, 3), (C4, 1),
        }, new[] { 174.61, 130.81, 174.61, 116.54, 174.61, 130.81, 146.83, 174.61 }, new[] { Maj, Maj, Maj, Maj, Maj, Maj, Min, Maj });

        // ---- the stems: each gets time t (s) inside the loop ----

        int BarAt(double t) => (int)(t / (_beat * 4)) % _roots.Length;

        // The note sounding at t, with how far into it we are and how long it lasts (s); 0 Hz between notes.
        (double f, double into, double length) NoteAt(double t)
        {
            double beat = (t / _beat) % _beats, at = 0;
            foreach (var (f, beats) in _melody)
            {
                if (beat < at + beats) return (f, (beat - at) * _beat, beats * _beat);
                at += beats;
            }
            return (0, 0, 1);
        }

        // The tune, an octave up, as a bright pulse that holds, with a short gap before each note so repeats are heard
        // (jin-gle bells), plus a light bass on the bar's root.
        double MelodyAndBass(double t)
        {
            var (f, into, length) = NoteAt(t);
            double voice = 0;
            if (into < length - 0.05)
            {
                double attack = Math.Min(1, into / 0.008);
                double env = attack * (0.55 + 0.45 * Math.Exp(-into * 5));
                double vib = 1 + 0.004 * Math.Sin(2 * Math.PI * 5.5 * into) * Math.Min(1, into * 2);
                voice = Pulse(f * 2 * vib, t, 0.25) * env * 0.26;
            }
            int beatIndex = (int)(t / _beat);
            double tb = t - beatIndex * _beat;
            double bass = Triangle(_roots[BarAt(t)] / 2 * (beatIndex % 2 == 0 ? 1 : 1.498), t) * Math.Exp(-tb * 4) * 0.3;
            return voice + bass;
        }

        // Sleigh bells shaken in eighths, a little louder on the beat.
        double Bells(double t)
        {
            double step = _beat / 2;
            int n = (int)(t / step);
            double ts = t - n * step;
            double accent = n % 2 == 0 ? 1.0 : 0.6;
            double jingle = Math.Sin(2 * Math.PI * 5274 * t) * 0.5 + Math.Sin(2 * Math.PI * 6645 * t) * 0.35 + Noise(t) * 0.45;
            return jingle * Math.Exp(-ts * 22) * 0.3 * accent;
        }

        // A soft kick on 1 and 3.
        double Kick(double t)
        {
            double tb = t % (_beat * 2);
            return Math.Sin(2 * Math.PI * (45 * tb + 50 * (1 - Math.Exp(-tb * 30)) / 30)) * Math.Exp(-tb * 10) * 0.7;
        }

        // The bar's chord, held, with a slow shimmer.
        double Chords(double t)
        {
            int bar = BarAt(t);
            double r = _roots[bar] * 2;
            double wobble = 1 + 0.003 * Math.Sin(2 * Math.PI * 5 * t);
            return (Math.Sin(2 * Math.PI * r * wobble * t) + Math.Sin(2 * Math.PI * r * _thirds[bar] * t) + Math.Sin(2 * Math.PI * r * 1.498 * t)) * 0.1;
        }

        // A glockenspiel up and down the chord in eighths: a bell tone that rings out.
        double Glock(double t)
        {
            double step = _beat / 2;
            int n = (int)(t / step);
            int bar = BarAt(t);
            double[] ratios = { 1, _thirds[bar], 1.498, 2, 1.498, _thirds[bar], 1, 1.498 };
            double f = _roots[bar] * 4 * ratios[n % ratios.Length];
            double ts = t - n * step;
            double bell = Math.Sin(2 * Math.PI * f * t) + 0.4 * Math.Sin(2 * Math.PI * f * 2.76 * t) * Math.Exp(-ts * 12);
            return bell * Math.Exp(-ts * 5) * 0.12;
        }

        // A harmony under the tune: the chord tone just below each melody note, softly.
        double Harmony(double t)
        {
            var (f, into, length) = NoteAt(t);
            if (f == 0 || into >= length - 0.05) return 0;
            int bar = BarAt(t);
            double r = _roots[bar];
            double best = 0;
            foreach (double k in new[] { 1, _thirds[bar], 1.498, 2, 2 * _thirds[bar], 2 * 1.498, 4 })
            {
                double tone = r * k;
                if (tone < f * 0.95 && tone > best) best = tone;
            }
            if (best == 0) return 0;
            double env = Math.Min(1, into / 0.02) * (0.6 + 0.4 * Math.Exp(-into * 3));
            return Triangle(best * 2, t) * env * 0.12;
        }

        static double Pulse(double f, double t, double duty) => (f * t - Math.Floor(f * t)) < duty ? 1 : -1;
        static double Saw(double f, double t) => 2 * (f * t - Math.Floor(f * t + 0.5));
        static double Triangle(double f, double t) => 2 * Math.Abs(Saw(f, t)) - 1;

        static double Noise(double t)
        {
            uint x = (uint)(t * Rate) * 2654435761u + 0x7F4A7C15u;
            x ^= x << 13; x ^= x >> 17; x ^= x << 5;
            return (x / (double)uint.MaxValue) * 2 - 1;
        }
    }
}

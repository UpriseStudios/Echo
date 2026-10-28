using System;

namespace Echo.Game.Clips
{
    /// <summary>
    /// The stretch of a reel a clip covers (a user revision: clips are trimmed in the clip editor), in sim ticks from
    /// the start of the run. The timeline runs to a moment after the final death so the shatter can be in the clip.
    /// A GIF is at most 10 s (it gets large fast), a video at most 60 s (TikTok and YouTube Shorts), both at least 1 s.
    /// </summary>
    public readonly struct ClipRange
    {
        public const int TicksPerSecond = 60;
        public const int TailTicks = 72;                       // 1.2 s after the death, for the shatter
        public const int MinTicks = 1 * TicksPerSecond;
        public const int GifMaxTicks = 10 * TicksPerSecond;
        public const int VideoMaxTicks = 60 * TicksPerSecond;
        public const int GifDefaultTicks = 5 * TicksPerSecond;
        public const int VideoDefaultTicks = 15 * TicksPerSecond;

        // Marketing content plan: short-form clips are 9 to 20 s.
        public const int VerticalMinTicks = 9 * TicksPerSecond;
        public const int VerticalMaxTicks = 20 * TicksPerSecond;
        public const int VerticalDefaultTicks = 15 * TicksPerSecond;

        public readonly int Start;
        public readonly int End;

        public ClipRange(int start, int end)
        {
            Start = start;
            End = end;
        }

        public int Ticks => End - Start;
        public float Seconds => Ticks / (float)TicksPerSecond;

        /// <summary>The whole timeline of a run that ended at <paramref name="deathTick"/>.</summary>
        public static int Length(int deathTick) => Math.Max(0, deathTick) + TailTicks;

        public static int MaxTicks(ClipFormat format) =>
            format == ClipFormat.Gif ? GifMaxTicks : format == ClipFormat.Vertical ? VerticalMaxTicks : VideoMaxTicks;

        public static int MinTicksFor(ClipFormat format) => format == ClipFormat.Vertical ? VerticalMinTicks : MinTicks;

        /// <summary>The final moments, ending just after the death: what a highlight usually is.</summary>
        public static ClipRange Default(ClipFormat format, int deathTick)
        {
            int length = Length(deathTick);
            int ticks = format == ClipFormat.Gif ? GifDefaultTicks : format == ClipFormat.Vertical ? VerticalDefaultTicks : VideoDefaultTicks;
            return new ClipRange(Math.Max(0, length - ticks), length);
        }

        /// <summary>
        /// Keeps a range inside the timeline and the format's limits. When it has to give, the handle the player
        /// is moving stays where they put it and the other one follows.
        /// </summary>
        public static ClipRange Clamp(int start, int end, ClipFormat format, int deathTick, bool movingEnd)
        {
            int length = Length(deathTick);
            int min = Math.Min(MinTicksFor(format), length), max = MaxTicks(format);
            start = Math.Max(0, Math.Min(start, length));
            end = Math.Max(0, Math.Min(end, length));
            if (movingEnd)
            {
                end = Math.Max(end, Math.Min(min, length));
                if (end - start < min) start = end - min;
                if (end - start > max) start = end - max;
            }
            else
            {
                start = Math.Min(start, length - min);
                if (end - start < min) end = start + min;
                if (end - start > max) end = start + max;
            }
            start = Math.Max(0, start);
            end = Math.Min(length, end);
            return new ClipRange(start, end);
        }

        /// <summary>"1:23.4" for a tick.</summary>
        public static string Time(int tick)
        {
            int tenths = Math.Max(0, tick) * 10 / TicksPerSecond;
            return $"{tenths / 600}:{tenths / 10 % 60:00}.{tenths % 10}";
        }
    }
}

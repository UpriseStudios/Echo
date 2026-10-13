using System;
using Echo.Core.Courses;
using Echo.Core.Sim;

namespace Echo.Core.Rules
{
    /// <summary>
    /// The Copycat twist (redesigned head-on, chosen by the user). Every few chunks a mirror of the player runs at
    /// them from ahead, at the run's speed, copying their taps (and how long they held them) DelayTicks late.
    /// It runs on a phantom flat floor, ignoring the course, and is met on the clear floor at a chunk boundary.
    /// Jump early and you pass over it before it copies the jump; tap at the wrong moment half a second earlier
    /// and it leaps into your path. Touching it kills; getting past it counts as a near-miss.
    /// CopycatPlanner proves every encounter passable before it is scheduled.
    /// </summary>
    public static class Copycat
    {
        public const int DelayTicks = 30;
        public const int FirstChunk = 3;
        public const int EveryChunks = 3;

        /// <summary>Ticks it stays after the meeting, running off behind the player.</summary>
        public const int LingerTicks = 90;

        /// <summary>Flat floor with no hazards, for the copycat's own run.</summary>
        public static Course PhantomFloor() => new Course(new ChunkLibrary(new[]
        {
            new Chunk("phantom_floor", 1, 1_000_000, Array.Empty<Gap>(), Array.Empty<Spike>(), Array.Empty<Ceiling>(), Array.Empty<Mover>()),
        }), 1);

        /// <summary>Steps it one tick with the player's tap and finger from DelayTicks ago.</summary>
        public static void Step(ref CopycatState c, bool tap, bool held, Course floor, int tick, int speed, in SimConfig cfg)
        {
            c.PrevBody = c.Body;
            Runner.Step(ref c.Body, tap, floor, tick, speed, cfg, held);
        }

        /// <summary>The ghost-sized (80%) box: it is a phantom, and kinder to brush past than a full one.</summary>
        public static Aabb Box(in CopycatState c, in SimConfig cfg) =>
            Aabb.FromBottomCentre(c.X, c.Body.Y, cfg.GhostHitboxW, cfg.GhostHitboxH);
    }
}

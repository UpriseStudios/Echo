using System.Collections.Generic;
using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Rules;
using Echo.Core.Sim;

namespace Echo.Core.HowToPlay
{
    /// <summary>
    /// How to play (a user revision, asked for by testers): short clips of real play with captions, covering jumping and
    /// jump height, the double jump, ghosts and the power-ups. Each clip is a scripted run on a short course made of the
    /// game's own chunks, solved by ClipScript, so it always plays on the current content and art. A clip whose chunks are
    /// missing or that can't be solved is left out (HowToPlayTests fails when that happens).
    /// </summary>
    public static class HowToPlayDirector
    {
        public static readonly string[] Topics = { "JUMPING", "DOUBLE JUMP", "GHOSTS", "POWER-UPS" };

        const int Unit = 1000;
        const uint CourseSeed = 1;
        const int HoldAfterDeath = 100;   // ticks the clip stays on a death before moving on
        const int Tail = 40;              // ticks after the last move clears

        // Chunks the clips are built from (Content/Chunks); a renamed one drops its clip.
        public const string SpikeChunk = "t0_01_first_spike";
        public const string HopChunk = "t3_hop_under_ceiling";
        public const string GapChunk = "t1_gap_wide";

        public static List<HowToPlayClip> Build(ChunkLibrary library, in SimConfig cfg, string skinId)
        {
            var clips = new List<HowToPlayClip>();
            Add(clips, Jumping(library, cfg, skinId));
            Add(clips, DoubleJump(library, cfg, skinId));
            HowToPlayClip lost = LostRun(library, cfg, skinId);
            Add(clips, lost);
            GhostRecord ghost = lost != null ? GhostOf(lost, cfg) : null;
            if (ghost != null)
            {
                Add(clips, PassTheGhost(library, cfg, skinId, ghost));
                Add(clips, OldGhosts(library, cfg, skinId));
                Add(clips, Shield(library, cfg, skinId, ghost));
                Add(clips, Phase(library, cfg, skinId, ghost));
                Add(clips, Lantern(library, cfg, skinId, ghost));
            }
            Add(clips, ExtraLife(library, cfg, skinId));
            return clips;
        }

        static void Add(List<HowToPlayClip> clips, HowToPlayClip clip)
        {
            if (clip != null) clips.Add(clip);
        }

        // ---- 1. Jumping: tap to jump, hold for height, a quick tap for a short hop ----

        static HowToPlayClip Jumping(ChunkLibrary library, in SimConfig cfg, string skinId)
        {
            Course course = CourseOf(library, Flat(4), SpikeChunk, HopChunk, Flat(6));
            if (course == null) return null;
            var s = new ClipScript(0, Topics[0], course, cfg, skinId, null, 1);
            int spikeEnd = course.ChunkStartX(2) - Unit;
            int jump = s.Move(HowToMove.Jump, r => r.Player.X > spikeEnd, out _);
            int hopEnd = course.ChunkStartX(3) - Unit;
            int hop = s.Move(HowToMove.Hop, r => r.Player.X > hopEnd, out _);
            if (jump < 0 || hop < 0) return null;
            s.Beat(jump - 60, jump, "TAP TO JUMP");
            s.Beat(jump, jump + 40, "HOLD YOUR FINGER DOWN TO JUMP HIGHER", slowMo: true);
            s.Beat(hop - 70, hop, "LOW CEILING AHEAD...");
            s.Beat(hop, hop + 50, "A QUICK TAP MAKES A SHORT HOP", slowMo: true);
            return Finish(s, s.TickWhen(r => r.Player.X > hopEnd) + Tail);
        }

        // ---- 2. The double jump ----

        static HowToPlayClip DoubleJump(ChunkLibrary library, in SimConfig cfg, string skinId)
        {
            Course course = CourseOf(library, Flat(4), GapChunk, Flat(6));
            if (course == null) return null;
            var s = new ClipScript(1, Topics[1], course, cfg, skinId, null, 1);
            int gapEnd = course.ChunkStartX(2);
            int jump = s.Move(HowToMove.DoubleJump, r => r.Player.X > gapEnd && r.Player.Grounded, out int second);
            if (jump < 0) return null;
            s.Beat(jump - 50, second, "JUMP...");
            s.Beat(second, second + 45, "...AND TAP AGAIN IN THE AIR: A DOUBLE JUMP", slowMo: true);
            int landed = s.TickWhen(r => r.Player.X > gapEnd && r.Player.Grounded);
            s.Beat(second + 45, landed + Tail, "IT SAVES A JUMP THAT FALLS SHORT");
            return Finish(s, landed + Tail);
        }

        // ---- 3. Ghosts: a run that ends, then comes back as a ghost to slip past ----

        static Course GhostCourse(ChunkLibrary library) => CourseOf(library, Flat(3), Flat(3), Flat(3), SpikeChunk, Flat(6));

        static HowToPlayClip LostRun(ChunkLibrary library, in SimConfig cfg, string skinId)
        {
            Course course = GhostCourse(library);
            if (course == null) return null;
            var s = new ClipScript(2, Topics[2], course, cfg, skinId, null, 1);
            RunSimulation run = s.Play(null);   // no taps: the spike ends it
            if (!run.IsDead) return null;
            int death = run.DeathTick;
            s.Beat(death - 70, death, "EVERY RUN THAT ENDS...");
            s.Beat(death, death + HoldAfterDeath, "...COMES BACK AS A GHOST");
            return Finish(s, death + HoldAfterDeath);
        }

        static GhostRecord GhostOf(HowToPlayClip lost, in SimConfig cfg)
        {
            var run = new Reels.ReelPlayer(lost.Reel, lost.Course, cfg);
            while (!run.Finished) run.Step();
            return run.Run.ToGhostRecord();
        }

        // The ghost is overtaken at a chunk boundary before its grave (EncounterPlanner): the runner must get past it.
        static bool PastGhost(RunSimulation r) =>
            r.Ghosts.Count > 0 && r.Player.X > r.Ghosts.ReplayAt(0).State.X + Unit && r.Player.Grounded;

        static HowToPlayClip PassTheGhost(ChunkLibrary library, in SimConfig cfg, string skinId, GhostRecord ghost)
        {
            Course course = GhostCourse(library);
            var s = new ClipScript(2, Topics[2], course, cfg, skinId, new[] { ghost }, 2);
            int over = s.Move(HowToMove.Jump, PastGhost, out _);
            int spikeEnd = course.ChunkStartX(4) - Unit;
            int spike = s.Move(HowToMove.Jump, r => r.Player.X > spikeEnd, out _);
            if (over < 0 || spike < 0) return null;
            s.Beat(over - 80, over, "IT REPLAYS THAT RUN, SLOWLY. TOUCH IT AND YOUR RUN IS OVER", slowMo: true);
            s.Beat(over, over + 45, "JUMP OVER IT TO SLIP PAST", slowMo: true);
            s.Beat(spike - 40, spike + 80, "THE GHOST THAT CATCHES YOU MOST BECOMES YOUR NEMESIS");
            return Finish(s, s.TickWhen(r => r.Player.X > spikeEnd) + Tail);
        }

        // Old ghosts give way (GhostManager.EncounterOrder, EncounterPlanner): ghosts are met only at chunk boundaries, at
        // most 2 to a boundary, killers first, then the newest. Three runs that died at the same spike, where only one
        // boundary is open: the newest two are met and must be jumped; the oldest becomes a harmless echo.
        static Course CrowdCourse(ChunkLibrary library) => CourseOf(library, Flat(6), Flat(5), SpikeChunk, Flat(6));

        static HowToPlayClip OldGhosts(ChunkLibrary library, in SimConfig cfg, string skinId)
        {
            Course course = CrowdCourse(library);
            if (course == null) return null;
            int spikeChunk = 2;
            var crowd = new List<GhostRecord>();
            // Different early hops, so the three don't move as one; each still ends on the spike.
            foreach (int hop in new[] { 12, 30, -1 })
            {
                GhostRecord g = LostOnSpike(course, cfg, skinId, crowd.Count + 1, hop, spikeChunk);
                if (g == null) return null;
                crowd.Add(g);
            }
            var s = new ClipScript(2, Topics[2], course, cfg, skinId, crowd, crowd.Count + 1);
            RunSimulation start = s.Play(r => true);
            int echo = -1, lethal = 0;
            for (int i = 0; i < start.Ghosts.Count; i++)
            {
                if (start.Ghosts.ReplayAt(i).Harmless) echo = echo < 0 ? i : echo;
                else lethal++;
            }
            // The oldest (run 1) is the one left without a place.
            if (echo < 0 || start.Ghosts.RecordAt(echo).RunIndex != 1 || lethal == 0) return null;

            int over = s.Move(HowToMove.Jump, r => PastLethalGhosts(r), out _);
            int spikeEnd = course.ChunkStartX(3) - Unit;
            int spike = s.Move(HowToMove.Jump, r => r.Player.X > spikeEnd, out _);
            if (over < 0 || spike < 0) return null;
            int through = s.TickWhen(r => System.Math.Abs(r.Player.X - r.Ghosts.ReplayAt(echo).State.X) < Unit / 2);
            if (through < 0) return null;
            s.Beat(0, over - 50, "THE OLDER A GHOST, THE FAINTER IT LOOKS");
            s.Beat(over - 50, over + 30, "WHEN IT GETS CROWDED, THE NEWEST GHOSTS STAY DEADLY: JUMP THEM", slowMo: true);
            s.Beat(System.Math.Max(over + 30, through - 40), through + 50, "THE OLDEST FADE INTO ECHOES: RUN STRAIGHT THROUGH THEM", slowMo: true);
            int end = s.TickWhen(r => r.Player.X > spikeEnd) + 70;
            s.Beat(through + 50, end, "A GHOST THAT HAS CAUGHT YOU GETS FIRST PICK, SO IT STAYS DEADLY");
            return Finish(s, end);
        }

        static bool PastLethalGhosts(RunSimulation r)
        {
            if (!r.Player.Grounded) return false;
            for (int i = 0; i < r.Ghosts.Count; i++)
                if (!r.Ghosts.ReplayAt(i).Harmless && r.Player.X <= r.Ghosts.ReplayAt(i).State.X + Unit) return false;
            return true;
        }

        // A run on the course that hops once at <paramref name="hopTick"/> (or not at all) and ends on the spike chunk.
        static GhostRecord LostOnSpike(Course course, in SimConfig cfg, string skinId, int runIndex, int hopTick, int spikeChunk)
        {
            var reel = new Reels.Reel
            {
                Seed = course.Seed,
                RunIndex = runIndex,
                Run = new GhostRecord
                {
                    RunIndex = runIndex,
                    SkinId = skinId ?? "",
                    TapTicks = hopTick >= 0 ? new[] { hopTick } : new int[0],
                    ReleaseTicks = hopTick >= 0 ? new[] { hopTick + 1 } : new int[0],
                },
            };
            var player = new Reels.ReelPlayer(reel, course, cfg);
            while (!player.Finished && player.Run.Tick < 3600) player.Step();
            if (!player.Run.IsDead || course.ChunkIndexAt(player.Run.Player.X) != spikeChunk) return null;
            return player.Run.ToGhostRecord();
        }

        // ---- 4. Power-ups ----

        static HowToPlayClip Shield(ChunkLibrary library, in SimConfig cfg, string skinId, GhostRecord ghost)
        {
            Course course = GhostCourse(library);
            var s = new ClipScript(3, Topics[3], course, cfg, skinId, new[] { ghost }, 2);
            s.Reel.ShieldTick = 0;
            int hit = s.TickWhen(r => r.ShieldBreaks > 0);
            if (hit < 0) return null;
            s.From(hit);
            int spikeEnd = course.ChunkStartX(4) - Unit;
            int spike = s.Move(HowToMove.Jump, r => r.Player.X > spikeEnd, out _);
            if (spike < 0) return null;
            s.Clip.PowerButtons = true;
            s.Beat(0, hit - 20, "POWER-UPS: EARN THEM WITH A REWARDED AD, USE THEM WITH THE BUTTONS BELOW");
            s.Beat(hit - 20, hit + 60, "GHOST SHIELD: SHRUGS OFF ONE GHOST", slowMo: true);
            return Finish(s, s.TickWhen(r => r.Player.X > spikeEnd) + Tail);
        }

        static HowToPlayClip Phase(ChunkLibrary library, in SimConfig cfg, string skinId, GhostRecord ghost)
        {
            Course course = GhostCourse(library);
            var s = new ClipScript(3, Topics[3], course, cfg, skinId, new[] { ghost }, 2);
            s.Reel.PhaseTick = 0;
            int through = s.TickWhen(PastGhost);
            if (through < 0) return null;
            s.From(through);
            int spikeEnd = course.ChunkStartX(4) - Unit;
            int spike = s.Move(HowToMove.Jump, r => r.Player.X > spikeEnd, out _);
            if (spike < 0) return null;
            s.Clip.PowerButtons = true;
            s.Beat(through - 70, through + 30, "PHASE: GHOSTS CAN'T TOUCH YOU FOR 100 M", slowMo: true);
            return Finish(s, s.TickWhen(r => r.Player.X > spikeEnd) + Tail);
        }

        static HowToPlayClip Lantern(ChunkLibrary library, in SimConfig cfg, string skinId, GhostRecord ghost)
        {
            Course course = GhostCourse(library);
            var s = new ClipScript(3, Topics[3], course, cfg, skinId, new[] { ghost }, 2);
            int over = s.Move(HowToMove.Jump, PastGhost, out _);
            int spikeEnd = course.ChunkStartX(4) - Unit;
            int spike = s.Move(HowToMove.Jump, r => r.Player.X > spikeEnd, out _);
            if (over < 0 || spike < 0) return null;
            s.Clip.Lantern = true;
            s.Clip.PowerButtons = true;
            s.Beat(0, over + 40, "GHOST LANTERN: SEE WHERE EVERY GHOST IS GOING");
            return Finish(s, s.TickWhen(r => r.Player.X > spikeEnd) + Tail);
        }

        static HowToPlayClip ExtraLife(ChunkLibrary library, in SimConfig cfg, string skinId)
        {
            Course course = GhostCourse(library);
            if (course == null) return null;
            var s = new ClipScript(3, Topics[3], course, cfg, skinId, null, 1);
            s.Reel.ExtraLife = true;
            int revived = s.TickWhen(r => r.ExtraLifeUsed);
            if (revived < 0) return null;
            s.From(revived + 1);
            int spikeEnd = course.ChunkStartX(4) - Unit;
            int spike = s.Move(HowToMove.Jump, r => r.Player.X > spikeEnd, out _);
            if (spike < 0) return null;
            s.Clip.PowerButtons = true;
            s.Beat(revived - 30, revived + 70, "EXTRA 1-UP: ONE MORE GO WHEN A RUN ENDS", slowMo: true);
            s.Beat(revived + 70, spike + 90, "HOLD UP TO 2 OF EACH, USE 1 OF EACH PER RUN. GOOD LUCK!");
            return Finish(s, s.TickWhen(r => r.Player.X > spikeEnd) + 90);
        }

        // ---- building blocks ----

        static HowToPlayClip Finish(ClipScript s, int endTick)
        {
            s.Clip.EndTick = endTick;
            return s.Clip;
        }

        static object Flat(int units) => new Chunk("howto_flat", 0, units * Unit,
            new Gap[0], new Spike[0], new Ceiling[0], new Mover[0]);

        // Chunks by id (strings) or ready made; null when one is missing from the library.
        static Course CourseOf(ChunkLibrary library, params object[] parts)
        {
            var chunks = new List<Chunk>();
            foreach (object part in parts)
            {
                Chunk chunk = part as Chunk ?? Find(library, part as string);
                if (chunk == null) return null;
                chunks.Add(chunk);
            }
            return new Course(library, chunks, CourseSeed);
        }

        static Chunk Find(ChunkLibrary library, string id)
        {
            foreach (Chunk c in library.All)
                if (c.Id == id) return c;
            return null;
        }
    }
}

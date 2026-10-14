using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.HowToPlay;
using Echo.Core.Reels;
using Echo.Core.Sim;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    /// <summary>
    /// How to play: every clip is built from today's chunks and shows what its captions say. When chunks or SimConfig
    /// change, this is where a clip that no longer works shows up.
    /// </summary>
    public class HowToPlayTests
    {
        static readonly SimConfig Cfg = SimConfig.Default;
        static List<HowToPlayClip> _clips;
        static long _buildMs;

        static List<HowToPlayClip> Clips()
        {
            if (_clips != null) return _clips;
            var watch = Stopwatch.StartNew();
            _clips = HowToPlayDirector.Build(TestCourses.Content(), Cfg, "");
            _buildMs = watch.ElapsedMilliseconds;
            return _clips;
        }

        // The clip played to its end exactly as the game plays it, watching every tick.
        static ReelPlayer PlayOut(HowToPlayClip clip, System.Action<ReelPlayer> everyTick = null)
        {
            var player = new ReelPlayer(clip.Reel, clip.Course, Cfg);
            while (!player.Finished && player.Run.Tick < clip.EndTick)
            {
                player.Step();
                everyTick?.Invoke(player);
            }
            return player;
        }

        static HowToPlayClip Clip(int topic, string caption) =>
            Clips().Single(c => c.Topic == topic && c.Beats.Any(b => b.Text.Contains(caption)));

        [Test]
        public void EveryClip_IsBuilt_CoveringAllFourTopics()
        {
            List<HowToPlayClip> clips = Clips();
            TestContext.WriteLine($"{clips.Count} clips built in {_buildMs} ms");
            Assert.AreEqual(9, clips.Count, "jumping, double jump, three ghost clips, four power-ups");
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, clips.Select(c => c.Topic).Distinct().ToArray(), "in topic order");
            foreach (HowToPlayClip clip in clips)
            {
                Assert.IsNotEmpty(clip.Beats, clip.TopicName);
                Assert.Greater(clip.EndTick, 60, clip.TopicName);
                Assert.Less(clip.EndTick, 60 * 9, $"{clip.TopicName}: a short clip");
                foreach (HowToPlayBeat b in clip.Beats)
                {
                    Assert.Less(b.From, b.To, b.Text);
                    Assert.LessOrEqual(b.From, clip.EndTick, b.Text);
                }
            }
        }

        [Test]
        public void TheClips_AreTheSameEveryTime()
        {
            List<HowToPlayClip> again = HowToPlayDirector.Build(TestCourses.Content(), Cfg, "");
            for (int i = 0; i < Clips().Count; i++)
            {
                CollectionAssert.AreEqual(Clips()[i].Reel.Run.TapTicks, again[i].Reel.Run.TapTicks);
                CollectionAssert.AreEqual(Clips()[i].Reel.Run.ReleaseTicks, again[i].Reel.Run.ReleaseTicks);
            }
        }

        [Test]
        public void OnlyTheLostRun_Ends_AndTheExtraLifeClip_ComesBack()
        {
            foreach (HowToPlayClip clip in Clips())
            {
                ReelPlayer played = PlayOut(clip);
                bool lost = clip.Beats.Any(b => b.Text.Contains("COMES BACK AS A GHOST"));
                Assert.AreEqual(lost, played.Run.IsDead, $"{clip.TopicName}: {clip.Beats[0].Text}");
            }
            ReelPlayer oneUp = PlayOut(Clip(3, "EXTRA 1-UP"));
            Assert.IsTrue(oneUp.Run.ExtraLifeUsed, "it died once and came back");
        }

        [Test]
        public void Jumping_ShowsAFullJump_ThenAShortHop()
        {
            HowToPlayClip clip = Clip(0, "SHORT HOP");
            var apexes = new List<int>();
            int lastVy = 0, jumpStartY = 0;
            PlayOut(clip, p =>
            {
                RunnerState s = p.Run.Player;
                if (lastVy <= 0 && s.Vy > 0) jumpStartY = s.Y;
                if (lastVy > 0 && s.Vy <= 0) apexes.Add(s.Y - jumpStartY);
                lastVy = s.Vy;
            });
            Assert.AreEqual(2, apexes.Count, "two jumps");
            Assert.Greater(apexes[0], apexes[1] * 5 / 4, $"the held jump ({apexes[0]}) is clearly higher than the hop ({apexes[1]})");
        }

        [Test]
        public void TheDoubleJump_UsesTheAirJump()
        {
            bool used = false;
            PlayOut(Clip(1, "DOUBLE JUMP"), p => used |= p.Run.Player.AirJumpsLeft < Cfg.AirJumps);
            Assert.IsTrue(used);
        }

        [Test]
        public void TheGhost_IsALethalOne_TheShieldTakesItsHit_AndPhaseLetsYouThrough()
        {
            foreach (string caption in new[] { "SLIP PAST", "SHIELD", "PHASE", "LANTERN" })
            {
                var run = new ReelPlayer(Clip(caption == "SLIP PAST" ? 2 : 3, caption).Reel, Clip(caption == "SLIP PAST" ? 2 : 3, caption).Course, Cfg).Run;
                Assert.AreEqual(1, run.GhostsAtStart, caption);
                Assert.IsFalse(run.Ghosts.ReplayAt(0).Harmless, $"{caption}: a real ghost, not a faint echo");
            }
            Assert.AreEqual(1, PlayOut(Clip(3, "SHIELD")).Run.ShieldBreaks);
            ReelPlayer phased = PlayOut(Clip(3, "PHASE"));
            Assert.IsTrue(phased.Run.PhaseUsed);
            Assert.IsFalse(phased.Run.IsDead);
            Assert.IsTrue(Clip(3, "LANTERN").Lantern);
        }

        [Test]
        public void OldGhosts_TheOldestIsAHarmlessEcho_TheNewerOnesStayDeadly_AndYouRunThroughIt()
        {
            HowToPlayClip clip = Clip(2, "RUN STRAIGHT THROUGH");
            Assert.AreEqual(3, clip.Reel.Ghosts.Count);
            var player = new ReelPlayer(clip.Reel, clip.Course, Cfg);
            GhostManager ghosts = player.Run.Ghosts;
            int echo = -1, lethal = 0;
            for (int i = 0; i < ghosts.Count; i++)
            {
                if (ghosts.ReplayAt(i).Harmless) echo = i;
                else lethal++;
            }
            Assert.GreaterOrEqual(echo, 0, "one ghost has no place left");
            Assert.AreEqual(1, ghosts.RecordAt(echo).RunIndex, "and it is the oldest");
            Assert.AreEqual(2, lethal, "the two newest are met, and deadly");

            bool passedThrough = false;
            while (!player.Finished && player.Run.Tick < clip.EndTick)
            {
                player.Step();
                passedThrough |= System.Math.Abs(player.Run.Player.X - ghosts.ReplayAt(echo).State.X) < 300;
            }
            Assert.IsTrue(passedThrough, "the runner goes right through the echo");
            Assert.IsFalse(player.Run.IsDead);
        }

        [Test]
        public void ACourseWithOpeningChunks_StartsWithThem_ThenCarriesOn()
        {
            ChunkLibrary library = TestCourses.Content();
            Chunk first = library.All[library.All.Length - 1];
            var course = new Course(library, new[] { first }, 7);
            Assert.AreSame(first, course.ChunkAt(0));
            Assert.IsNotNull(course.ChunkAt(3));
            Assert.AreEqual(first.Length, course.ChunkStartX(1));
        }
    }
}

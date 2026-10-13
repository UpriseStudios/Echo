using System.IO;
using Echo.Core.Ghosts;
using Echo.Core.Rules;
using Echo.Game.Persistence;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    public class SaveTests
    {
        const string Version = "a1b2c3d4";
        string _dir;
        SaveSystem _saves;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "echo_save_tests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _saves = new SaveSystem(Path.Combine(_dir, "echo_save.json"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        static Progress SampleProgress()
        {
            var p = new Progress { Best = 812, Clears = 3, Seed = 3_000_000_000u, NextRunIndex = 9, CycleBanishes = 2 };
            p.Ghosts.Add(new GhostRecord { RunIndex = 4, TapTicks = new[] { 40, 97 }, ReviveTicks = new[] { 200 }, DeathTick = 311, SpeedPct = 115, Kills = 3, LastKillRun = 8 });
            p.Ghosts.Add(new GhostRecord { RunIndex = 7, DeathTick = 52, SpeedPct = 115 });
            p.Stats.TotalRuns = 40;
            p.Stats.NemesesDefeated = 1;
            p.Stats.WorstNemesisKills = 5;
            p.Stats.WorstNemesisName = "The Second";
            p.Achievements.Add("close_one");
            return p;
        }

        [Test]
        public void RoundTrip_ThroughTheFile_KeepsEverything()
        {
            var data = new SaveData();
            data.Tutorial.SeenTapHint = true;
            data.Settings.Music = false;
            SaveMapper.Write(SampleProgress(), data, Version);
            _saves.Save(data);

            SaveData loaded = _saves.Load();
            Progress p = SaveMapper.ToProgress(loaded, Version, out bool discarded);

            Assert.IsFalse(discarded);
            Assert.AreEqual(812, p.Best);
            Assert.AreEqual(3, p.Clears);
            Assert.AreEqual(3_000_000_000u, p.Seed);
            Assert.AreEqual(9, p.NextRunIndex);
            Assert.AreEqual(2, p.CycleBanishes);
            Assert.AreEqual(2, p.Ghosts.Count);
            GhostRecord g = p.Ghosts[0];
            Assert.AreEqual(4, g.RunIndex);
            CollectionAssert.AreEqual(new[] { 40, 97 }, g.TapTicks);
            CollectionAssert.AreEqual(new[] { 200 }, g.ReviveTicks);
            Assert.AreEqual(311, g.DeathTick);
            Assert.AreEqual(115, g.SpeedPct);
            Assert.AreEqual(3, g.Kills);
            Assert.AreEqual(8, g.LastKillRun);
            Assert.AreEqual(40, p.Stats.TotalRuns);
            Assert.AreEqual("The Second", p.Stats.WorstNemesisName);
            CollectionAssert.AreEqual(new[] { "close_one" }, loaded.Achievements);
            CollectionAssert.AreEqual(new[] { "close_one" }, p.Achievements);
            Assert.IsTrue(loaded.Tutorial.SeenTapHint);
            Assert.IsFalse(loaded.Tutorial.SeenGhostHint);
            Assert.IsFalse(loaded.Settings.Music);
        }

        [Test]
        public void Schema2_RoundTrips_SkinDeathAndUnlockStats()
        {
            var progress = SampleProgress();
            GhostRecord first = progress.Ghosts[0];
            first.SkinId = "char_wizard";
            first.DeathX = 41_230;
            first.Cause = DeathCause.Ghost;
            first.KillerRunIndex = 2;
            progress.Stats.Deaths = 77;
            progress.Stats.MaxDistance = 640;
            progress.Stats.MaxGhostsAtStart = 16;
            progress.Stats.MaxGhostsCleared = 12;

            var data = new SaveData();
            SaveMapper.Write(progress, data, Version);
            _saves.Save(data);
            SaveData loaded = _saves.Load();
            Assert.AreEqual(2, loaded.Schema);
            Progress p = SaveMapper.ToProgress(loaded, Version, out _);

            GhostRecord g = p.Ghosts[0];
            Assert.AreEqual("char_wizard", g.SkinId);
            Assert.AreEqual(41_230, g.DeathX);
            Assert.AreEqual(DeathCause.Ghost, g.Cause);
            Assert.AreEqual(2, g.KillerRunIndex);
            Assert.AreEqual(77, p.Stats.Deaths);
            Assert.AreEqual(640, p.Stats.MaxDistance);
            Assert.AreEqual(16, p.Stats.MaxGhostsAtStart);
            Assert.AreEqual(12, p.Stats.MaxGhostsCleared);
        }

        [Test]
        public void Schema1Ghosts_MigrateWearingTheDefaultSkin_WithNoKnownDeathX()
        {
            string json = "{\"schema\":1,\"contentVersion\":\"" + Version + "\",\"best\":10,\"ghosts\":[" +
                          "{\"runIndex\":3,\"tapTicks\":[5],\"deathTick\":90,\"speedPct\":100}]}";
            File.WriteAllText(_saves.FilePath, json);
            Progress p = SaveMapper.ToProgress(_saves.Load(), Version, out bool discarded);

            Assert.IsFalse(discarded);
            GhostRecord g = p.Ghosts[0];
            Assert.AreEqual(CosmeticsSaveData.DefaultCharacter, g.SkinId);
            Assert.AreEqual(-1, g.DeathX, "the gravestone falls back to a replay");
            Assert.AreEqual(DeathCause.None, g.Cause);
            Assert.AreEqual(0, p.Stats.Deaths);
        }

        [Test]
        public void AchievementProgress_RoundTrips_AndOldIdsMapToTheLaunchSet()
        {
            var data = new SaveData();
            SaveMapper.Write(SampleProgress(), data, Version);
            data.Achievements = new System.Collections.Generic.List<string> { "m500", "full_house", "instant", "close_one" };
            data.AchievementProgress = new System.Collections.Generic.Dictionary<string, int>
            {
                { "bad_decisions", 41 }, { "echo_chamber", 999 }, { "not_a_thing", 3 },
            };
            _saves.Save(data);
            Progress p = SaveMapper.ToProgress(_saves.Load(), Version, out _);

            CollectionAssert.AreEquivalent(new[] { "going_somewhere", "army_of_me", "close_one" }, p.Achievements);
            Assert.AreEqual(500, p.AchievementProgress["going_somewhere"], "migrated unlocks show as complete");
            Assert.AreEqual(41, p.AchievementProgress["bad_decisions"]);
            Assert.AreEqual(10, p.AchievementProgress["echo_chamber"], "capped at the target");
            Assert.IsFalse(p.AchievementProgress.ContainsKey("not_a_thing"));

            var again = new SaveData();
            SaveMapper.Write(p, again, Version);
            Assert.AreEqual(41, again.AchievementProgress["bad_decisions"]);
        }

        [Test]
        public void Reels_RoundTrip_AndOnesFromAnOlderVersionStayButCannotPlay()
        {
            var p = SampleProgress();
            var reel = new Echo.Core.Reels.Reel
            {
                ContentVersion = Version, Seed = 4242u, SpeedPct = 110, RunIndex = 9,
                Options = new RunOptions { Bounties = true, Twist = Twist.Mirror, TwistWeek = 2901 },
                Run = new GhostRecord { RunIndex = 9, TapTicks = new[] { 10, 50 }, ReleaseTicks = new[] { 20 }, ReviveTicks = new[] { 300 }, DeathTick = 900, SkinId = "char_frog" },
                ShieldTick = 40, PhaseTick = 70, ContinueTick = 300, ExtraLife = true,
                Score = 1234, Distance = 456, NearMisses = 7, GhostsAtStart = 2, NewBest = true, SavedOn = "9 Oct 2026",
            };
            reel.Ghosts.Add(new GhostRecord { RunIndex = 3, TapTicks = new[] { 5 }, DeathTick = 200, Kills = 2 });
            reel.Ghosts.Add(new GhostRecord { RunIndex = 4, TapTicks = new[] { 6 }, DeathTick = 210 });
            p.Reels.Add(reel);

            var data = new SaveData();
            SaveMapper.Write(p, data, Version);
            _saves.Save(data);
            Echo.Core.Reels.Reel back = SaveMapper.ToProgress(_saves.Load(), Version, out _).Reels[0];
            Assert.AreEqual(4242u, back.Seed);
            Assert.AreEqual(110, back.SpeedPct);
            Assert.AreEqual(9, back.RunIndex);
            Assert.IsTrue(back.Options.Bounties);
            Assert.AreEqual(Twist.Mirror, back.Options.Twist);
            Assert.AreEqual(2901, back.Options.TwistWeek);
            CollectionAssert.AreEqual(new[] { 10, 50 }, back.Run.TapTicks);
            CollectionAssert.AreEqual(new[] { 20 }, back.Run.ReleaseTicks);
            CollectionAssert.AreEqual(new[] { 300 }, back.Run.ReviveTicks);
            Assert.AreEqual(900, back.Run.DeathTick);
            Assert.AreEqual("char_frog", back.Run.SkinId);
            Assert.AreEqual((40, 70, 300, true), (back.ShieldTick, back.PhaseTick, back.ContinueTick, back.ExtraLife));
            Assert.AreEqual((1234, 456, 7, 2, true, "9 Oct 2026"), (back.Score, back.Distance, back.NearMisses, back.GhostsAtStart, back.NewBest, back.SavedOn));
            Assert.AreEqual(2, back.Ghosts.Count);
            Assert.AreEqual(2, back.Ghosts[0].Kills);
            Assert.IsTrue(back.PlayableOn(Version));

            Progress later = SaveMapper.ToProgress(_saves.Load(), "a-new-version", out _);
            Assert.AreEqual(1, later.Reels.Count, "kept on the shelf");
            Assert.IsFalse(later.Reels[0].PlayableOn("a-new-version"));
        }

        [Test]
        public void TwistBest_DuelAndGallery_RoundTrip_EvenWhenGhostsAreDiscarded()
        {
            var p = SampleProgress();
            p.TwistWeek = 2900;
            p.TwistBest = 340;
            p.DuelUsed = true;
            var nemesis = new GhostRecord { RunIndex = 7, TapTicks = new[] { 5, 9 }, DeathTick = 400, Kills = 6, SkinId = "char_cat" };
            RoguesGallery.Add(p.Gallery, RoguesGallery.Entry(nemesis, 1234u, Version, "7 Oct 2026"));

            var data = new SaveData();
            SaveMapper.Write(p, data, Version);
            _saves.Save(data);
            Progress loaded = SaveMapper.ToProgress(_saves.Load(), "a-new-version", out bool discarded);

            Assert.IsTrue(discarded);
            Assert.AreEqual(2900, loaded.TwistWeek);
            Assert.AreEqual(340, loaded.TwistBest);
            Assert.IsTrue(loaded.DuelUsed);
            GalleryEntry e = loaded.Gallery[0];
            Assert.AreEqual("The Seventh", e.Name);
            Assert.AreEqual(6, e.Kills);
            Assert.AreEqual("char_cat", e.SkinId);
            Assert.AreEqual(1234u, e.Seed);
            Assert.AreEqual(Version, e.ContentVersion, "kept so its replay can tell whether the course still matches");
            CollectionAssert.AreEqual(new[] { 5, 9 }, e.Record.TapTicks);
        }

        [Test]
        public void TheBestRun_AndThePlayerName_RoundTrip_ForGhostChallenges()
        {
            var p = SampleProgress();
            p.BestRun = new Echo.Core.Challenges.Challenge
            {
                Seed = 99, SpeedPct = 110, Distance = 321,
                Record = new GhostRecord { RunIndex = 8, TapTicks = new[] { 10, 40 }, DeathTick = 900, DeathX = 54_000, SkinId = "char_cat" },
            };
            var data = new SaveData();
            data.Settings.PlayerName = "Jamie";
            SaveMapper.Write(p, data, Version);
            _saves.Save(data);
            SaveData loaded = _saves.Load();
            Progress back = SaveMapper.ToProgress(loaded, Version, out _);

            Assert.AreEqual("Jamie", loaded.Settings.PlayerName);
            Assert.IsNotNull(back.BestRun);
            Assert.AreEqual(99u, back.BestRun.Seed);
            Assert.AreEqual(321, back.BestRun.Distance);
            Assert.AreEqual(Version, back.BestRun.ContentVersion, "stamped with the version it was set on");
            CollectionAssert.AreEqual(new[] { 10, 40 }, back.BestRun.Record.TapTicks);
            Assert.AreEqual("char_cat", back.BestRun.Record.SkinId);
        }

        [Test]
        public void PowerUps_RoundTrip_AndAreCappedAtTwoOfEach()
        {
            var data = new SaveData();
            Echo.Game.Cosmetics.PowerUpInventory.Add(data.PowerUps, PowerUp.Shield);
            Echo.Game.Cosmetics.PowerUpInventory.Add(data.PowerUps, PowerUp.Shield);
            Assert.IsFalse(Echo.Game.Cosmetics.PowerUpInventory.Add(data.PowerUps, PowerUp.Shield), "two at most");
            Echo.Game.Cosmetics.PowerUpInventory.Add(data.PowerUps, PowerUp.ExtraLife);
            _saves.Save(data);
            SaveData loaded = _saves.Load();
            Assert.AreEqual(2, loaded.PowerUps.Shield);
            Assert.AreEqual(1, loaded.PowerUps.ExtraLife);
            Assert.IsTrue(Echo.Game.Cosmetics.PowerUpInventory.Take(loaded.PowerUps, PowerUp.ExtraLife));
            Assert.IsFalse(Echo.Game.Cosmetics.PowerUpInventory.Take(loaded.PowerUps, PowerUp.ExtraLife));
        }

        [Test]
        public void ContentVersionMismatch_DiscardsGhosts_KeepsBestAndCosmetics()
        {
            var data = new SaveData();
            data.Cosmetics.Owned.Add("runner_neon");
            SaveMapper.Write(SampleProgress(), data, Version);
            _saves.Save(data);

            SaveData loaded = _saves.Load();
            Progress p = SaveMapper.ToProgress(loaded, "ffffffff", out bool discarded);

            Assert.IsTrue(discarded);
            Assert.AreEqual(0, p.Ghosts.Count);
            Assert.AreEqual(812, p.Best);
            CollectionAssert.Contains(loaded.Cosmetics.Owned, "runner_neon");
        }

        [Test]
        public void FileUsesTheTechSpecFieldNames()
        {
            var data = new SaveData();
            SaveMapper.Write(SampleProgress(), data, Version);
            _saves.Save(data);
            string json = File.ReadAllText(_saves.FilePath);
            foreach (string key in new[] { "\"schema\"", "\"contentVersion\"", "\"nextRunIndex\"", "\"cycleBanishes\"", "\"tapTicks\"",
                         "\"reviveTicks\"", "\"lastKillRun\"", "\"worstNemesisName\"", "\"equipped\"", "\"haptics\"" })
                StringAssert.Contains(key, json);
        }

        [Test]
        public void Save_ReplacesTheExistingFile_AndLeavesNoTempFile()
        {
            var data = new SaveData { Best = 1 };
            _saves.Save(data);
            data.Best = 2;
            _saves.Save(data);
            Assert.AreEqual(2, _saves.Load().Best);
            Assert.IsFalse(File.Exists(_saves.FilePath + ".tmp"));
        }

        [Test]
        public void MissingFile_LoadsAsNull()
        {
            Assert.IsNull(_saves.Load());
        }

        [Test]
        public void CorruptFile_IsMovedToBak_AndLoadsAsNull()
        {
            File.WriteAllText(_saves.FilePath, "{ not json");
            Assert.IsNull(_saves.Load());
            Assert.IsFalse(File.Exists(_saves.FilePath));
            Assert.AreEqual("{ not json", File.ReadAllText(_saves.BackupPath));
        }

        [Test]
        public void UnknownSchema_IsTreatedAsCorrupt()
        {
            File.WriteAllText(_saves.FilePath, "{ \"schema\": 99 }");
            Assert.IsNull(_saves.Load());
            Assert.IsTrue(File.Exists(_saves.BackupPath));
        }

        [Test]
        public void NextRunIndex_IsAlwaysAfterTheNewestGhost()
        {
            var data = new SaveData { ContentVersion = Version, NextRunIndex = 2 };
            data.Ghosts.Add(new GhostSaveData { RunIndex = 6, DeathTick = 10 });
            Progress p = SaveMapper.ToProgress(data, Version, out _);
            Assert.AreEqual(7, p.NextRunIndex);
        }
    }
}

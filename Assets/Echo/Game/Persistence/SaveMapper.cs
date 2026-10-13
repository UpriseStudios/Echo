using System;
using Echo.Core.Challenges;
using Echo.Core.Achievements;
using Echo.Core.Ghosts;
using Echo.Core.Reels;
using Echo.Core.Rules;

namespace Echo.Game.Persistence
{
    /// <summary>Copies between the save document and Core's plain <see cref="Progress"/>.</summary>
    public static class SaveMapper
    {
        /// <summary>
        /// tech spec 6: ghosts saved under a different content version would replay on a different
        /// course, so they are discarded. Best score, stats and cosmetics are kept.
        /// </summary>
        public static Progress ToProgress(SaveData s, string contentVersion, out bool ghostsDiscarded)
        {
            var p = new Progress
            {
                Best = Math.Max(0, s.Best),
                Clears = Math.Max(0, s.Clears),
                Seed = unchecked((uint)s.Seed),
                NextRunIndex = Math.Max(1, s.NextRunIndex),
                CycleBanishes = Math.Max(0, s.CycleBanishes),
            };
            var st = s.Stats ?? new StatsSaveData();
            p.Stats.TotalRuns = st.TotalRuns;
            p.Stats.NemesesDefeated = st.NemesesDefeated;
            p.Stats.WorstNemesisKills = st.WorstNemesisKills;
            p.Stats.WorstNemesisName = st.WorstNemesisName ?? "";
            p.Stats.Deaths = Math.Max(0, st.Deaths);
            p.Stats.MaxDistance = Math.Max(0, st.MaxDistance);
            p.Stats.MaxGhostsAtStart = Math.Max(0, st.MaxGhostsAtStart);
            p.Stats.MaxGhostsCleared = Math.Max(0, st.MaxGhostsCleared);
            if (s.Achievements != null)
                foreach (string saved in s.Achievements)
                {
                    string id = MigrateAchievement(saved);
                    if (id == null || p.Achievements.Contains(id)) continue;
                    p.Achievements.Add(id);
                    AchievementDef def = AchievementCatalog.Find(id);
                    if (def != null) p.AchievementProgress[id] = def.Target;
                }
            if (s.AchievementProgress != null)
                foreach (var kv in s.AchievementProgress)
                {
                    AchievementDef def = AchievementCatalog.Find(kv.Key);
                    if (def == null || kv.Value <= 0) continue;
                    int value = Math.Min(kv.Value, def.Target);
                    if (!p.AchievementProgress.TryGetValue(kv.Key, out int had) || value > had) p.AchievementProgress[kv.Key] = value;
                }

            // Value-adds: twist bests, the duel flag and the Rogues' Gallery. Gallery runs keep their own content
            // version, so they survive a content change even when the live ghosts do not.
            p.TwistWeek = s.TwistWeek;
            p.TwistBest = Math.Max(0, s.TwistBest);
            p.DuelUsed = s.DuelUsed;
            p.BanishTenUsed = s.BanishTenUsed;
            p.RunsSinceBest = Math.Max(0, s.RunsSinceBest);
            if (!string.IsNullOrEmpty(s.BestRun) && ChallengeCode.TryDecode(s.BestRun, out Challenge bestRun)) p.BestRun = bestRun;
            if (s.Gallery != null)
                foreach (GallerySaveData e in s.Gallery)
                {
                    if (e?.Ghost == null) continue;
                    RoguesGallery.Add(p.Gallery, new GalleryEntry
                    {
                        Name = e.Name ?? "", SkinId = e.SkinId ?? "", Kills = Math.Max(0, e.Kills),
                        DefeatedOn = e.DefeatedOn ?? "", Epitaph = e.Epitaph ?? "",
                        Seed = unchecked((uint)e.Seed), ContentVersion = e.ContentVersion ?? "",
                        Record = ToRecord(e.Ghost, false),
                    });
                }

            // Reels keep their own content version: from an older one they stay on the shelf but cannot play.
            if (s.Reels != null)
                foreach (ReelSaveData r in s.Reels)
                {
                    if (r?.Run == null || p.Reels.Count >= ReelRules.Capacity) continue;
                    p.Reels.Add(ToReel(r));
                }

            ghostsDiscarded = false;
            if (s.Ghosts == null || s.Ghosts.Count == 0) return p;
            if (s.ContentVersion != contentVersion)
            {
                ghostsDiscarded = true;
                return p;
            }

            // Schema 1 ghosts predate skins: they migrate wearing the default character (skins addendum), and their
            // gravestone position is recovered by replay when needed.
            bool v1 = s.Schema < 2;
            foreach (GhostSaveData g in s.Ghosts)
            {
                if (g == null || g.RunIndex <= 0) continue;
                GhostRecord record = ToRecord(g, v1);
                if (FindByRunIndex(p.Ghosts, record.RunIndex)) continue; // hand-edited save: keep the first
                p.Ghosts.Add(record);
                if (record.RunIndex >= p.NextRunIndex) p.NextRunIndex = record.RunIndex + 1;
            }
            return p;
        }

        public static void Write(Progress p, SaveData s, string contentVersion)
        {
            s.Schema = SaveData.CurrentSchema;
            s.ContentVersion = contentVersion;
            s.Best = p.Best;
            s.Clears = p.Clears;
            s.Seed = p.Seed;
            s.NextRunIndex = p.NextRunIndex;
            s.CycleBanishes = p.CycleBanishes;

            s.Ghosts = s.Ghosts ?? new System.Collections.Generic.List<GhostSaveData>();
            s.Ghosts.Clear();
            foreach (GhostRecord g in p.Ghosts.All)
            {
                s.Ghosts.Add(ToSave(g));
            }

            s.Stats = s.Stats ?? new StatsSaveData();
            s.Stats.TotalRuns = p.Stats.TotalRuns;
            s.Stats.NemesesDefeated = p.Stats.NemesesDefeated;
            s.Stats.WorstNemesisKills = p.Stats.WorstNemesisKills;
            s.Stats.WorstNemesisName = p.Stats.WorstNemesisName;
            s.Stats.Deaths = p.Stats.Deaths;
            s.Stats.MaxDistance = p.Stats.MaxDistance;
            s.Stats.MaxGhostsAtStart = p.Stats.MaxGhostsAtStart;
            s.Stats.MaxGhostsCleared = p.Stats.MaxGhostsCleared;

            s.Achievements = new System.Collections.Generic.List<string>(p.Achievements);
            s.AchievementProgress = new System.Collections.Generic.Dictionary<string, int>(p.AchievementProgress);

            s.TwistWeek = p.TwistWeek;
            s.TwistBest = p.TwistBest;
            s.DuelUsed = p.DuelUsed;
            s.BanishTenUsed = p.BanishTenUsed;
            s.RunsSinceBest = p.RunsSinceBest;
            if (p.BestRun != null && string.IsNullOrEmpty(p.BestRun.ContentVersion)) p.BestRun.ContentVersion = contentVersion;
            s.BestRun = p.BestRun != null ? ChallengeCode.Encode(p.BestRun) : "";
            s.Gallery = new System.Collections.Generic.List<GallerySaveData>();
            foreach (GalleryEntry e in p.Gallery)
                s.Gallery.Add(new GallerySaveData
                {
                    Name = e.Name, SkinId = e.SkinId, Kills = e.Kills, DefeatedOn = e.DefeatedOn, Epitaph = e.Epitaph,
                    Seed = e.Seed, ContentVersion = e.ContentVersion, Ghost = ToSave(e.Record),
                });
            s.Reels = new System.Collections.Generic.List<ReelSaveData>();
            foreach (Reel r in p.Reels) s.Reels.Add(ToSave(r));
        }

        static Reel ToReel(ReelSaveData r)
        {
            var reel = new Reel
            {
                ContentVersion = r.ContentVersion ?? "",
                Seed = unchecked((uint)r.Seed),
                SpeedPct = r.SpeedPct,
                RunIndex = r.RunIndex,
                Options = new RunOptions
                {
                    Bounties = r.Bounties,
                    Twist = Enum.IsDefined(typeof(Twist), r.Twist) ? (Twist)r.Twist : Twist.None,
                    TwistWeek = r.TwistWeek,
                    Challenge = r.Challenge,
                },
                Run = ToRecord(r.Run, false),
                ShieldTick = r.ShieldTick,
                PhaseTick = r.PhaseTick,
                ContinueTick = r.ContinueTick,
                ExtraLife = r.ExtraLife,
                Score = Math.Max(0, r.Score),
                Distance = Math.Max(0, r.Distance),
                NearMisses = Math.Max(0, r.NearMisses),
                GhostsAtStart = Math.Max(0, r.GhostsAtStart),
                NewBest = r.NewBest,
                SavedOn = r.SavedOn ?? "",
            };
            if (r.Ghosts != null)
                foreach (GhostSaveData g in r.Ghosts)
                    if (g != null && g.RunIndex > 0) reel.Ghosts.Add(ToRecord(g, false));
            reel.Ghosts.Sort((a, b) => a.RunIndex.CompareTo(b.RunIndex));
            return reel;
        }

        static ReelSaveData ToSave(Reel r)
        {
            var save = new ReelSaveData
            {
                ContentVersion = r.ContentVersion, Seed = r.Seed, SpeedPct = r.SpeedPct, RunIndex = r.RunIndex,
                Bounties = r.Options.Bounties, Twist = (int)r.Options.Twist, TwistWeek = r.Options.TwistWeek, Challenge = r.Options.Challenge,
                Run = ToSave(r.Run), ShieldTick = r.ShieldTick, PhaseTick = r.PhaseTick, ContinueTick = r.ContinueTick, ExtraLife = r.ExtraLife,
                Score = r.Score, Distance = r.Distance, NearMisses = r.NearMisses, GhostsAtStart = r.GhostsAtStart,
                NewBest = r.NewBest, SavedOn = r.SavedOn,
            };
            foreach (GhostRecord g in r.Ghosts) save.Ghosts.Add(ToSave(g));
            return save;
        }

        // The v1 placeholder achievements map onto their launch-set successors (skins addendum); others drop.
        static string MigrateAchievement(string id)
        {
            switch (id)
            {
                case "full_house": return AchievementIds.ArmyOfMe;
                case "grudge": return AchievementIds.Seriously;
                case "first_nemesis": return AchievementIds.DejaVu;
                case "crowded": return AchievementIds.TheyreEverywhere;
                case "threading": return AchievementIds.CloseOne;
                case "m500": return AchievementIds.GoingSomewhere;
                case "m1000": return AchievementIds.LongWayFromHome;
                case "nemesis_down":
                case "five_clears": return AchievementIds.FreshStart;
                default: return AchievementCatalog.Find(id) != null ? id : null;
            }
        }

        static GhostRecord ToRecord(GhostSaveData g, bool v1) => new GhostRecord
        {
            RunIndex = g.RunIndex,
            TapTicks = SortedCopy(g.TapTicks),
            ReviveTicks = SortedCopy(g.ReviveTicks),
            ReleaseTicks = SortedCopy(g.ReleaseTicks),
            DeathTick = g.DeathTick,
            SpeedPct = g.SpeedPct,
            Kills = g.Kills,
            LastKillRun = g.LastKillRun,
            SkinId = v1 || string.IsNullOrEmpty(g.SkinId) ? CosmeticsSaveData.DefaultCharacter : g.SkinId,
            DeathX = v1 ? -1 : g.DeathX,
            Cause = v1 || !Enum.IsDefined(typeof(DeathCause), g.Cause) ? DeathCause.None : (DeathCause)g.Cause,
            KillerRunIndex = v1 ? 0 : Math.Max(0, g.KillerRunIndex),
        };

        static GhostSaveData ToSave(GhostRecord g) => new GhostSaveData
        {
            RunIndex = g.RunIndex,
            TapTicks = (int[])g.TapTicks.Clone(),
            ReviveTicks = (int[])g.ReviveTicks.Clone(),
            ReleaseTicks = (int[])g.ReleaseTicks.Clone(),
            DeathTick = g.DeathTick,
            SpeedPct = g.SpeedPct,
            Kills = g.Kills,
            LastKillRun = g.LastKillRun,
            SkinId = g.SkinId ?? "",
            DeathX = g.DeathX,
            Cause = (int)g.Cause,
            KillerRunIndex = g.KillerRunIndex,
        };

        static bool FindByRunIndex(GhostSet set, int runIndex)
        {
            for (int i = 0; i < set.Count; i++)
                if (set[i].RunIndex == runIndex) return true;
            return false;
        }

        static int[] SortedCopy(int[] ticks)
        {
            if (ticks == null) return Array.Empty<int>();
            var copy = (int[])ticks.Clone();
            Array.Sort(copy);
            return copy;
        }
    }
}

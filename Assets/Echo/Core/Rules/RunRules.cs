using System;
using System.Collections.Generic;
using Echo.Core.Challenges;
using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Sim;

namespace Echo.Core.Rules
{
    /// <summary>Run start, death-screen offers and the commit (tech spec 7).</summary>
    public static class RunRules
    {
        public const int MaxBanishesPerCycle = 3;

        public const int DuelKills = 5;

        /// <summary>Balance addendum: how many ghosts Banish 10 clears.</summary>
        public const int BanishTenCount = 10;

        public static RunSimulation StartRun(Progress p, Course course, in SimConfig cfg, RunOptions options = default)
        {
            if (course.Seed != p.Seed) throw new ArgumentException("course seed does not match progress seed", nameof(course));
            options.Duel = false;
            options.Daily = false;
            return new RunSimulation(course, p.Ghosts, p.NextRunIndex, cfg.SpeedPctForClears(p.Clears), cfg, options);
        }

        /// <summary>The best a run is measured against: the classic best, or this week's twist best.</summary>
        public static int BestFor(RunSimulation run, Progress p) =>
            run.Options.Twist == Twist.None ? p.Best : p.TwistWeek == run.Options.TwistWeek ? p.TwistBest : 0;

        // Nemesis Duel (value-adds doc): offered once a Nemesis has 5 kills, once per high-score cycle.
        public static bool CanDuel(RunSimulation run, Progress p)
        {
            GhostRecord nemesis = p.Ghosts.Nemesis;
            return run.IsDead && !run.Options.Duel && !run.Options.Challenge && !run.Options.Daily && !p.DuelUsed && nemesis != null && nemesis.Kills >= DuelKills;
        }

        /// <summary>The current course with only the Nemesis. Uses up this cycle's duel.</summary>
        /// <summary>Ghost Challenges (value-adds doc): the friend's run alone, on its own course and at its own speed.</summary>
        public static RunSimulation StartChallenge(Progress p, Course course, in SimConfig cfg, Challenge challenge)
        {
            if (course.Seed != challenge.Seed) throw new InvalidOperationException("a challenge is played on its own course");
            var ghosts = new GhostSet();
            ghosts.Add(challenge.FreshRecord());
            if (challenge.Lite)
                return new RunSimulation(course, ghosts, p.NextRunIndex, challenge.SpeedPct, cfg, new RunOptions { Challenge = true });

            // Balance addendum (fair challenges): the challenger's ghosts as they were when their run started, fresh
            // copies so kills never carry between attempts, under the twist it was recorded with. The run takes the
            // challenger's run index, so the same bounty ghost is picked. Duels never become challenges.
            Echo.Core.Reels.Reel snap = challenge.Snapshot;
            foreach (GhostRecord g in snap.Ghosts)
                if (g.RunIndex != challenge.Record.RunIndex) ghosts.Add(g.Clone());
            var options = new RunOptions
            {
                Challenge = true,
                Bounties = snap.Options.Bounties,
                Twist = snap.Options.Twist,
                TwistWeek = snap.Options.TwistWeek,
            };
            return new RunSimulation(course, ghosts, challenge.Record.RunIndex, challenge.SpeedPct, cfg, options);
        }

        /// <summary>Settles a challenge run. Challenge runs never touch the player's ghosts, best or Nemesis.</summary>
        public static ChallengeOutcome CommitChallenge(RunSimulation run, Challenge challenge)
        {
            if (!run.Options.Challenge) throw new InvalidOperationException("not a challenge");
            if (run.Committed) throw new InvalidOperationException("run already committed");
            run.Committed = true;
            bool byScore = !challenge.Lite;
            int mine = byScore ? run.Score : run.Distance, theirs = byScore ? challenge.Score : challenge.Distance;
            return new ChallengeOutcome
            {
                Distance = run.Distance,
                TheirDistance = challenge.Distance,
                Score = run.Score,
                TheirScore = challenge.Score,
                ByScore = byScore,
                Won = mine > theirs,
                Margin = Math.Abs(mine - theirs),
                // The challenger's own ghost ended it (not just any of their graveyard).
                KilledByGhost = run.Cause == DeathCause.Ghost && run.Killer != null && run.Killer.RunIndex == challenge.Record.RunIndex,
            };
        }

        public static RunSimulation StartDuel(Progress p, Course course, in SimConfig cfg)
        {
            GhostRecord nemesis = p.Ghosts.Nemesis ?? throw new InvalidOperationException("no Nemesis to duel");
            var alone = new GhostSet();
            alone.Add(nemesis);
            p.DuelUsed = true;
            return new RunSimulation(course, alone, p.NextRunIndex, cfg.SpeedPctForClears(p.Clears), cfg, new RunOptions { Duel = true });
        }

        /// <summary>
        /// Ends a duel. Past the point where the Nemesis died: banished for free (and into the gallery). Otherwise
        /// it gains a kill (the sim has already counted one if it was the Nemesis that killed you). No ghost is
        /// recorded and no best is set.
        /// </summary>
        public static CommitResult CommitDuel(Progress p, RunSimulation run, GhostRecord nemesis)
        {
            if (!run.Options.Duel) throw new InvalidOperationException("not a duel");
            if (run.Committed) throw new InvalidOperationException("run already committed");
            run.Committed = true;
            var result = new CommitResult { Score = run.Score, PreviousBest = p.Best, Duel = true, DuelWon = run.DuelWon };
            if (run.DuelWon)
            {
                p.Ghosts.Remove(nemesis);
                p.Stats.NemesesDefeated++;
                result.NemesisDefeated = true;
                result.DefeatedNemesis = nemesis;
                result.DefeatedNemesisName = NemesisNames.For(nemesis.RunIndex);
            }
            else
            {
                p.Stats.Deaths += 1 + run.LivesUsed + (run.ContinueUsed ? 1 : 0);
                if (run.Killer != nemesis) nemesis.Kills++;
                nemesis.LastKillRun = run.RunIndex;
                NoteWorstNemesis(p);
            }
            result.Nemesis = p.Ghosts.Nemesis;
            return result;
        }

        // Balance addendum: once on every run, with no score condition; never in duels or friend challenges.
        public static bool CanContinue(RunSimulation run) =>
            run.IsDead && !run.Committed && !run.Options.Duel && !run.Options.Challenge && !run.Options.Daily && !run.ContinueUsed && !run.BanishUsed;

        /// <summary>
        /// Balance addendum: Banish 10 is offered when the graveyard is full (GhostCap) once this run is committed,
        /// once per high-score cycle. A run that sets a new best clears every ghost instead, so it is never offered then.
        /// </summary>
        public static bool CanBanishTen(RunSimulation run, Progress p, in SimConfig cfg)
        {
            if (!run.IsDead || run.Options.Duel || run.Options.Challenge || run.Options.Daily || p.BanishTenUsed) return false;
            bool clears = run.Options.Twist == Twist.None && run.Score > p.Best;
            int after = run.Committed ? p.Ghosts.Count : clears ? 0 : Math.Min(cfg.GhostCap, p.Ghosts.Count + 1);
            return after >= cfg.GhostCap;
        }

        /// <summary>
        /// The ghosts Banish 10 takes: the non-Nemesis ghosts that have killed the player most, ties to the oldest, so
        /// when fewer than 10 have kills the oldest fill the rest. The Nemesis is never removed.
        /// </summary>
        public static List<GhostRecord> ChooseBanishTen(GhostSet ghosts)
        {
            GhostRecord nemesis = ghosts.Nemesis;
            var candidates = new List<GhostRecord>();
            foreach (GhostRecord g in ghosts.All)
                if (g != nemesis) candidates.Add(g);
            candidates.Sort((a, b) => a.Kills != b.Kills ? b.Kills.CompareTo(a.Kills) : a.RunIndex.CompareTo(b.RunIndex));
            if (candidates.Count > BanishTenCount) candidates.RemoveRange(BanishTenCount, candidates.Count - BanishTenCount);
            return candidates;
        }

        /// <summary>
        /// Removes the 10 after a successful rewarded ad. The run must be committed first (its ghost added and the
        /// cap applied), so a full graveyard of 30 leaves 20. Returns the removed ghosts for their graves.
        /// </summary>
        public static List<GhostRecord> BanishTen(RunSimulation run, Progress p, in SimConfig cfg)
        {
            if (!run.Committed) throw new InvalidOperationException("balance addendum: the run is committed before Banish 10");
            if (!CanBanishTen(run, p, cfg)) throw new InvalidOperationException("Banish 10 is not available");
            List<GhostRecord> removed = ChooseBanishTen(p.Ghosts);
            foreach (GhostRecord g in removed) p.Ghosts.Remove(g);
            p.BanishTenUsed = true;
            return removed;
        }

        public static bool CanBanish(RunSimulation run, Progress p) =>
            run.IsDead && !run.Committed && !run.Options.Duel && !run.Options.Challenge && !run.Options.Daily && !run.BanishUsed
            && run.Cause == DeathCause.Ghost && run.Killer != null
            && p.CycleBanishes < MaxBanishesPerCycle
            && p.Ghosts.Contains(run.Killer)
            && run.Killer != p.Ghosts.Nemesis;

        /// <summary>Removes the killer after a successful rewarded ad. The caller commits the run next.</summary>
        public static void Banish(RunSimulation run, Progress p)
        {
            if (!CanBanish(run, p)) throw new InvalidOperationException("banish is not available for this run");
            p.Ghosts.Remove(run.Killer);
            p.CycleBanishes++;
            run.BanishUsed = true;
        }

        /// <summary>
        /// Commit order (tech spec 7), steps 1 to 3. The caller then evaluates achievements and saves (step 4).
        /// <paramref name="newSeed"/> is only used on a new best; it may come from a non-deterministic source.
        /// </summary>
        public static CommitResult Commit(Progress p, RunSimulation run, uint newSeed, in SimConfig cfg)
        {
            if (!run.IsDead) throw new InvalidOperationException("tech spec 7: a run still in progress is discarded, not committed");
            if (run.Committed) throw new InvalidOperationException("run already committed");
            if (run.Options.Duel) throw new InvalidOperationException("duels commit through CommitDuel");
            if (run.Options.Challenge) throw new InvalidOperationException("challenges commit through CommitChallenge");
            if (run.Options.Daily) throw new InvalidOperationException("daily runs are settled by the server, never committed to progress");
            run.Committed = true;

            var result = new CommitResult { Score = run.Score, PreviousBest = p.Best };
            p.Stats.TotalRuns++;
            p.Stats.Deaths += 1 + run.LivesUsed + (run.ContinueUsed ? 1 : 0);
            if (run.Distance > p.Stats.MaxDistance) p.Stats.MaxDistance = run.Distance;
            if (run.GhostsAtStart > p.Stats.MaxGhostsAtStart) p.Stats.MaxGhostsAtStart = run.GhostsAtStart;
            NoteWorstNemesis(p);

            // Weekly Twists: a twist run has its own weekly best and never clears the ghosts; the classic best and
            // its ghost clear belong to classic runs, so personal bests stay comparable (value-adds doc).
            bool twist = run.Options.Twist != Twist.None;
            if (twist)
            {
                if (p.TwistWeek != run.Options.TwistWeek)
                {
                    p.TwistWeek = run.Options.TwistWeek;
                    p.TwistBest = 0;
                }
                result.TwistRun = true;
                result.PreviousTwistBest = p.TwistBest;
                if (result.Score > p.TwistBest)
                {
                    result.NewTwistBest = true;
                    p.TwistBest = result.Score;
                }
            }

            if (!twist && result.Score > p.Best)
            {
                GhostRecord nemesis = p.Ghosts.Nemesis;
                result.NewBest = true;
                result.NemesisDefeated = nemesis != null;
                result.DefeatedNemesis = nemesis;
                result.DefeatedNemesisName = nemesis != null ? NemesisNames.For(nemesis.RunIndex) : null;
                result.GhostsCleared = p.Ghosts.Count;
                if (result.GhostsCleared > p.Stats.MaxGhostsCleared) p.Stats.MaxGhostsCleared = result.GhostsCleared;
                if (nemesis != null) p.Stats.NemesesDefeated++;

                p.Best = result.Score;
                p.BestRun = ChallengeRuns.FromRun(run);
                // Balance addendum: the best run keeps its ghost snapshot, so Stats can send a full challenge.
                p.BestReel = Echo.Core.Reels.ReelRules.CanSave(run) ? Echo.Core.Reels.ReelRules.From(run, "", "", true) : null;
                p.Ghosts.Clear();
                p.CycleBanishes = 0;
                p.BanishTenUsed = false;
                p.DuelUsed = false;
                p.RunsSinceBest = 0;
                p.Seed = newSeed;
                p.Clears++;
            }
            else
            {
                if (!twist) p.RunsSinceBest++;
                result.AddedGhost = run.ToGhostRecord();
                p.Ghosts.Add(result.AddedGhost);
                result.Evicted = p.Ghosts.ApplyCap(cfg.GhostCap);
            }

            result.Nemesis = p.Ghosts.Nemesis;
            p.NextRunIndex++;
            return result;
        }

        static void NoteWorstNemesis(Progress p)
        {
            GhostRecord nemesis = p.Ghosts.Nemesis;
            if (nemesis == null || nemesis.Kills <= p.Stats.WorstNemesisKills) return;
            p.Stats.WorstNemesisKills = nemesis.Kills;
            p.Stats.WorstNemesisName = NemesisNames.For(nemesis.RunIndex);
        }
    }
}

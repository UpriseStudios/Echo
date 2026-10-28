using System;
using System.Collections.Generic;
using Echo.Core.Ghosts;
using Echo.Core.Rules;

namespace Echo.Core.Achievements
{
    /// <summary>
    /// Achievements from events, never per-frame checks (skins addendum): a run ending (step 4 of the commit
    /// order), a session starting, and a quick restart. Each event reports progress; an achievement unlocks
    /// when its progress reaches the target, and only ever once.
    /// </summary>
    public static class AchievementRules
    {
        public const int NoFearGhosts = 10;
        public const int NoFearMetres = 500;
        public const int SoCloseMetres = 10;
        public const int GraveyardWaiting = 3;

        /// <summary>A committed run. Returns the newly unlocked ids in display order.</summary>
        public static List<string> OnRunEnded(RunSimulation run, CommitResult result, Progress p, int sessionRuns)
        {
            var earned = new List<string>();
            bool byGhost = run.Cause == DeathCause.Ghost && run.Killer != null;

            Report(p, AchievementIds.HelloAgain, run.GhostsPassed > 0 || byGhost ? 1 : 0, earned);
            Report(p, AchievementIds.ThatWasMe, byGhost ? 1 : 0, earned);
            int kills = byGhost ? run.Killer.Kills : 0;
            Report(p, AchievementIds.DejaVu, kills, earned);
            Report(p, AchievementIds.Seriously, kills, earned);
            Report(p, AchievementIds.SwornEnemy, kills, earned);

            int ghosts = run.GhostsAtStart;
            Report(p, AchievementIds.CrowdedHouse, ghosts, earned);
            Report(p, AchievementIds.EchoChamber, ghosts, earned);
            Report(p, AchievementIds.Haunted, ghosts, earned);
            Report(p, AchievementIds.TheyreEverywhere, ghosts, earned);
            Report(p, AchievementIds.ArmyOfMe, ghosts, earned);
            Report(p, AchievementIds.CostumeParty, DistinctSkins(run.Ghosts), earned);

            Report(p, AchievementIds.CloseOne, run.NearMisses > 0 ? 1 : 0, earned);
            Report(p, AchievementIds.ThreadTheNeedle, run.NearMissPair ? 1 : 0, earned);
            Report(p, AchievementIds.HowDidISurvive, run.NearMissTriple ? 1 : 0, earned);
            bool toTheCourse = run.Cause == DeathCause.Hazard || run.Cause == DeathCause.Fall;
            Report(p, AchievementIds.NoFear, ghosts >= NoFearGhosts && run.Distance >= NoFearMetres && toTheCourse ? 1 : 0, earned);

            int cleared = result.NewBest ? result.GhostsCleared : 0;
            Report(p, AchievementIds.FreshStart, cleared > 0 ? 1 : 0, earned);
            Report(p, AchievementIds.Exorcist, cleared, earned);
            Report(p, AchievementIds.MassExorcism, cleared, earned);
            // Scores count roughly a point per metre, so "within 10 m of your best" is read off the score.
            bool soClose = !result.NewBest && result.PreviousBest > 0 && result.PreviousBest - result.Score <= SoCloseMetres;
            Report(p, AchievementIds.SoClose, soClose ? 1 : 0, earned);
            Report(p, AchievementIds.SmashedIt, result.NewBest && result.PreviousBest > 0 ? result.Score - result.PreviousBest : 0, earned);
            Report(p, AchievementIds.PerfectEcho, PassedAGrave(run) ? 1 : 0, earned);

            Report(p, AchievementIds.GraveyardShift, ghosts, earned);

            Report(p, AchievementIds.GettingStarted, run.Distance, earned);
            Report(p, AchievementIds.GoingSomewhere, run.Distance, earned);
            Report(p, AchievementIds.LongWayFromHome, run.Distance, earned);
            bool untouched = result.NewBest && ghosts > 0 && run.NearMisses == 0 && run.GhostHits == 0;
            Report(p, AchievementIds.Untouchable, untouched ? 1 : 0, earned);

            Report(p, AchievementIds.BadDecisions, p.Stats.Deaths, earned);
            Report(p, AchievementIds.JustOneMore, sessionRuns, earned);

            earned.Sort((a, b) => IndexOf(a).CompareTo(IndexOf(b)));
            return earned;
        }

        /// <summary>The game opened with graves already on the course: Unfinished Business.</summary>
        public static List<string> OnSessionStarted(Progress p)
        {
            var earned = new List<string>();
            Report(p, AchievementIds.UnfinishedBusiness, p.Ghosts.Count >= GraveyardWaiting ? 1 : 0, earned);
            return earned;
        }

        /// <summary>
        /// A Daily Haunt run ended (version 2.0) on <paramref name="day"/> (whole UTC days, from the server), going
        /// further than <paramref name="leadersPassed"/> of today's leaders.
        /// </summary>
        public static List<string> OnDailyRunEnded(Progress p, int day, int leadersPassed)
        {
            var earned = new List<string>();
            Echo.Core.Daily.DailyRules.NotePlayed(p.Daily, day);
            Report(p, AchievementIds.HauntedByStrangers, p.Daily.DaysPlayed, earned);
            Report(p, AchievementIds.DailyGrind, p.Daily.Streak, earned);
            Report(p, AchievementIds.PassingThrough, leadersPassed > 0 ? 1 : 0, earned);
            return earned;
        }

        /// <summary>A day the player played has closed; the server reports where they finished (0: no rank).</summary>
        public static List<string> OnDailyDayClosed(Progress p, int finalRank)
        {
            var earned = new List<string>();
            Report(p, AchievementIds.TopTen, finalRank >= 1 && finalRank <= Echo.Core.Daily.DailyRules.Leaders ? 1 : 0, earned);
            return earned;
        }

        /// <summary>Seasons doc: Snowed Under's ghosts, the graveyard's cap.</summary>
        public const int SnowedUnderGhosts = 30;

        /// <summary>A main-game run ended during a seasonal event (seasons doc).</summary>
        public static List<string> OnSeasonalRunEnded(Progress p, RunSimulation run, SeasonalContext s)
        {
            var earned = new List<string>();
            if (s.Christmas != null)
                ReportSeasonal(p, AchievementIds.SnowedUnder, s.Christmas, run.GhostsAtStart >= SnowedUnderGhosts ? 1 : 0, earned);
            return earned;
        }

        /// <summary>
        /// A Daily Haunt run the server accepted during a seasonal event, on <paramref name="day"/> (whole UTC days from
        /// the server), going further than <paramref name="leadersPassed"/> of that day's leaders.
        /// </summary>
        public static List<string> OnSeasonalDailyRun(Progress p, int day, int leadersPassed, SeasonalContext s)
        {
            var earned = new List<string>();
            if (s.Christmas != null)
            {
                string twelve = AchievementIds.TwelveDays;
                bool newDay = !SameEvent(p, twelve, s.Christmas) || !p.Seasonal.LastDay.TryGetValue(twelve, out int last) || day > last;
                ReportSeasonal(p, twelve, s.Christmas, Fresh(p, twelve, s.Christmas) + (newDay ? 1 : 0), earned);
                p.Seasonal.LastDay[twelve] = day;
                ReportSeasonal(p, AchievementIds.LeadTheWay, s.Christmas, Fresh(p, AchievementIds.LeadTheWay, s.Christmas) + Math.Max(0, leadersPassed), earned);
            }
            if (s.NewYearsDay != null) ReportSeasonal(p, AchievementIds.FirstFooter, s.NewYearsDay, 1, earned);
            return earned;
        }

        /// <summary>A seasonal achievement's progress in the event counting now: none between events or from an earlier one.</summary>
        public static int SeasonalProgressNow(Progress p, string id, SeasonalContext s)
        {
            AchievementDef def = AchievementCatalog.Find(id);
            string key = def != null && def.Seasonal ? s.KeyFor(def.Season) : null;
            return key == null ? 0 : Fresh(p, id, key);
        }

        static bool SameEvent(Progress p, string id, string eventKey) => p.Seasonal.Event.TryGetValue(id, out string had) && had == eventKey;

        // The progress made in this event so far: none if what's saved belongs to an earlier one.
        static int Fresh(Progress p, string id, string eventKey) => SameEvent(p, id, eventKey) ? ProgressOf(p, id) : 0;

        // Seasons doc: progress belongs to one event and starts again at the next if unfinished; once earned, kept.
        static void ReportSeasonal(Progress p, string id, string eventKey, int value, List<string> earned)
        {
            if (p.Achievements.Contains(id)) return;
            if (!SameEvent(p, id, eventKey))
            {
                p.AchievementProgress.Remove(id);
                p.Seasonal.LastDay.Remove(id);
                p.Seasonal.Event[id] = eventKey;
            }
            Report(p, id, value, earned);
        }

        /// <summary>The player restarted within a second of dying (the game measures the time).</summary>
        public static List<string> OnQuickRestart(Progress p)
        {
            var earned = new List<string>();
            Report(p, AchievementIds.ICanFixThis, ProgressOf(p, AchievementIds.ICanFixThis) + 1, earned);
            return earned;
        }

        public static int ProgressOf(Progress p, string id) => p.AchievementProgress.TryGetValue(id, out int v) ? v : 0;

        // Progress only ever rises; reaching the target unlocks once.
        static void Report(Progress p, string id, int value, List<string> earned)
        {
            AchievementDef def = AchievementCatalog.Find(id) ?? throw new ArgumentException($"unknown achievement {id}");
            int now = Math.Min(Math.Max(ProgressOf(p, id), value), def.Target);
            if (now > 0) p.AchievementProgress[id] = now;
            if (now < def.Target || p.Achievements.Contains(id)) return;
            p.Achievements.Add(id);
            earned.Add(id);
        }

        static int DistinctSkins(GhostManager ghosts)
        {
            var skins = new HashSet<string>();
            for (int i = 0; i < ghosts.Count; i++)
                if (!string.IsNullOrEmpty(ghosts.RecordAt(i).SkinId)) skins.Add(ghosts.RecordAt(i).SkinId);
            return skins.Count;
        }

        // Perfect Echo: the run went at least a metre past where one of its ghosts' runs ended.
        static bool PassedAGrave(RunSimulation run)
        {
            for (int i = 0; i < run.Ghosts.Count; i++)
            {
                int deathX = run.Ghosts.RecordAt(i).DeathX;
                if (deathX >= 0 && run.MaxX > deathX + 1000) return true;
            }
            return false;
        }

        static int IndexOf(string id) => Array.FindIndex(AchievementCatalog.All, d => d.Id == id);
    }
}

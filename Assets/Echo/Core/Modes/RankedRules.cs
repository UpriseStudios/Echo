using System;
using Echo.Core.Courses;
using Echo.Core.Daily;
using Echo.Core.Ghosts;
using Echo.Core.Reels;
using Echo.Core.Rules;
using Echo.Core.Sim;

namespace Echo.Core.Modes
{
    /// <summary>
    /// Ghost Duels (modes doc; "Ranked" in code, a user decision, since the Nemesis Duel owns "duel"): race one stranger's
    /// run on one of the season's arenas, one attempt, and go further than it to win. The rival runs alongside in
    /// lockstep and can never kill you; there are no other ghosts. Ratings are Elo (K 40 for the first 10 duels, then
    /// 24), in whole numbers like the rest of Core: the expected score comes from a table, in thousandths.
    /// </summary>
    public static class RankedRules
    {
        public const int StartRating = 1000;
        public const int MatchWindow = 150;
        public const int RankedPerDay = 10;
        public const int ProvisionalDuels = 10;
        public const int ProvisionalK = 40, SettledK = 24;
        public const int ArenasPerSeason = 10;
        public const int BandWidth = 100;
        public const int PoolPerBand = 200;
        public const int WraithTop = 100;

        /// <summary>The rival's run index, and the player's (the rival must sort first).</summary>
        public const int RivalRunIndex = 1, PlayerRunIndex = 2;

        public const string Bronze = "bronze", Silver = "silver", Gold = "gold", Phantom = "phantom", Wraith = "wraith";

        // 1000 / (1 + 10^(d / 400)) for d = -800, -775, ... 800, d being the opponent's rating minus yours.
        static readonly int[] Expected =
        {
            990, 989, 987, 985, 983, 980, 977, 973, 969, 965, 960, 954, 947, 939, 930, 920, 909, 896, 882, 867, 849, 830, 808,
            785, 760, 733, 703, 673, 640, 606, 571, 536, 500, 464, 429, 394, 360, 327, 297, 267, 240, 215, 192, 170, 151, 133,
            118, 104, 91, 80, 70, 61, 53, 46, 40, 35, 31, 27, 23, 20, 17, 15, 13, 11, 10,
        };

        /// <summary>Your expected score against the opponent, in thousandths (500 for equal ratings).</summary>
        public static int ExpectedScore(int rating, int opponent)
        {
            int d = Math.Max(-800, Math.Min(800, opponent - rating)) + 800;
            int i = d / 25, rem = d % 25;
            if (i >= Expected.Length - 1) return Expected[Expected.Length - 1];
            return Expected[i] + (Expected[i + 1] - Expected[i]) * rem / 25;
        }

        public static int K(int duelsPlayed) => duelsPlayed < ProvisionalDuels ? ProvisionalK : SettledK;

        /// <summary>The rating change for a result (1000 a win, 500 a draw, 0 a loss), rounded half away from zero.</summary>
        public static int Change(int rating, int opponent, int duelsPlayed, int scoreThousandths)
        {
            int numerator = K(duelsPlayed) * (scoreThousandths - ExpectedScore(rating, opponent));
            return RoundDiv(numerator, 1000);
        }

        /// <summary>The opponent's change: the same sum from their side, at half the size (they weren't there).</summary>
        public static int OpponentChange(int opponentRating, int rating, int opponentDuels, int opponentScoreThousandths) =>
            RoundDiv(K(opponentDuels) * (opponentScoreThousandths - ExpectedScore(opponentRating, rating)), 2000);

        static int RoundDiv(int a, int b) => a >= 0 ? (a + b / 2) / b : -((-a + b / 2) / b);

        /// <summary>1000 a win, 500 a draw, 0 a loss: further than the rival's run wins.</summary>
        public static int Score(int distance, int rivalDistance) => distance > rivalDistance ? 1000 : distance == rivalDistance ? 500 : 0;

        /// <summary>The tier: the season's top 100 are Wraith whatever their rating.</summary>
        public static string Tier(int rating, int rank) =>
            rank >= 1 && rank <= WraithTop ? Wraith : rating >= 1400 ? Phantom : rating >= 1200 ? Gold : rating >= 1000 ? Silver : Bronze;

        public static int TierOrder(string tier) => tier switch
        {
            Bronze => 0, Silver => 1, Gold => 2, Phantom => 3, Wraith => 4, _ => -1,
        };

        /// <summary>Between seasons: halfway back to the start, so good players begin near the top but must prove it.</summary>
        public static int SoftReset(int rating) => (rating + StartRating) / 2;

        /// <summary>The rating band a run is pooled in.</summary>
        public static int Band(int rating) => Math.Max(0, rating) / BandWidth;

        /// <summary>A ranked or practice run: the arena, the rival alone and harmless, at 100% speed.</summary>
        public static RunSimulation Start(Course course, GhostRecord rival, in SimConfig cfg)
        {
            GhostRecord r = rival.Clone();
            r.RunIndex = RivalRunIndex;
            r.Leader = false;
            r.Kills = 0;
            r.LastKillRun = 0;
            GhostSet set = DailyRules.Ghosts(new GhostRecord[0], new[] { r });
            return new RunSimulation(course, set, PlayerRunIndex, DailyRules.SpeedPct, cfg, new RunOptions { Daily = true, Ranked = true });
        }

        /// <summary>Why a submitted duel breaks the rules, or null: the daily rules, ranked, one rival.</summary>
        public static string Check(Reel reel)
        {
            string daily = DailyRules.Check(reel);
            if (daily != null) return daily;
            RunOptions o = reel.Options;
            if (!o.Ranked || o.OneLife || o.Graveyard) return "options";
            if (reel.RunIndex != PlayerRunIndex) return "run-index";
            if (reel.Ghosts.Count != 1 || reel.Ghosts[0].RunIndex != RivalRunIndex) return "ghosts";
            return null;
        }
    }
}

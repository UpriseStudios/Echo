using static Echo.Core.Achievements.AchievementIds;

namespace Echo.Core.Achievements
{
    /// <summary>Titles, requirements and targets for the launch set (skins addendum). Thresholds tune here.</summary>
    public static class AchievementCatalog
    {
        public static readonly AchievementDef[] All =
        {
            new AchievementDef(HelloAgain, "Hello Again", "Encounter your first ghost"),
            new AchievementDef(ThatWasMe, "That Was Me?!", "Get killed by your own ghost", hidden: true),
            new AchievementDef(DejaVu, "Déjà Vu", "Get killed by the same ghost twice", 2),
            new AchievementDef(Seriously, "Seriously?", "Get killed by the same ghost 3 times", 3),
            new AchievementDef(SwornEnemy, "Sworn Enemy", "Get killed by the same ghost 5 times", 5, hidden: true),

            new AchievementDef(CrowdedHouse, "Crowded House", "Have 5 ghosts active", 5),
            new AchievementDef(EchoChamber, "Echo Chamber", "Have 10 ghosts active", 10),
            new AchievementDef(Haunted, "Haunted", "Have 15 ghosts active", 15),
            new AchievementDef(TheyreEverywhere, "They're Everywhere", "Have 20 ghosts active", 20),
            new AchievementDef(ArmyOfMe, "Army of Me", "Reach the 30-ghost cap", 30),
            new AchievementDef(CostumeParty, "Costume Party", "Have ghosts in 5 different skins at once", 5),

            new AchievementDef(CloseOne, "Close One", "Trigger your first near-miss"),
            new AchievementDef(ThreadTheNeedle, "Thread the Needle", "Near-miss 2 ghosts within 2 seconds"),
            new AchievementDef(HowDidISurvive, "How Did I Survive That?", "Near-miss 3 ghosts within 3 seconds"),
            new AchievementDef(NoFear, "No Fear", "With 10+ ghosts, reach 500 m and die to the course"),

            new AchievementDef(FreshStart, "Fresh Start", "Beat your best and clear all ghosts"),
            new AchievementDef(Exorcist, "Exorcist", "Clear 10+ ghosts with a new best", 10),
            new AchievementDef(MassExorcism, "Mass Exorcism", "Clear 20+ ghosts at once", 20),
            new AchievementDef(SoClose, "So Close", "Die within 10 m of your best"),
            new AchievementDef(SmashedIt, "Absolutely Smashed It", "Beat your best by 100+ m", 100),
            new AchievementDef(PerfectEcho, "Perfect Echo", "Run past where one of your ghosts died"),

            new AchievementDef(GraveyardShift, "Graveyard Shift", "Have 10 gravestones on the course", 10),
            new AchievementDef(UnfinishedBusiness, "Unfinished Business", "Come back to a graveyard waiting", hidden: true),

            new AchievementDef(GettingStarted, "Getting Started", "Reach 100 m", 100),
            new AchievementDef(GoingSomewhere, "Going Somewhere", "Reach 500 m", 500),
            new AchievementDef(LongWayFromHome, "Long Way From Home", "Reach 1,000 m", 1000),
            new AchievementDef(Untouchable, "Untouchable", "Set a new best without a ghost near-miss or hit"),

            new AchievementDef(BadDecisions, "Bad Decisions", "Die 100 times", 100),
            new AchievementDef(ICanFixThis, "I Can Fix This", "Restart within 1 s of dying, 25 times", 25),
            new AchievementDef(JustOneMore, "Just One More", "Play 25 runs in one session", 25),

            // Version 2.0, the Daily Haunt: online extras, not needed for the Elementals.
            new AchievementDef(HauntedByStrangers, "Haunted by Strangers", "Play the Daily Haunt on 7 different days", 7, online: true),
            new AchievementDef(PassingThrough, "Passing Through", "Pass a leader ghost in the Daily Haunt", online: true),
            new AchievementDef(TopTen, "Top Ten", "Finish a day in the Daily Haunt's top 10", online: true),
            new AchievementDef(DailyGrind, "Daily Grind", "Play the Daily Haunt 3 days in a row", 3, online: true),

            // Version 2.1, seasonal events: each only during its event, and kept once earned.
            new AchievementDef(TwelveDays, "Twelve Days", "Play the Daily Haunt on 12 different days in December", 12, online: true, season: Christmas),
            new AchievementDef(LeadTheWay, "Lead the Way", "Pass 25 leader ghosts in the Daily Haunt during December", 25, online: true, season: Christmas),
            new AchievementDef(SnowedUnder, "Snowed Under", "Have 30 ghosts active during December", season: Christmas),
            new AchievementDef(FirstFooter, "First Footer", "Finish a Daily Haunt run on 1 January", online: true, season: NewYear),
        };

        // Season ids (the game's schedule names them the same).
        public const string Christmas = "christmas";
        public const string NewYear = "newyear";

        /// <summary>The single-player set (all but the online extras): the Elementals need every one of these.</summary>
        public static int SinglePlayerCount
        {
            get
            {
                int n = 0;
                foreach (AchievementDef def in All) if (!def.Online && !def.Seasonal) n++;
                return n;
            }
        }

        /// <summary>How many of the single-player set are among <paramref name="earned"/>.</summary>
        public static int SinglePlayerEarned(System.Collections.Generic.IEnumerable<string> earned)
        {
            int n = 0;
            foreach (string id in earned)
            {
                AchievementDef def = Find(id);
                if (def != null && !def.Online && !def.Seasonal) n++;
            }
            return n;
        }

        public static AchievementDef Find(string id)
        {
            foreach (AchievementDef def in All)
                if (def.Id == id) return def;
            return null;
        }
    }
}

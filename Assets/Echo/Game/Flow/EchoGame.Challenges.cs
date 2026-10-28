using System.Threading.Tasks;
using Echo.Core.Challenges;
using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Net;
using Echo.Core.Reels;
using Echo.Core.Rules;
using Echo.Services;
using Echo.Services.Analytics;
using Echo.Services.Backend;
using UnityEngine;

namespace Echo.Game.Flow
{
    /// <summary>
    /// Ghost Challenges (value-adds doc), made fair in 2.0 (balance addendum): a challenge carries the challenger's
    /// whole haunted course (the ghosts their run started with, its twist and bounty) and is raced on score. It is
    /// stored on the server behind https://echo.uprisestudios.co.uk/c/OWNER/ID. Lite challenges (echoghost://
    /// links, runs from before 2.0, or with server challenges switched off) carry the run alone and are raced on
    /// distance. A friend's attempts never add ghosts; the result shows the attempt count.
    /// </summary>
    public sealed partial class EchoGame
    {
        int _challengeAttempts;
        Challenge _attemptsFor;
        GhostRecord _challengeNemesis;
        string _pendingOwner, _pendingChallengeId;
        bool _servicesSettled;   // real services are running (or will not run), so server calls go to the right place

        /// <summary>Opens a challenge from a link, a pasted message or a bare code. Anything else is ignored.</summary>
        public void OpenChallengeLink(string text)
        {
            if (ChallengeLinks.TryParse(text, out string owner, out string id))
            {
                _pendingOwner = owner;
                _pendingChallengeId = id;
                ProcessPendingLink();
                return;
            }
            if (!ChallengeCode.TryParse(text, out Challenge c)) return;
            _pendingChallenge = c;
            if (State == GameState.Title) PresentPendingChallenge();
        }

        // A server challenge waits until the title is up and the real services have started (a link can open the
        // game before the consent choice, and nothing may reach the server before it).
        void ProcessPendingLink()
        {
            if (_pendingOwner == null || !_servicesSettled || State != GameState.Title || ModalOpen) return;
            string owner = _pendingOwner, id = _pendingChallengeId;
            _pendingOwner = _pendingChallengeId = null;
            _ = FetchServerChallenge(owner, id);
        }

        async Task FetchServerChallenge(string owner, string id)
        {
            _hud.QueueToast("OPENING CHALLENGE...");
            BackendFlags flags = await _services.Backend.GetFlagsAsync();
            if (!flags.ServerChallengesEnabled)
            {
                Ask("CHALLENGES ARE RESTING", "Friend challenges are switched off for a moment. Try the link again soon.", "OK", null, null, null);
                return;
            }
            GetChallengeResult r = await _services.Backend.GetChallengeAsync(owner, id);
            if (this == null) return;
            if (!r.Ok || !ReelPacket.TryDecode(r.Packet, out Reel reel))
            {
                if (r.Error == BackendFlags.Disabled)
                {
                    Ask("CHALLENGES ARE RESTING", "Friend challenges are switched off for a moment. Try the link again soon.", "OK", null, null, null);
                    return;
                }
                string body = r.Error == "expired" ? "This challenge is more than 30 days old, so it has expired. Ask for a new one."
                    : r.Error == "offline" ? "Opening a challenge needs a connection. Check yours and tap the link again."
                    : "This challenge couldn't be found. Ask your friend to send it again.";
                Ask("CHALLENGE NOT AVAILABLE", body, "OK", null, null, null);
                return;
            }
            Challenge c = ChallengeRuns.FromReel(reel);
            c.Name = ChallengeNames.Clean(r.Name);
            c.Score = r.Score;
            c.Distance = r.Distance;
            c.OwnerId = owner;
            c.ChallengeId = id;
            _pendingChallenge = c;
            if (State == GameState.Title) PresentPendingChallenge();
        }

        void PresentPendingChallenge()
        {
            if (_challenge == null || ChallengeCardOpen) return;
            Challenge c = _pendingChallenge;
            _pendingChallenge = null;
            bool sameVersion = c.ContentVersion == _contentVersion;
            Log(AnalyticsEvents.ChallengeOpened, ("distance", c.Distance), ("versionMatch", sameVersion), ("lite", c.Lite));
            if (!sameVersion)
            {
                // Value-adds doc: a challenge from another content version cannot be replayed on today's course.
                Ask("OLDER CHALLENGE", $"{c.GhostName} comes from another version of Echo, so its course has changed. Send yours instead?",
                    "Send mine", "Close", SendBestChallenge, null);
                return;
            }
            string body = c.Lite
                ? $"{c.GhostName} dares you to beat {c.Distance} m on their course.\n<size=80%>Lite challenge: just their run, no other ghosts.</size>"
                : $"{c.GhostName} dares you to beat {c.Score:N0} on their haunted course: {Plural(c.Snapshot.Ghosts.Count + 1, "ghost")}, {c.Distance} m."
                  + (c.Snapshot.Options.Twist != Twist.None ? $"\n<size=80%>Recorded in {WeeklyTwists.Name(c.Snapshot.Options.Twist)} week, so you race it under that twist.</size>" : "");
            Ask("GHOST CHALLENGE", body, "Race it", "Not now", () => StartChallenge(c), null);
        }

        void StartChallenge(Challenge c)
        {
            if (c != _attemptsFor)
            {
                _attemptsFor = c;
                _challengeAttempts = 0;
            }
            _challengeAttempts++;
            _activeChallenge = c;
            _passedTheirGrave = false;
            _title.Hide();
            HideDeathScreen();
            _course = new Course(_library, c.Seed);
            _run = RunRules.StartChallenge(_progress, _course, _cfg, c);
            // Balance addendum: the challenge plays under the twist it was recorded with.
            ApplyTwist(_run.Options.Twist);
            _bountySeen = false;
            _duelNemesis = null;
            SnapshotPrevious();
            _ghostViews.ResetPop();
            _challengeNemesis = c.Lite ? null : NemesisRules.Select(c.Snapshot.Ghosts);
            _ghostViews.SetNameTag(c.GhostName, c.Lite ? -1 : c.Record.RunIndex, c.Lite ? "" : $"{Who(c)}'s Nemesis: ");
            if (_graves != null)
            {
                _graves.Plant(_run.Ghosts, _course, _cfg, _challengeNemesis);
                // Their grave is the finish line.
                for (int i = 0; i < _run.Ghosts.Count; i++)
                    if (_run.Ghosts.RecordAt(i).RunIndex == c.Record.RunIndex)
                        _graves.SetEpitaph(i, $"{Who(c)} fell here · {c.Distance} m");
            }
            _livesEarnedSeen = 0;
            _livesUsedSeen = 0;
            _hud.SetLives(0);
            _hud.QueueToast(c.Lite ? $"BEAT {WhoUpper(c)}: {c.Distance} M" : $"BEAT {WhoUpper(c)}: {c.Score:N0}");
            if (_run.Options.Twist != Twist.None) _hud.QueueToast($"AS RECORDED: {WeeklyTwists.Name(_run.Options.Twist).ToUpperInvariant()}");
            if (_challengeNemesis != null) _hud.QueueToast($"{WhoUpper(c)}'S NEMESIS: {NemesisNames.For(_challengeNemesis.RunIndex).ToUpperInvariant()}");
            Log(AnalyticsEvents.ChallengeRun, ("attempt", _challengeAttempts), ("ghostCount", _run.GhostsAtStart), ("lite", c.Lite),
                ("twist", WeeklyTwists.Name(_run.Options.Twist)), ("theirDistance", c.Distance));
            StartRunning();
        }

        void EndChallenge()
        {
            Challenge c = _activeChallenge;
            ChallengeOutcome o = RunRules.CommitChallenge(_run, c);
            Log(AnalyticsEvents.ChallengeResult, ("won", o.Won), ("attempts", _challengeAttempts), ("ghostCount", _run.GhostsAtStart),
                ("lite", c.Lite), ("margin", o.Margin), ("killedByGhost", o.KilledByGhost), ("distance", o.Distance), ("theirDistance", o.TheirDistance));
            SetState(GameState.DeathScreen);
            _hud.SetVisible(false);
            _deathScreen.SetScoreLabel("SCORE");
            string unit = o.ByScore ? "points" : "m";
            string attempt = $" on attempt {_challengeAttempts}";
            string line = o.Won ? (o.ByScore ? $"Beat {Who(c)}'s score by {o.Margin:N0}{attempt}" : $"You outlasted {Who(c)} by {o.Margin} m{attempt}")
                : o.KilledByGhost ? $"{c.GhostName} got you · attempt {_challengeAttempts}"
                : o.Margin == 0 ? $"Dead level with {Who(c)} · attempt {_challengeAttempts}"
                : $"{Who(c)} wins by {o.Margin:N0} {unit} · attempt {_challengeAttempts}";
            _deathScreen.Show(_run.Score, o.ByScore ? c.Score : _progress.Best, false, "", line, o.ByScore ? $"{Who(c)}" : "Best");
            _deathScreen.SetOffers(false, false);
            _deathScreen.SetDuel(null);
            _deathScreen.SetChallenge("Send one back");
            _deathScreen.SetSaveReel(ReelRules.CanSave(_run) ? "Save reel" : null, false, true);
            _deathScreen.SetBanner(o.Won ? "CHALLENGE WON!" : null);
        }

        // "Jamie", or "your friend" for an unnamed challenge.
        static string Who(Challenge c) => string.IsNullOrEmpty(c.Name) ? "your friend" : c.Name;
        static string WhoUpper(Challenge c) => Who(c).ToUpperInvariant();

        // ---- sending ----

        void OnSendRunAsChallenge()
        {
            if (State != GameState.DeathScreen || !ChallengeRuns.CanSend(_run)) return;
            Challenge lite = ChallengeRuns.FromRun(_run);
            lite.ContentVersion = _contentVersion;
            Reel reel = ReelRules.CanSave(_run) ? ReelRules.From(_run, _contentVersion, "", false) : null;
            SendChallenge(reel, lite);
        }

        /// <summary>Stats (and "Send mine"): the best run, with its ghosts when it was set in 2.0 or later.</summary>
        void SendBestChallenge() => SendChallenge(_progress.BestReel, _progress.BestRun);

        void SendReelChallenge(int index)
        {
            if (index < 0 || index >= _progress.Reels.Count) return;
            Reel reel = _progress.Reels[index];
            Challenge lite = ChallengeRuns.FromReel(reel);
            lite.Snapshot = null;
            SendChallenge(reel, lite);
        }

        /// <summary>
        /// Sends a full challenge when it can (a reel with at most 30 ghosts, server challenges on, the server
        /// reachable), and a Lite link when server challenges are switched off or the run has no snapshot. A server
        /// failure says so and changes nothing, so the player can try again (Daily Haunt doc).
        /// </summary>
        async void SendChallenge(Reel reel, Challenge lite)
        {
            if (lite == null)
            {
                Ask("NO BEST YET", "Set a best score first, then challenge a friend to beat it.", "OK", null, null, null);
                return;
            }
            if (!string.IsNullOrEmpty(lite.ContentVersion) && lite.ContentVersion != _contentVersion)
            {
                Ask("OLD BEST", "Your best was set on an older version of Echo. Set a new best to send a challenge.", "OK", null, null, null);
                return;
            }
            if (string.IsNullOrEmpty(_save.Settings.PlayerName))
            {
                AskName(() => SendChallenge(reel, lite));
                return;
            }
            string name = ChallengeNames.Clean(_save.Settings.PlayerName);
            bool full = ChallengeRuns.CanSendFull(reel) && reel.PlayableOn(_contentVersion);
            if (full && _servicesSettled) full = (await _services.Backend.GetFlagsAsync()).ServerChallengesEnabled;
            else full = false;
            if (!full)
            {
                ShareLite(lite, name);
                return;
            }
            _hud.QueueToast("CREATING CHALLENGE...");
            CreateChallengeResult r = await _services.Backend.CreateChallengeAsync(ReelPacket.Encode(reel), name);
            if (this == null) return;
            if (!r.Ok && r.Error == BackendFlags.Disabled)
            {
                ShareLite(lite, name);   // switched off on the server since the flags were read: the link needs no server
                return;
            }
            if (!r.Ok)
            {
                Ask("COULDN'T CREATE A CHALLENGE", "Try again." + (r.Error == "rate-limit" ? " You've sent a lot today; try again tomorrow." : ""),
                    "Try again", "Close", () => SendChallenge(reel, lite), null);
                return;
            }
            LastChallengeLink = r.Link;
            GhostRecord nemesis = NemesisRules.Select(reel.Ghosts);
            _services.Share.ShareText($"{name}'s Ghost challenges you to beat {r.Score:N0} in Echo, past {Plural(reel.Ghosts.Count + 1, "ghost")}!\n{r.Link}{StoreLine()}");
            Log(AnalyticsEvents.ChallengeSent, ("ghostCount", reel.Ghosts.Count), ("hasNemesis", nemesis != null),
                ("twist", WeeklyTwists.Name(reel.Options.Twist)), ("lite", false));
        }

        void ShareLite(Challenge c, string name)
        {
            c.Name = name;
            c.ContentVersion = _contentVersion;
            LastChallengeLink = ChallengeCode.Link(c);
            _services.Share.ShareText($"{c.GhostName} challenges you to beat {c.Distance} m in Echo!\n{LastChallengeLink}{StoreLine()}");
            Log(AnalyticsEvents.ChallengeSent, ("ghostCount", 0), ("hasNemesis", false), ("twist", "None"), ("lite", true));
        }

        string StoreLine() =>
            _serviceConfig != null && EchoServiceConfig.IsSet(_serviceConfig.StoreUrl) ? "\nNo Echo yet? " + _serviceConfig.StoreUrl : "";

        /// <summary>The last challenge link shared, for tests.</summary>
        public string LastChallengeLink { get; private set; }

        /// <summary>
        /// A reel's challenge link for its clip: a full server challenge when one can be made, otherwise its Lite
        /// link, so a clip is always shared with something to race.
        /// </summary>
        async Task<string> ChallengeLinkFor(Reel reel, string name)
        {
            if (ChallengeRuns.CanSendFull(reel) && _servicesSettled && (await _services.Backend.GetFlagsAsync()).ServerChallengesEnabled)
            {
                CreateChallengeResult r = await _services.Backend.CreateChallengeAsync(ReelPacket.Encode(reel), name);
                if (r.Ok)
                {
                    Log(AnalyticsEvents.ChallengeSent, ("ghostCount", reel.Ghosts.Count), ("hasNemesis", NemesisRules.Select(reel.Ghosts) != null),
                        ("twist", WeeklyTwists.Name(reel.Options.Twist)), ("lite", false));
                    return r.Link;
                }
            }
            Challenge lite = ChallengeRuns.FromReel(reel);
            lite.Snapshot = null;
            lite.Name = name;
            Log(AnalyticsEvents.ChallengeSent, ("ghostCount", 0), ("hasNemesis", false), ("twist", "None"), ("lite", true));
            return ChallengeCode.Link(lite);
        }

        void OnPasteChallenge()
        {
            string text = GUIUtility.systemCopyBuffer;
            if (ChallengeLinks.TryParse(text, out _, out _) || ChallengeCode.TryParse(text, out _))
            {
                CloseMenu();
                OpenChallengeLink(text);
                return;
            }
            Ask("NOTHING TO PASTE", "Copy a challenge link from a friend's message, then tap Paste challenge.", "OK", null, null, null);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Echo.Game.Cosmetics;
using Echo.Game.Persistence;
using Echo.Game.UI;
using Echo.Services.Analytics;
using Echo.Services.Backend;
using UnityEngine;

namespace Echo.Game.Flow
{
    /// <summary>
    /// Naughty vs Nice (modes doc) from the game's side: picking a side on the first Daily Haunt visit of the event, the
    /// lobby's tally (refreshed every 5 minutes), "+812 m for Team Nice" on the daily death screen, the result revealed
    /// once it's over, and the profile (server-granted rewards and badges, a user decision: shown at the top of Stats).
    /// The server decides everything; the game only shows it.
    /// </summary>
    public sealed partial class EchoGame
    {
        const float TeamRefreshSeconds = 300f;

        TeamEventResult _team;
        float _teamFetchedAt = -TeamRefreshSeconds;
        bool _teamLoading, _teamRevealChecked;

        /// <summary>The team event as the server last described it (tests).</summary>
        public TeamEventResult LastTeamEvent => _team;

        bool TeamForced => ForceModes && Debug.isDebugBuild;

        static string Side(string team) => team == "nice" ? "Nice" : team == "naughty" ? "Naughty" : team;

        async Task LoadTeamEvent()
        {
            if (_teamLoading) return;
            _teamLoading = true;
            _teamFetchedAt = Time.realtimeSinceStartup;
            TeamEventResult r = await _services.Backend.GetTeamEventAsync(DailyDateOverride, TeamForced);
            _teamLoading = false;
            if (this == null) return;
            _team = r.Ok ? r : null;
            ShowTally();
            if (_team == null) return;
            if (_team.Team != "") RememberTeam(_team);
            if (_team.Open && _team.Team == "" && _dailyLobby != null && _dailyLobby.IsOpen && !ChallengeCardOpen) AskForSide();
            else if (_team.Ended && _team.Team != "") RevealTeamResult();
        }

        void ShowTally()
        {
            if (_dailyLobby == null) return;
            if (_team == null || !(_team.Open || _team.Ended))
            {
                _dailyLobby.SetTally(null, 0.5f);
                return;
            }
            long total = _team.Nice + _team.Naughty;
            float niceShare = total > 0 ? _team.Nice / (float)total : 0.5f;
            string head = $"NICE {_team.Nice:N0} m <size=80%>(+{_team.NiceToday:N0} today)</size>     NAUGHTY {_team.Naughty:N0} m <size=80%>(+{_team.NaughtyToday:N0})</size>";
            string you = _team.Ended
                ? (_team.Winner == "draw" ? "It's a draw!" : $"Team {Side(_team.Winner)} won!")
                : _team.Team == "" ? "Naughty vs Nice: pick a side to join in"
                : $"You: Team {Side(_team.Team)} · {_team.YourMetres:N0} m";
            _dailyLobby.SetTally($"{head}\n{you}", niceShare);
        }

        void AskForSide() =>
            Ask("NAUGHTY OR NICE?",
                "Pick a side for December. Each day, your best Daily Haunt run adds to your team's total. You can't swap later.",
                "Nice", "Naughty", () => JoinTeam("nice"), () => JoinTeam("naughty"));

        async void JoinTeam(string team)
        {
            TeamEventResult r = await _services.Backend.JoinTeamAsync(team, DailyDateOverride, TeamForced);
            if (this == null) return;
            if (!r.Ok)
            {
                _hud.QueueToast(r.Error == "closed" ? "NAUGHTY VS NICE HAS CLOSED" : "COULDN'T JOIN: TRY AGAIN");
                return;
            }
            _team = r;
            RememberTeam(r);
            ShowTally();
            Log(AnalyticsEvents.TeamJoined, ("team", r.Team), ("assigned", r.Assigned));
            if (r.Assigned)
                Ask("THE ELVES HAVE SPOKEN", $"Team {Side(team)} has plenty of members for now, so the elves have assigned you to Team {Side(r.Team)}.",
                    "OK", null, null, null);
        }

        void RememberTeam(TeamEventResult r)
        {
            if (_save.Modes.TeamEvent == r.Event && _save.Modes.Team == r.Team) return;
            _save.Modes.TeamEvent = r.Event;
            _save.Modes.Team = r.Team;
            Save();
        }

        void TickTeam()
        {
            if (_dailyLobby == null || !_dailyLobby.IsOpen || _team == null) return;
            if (Time.realtimeSinceStartup - _teamFetchedAt >= TeamRefreshSeconds) _ = LoadTeamEvent();
        }

        /// <summary>The daily death screen's line for a run that added to the team, or null.</summary>
        string TeamLine(DailySubmitResult r)
        {
            if (r.TeamContribution <= 0 || string.IsNullOrEmpty(r.Team)) return null;
            Log(AnalyticsEvents.TeamContribution, ("metres", r.TeamContribution));
            if (_team != null && _team.Team == r.Team)
            {
                if (r.Team == "nice") _team.Nice += r.TeamContribution;
                else _team.Naughty += r.TeamContribution;
                _team.YourMetres += r.TeamContribution;
            }
            return $"+{r.TeamContribution:N0} m for Team {Side(r.Team)}";
        }

        // A player on a side, coming back after the event has ended: the reveal, once.
        void CheckTeamReveal()
        {
            if (_teamRevealChecked || !_servicesSettled || string.IsNullOrEmpty(_save.Modes.Team) || _save.Modes.Revealed.Contains(_save.Modes.TeamEvent))
                return;
            _teamRevealChecked = true;
            _ = LoadTeamEvent();
        }

        void RevealTeamResult()
        {
            TeamEventResult t = _team;
            if (t == null || !t.Ended || _save.Modes.Revealed.Contains(t.Event)) return;
            _save.Modes.Revealed.Add(t.Event);
            Save();
            string title = t.Winner == "draw" ? "IT'S A DRAW!" : $"TEAM {Side(t.Winner).ToUpperInvariant()} WINS!";
            var body = $"Nice {t.Nice:N0} m · Naughty {t.Naughty:N0} m\n" +
                       $"You gave {t.YourMetres:N0} m for Team {Side(t.Team)} over {t.YourDays} {(t.YourDays == 1 ? "day" : "days")}.";
            if (!string.IsNullOrEmpty(t.TopNice) || !string.IsNullOrEmpty(t.TopNaughty))
                body += $"\nTop elves: {t.TopNice} ({t.TopNiceMetres:N0} m) · {t.TopNaughty} ({t.TopNaughtyMetres:N0} m)";
            var earned = new List<string>();
            foreach (string id in t.Rewards)
            {
                earned.Add(RewardName(id));
                Log(AnalyticsEvents.ModeRewardEarned, ("mode", "naughty_nice"), ("reward", id));
            }
            if (earned.Count > 0) body += "\nYou earned: " + string.Join(", ", earned);
            Ask(title, body, "OK", null, null, null);
            _ = SyncProfile();
        }

        string RewardName(string id)
        {
            CosmeticItem item = _catalog != null ? _catalog.Find(id) : null;
            return item != null ? item.DisplayName : BadgeNames.Name(id) + " badge";
        }

        /// <summary>
        /// The profile from the server: rewards it has granted become owned (kept for good), badges and the online name are
        /// kept for Stats. Only when signed in already (the Daily Haunt or a challenge), never just for this.
        /// </summary>
        async Task SyncProfile()
        {
            if (!_servicesSettled || !_services.Backend.SignedIn) return;
            ProfileResult p = await _services.Backend.GetProfileAsync(DailyDateOverride, TeamForced);
            string name = await _services.Backend.GetPlayerNameAsync();
            if (this == null || !p.Ok) return;
            bool changed = false;
            if (_catalog != null)
                foreach (string id in p.Cosmetics)
                {
                    CosmeticItem item = _catalog.Find(id);
                    if (item == null || _save.Cosmetics.Owned.Contains(id)) continue;
                    _save.Cosmetics.Owned.Add(id);
                    _hud.QueueToast("Unlocked: " + item.DisplayName);
                    changed = true;
                }
            _save.Modes.Badges.Clear();
            foreach (BadgeInfo b in p.Badges) _save.Modes.Badges.Add(new BadgeSaveData { Id = b.Id, Until = b.Until });
            if (!string.IsNullOrEmpty(name)) _save.Modes.PlayerName = name;
            Save();
            if (changed && _skins != null) _skins.Refresh();
            if (_stats != null && _stats.IsOpen) ShowStats();
        }

        /// <summary>Stats' profile: the online name, the event side and badges still showing.</summary>
        StatsView.Profile ProfileNow()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var badges = new List<string>();
            foreach (BadgeSaveData b in _save.Modes.Badges)
                if (b.Until > now) badges.Add(BadgeNames.Name(b.Id));
            return new StatsView.Profile
            {
                Name = _save.Modes.PlayerName,
                Team = string.IsNullOrEmpty(_save.Modes.Team) ? "" : $"Team {Side(_save.Modes.Team)}",
                Badges = badges,
            };
        }

        /// <summary>Delete online data: the team, badges and online name go from the device too.</summary>
        void ForgetModes()
        {
            _save.Modes.TeamEvent = "";
            _save.Modes.Team = "";
            _save.Modes.Badges.Clear();
            _save.Modes.PlayerName = "";
            _team = null;
        }
    }
}

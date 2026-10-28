# Competitive Modes

## Overview and release schedule

Four new modes, each a different way to compete with other players. All four are built behind their own switch and schedule, so each goes live on its own date without a store update.

| Mode | In one line | Built in | Goes live |
| --- | --- | --- | --- |
| Naughty vs Nice | Pick a side; every day's best Daily Haunt run adds to your team's total | 2.1 | 1 to 24 Dec 2026, winner revealed Christmas Day |
| One Life | One attempt per day, no retries | 2.1 | Christmas Day special (25 Dec), then Sundays from January |
| The Community Graveyard | A week-long course where every player's deaths haunt everyone | 2.2 | Mid-January 2027, then every week |
| Ghost Duels | Race a stranger's ghost to climb ranked tiers | 2.2 (or 2.3 if 2.2 runs long) | Season 1 from about February to Easter 2027 |

**Order of building:** the shared mode framework first, then the two small 2.1 modes, then the two bigger ones.

**If 2.1 gets tight:** Naughty vs Nice matters most for December. Cut One Life from 2.1 first and move it to 2.2; Christmas Day can be its launch next year.

## Shared rules

Every competitive mode follows the same rules as the Daily Haunt, so players and the server treat them the same way.

**Mode framework**

- A **Modes** screen reached from the title, listing the main game, the Daily Haunt and any live event modes as cards. A card shows the mode's name, a countdown, and a dot if the player hasn't played it today.
- A Remote Config key `modes` holds each mode's on/off switch and start and end dates (UTC). A mode only shows when it's switched on and inside its dates.
- **Cloud Code checks the same key** on every endpoint, so a mode switched off is closed for every game version.
- A development-only setting forces any mode on and overrides the date, for testing.

**Fairness and anti-cheat (all modes)**

- The game sends taps only. Cloud Code replays every run with the shared simulation code and works out the distance itself.
- Every run records the exact course seed and ghost set it was played against, and the server replays against that.
- No Continue, Banish, Banish 10, power-ups, bounty ghosts or weekly twists in any competitive mode. Seasonal visuals are fine; seasonal pickups are not.
- Rate limits and size limits as in the Daily Haunt. Runs that fail the replay check are stored with the server's result and flagged.
- Leaderboards and all shared data are written by Cloud Code only (Access Control).

**Players and privacy**

- Players appear under their generated Unity names, as on the Daily Haunt board.
- "Delete online data" removes the player's records, ghosts, team membership and rating from every mode.
- Data is stored by day or by week and deleted once past its retention period, as for the Daily Haunt. Update the privacy policy and store data forms for each mode before it goes live.

**Analytics (all modes):** `mode_opened`, `mode_run_end` (mode, distance, rank), `mode_reward_earned`, plus each mode's own events below.

## Mode 1: Naughty vs Nice

A December team event layered on the Daily Haunt. Players pick a side, and each day their best Daily Haunt distance is added to their team's total. It isn't a separate course: it gives the Daily Haunt a team goal for the month.

| Rule | Detail |
| --- | --- |
| Dates | 1 Dec 00:00 UTC to 24 Dec 23:59 UTC. Winner revealed on 25 December. |
| Joining | On the first Daily Haunt visit during the event, the player picks Naughty or Nice. The choice is permanent for the event. |
| Balance | If one side has more than 55% of members, new players can only join the smaller side ("The elves have assigned you to Team Nice"). |
| What counts | Each player's best Daily Haunt distance per day: only their best, so grinding all day doesn't outweigh turning up daily. The Daily Haunt's own rules apply unchanged, including the capped banish of your own ghosts. |
| Team score | The sum of all members' daily bests across the event |
| Late runs | Runs past midnight that miss the board (grace runs) don't count |

**Rewards**

| Who | Reward |
| --- | --- |
| Everyone on the winning side who contributed on at least one day | Halo accessory (Nice) or coal-black trail (Naughty) |
| Anyone who contributed on 5 or more days, either side | Team badge on their Daily Haunt profile |
| The top contributor on each side | Their name on the result screen |

**Screens**

- **Daily Haunt lobby:** a tally bar showing both teams' totals and today's change, refreshed every 5 minutes.
- **Daily death screen:** "+812 m for Team Nice" when a run improves today's best.
- **25 December:** a reveal screen with the result, the final totals and your contribution.

**Server**

- Team membership is stored in the player's protected data, written by Cloud Code.
- When `SubmitDailyRun` sees a new daily best, it adds the improvement (new best minus old best) to that team's total for the day. Totals live in a game data item, updated with a write lock and retried on conflict.
- A `GetTeamEvent` endpoint returns totals, your team and your contribution, cached for 5 minutes.
- Analytics: `team_joined` (team, assigned or chosen), `team_contribution` (metres).

**Reusable:** the same mode can return in other seasons with new sides, for example Pumpkins vs Bats at Halloween or Hearts vs Heartbreakers at Valentine's.

## Mode 2: One Life

One attempt per day on a fresh course, with no retries. The tension of a single run, for players who've learned the game.

| Rule | Detail |
| --- | --- |
| When it runs | Only on days set in the schedule: Christmas Day 2026 as a special, then every Sunday from January |
| Course | Its own seed per day (derived from the date and a separate salt), not the Daily Haunt course |
| Attempts | One per player per day. The attempt is used the moment the run starts. |
| Ghosts | Today's top 10 One Life runs, shown as named leader ghosts, as in the Daily Haunt. No personal ghosts, since there's only one run. |
| Leaderboard | Its own `onelife` board, reset each day at 00:00 UTC, with an archive |
| Before starting | A clear warning: "One run. No retries. Closing the game ends your run." |

**When a run ends early**

- **Closing the app mid-run** counts as the run ending at that point. The run's taps so far are submitted when the app next opens.
- **Losing connection mid-run** doesn't matter: the run finishes on the phone and is queued for submission, like a Daily Haunt run.
- **The game crashing** is treated the same as closing the app. Watch crash reports closely around Christmas Day.

**Rewards**

- **Christmas Day:** everyone who finishes a run gets a "Christmas Day 2026" gravestone style. The day's top 3 get a gold version.
- **Sundays:** a weekly "One Life" badge for the top 100, shown on the player's profile for that week.

**Server**

- `StartOneLife` checks the player hasn't used today's attempt, records the attempt as used, and returns the seed, the leader ghost set and a single-use run token.
- `SubmitOneLife` requires that token, replays the run and updates the board. A second token request on the same day is refused.
- Analytics: `onelife_started`, `onelife_finished` (distance, rank), `onelife_abandoned` (app closed mid-run).

## Mode 3: The Community Graveyard

One course for the whole week, and every player's deaths haunt everyone. Each midnight, the deadliest spots from all players' failed runs become ghosts on the course for the next day, so the graveyard grows as the community fails.

| Rule | Detail |
| --- | --- |
| Week | Monday 00:00 UTC to Sunday 23:59 UTC, one course seed per week |
| Monday | Starts clean: "Fresh graves." No community ghosts on day one. |
| Each midnight | The server picks up to 30 ghosts from all deaths so far that week, and that set is fixed for the whole of the next day, the same for every player |
| Choosing the ghosts | Split the course into 50 m sections. Rank sections by how many players died there. Take one representative death from each of the deadliest sections, up to 3 per section and 30 in total, so the ghosts spread across the course instead of piling up in one spot. |
| Your runs | Every run you finish, whether a death or a new best, goes into the pool for future days. No personal ghosts of your own; the community's ghosts are the challenge. |
| Ghost labels | Ghosts show how deadly their spot is instead of a player name: "This spot has claimed 312 runners." |

**Scoring**

| Board | How it works |
| --- | --- |
| Daily | Best distance against that day's ghost set |
| Weekly | The sum of your best distance on each day of the week, so turning up every day matters more than one lucky run |

Later days are harder, which the weekly sum handles fairly: everyone faces the same harder course on the same day.

**Screens**

- **Lobby:** the graveyard's size this week ("4,213 deaths and counting"), today's top runs, your daily and weekly rank, and days left.
- **During a run:** community ghosts wear their owners' skins, so a December graveyard fills with Santas.
- **End of the week:** a summary with the week's deadliest spot, your weekly rank and the champion.

**Rewards**

- **Weekly top 10:** a "Gravekeeper" badge for the following week.
- **Played all 7 days:** progress towards a Graveyard skin (for example 4 full weeks unlocks it).

**Server**

- Each submitted run stores its death point, taps and skin in that week's record.
- A nightly job at 00:00 UTC builds the next day's ghost set. Use UGS's Scheduler and Triggers to run a Cloud Code function. As a fallback, if the set doesn't exist when someone first asks for it after midnight, build it then, with a lock so only one request does the work.
- `GetGraveyard` returns the week's seed, today's ghost set and its version, the graveyard's size, boards and your ranks.
- `SubmitGraveyardRun` replays the run against that version and updates both boards.
- Analytics: `graveyard_opened`, `graveyard_run_end` (distance, day of week), `graveyard_week_complete`.

## Mode 4: Ghost Duels

Race a stranger's ghost, one attempt each, and climb ranked tiers over a season. It's asynchronous: your opponent played earlier, and your run becomes someone else's opponent later.

**How a duel works**

1. Tap **Duel**. The server picks one of the season's arenas and an opponent run from a player near your rating.
2. You see your opponent's name, skin and tier, but not their distance.
3. You get one attempt. The opponent's ghost runs alongside you as a rival marker: it can't kill you, but you can see whether you're ahead.
4. Go further than their run and you win. Equal distance is a draw.

**Rules**

| Rule | Detail |
| --- | --- |
| Arenas | 10 fixed courses per season, so runs can be compared |
| Hazards | The course only: no other ghosts in Duels |
| Ranked duels per day | 10. Unranked practice against random ghosts is unlimited. |
| Matching | Opponents within 150 rating points of yours. Never your own run, and never the same opponent twice in a row. |
| Opponent pool | Every ranked run joins its arena's pool, tagged with the player's rating. The latest 200 runs per arena and rating band are kept. |
| Not enough players | Fall back to a wider rating range, then to a set of reference runs you record yourself for each arena |

**Rating and tiers**

- Everyone starts at 1,000. Ratings use a simple Elo system: bigger swings (K = 40) for your first 10 duels, then K = 24.
- The opponent's rating changes too, at half the size, since they weren't there to play. They get a message next time they open the game: "Your ghost beat 3 challengers while you were away."

| Tier | Rating |
| --- | --- |
| Bronze | Below 1,000 |
| Silver | 1,000 to 1,199 |
| Gold | 1,200 to 1,399 |
| Phantom | 1,400 and above |
| Wraith | The season's top 100, whatever their rating |

**Seasons**

- **Season 1:** about 1 February to Easter (28 March 2027). Later seasons run 8 to 10 weeks each, lined up with the seasonal events.
- **Soft reset between seasons:** your new rating is halfway between your old one and 1,000, so good players start near the top but have to prove it again.
- **Rewards by final tier:** a tier badge and trail for each tier, an animated trail for Phantom, and a unique skin variant for Wraith. Rewards go by your final rating, not your peak, so deliberately losing to get easier opponents never pays off.

**Server**

- `StartDuel` returns a single-use duel token, the arena seed, and the opponent's run, name, skin and tier.
- `SubmitDuel` requires the token, replays the run, decides the result, updates both ratings, adds the run to the pool, and records a message for the opponent.
- `GetDuelProfile` returns your rating, tier, duels left today, season dates and recent results. The season's `duel_season_N` leaderboard ranks players by rating.
- Analytics: `duel_started`, `duel_result` (won or lost, rating change, tier), `duel_tier_up`, `duel_season_reward`.

## Claude Code prompts

Export this tab as Markdown to `docs/echo-modes.md` first. Each prompt builds its modes switched off; going live is a Remote Config change you make yourself.

**Prompt A: Mode framework, Naughty vs Nice and One Life (2.1)**

Run this after 2.1's Christmas phase is approved (Prompt 2 in the 2.1 pack), so the 2.1 seasonal work stays separate.

```
Build the competitive mode framework, Naughty vs Nice and One Life from docs/echo-modes.md, for release in 2.1. All modes ship switched off.

## Ground rules
1. Same rules as the Daily Haunt: the game sends taps only; Cloud Code replays every run with the shared simulation and decides the result; clients never write leaderboards or shared data.
2. Every mode is controlled by the Remote Config key "modes" (switch plus UTC start and end), checked in the game and in every Cloud Code endpoint. A dev-only setting forces any mode on and overrides the date.
3. No Continue, banish, power-ups, bounty ghosts or twists in One Life. Naughty vs Nice uses Daily Haunt runs exactly as they are.
4. Delete online data must cover team membership, One Life records and anything else new.
5. UGS development environment only. Run all tests before you start and after each phase.

Stop after each phase for my review.

Phase 1: Mode framework: the Modes screen, the "modes" key, server-side checks, the dev override.
Phase 2: Naughty vs Nice: joining and balancing, daily-best contributions added in SubmitDailyRun with locked writes, GetTeamEvent, the lobby tally, death-screen contribution line, the 25 December reveal, rewards, analytics.
Phase 3: One Life: StartOneLife and SubmitOneLife with single-use tokens, its own seed and daily board, leader ghosts, the warning screen, handling for app closes and crashes, Christmas Day and Sunday rewards, analytics.

Include tests for team balancing, contribution maths, one attempt per day, and token reuse being refused.
```

**Prompt B: The Community Graveyard (2.2)**

```
Build the Community Graveyard from docs/echo-modes.md for release in 2.2, switched off, using the mode framework from 2.1.

1. Weekly seed, runs stored per week with death point, taps and skin.
2. The nightly ghost selection exactly as specified (50 m sections, ranked by deaths, up to 3 per section, 30 total), run by a UGS Scheduler trigger at 00:00 UTC, with the locked lazy-build fallback.
3. GetGraveyard and SubmitGraveyardRun, replaying against the ghost set version the run used.
4. Daily and weekly boards (weekly = sum of daily bests), lobby, "This spot has claimed N runners" labels, end-of-week summary, rewards and analytics.
5. Tests: selection is deterministic for the same input; two players on the same day get identical ghost sets; replay against an older version still validates.
Stop after the server side and again after the game side for my review.
```

**Prompt C: Ghost Duels (2.2 or 2.3)**

```
Build Ghost Duels from docs/echo-modes.md, switched off, using the mode framework.

1. 10 arena seeds per season, set in config.
2. StartDuel and SubmitDuel with single-use tokens; opponent selection within 150 rating points, widening when needed, then falling back to reference runs; never the player's own run or the same opponent twice in a row.
3. Elo ratings (K 40 for the first 10 duels, then 24), opponent changes at half size, messages for opponents, 10 ranked duels a day, unlimited unranked practice.
4. Tiers, season leaderboard, soft reset between seasons, rewards by final tier.
5. The opponent ghost is a harmless rival marker, never a hazard.
6. Tests for rating maths, matching rules, daily limits, season reset and token reuse.
Also give me a simple tool to record the reference runs for each arena.
Stop after the server side and again after the game side for my review.
```

**Before each mode goes live**

- [ ] Turn on its switch in production Remote Config, with the right dates
- [ ] Update the privacy policy and store data forms if the mode stores anything new
- [ ] Add the mode to the store description and the release notes or promotional text
- [ ] Record clips for the launch using the marketing recording mode
- [ ] Watch crash reports and server errors closely for the first 48 hours

# Update 2.0 — Daily Haunt

## Release overview

Version 2.0 adds the **Daily Haunt**, a shared daily course with a global leaderboard, and ships the balance changes in Addendum — Balance Update. It is Echo's first release with a backend, built on Unity Gaming Services, which the project is already linked to.

| Feature | Summary | Spec |
| --- | --- | --- |
| Daily Haunt | One course per day for everyone, haunted by today's top 10 runs and your own failed attempts | This tab |
| Banish 10 | Rewarded ad to clear the 10 deadliest ghosts when you hit 30 | Balance Update |
| Continue on every run | Once per run, no score condition | Balance Update |
| Fair friend challenges | Challenges carry the challenger's full ghost set, stored on the server behind a short link | Balance Update, plus the backend sections here |
| Backend | Anonymous sign-in, Cloud Code with server-side replay checks, Leaderboards, Cloud Save | This tab |

**Decisions from review (13 Oct 2026).**

| Topic | Decision |
| --- | --- |
| Version | This release is **2.0** (the stores already have 1.4). |
| Power-ups | None in the Daily Haunt: no Ghost Shield, Phase, Ghost Lantern or Extra 1-UP. Lives earned by playing (1-UP) work as normal, since every player earns them the same way. |
| Achievements | The 4 new Daily Haunt achievements are online extras. The Elementals still unlock with the original 30 single-player achievements. |
| Leader ghosts | Leader ghosts always take precedence over your own ghosts when ghosts are placed. "Passing" a leader is measured by distance (going further than their run), not by overtaking the ghost itself. |
| Your daily ghosts | Capped at 20, not 30. |
| Domain | `echo.uprisestudios.co.uk`. Challenge links move to `https://echo.uprisestudios.co.uk/c/...` with Android App Links and iOS Universal Links. Existing `echoghost://` links keep working as Lite challenges. |
| Highlights reel | Daily runs can be saved as reels, with the leader ghosts and daily ghosts they faced. |

**Changes from the value-add features doc.** That doc had yesterday's top runs haunting today's course. Ghosts replay on a specific course, so a run from yesterday's course would fall into today's gaps. Today's course is haunted by **today's** top 10 runs instead, refreshed through the day. Because each ghost waits just before where its run ended, the top 10 ghosts mark the leaderboard: pass one and you've beaten that player.

## Daily Haunt

Everyone plays the same course each day, scored on distance alone. The course is haunted by today's top 10 runs and by your own failed attempts, just like the main game.

**Rules**

| Rule | Detail |
| --- | --- |
| Day | Midnight to midnight UTC (01:00 UK time in summer). A countdown shows time left. |
| Course | One seed per day from the server; same course for everyone |
| Score | Distance in metres only. No near-miss points or ghost multiplier: players fail different numbers of times, so a multiplier would reward failing more. |
| Leaderboard | Best run per player counts. Shows the top 100 and your own rank. |
| Leader ghosts | Today's top 10 runs, each waiting just before where it ended, wearing its owner's skin with a name tag. Refreshed every 5 minutes; a run picks up the latest set when it starts. They take precedence over your own ghosts when ghosts are placed. You pass a leader by going further than their run's distance. |
| Your ghosts | Your failed daily attempts become ghosts on the daily course, capped at 20, and clear when you set a new daily best. They are separate from your main-game ghosts. |
| Your own leader ghost | If you're in the top 10, your best run isn't shown as a leader ghost to you |
| Not available | Continue, Banish, Banish 10, Nemesis Duel, bounty ghosts, weekly twists and power-ups (Ghost Shield, Phase, Ghost Lantern, Extra 1-UP) |
| Retries | Unlimited |
| Connection | Needed to start a run and to submit it. Offline players see "The Daily Haunt needs a connection". |
| Isolation | Daily runs never change your main-game ghosts, best score, Nemesis or graveyard |

**Screens**

| Screen | Contents |
| --- | --- |
| Title | A "Daily Haunt" button with the countdown and a dot if you haven't played today |
| Daily lobby | Today's best, your rank, the top 10 with names and distances, yesterday's winner, "Play" |
| Daily death screen | Distance, your best today, your rank (and change), the next leader ghost to beat: "Next: SwiftGhost · 812 m". Tap to retry. |

**Names.** Players use the auto-generated names from Unity Authentication (for example "SwiftGhost#4821"). Custom names need a moderation plan, so they wait for a later update.

**Achievements (new)**

| Achievement | Requirement |
| --- | --- |
| Haunted by Strangers | Play the Daily Haunt on 7 different days |
| Passing Through | Pass a leader ghost |
| Top Ten | Finish a day in the top 10, so other players met your ghost |
| Daily Grind | Play the Daily Haunt 3 days in a row |

These are online extras: the Elementals still unlock with the original 30 single-player achievements.

**Highlights reel.** A daily run can be saved as a reel like any other run; the reel stores the leader ghosts and daily ghosts it faced, so it plays back exactly.

## Backend

The backend uses Unity Gaming Services (UGS) with no servers of your own to run. The game client never writes scores or shared data directly: every write goes through Cloud Code, which re-runs the submitted taps through Echo's own simulation before accepting anything.

| Service | Used for |
| --- | --- |
| Authentication | Anonymous sign-in the first time a player opens the Daily Haunt or sends a challenge; auto-generated player names |
| Cloud Code (C# module) | Every server action: daily seed, run submission and replay checks, leader ghosts, challenges |
| Leaderboards | The `daily` leaderboard, reset every day at 00:00 UTC with an archive of the previous day |
| Cloud Save | Players' daily runs and challenges (server-written), and the cached leader ghost set |
| Access Control | Blocks clients from writing leaderboard scores or Cloud Save data directly; only Cloud Code can |
| Remote Config | Kill switches for the Daily Haunt and server-stored challenges |
| Environments | `development` for testing, `production` for release |

**Sharing the simulation.** The Cloud Code module references the same simulation code as the game (Echo.Core, or wherever runner, course and ghost logic lives). It must not depend on UnityEngine. If the current code does, extracting it is the first job, because server-side checks only work when both sides run identical code.

**Deep links.** Challenge links use `echo.uprisestudios.co.uk`, which hosts:

- `apple-app-site-association` for iOS Universal Links
- `.well-known/assetlinks.json` for Android App Links
- a simple landing page at `/c/...` that shows "Open in Echo" plus store badges, for players who don't have the game

Players who install from that page then tap the link again to open the challenge. Remembering a challenge across install (deferred deep links) needs a third-party service and is out of scope.

**Accounts.** Anonymous accounts are tied to the install, so reinstalling loses daily history and sent challenges. Linking to Sign in with Apple or Google Play Games is a good follow-up but isn't in 1.1.

**Cost.** UGS services have free usage tiers, and a game at Echo's scale should start inside them. Check current pricing on the Unity dashboard before launch, and set a billing alert.

## Cloud Code API, data and anti-cheat

The client only ever sends taps; the server works out the distance or score itself by replaying them. A run that doesn't replay to the claimed result is stored with the server's result and flagged.

**Cloud Code functions**

| Function | Input | What it does | Returns |
| --- | --- | --- | --- |
| `GetDaily` | none | Works out today's date and seed; rebuilds the leader ghost cache if it is over 5 minutes old | Date, seed, content version, end time, leader ghosts and their version, your best, rank and daily ghosts, yesterday's winner |
| `SubmitDailyRun` | Date, leader version, taps, release ticks, revive ticks, death tick, skin, client distance | Loads today's course, that leader set and your daily ghosts, then replays the run. Saves the run as a daily ghost, or clears your daily ghosts on a new best. Updates the leaderboard and rebuilds the leader cache if you entered the top 10. | Server distance, best, rank, new-best flag, next leader to beat |
| `CreateChallenge` | Seed, content version, twist, ghost snapshot, the run | Checks the snapshot (30 ghosts at most), replays the run against it to confirm the score, and stores it | Challenge id and link |
| `GetChallenge` | Owner id, challenge id | Loads the challenge | Challenge data, or "not found" or "expired" |

**Daily seed.** Derived on the server from the UTC date and a secret salt kept in Cloud Code, so nobody can work out tomorrow's course early.

**Leader ghost cache.** Rebuilt by reading the top 10 from the `daily` leaderboard and each of those players' best daily run. Each rebuild gets a new version number. The last 12 versions for the day are kept (about an hour), so a run submitted against a slightly old set still replays correctly.

**Storage**

| Data | Where | Written by | Readable by |
| --- | --- | --- | --- |
| Daily best run (`daily_best`) | Player data, protected | Cloud Code | That player, and Cloud Code |
| Daily ghosts (`daily_ghosts`) | Player data, protected | Cloud Code | That player, and Cloud Code |
| Challenges (`challenge_<id>`) | Sender's player data, protected; last 20 kept | Cloud Code | Cloud Code (served through `GetChallenge`) |
| Leader ghost cache (`daily_leaders`) | Game data custom item | Cloud Code | Everyone |
| Daily scores | `daily` leaderboard: descending, keep best, reset at 00:00 UTC, archive on reset | Cloud Code | Everyone |

Challenge links take the form `https://echo.uprisestudios.co.uk/c/<ownerId>/<challengeId>`. Challenges expire after 30 days.

**Anti-cheat and limits**

- Clients can't write scores or saved data directly (Access Control).
- Runs for any date other than today are rejected, with a 5-minute grace period after midnight for runs started before it.
- Runs must use today's content version, and a leader version from the last hour.
- Sanity limits: tap count, death tick and payload size. Oversized or malformed payloads are rejected before replay.
- Rate limits: 1 daily submission every 2 seconds per player, and 20 challenges per player per day.
- A mismatch between client and server distance is logged with the run, so desync bugs show up as well as cheating.

**When things fail**

- If a daily submission fails, the run is queued on the device and retried until the end of that day. The death screen shows "Saving your run" rather than a rank.
- If `CreateChallenge` fails, show "Couldn't create a challenge. Try again" and keep the death screen open.
- If Remote Config turns the Daily Haunt off, its button shows "Back soon".

## Release plan, testing and store text

Build the balance changes first, since they need no backend and can ship on their own if the server work runs long.

| Stage | Scope | Done when |
| --- | --- | --- |
| 1. Balance | Banish 10, Continue on every run | Testers stuck at 30 ghosts can clear 10 and keep playing |
| 2. Backend foundation | Shared simulation code, UGS sign-in, Cloud Code module deployed to `development`, Access Control, Remote Config switches | A test run replays to the same distance on device and in Cloud Code |
| 3. Fair challenges | Ghost snapshots, `CreateChallenge` and `GetChallenge`, deep links, landing page, Lite fallback, gallery snapshots | A friend on another phone opens a link and faces the same ghosts |
| 4. Daily Haunt | `GetDaily`, `SubmitDailyRun`, leaderboard, leader ghosts, daily screens, achievements | Two testers play the same day and see each other's ghosts |
| 5. Release | `production` environment, analytics checks, store text, phased rollout | 1.1 live in the stores |

**Testing**

- **Replay parity:** a test that replays 500 random runs on the device code and the Cloud Code module and checks they match exactly. This is the most important test in the release.
- **Day rollover:** in `development`, let the date be overridden so midnight, the grace period and leaderboard resets can be tested on demand.
- **Tampering:** submit edited taps, a faked distance, an old date and an oversized payload, and check each is rejected or corrected.
- **Network:** airplane mode at the death screen, a slow connection, and the app closed mid-submit. Runs must be saved and retried.
- **Tester round:** release to the 21 testers through TestFlight and Google Play internal testing for at least 3 days, so day rollovers are tested for real.

**Rollout.** Use a phased release (App Store phased release, Play staged rollout). Keep the Remote Config switches ready to turn off the Daily Haunt or server challenges without a new build.

**What's New text** (version 2.0)

```
The Daily Haunt is here. One new course every day, the same for everyone, haunted by the day's top 10 runs. Pass their ghosts to climb the leaderboard.

Also new:
• Stuck at 30 ghosts? Banish the 10 that keep getting you
• Continue is now available on every run
• Friend challenges now include every ghost you faced, so they're a fair fight
```

**Store listing.** Add a Daily Haunt section to the full description and "A new shared course every day, haunted by the day's top runs" to the Features list. Remove that line from the "add later" list.

**References**

- [Leaderboard resets](https://docs.unity.com/en-us/leaderboards/concepts/resets)
- [Cloud Save game data](https://docs.unity.com/en-us/cloud-save/concepts/game-data)
- [Store data using Cloud Code](https://docs.unity.com/en-us/cloud-save/tutorials/cloud-code)
- [Leaderboard reset trigger example](https://docs.unity.com/en-us/triggers/tutorials/use-cases/reward-top-players)

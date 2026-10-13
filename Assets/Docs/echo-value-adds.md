# Echo — Value-Add Features

This document defines post-launch features that add fun, variety and growth to **Echo** without changing the core one-tap loop.

## Design Principles

- The core loop stays untouched: one tap, instant restart, your past runs as ghosts.
- Every feature should reuse the replay system. A ghost is just tap times plus a course seed, which makes ghosts cheap to store, send and share.
- Features should create stories players want to tell ("The Seventh got me 9 times").
- Nothing here should add a currency, energy system or pay-to-win advantage.
- Each feature ships behind its own flag so it can go out as a separate update.

---

# Priority Summary

| Feature | Purpose | Effort | Backend needed | Priority |
|---|---|---|---|---|
| Ghost Challenges | Growth | Medium | Small (link handling) | 1 |
| Weekly Twists | Retention | Low–Medium | No (or remote config) | 1 |
| Ghost Bounties | Fun / risk-taking | Low | No | 2 |
| Rogues' Gallery | Collection / stories | Low | No | 2 |
| Nemesis Duel | Rivalry payoff | Medium | No | 3 |
| Replay Theatre | Sharing | Medium | No | 3 |
| Daily Haunt | Retention / community | High | Yes | 4 |
| Themed Biomes | Variety / pack value | Medium (mostly art) | No | 4 |

Recommended first update: **Ghost Challenges** and **Weekly Twists**. Both reuse the replay system and give the best value for the effort.

---

# Growth

## Ghost Challenges

Share your best run as a link. Your friend plays the same course with your run as a ghost and tries to outlast it.

**How it works**

- From the death screen or Stats screen, tap **Challenge a Friend** to share a link.
- The link carries the course seed, content version, skin id and tap times of the run.
- Opening the link launches Echo into a challenge run on that course, with the friend's run as a single named ghost (for example "Jamie's Ghost").
- The challenge ghost is marked differently from your own ghosts (for example a name tag above its head).
- Outcome screen: "You outlasted Jamie by 142 m" or "Jamie's Ghost got you", with a button to send a challenge back.
- Challenge runs never affect your own ghosts, best score or Nemesis.

**Rules**

- If the content version doesn't match (the course library has changed), show "This challenge is from an older version" and offer to send your own instead.
- Players who don't have Echo installed go to the store listing; the challenge opens after install if deep-link deferral is supported, otherwise from a "Paste challenge code" fallback.

**Technical notes**

- Typical payload is well under 1 KB. Encode as compact binary (delta-encoded tap ticks), then base64url, to keep links short.
- Use platform deep links (Universal Links on iOS, App Links on Android).
- Validate payloads on open: reject malformed data, tap counts above a sane limit, and unknown content versions.

**Achievement ideas:** *Sent Packing* (win 5 challenges), *Grudge Match* (rematch the same friend 3 times).

---

## Daily Haunt

One shared course per day. Yesterday's top 10 runs worldwide appear as ghosts on today's course.

**How it works**

- A separate mode from the main game, reachable from the Title screen.
- Everyone gets the same daily seed.
- Today's run is haunted by the top 10 runs from yesterday's Daily Haunt, shown with player names.
- Leaderboard for today's best distance.
- Your Daily Haunt runs never become ghosts in your main game.

**Rules**

- One leaderboard entry per player per day (best run counts).
- Players can retry as often as they like.

**Technical notes**

- Needs a small backend: daily seed, score submission, and storing the top runs' tap data.
- Server-side replay validation is possible because the sim is deterministic: re-simulate submitted tap data and reject impossible scores.
- Fallback when offline: play the daily course without the community ghosts.

**Achievement ideas:** *Haunted by Strangers* (complete 7 Daily Haunts), *Top Ten* (make the top 10 and become tomorrow's ghost).

---

# Variety and Retention

## Weekly Twists

One rule change per week that refreshes the main game. Twists rotate automatically.

| Twist | Effect | Implementation |
|---|---|---|
| Shadow Week | Ghosts are invisible until about 0.5 s before they reach you | Rendering only |
| Copycat | A live echo of you repeats your taps 0.5 s behind and is lethal | New ghost fed from the current run's tap buffer |
| Mirror | The course runs right to left | Rendering and camera flip; sim unchanged |
| Lights Out | Only a small radius around the player is visible | Rendering only |
| Crowd Control | Ghosts start harmless and become lethal after 300 m | Grace-period rule change |
| Giant Ghosts | Ghosts are 1.5× size (hitbox included) | Ghost hitbox rule change |

**Rules**

- Twists apply to the main game for the week, with a clear banner on the Title screen.
- Ghosts recorded during a twist week remain valid afterwards because twists that change rules apply only at runtime, not to recorded data.
- Players can opt out to a "Classic" toggle so a twist never blocks someone chasing their best.
- Scores during twist weeks go on a separate weekly board, so personal bests stay comparable.

**Technical notes**

- Store the rotation schedule locally with an optional remote-config override.
- Rule-changing twists must stay deterministic so replays still match.

**Achievement ideas:** *Twisted* (play 4 different twists), *Seeing Things* (reach 500 m in Shadow Week).

---

## Ghost Bounties

One random ghost each run carries a bounty marker. Near-miss it for a big score bonus.

**How it works**

- At run start, one active ghost is chosen as the bounty (seeded, so it's deterministic).
- The bounty ghost shows a small icon above it (coin or skull).
- Near-missing the bounty ghost awards +50 instead of +5, with a distinct sound and effect.
- Touching the bounty ghost still kills you.

**Rules**

- Only appears with 3 or more ghosts active.
- Never the Nemesis, so the Nemesis stays purely a rival.

**Achievement ideas:** *Bounty Hunter* (claim 10 bounties), *Greedy* (die to a bounty ghost while chasing it).

---

## Nemesis Duel

When your Nemesis reaches 5 kills, you're offered a one-on-one duel.

**How it works**

- Offered on the death screen when the Nemesis reaches 5 kills: "The Seventh challenges you."
- The duel is a run on the current course with only the Nemesis present.
- Get past the point where the Nemesis died and it is banished for free.
- Lose, and the Nemesis gains a kill.

**Rules**

- Limited to once per high-score cycle.
- This is a skill-based alternative to the rewarded-ad Banish. Watch the Banish ad rate after launch; if it drops sharply, raise the kill threshold or make the duel rarer.

**Achievement ideas:** *Settled It* (win a Nemesis Duel), *Not Again* (lose a duel to the same Nemesis twice).

---

# Collection and Sharing

## Rogues' Gallery

An album of every Nemesis you've defeated.

**How it works**

- A page in the Stats screen.
- Each entry shows the Nemesis name, skin, kill count, the date it was defeated and its epitaph.
- Entries are sorted by kill count, so the most notorious sits at the top.
- Tap an entry to watch a short replay of that Nemesis's run.

**Technical notes**

- Store the Nemesis's ghost record when it's defeated (well under 1 KB each). Cap at 50 entries, dropping the lowest kill counts first.

**Achievement ideas:** *Rogues' Gallery* (defeat 5 Nemeses), *Most Wanted* (defeat a Nemesis with 10+ kills).

---

## Replay Theatre

After a new best, an optional 10-second montage of every ghost from that cycle running together, ending on the clear.

**How it works**

- Offered on the new-best screen alongside Ghost Photo Mode.
- All the cycle's ghosts replay together, sped up, over the music, finishing with the ghost clear.
- A caption shows ghost count, Nemesis name and new best.
- Share button exports the clip.

**Technical notes**

- The montage replays recorded data, so it needs no extra storage, only the ghosts already saved before the clear (keep them in memory until the screen closes).
- Video export needs a native recording plugin. Ship the in-game montage first and add export when the plugin is in place.

---

## Themed Biomes

Backdrops that rotate by distance and match the skin packs.

| Biome | Distance | Matches |
|---|---|---|
| Graveyard | 0–300 m | Undead and Monsters packs |
| Castle | 300–700 m | Dungeon and Royal packs |
| Space | 700 m+ | Space pack, Astronaut, Robot |

**Rules**

- Biomes are visual only: backdrop, palette and decorative props. Course layout and obstacles are unchanged.
- Owning a matching pack adds a small flourish (for example the Astronaut leaves a star trail in the Space biome). Cosmetic only.

---

# Implementation Notes

- Ghost Challenges, Daily Haunt, Rogues' Gallery and Replay Theatre all reuse the existing ghost record format. Keep that format stable and versioned.
- Add analytics events for each feature: `challenge_sent`, `challenge_opened`, `challenge_result`, `twist_active`, `bounty_claimed`, `duel_offered`, `duel_result`, `gallery_viewed`, `theatre_played`, `theatre_shared`.
- Measure each feature against a clear goal before investing further:

| Feature | Success signal |
|---|---|
| Ghost Challenges | Challenge links opened per link sent; installs from challenges |
| Weekly Twists | Day-7 and day-30 retention during twist weeks vs before |
| Ghost Bounties | Near-miss rate and average run length |
| Nemesis Duel | Duel acceptance rate; effect on Banish ad usage |
| Replay Theatre | Share rate after a new best |

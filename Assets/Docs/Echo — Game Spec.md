# Echo — Game Spec

Sep 26, 2026 · @Jamie

## Overview

Echo is a one-tap endless runner where every failed run comes back as a ghost you must avoid. Die, and your last attempt replays alongside you as a lethal obstacle; die again, and there are two. Your mistakes become the level.

**Pillars**

- **One input.** Tap to jump. Nothing else to learn.
- **Fair because it's you.** Every ghost is a run you played. Deaths feel self-inflicted, not random.
- **Instant retry.** Under 1 second from death to running again.
- **Screenshot-worthy chaos.** A screen full of 20+ ghosts is funny and shareable.

**Target:** iOS and Android, portrait, sessions of 1–5 minutes. Casual and hyper-casual players, plus streamers who like rage-bait games.

**Tagline:** Your past is trying to kill you.

## Core loop and controls

The player auto-runs right at a fixed speed; tapping anywhere makes them jump. A run ends on contact with an obstacle or a ghost.

1. Tap to start. The runner begins moving, and any existing ghosts start with them.
2. Jump over gaps and spikes; avoid touching ghosts.
3. Die. The run is saved as a new ghost, and the score is shown for about 0.6 seconds.
4. Tap to restart immediately, now with one extra ghost.
5. Beat your best score to clear every ghost and start fresh.

**Jump**

- Tap = fixed-height jump. No variable height in v1; it keeps ghost behaviour readable.
- One jump at a time. No double jump.
- Input buffer of 100 ms before landing, so taps just before touchdown still count.
- Coyote time of 80 ms after leaving a ledge.

**Speed**

- Constant within a run, so ghosts stay aligned with the world.
- Base speed rises slightly with each "clear" (new best), capped at 1.5x.

## Ghost system

Each ghost replays one earlier run exactly, from the start line, and kills on contact. Ghosts are deterministic, so they are stored as tap timestamps, not positions.

**Recording**

- Store the list of tap times (in fixed-timestep ticks) plus the tick of death.
- Physics runs on a fixed 60 Hz step so replays match the original exactly.

**Replay**

- All ghosts start with the player on tap-to-start and run their recorded taps.
- A ghost that reaches its death tick plays a short fade and disappears. Early deaths make short-lived ghosts; long runs make long-lived ones.
- Ghosts ignore each other.

**Collision**

- Ghost hitbox is 80% of the player's, so near-misses feel generous.
- Ghosts are harmless for the first 1.0 second of a run. Otherwise every ghost overlaps the player at the start line.

**Clearing ghosts**

- New best score: all ghosts clear with a burst effect.
- Hard cap of 30 ghosts. At 31, the oldest ghost is removed.
- Closing the app keeps ghosts across sessions. Only a new best (or the 30-ghost cap) removes them.

**Readability**

- The newest ghost is the most opaque; older ghosts fade toward 25% opacity.
- A faint trail shows the arc each ghost is about to take, 0.3 seconds ahead.

**The Nemesis**

The ghost that has killed you most becomes your Nemesis, with its own look and name, so players build a personal rivalry with one of their own runs.

- Every ghost counts its kills. The ghost with the most kills (minimum 2) is the Nemesis; ties go to the most recent killer.
- Look: red outline instead of the standard colour, a slightly larger silhouette, and a small taunt animation when it passes the player.
- Name: generated from its run number, for example "Run #7" becomes "The Seventh". Shown on the death screen: "The Seventh got you again (4)."
- The Nemesis is never removed by the 30-ghost cap; the oldest non-Nemesis ghost goes instead.
- The Nemesis cannot be banished (see monetisation). The only way to beat it is to set a new best, which triggers a "Nemesis defeated" moment before the ghost clear.
- Stats: lifetime Nemesis count and "worst Nemesis" (most kills) on the stats screen, which gives players a story to share.

## World, obstacles and difficulty

The course is procedurally generated from a seed, and the seed stays fixed until the player sets a new best. That keeps ghosts meaningful: they replay on the same course they died on.

**Obstacles (v1)**

| Obstacle | Behaviour | Introduced at |
| --- | --- | --- |
| Gap | Fall = death; width from 1 to 3 jump-lengths of margin | Start |
| Spike | Static, ground level | Start |
| Low ceiling | Jumping into it = death; forces a well-timed small window | 200 m |
| Double spike | Two spikes close together; needs precise timing | 400 m |
| Moving block | Rises and falls on a fixed cycle | 700 m |

**Generation**

- The course is built from hand-made chunks of about 10 m, chained by the seed.
- Chunks are tagged by difficulty tier; tier rises with distance.
- Every chunk is validated as clearable at base speed.

**Difficulty curve**

- The course gets harder with distance; the ghosts get harder with failure.
- A new seed after each best means players don't just memorise one course.
- Daily Run mode (later): one shared seed per day for everyone, which makes leaderboards fair.

## Scoring and progression

Score is distance in metres, plus a bonus for near-misses with ghosts. Distance keeps it simple; near-misses reward playing close to your past self.

**Scoring**

- 1 point per metre.
- Near-miss: passing within 0.3 units of a ghost without touching it = +5, shown as a small "close" pop.
- Ghost multiplier: score gets x1.0 plus 0.05 per active ghost (x2.5 at the 30 cap). Brave players who don't clear can score higher.

**Stats shown on death**

- Score, best, and ghosts on screen.
- "Killed by: your run #7" when a ghost caused the death. This is the joke that makes people share.

**Progression (light)**

- Cosmetic unlocks at score milestones: runner skins and ghost trail colours.
- 10 achievements for v1, for example "Survive with 20 ghosts" and "Die to the same ghost 3 times".
- No levels, no upgrades, no stats that change gameplay.

## Art and audio direction

Recommended style: flat silhouettes on a single bold background colour, with ghosts as glowing outlines. It reads instantly on a small screen, stays clear with 30 ghosts, and is cheap to produce.

**Visual rules**

- Player: solid white silhouette with a small squash-and-stretch on jump and land.
- Ghosts: the same silhouette as a coloured outline with soft glow; opacity by age.
- World: one-colour ground and obstacles, one shade darker than the background.
- Background colour shifts hue slowly with distance (for example teal to violet to red), which doubles as a sense of progress.
- Deaths: a quick freeze frame (80 ms) and the player shatters into pixels.
- Ghost clear on a new best: every ghost pops at once, with a screen-wide flash.

**Alternative styles to test**

| Style | Why it could work | Risk |
| --- | --- | --- |
| Flat silhouettes (recommended) | Clear, cheap, readable at 30 ghosts | Can feel generic without a strong palette |
| 1-bit pixel art | Nostalgic, Flappy-adjacent, easy to make | Ghost layering harder to read |
| Hand-drawn doodle | Charming and distinctive for social clips | More art time per asset |

**Audio**

- One looping synth track that adds a layer for every 5 ghosts on screen, so tension rises with failure.
- Jump: short soft blip. Near-miss: whoosh. Death: glitchy cut. Ghost clear: rising chime.
- Ghosts are silent; 30 jump sounds at once would be noise.

## Screens and UX flow

There are four screens, and the game screen doubles as the title screen. The player is never more than one tap from running.

| Screen | Contents | Exit |
| --- | --- | --- |
| Title / game | Logo over the idle course, "Tap to run", best score, small icons for skins, settings, store | Tap anywhere |
| In run | Score top-centre, ghost count top-right, nothing else | Death |
| Death | Score, best, "Killed by: run #N" when relevant, share button, Banish Ghost and Continue offers (see monetisation) | Tap to retry |
| Skins / store | Grid of runner skins and trail colours, Supporter Pack, Restore Purchases, stats (including your worst Nemesis) | Back |

**First-time experience**

- No tutorial screen. The first 3 chunks are flat with single spikes, and a "tap" hint shows until the first jump.
- On the first death, a single line of text: "Careful. That was you." This teaches the ghost rule in one beat.

**Share**

- The share button exports a 5-second clip of the final moments with the ghost count and score overlaid.

## Monetisation

The game is free to play, with opt-in rewarded ads and cosmetic purchases only. There are no forced ads: the unbroken loop of death and instant restart is the product, and nothing interrupts it.

| Item | Type | Price | Rules |
| --- | --- | --- | --- |
| Banish Ghost | Rewarded ad (opt-in) | Free (watch ad) | On death by a ghost: watch an ad to delete that ghost. Once per run, max 3 per high-score cycle, never the Nemesis. |
| Continue | Rewarded ad (opt-in) | Free (watch ad) | Revive at the point of death with 1 s of invulnerability. Once per run, only when score is over 50% of best. |
| Supporter Pack | One-off IAP | £2.99 | Exclusive runner skin, ghost trail colour and background palette. Cosmetic only. |
| Skin packs | One-off IAP | £0.99–£1.99 | Cosmetic only: runner skins, ghost trail colours, background palettes. |

**Why Banish Ghost matters**

It is the one monetisation hook unique to this game. It lands at the exact moment of frustration ("that ghost again"), and the player chooses it; it never feels like a paywall.

**Guardrails**

- No forced ads in v1: no interstitials, no banners. Revisit only if soft-launch revenue demands it, and then only on the title screen, never between deaths.
- No ads during a run, ever. Ad offers appear only on the death screen and are skipped with the same tap that restarts.
- No energy, lives, timers or currency in v1.
- Everything cosmetic can also be unlocked by play; IAP is a shortcut.

**Balancing Banish Ghost**

Banish must stay a relief valve, not the obvious route to a new best. Test it in soft launch with these checks:

| Measure | Starting target | If it's too high |
| --- | --- | --- |
| New bests set within 3 runs after a banish | Under 30% of all new bests | Lower the cap from 3 to 1 per cycle |
| Banishes per player per session | Under 1.5 on average | Add a 5-run cooldown between banishes |
| Retention of banish users vs non-users | Within 5 points on day 7 | If banishers churn faster, banish is removing the fun; tighten further |

Other knobs if needed: a banished ghost returns after 10 runs, or banishing resets the ghost score multiplier for that run.

**Later, if retention is strong:** a season pass of cosmetic skins tied to Daily Run streaks.

## Technical spec (Unity)

The full build reference is in Technical Spec, and the prompt for building it with Claude Code is in Claude Code Prompt. The summary below is kept for quick reading.

Build in Unity 2D with custom deterministic movement instead of Rigidbody2D physics. Built-in physics is not guaranteed to replay identically, and ghosts depend on exact replays.

**Architecture**

| Component | Responsibility |
| --- | --- |
| SimClock | Fixed 60 Hz tick; accumulates frame time and steps the sim |
| RunnerSim | Pure C# struct: position, velocity, grounded; `Step(tapThisTick)` |
| CourseGenerator | Builds the chunk list from a seed; exposes collision shapes |
| GhostManager | Holds up to 30 `GhostRecord`s; steps one `RunnerSim` per ghost |
| RunRecorder | Records tap ticks and death tick for the current run |
| CollisionService | AABB checks: player vs course, player vs ghosts |
| Presentation | Sprites, trails, VFX; reads sim state, never writes it |
| AdsService / IAPService | Unity LevelPlay (or AdMob) and Unity IAP behind interfaces |

**Data**

- `GhostRecord` = `{ int[] tapTicks; int deathTick; int runIndex; int kills; }`. A 60-second run is typically under 1 KB.
- Save file (JSON): best score, current seed, ghost records, unlocked cosmetics, Remove Ads flag.

**Determinism rules**

- Use fixed-point or carefully ordered float maths within the sim; no `Time.deltaTime` in sim code.
- Tap input is sampled per tick, never mid-tick.
- The course and ghosts share one seed; changing the chunk library invalidates saved ghosts (store a content version).

**Performance**

- 30 ghosts = 30 tiny struct updates per tick, which is trivial. Draw ghosts with one shared material to batch them.
- Target 60 fps on a 2019 mid-range Android phone.

## Scope, milestones and open questions

The target is a soft launch in about 6 weeks of part-time work. The ghost prototype in week 1 is the go/no-go test: if dying to your own ghosts isn't funny with grey boxes, art won't save it.

| Milestone | Scope | Exit test |
| --- | --- | --- |
| 1. Grey-box prototype (week 1) | Runner, 5 chunks, ghost record/replay, collision | 5 people play 10 minutes each and laugh at a ghost death |
| 2. Core game (weeks 2–3) | All 5 obstacles, 30 chunks, scoring, near-miss, ghost clear, save file | Replays match exactly across 100 automated runs |
| 3. Art and audio (week 4) | Silhouette style, VFX, layered music, death and clear effects | Screenshots with 20 ghosts read clearly on a phone |
| 4. Monetisation and polish (week 5) | Rewarded ads, Supporter Pack, Banish Ghost, Nemesis, 6 skins, share clip, achievements | Ads never appear mid-run; purchase restore works |
| 5. Soft launch (week 6) | One small market, analytics | Day-1 retention of 35% or more; average session over 3 minutes |

**Out of scope for v1:** Daily Run, leaderboards, season pass, multiplayer ghosts.

**Open questions**

- [ ] Is the 30-ghost cap right, or should it be lower for readability?
- [ ] Should Banish Ghost be ad-only, or also a purchasable item?
- [ ] Which soft-launch market?
- [ ] Stretch idea: "friend ghosts", where a shared link lets you race a friend's run as a ghost.

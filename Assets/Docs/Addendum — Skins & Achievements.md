# Addendum — Skins & Achievements

This addendum sets the cosmetic roster, achievement list and related features for Echo v1 and beyond. It replaces section 16 of the Technical Spec and updates the art style in the Game Spec.

## Changes to the main spec

| Area | Was | Now |
| --- | --- | --- |
| Art style | Flat silhouettes | 16-bit pixel art. The live player must always read more clearly than its translucent outline ghosts. |
| Ghost appearance | Same silhouette for every ghost | Each ghost wears the skin used on its run, so ghost crowds become mixed-skin over time. |
| Gravestones | Not specified | A gravestone marks where each ghost's run ended. They are decorative only, with no collision or gameplay effect, and clear along with their ghosts on a new best. |
| Cosmetics | 6 runner skins, 4 trails, 4 palettes | 8 base skins, 5 unlockable skins, trails and palettes as below |
| Achievements | 10 placeholder achievements | 30 launch achievements, plus a later set that ships with skin packs |

## Design principles

- Cosmetics never affect gameplay.
- No separate currency. Skins come from the starter set, score milestones, achievements, or paid packs.
- Achievements reward the chaotic behaviour that makes Echo fun: ghost crowds, near-misses, rapid retries and chasing your best. Avoid grind that creates no interesting play.
- Some achievements stay hidden until first progress, to keep the surprise.
- Final thresholds get tuned from playtests and analytics before release.

## Skins

### Base roster (included)

These 8 skins are already implemented and stay free as Echo's core visual identity.

| Skin | Theme |
| --- | --- |
| Ninja | Fantasy / stealth |
| Knight | Fantasy |
| Wizard | Fantasy / magic |
| Princess | Fantasy / royal |
| Cat | Cute / novelty |
| Skeleton | Undead |
| Astronaut | Sci-fi |
| Robot | Sci-fi |

### Unlockable skins (launch)

These can only be earned, never bought.

| Skin | Unlock |
| --- | --- |
| Shadow Self | Have 15 ghosts active |
| RGB Glitch | Reach 1,000 m |
| Negative | Clear 15+ ghosts with a new best |
| Golden Echo | Clear 20+ ghosts at once |
| Grim Reaper | Die 250 times |

Grim Reaper's threshold is lowered from 500 deaths to 250 so it rewards commitment without turning into grind. It is unlock-only, so it no longer appears in the Undead Pack.

### Minor rewards

| Milestone | Reward |
| --- | --- |
| First ghost encounter | Starter trail |
| 100 m | Palette |
| 50 deaths | Bone trail |
| 500 m | Premium-style trail |

### Paid skin packs (after retention testing)

Packs are held until the core game passes soft-launch retention checks. Each pack has 4 skins at £0.99–£1.99.

| Pack | Skins | Notes |
| --- | --- | --- |
| Undead | Lich, Vampire, Mummy, Zombie | Lich replaces Grim Reaper, now unlock-only |
| Dungeon | Barbarian, Rogue, Paladin, Goblin |  |
| Critters | Fox, Frog, Raccoon, Bunny |  |
| Space | Alien, Space Marine, Retro Android, Cosmic Explorer |  |
| Retro | Arcade Hero, CRT Head, Cartridge Kid, Pixel Block | Cartridge Kid replaces Glitch Character, which overlapped RGB Glitch |
| Royal | King, Queen, Jester, Royal Guard |  |
| Monsters | Werewolf, Stitched Monster, Pumpkin Head, Banshee | Banshee replaces Ghost, which would be hard to tell apart from real ghosts; time for Halloween |
| Oddballs | Traffic Cone, Banana, Hotdog, Gnome | Built for screenshots |

Once 3 or more packs are live, offer a **Season of Echo** bundle at about £4.99.

**Halloween:** if Echo is live by late October, offer Pumpkin Head as a free, limited unlock during Halloween week, then move it into the Monsters Pack.

## Achievements

### Launch set (30)

Hidden achievements are marked with ★.

**First encounters**

| Achievement | Requirement |
| --- | --- |
| Hello Again | Encounter your first ghost |
| That Was Me?! ★ | Get killed by your own ghost for the first time |
| Déjà Vu | Get killed by the same ghost twice |
| Seriously? | Get killed by the same ghost 3 times |
| Sworn Enemy ★ | Get killed by the same ghost 5 times (renamed from "Nemesis" to avoid clashing with the Nemesis system) |

**Ghost population**

| Achievement | Requirement |
| --- | --- |
| Crowded House | Have 5 ghosts active |
| Echo Chamber | Have 10 ghosts active |
| Haunted | Have 15 ghosts active |
| They're Everywhere | Have 20 ghosts active |
| Army of Me | Reach the 30-ghost cap |
| Costume Party | Have ghosts wearing 5 different skins active at once |

**Near-misses**

| Achievement | Requirement |
| --- | --- |
| Close One | Trigger your first near-miss |
| Thread the Needle | Near-miss 2 ghosts within 2 seconds |
| How Did I Survive That? | Near-miss 3 ghosts within 3 seconds |
| No Fear | With 10+ ghosts active, reach 500 m and die to the course rather than a ghost |

**Personal bests and clears**

| Achievement | Requirement |
| --- | --- |
| Fresh Start | Beat your best and clear all ghosts |
| Exorcist | Clear 10+ ghosts with a new best |
| Mass Exorcism | Clear 20+ ghosts at once |
| So Close | Die within 10 m of your best |
| Absolutely Smashed It | Beat your previous best by 100+ m |
| Perfect Echo | Pass the point where one of your ghosts died, without dying yourself |

**Gravestones**

| Achievement | Requirement |
| --- | --- |
| Graveyard Shift | Have 10 gravestones on the course |
| Unfinished Business ★ | Return to the game with a graveyard waiting |

**Distance**

| Achievement | Requirement |
| --- | --- |
| Getting Started | Reach 100 m |
| Going Somewhere | Reach 500 m |
| Long Way From Home | Reach 1,000 m |
| Untouchable | Set a new best without any ghost near-miss or collision |

**Persistence**

| Achievement | Requirement |
| --- | --- |
| Bad Decisions | Die 100 times |
| I Can Fix This | Restart within 1 second of dying, 25 times |
| Just One More | Complete 25 runs in one session |

### Later set (ships with skin packs)

| Achievement | Requirement |
| --- | --- |
| Persistent | Die 500 times |
| One More Metre | Beat your previous best by less than 5 m |
| History Rewritten | Beat the score of your oldest surviving gravestone's run |
| Cemetery Run | Pass 10 of your own gravestones in one run |
| Nine Lives | Die 9 times wearing Cat |
| Wizardry | Near-miss 2 ghosts within 2 seconds wearing Wizard |
| Royal Pain | Get killed 3 times by Princess ghosts |
| Ground Control | Reach 500 m wearing Astronaut |
| Machine Learning | Get killed by your own Robot ghost |
| Silent but Deadly | Set a new best wearing Ninja |
| Me, Myself and I | Die to ghosts wearing 5 different skins |
| Ghost of Myself ★ | Die to a ghost wearing the skin you have equipped |

Bone to Pick, Knightmare and Rest in Pieces are dropped: the first two are special cases of Ghost of Myself, and Rest in Pieces duplicates Exorcist now that gravestones clear with ghosts.

## Related features

- **Nemesis accessory:** the Nemesis keeps its run's skin and gains a crown or horns, so "The Seventh" is still recognisably a wizard. This replaces the red outline and scale-up in the Game Spec.
- **Gravestone epitaphs:** each gravestone shows a short generated line, such as "Run #12 · jumped too early" or "Killed by The Seventh".
- **Ghost Photo Mode:** on a new best, every ghost freezes before the clear, with a share button. This becomes the main share moment.
- **Paid skins on ghosts:** a purchased skin appears on every ghost from runs that wore it, so a screen full of Banana ghosts advertises the Oddballs Pack in every screenshot.

## Implementation notes

- `GhostRecord` gains `SkinId` (string) and `DeathX` (int). `DeathX` places the gravestone without replaying the run, and comes from the deterministic sim, so it always matches.
- `GhostRecord` also gains `Cause` (hazard, fall or ghost) and `KillerRunIndex` for epitaphs.
- Achievements run in Core from events, never per-frame checks: `RunStarted`, `RunEnded`, `GhostEncountered`, `KilledByGhost`, `NearMiss`, `PersonalBestBeaten`, `GhostsCleared`, `GravestonesCleared`, `DistanceReached`, `SkinEquipped`.
- Each achievement has `Id`, `Hidden`, `Target` and `Progress` so the Stats screen can show progress bars.
- Save schema moves to version 2: add `skinId`, `deathX`, `cause` and `killerRunIndex` to each ghost, plus achievement progress. Version 1 saves migrate with the default skin.
- Skin sprites follow one 16-bit template (same frame size, pivot and animation set) so every skin works as both player and ghost, with ghosts rendered as a translucent outline pass.

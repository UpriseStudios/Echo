# Echo — Technical Spec

This is the build reference for Echo v1. The Game Spec tab says what the game is; this tab says how to implement it. Where they differ on implementation detail, this tab wins, and every deviation is listed in section 20.

## 1. Platform and stack

| Area | Choice |
| --- | --- |
| Engine | Unity 6 LTS (6000.0.x), 2D, URP 2D renderer |
| Targets | iOS 15+, Android 8.0 (API 26)+, portrait only |
| Reference resolution | 1080 × 1920, Canvas Scaler "scale with screen size", match 0.5, safe area respected |
| Frame rate | `Application.targetFrameRate = 60`; the sim runs at a fixed 60 Hz regardless |
| Input | Input System package |
| UI | UGUI + TextMeshPro |
| Serialisation | com.unity.nuget.newtonsoft-json |
| Ads | Unity LevelPlay, rewarded placements only |
| IAP | Unity IAP |
| Share | NativeShare (screenshot) |

## 2. Project structure

```
Assets/Echo/
  Core/             Echo.Core.asmdef        noEngineReferences: true
    Sim/ Course/ Ghosts/ Scoring/ Rules/ Achievements/
  Game/             Echo.Game.asmdef        refs Core, Services
    Bootstrap/ Flow/ Views/ UI/ Audio/ Input/ Persistence/ Cosmetics/
  Services/         Echo.Services.asmdef    refs Core
    Ads/ Iap/ Analytics/ Share/ Mocks/
  Content/          chunk JSON, sprites, audio, materials
  Editor/           Echo.Editor.asmdef      scene builder, chunk validator menu
  Tests/EditMode/   Echo.Tests.EditMode.asmdef   refs Core
  Tests/PlayMode/   Echo.Tests.PlayMode.asmdef   refs Game
```

Core holds every game rule and no Unity API. Game renders Core state and feeds it input; it never changes sim state except through Core's public methods.

## 3. Units and determinism

Ghosts only work if a replay matches the original run exactly, so the sim is integer-only and fixed-step.

- All sim values are `int`. 1 world unit = 1000 sub-units. Velocity is sub-units per tick; gravity is sub-units per tick squared.
- Core uses no floats, no `System.Random`, no `Time.*`, no `DateTime`, and never depends on dictionary iteration order.
- Game steps the sim from a time accumulator at 60 Hz, at most 5 ticks per frame. Views interpolate between the last two states; interpolation never writes back.
- Randomness comes from a `Xorshift32` in Core, seeded from the course seed. The seed itself can come from a non-deterministic source in Game.

## 4. Sim constants

All constants live in a `SimConfig` readonly struct so they can be tuned. They are part of the content version (section 6).

| Constant | Value | Effect |
| --- | --- | --- |
| TickRate | 60 per second |  |
| RunSpeedBase | 110 sub/tick | 6.6 units per second |
| SpeedPerClear | +5% of base per new best | capped at 150% |
| Gravity | 14 sub/tick² |  |
| JumpVelocity | 252 sub/tick | apex about 2.27 units, airtime 36 ticks (0.6 s), jump length about 3.96 units at base speed |
| MaxFallSpeed | 400 sub/tick |  |
| CoyoteTicks | 5 | about 83 ms |
| JumpBufferTicks | 6 | 100 ms |
| PlayerHitbox | 600 × 800 sub | anchored bottom-centre |
| GhostHitbox | 480 × 640 sub | 80% of player, bottom-centre |
| GhostGraceTicks | 60 | ghosts harmless for the first second |
| NearMissDistance | 300 sub | AABB gap, once per ghost per run |
| DeathFloorY | −2000 sub | falling below this kills |
| GhostCap | 30 |  |
| ReviveInvulnTicks | 60 | after Continue |

## 5. Runner simulation

`RunnerState { int x, y, vy, lastSafeX, coyote, buffer, invuln; bool grounded, alive; }`

`Runner.Step(ref RunnerState s, bool tap, Course course, int tick, int speed)` runs in this exact order:

1. If `tap`, set `buffer = JumpBufferTicks`.
2. If grounded, `coyote = CoyoteTicks`; otherwise decrement to a minimum of 0.
3. If `buffer > 0` and `coyote > 0`: `vy = JumpVelocity`, `grounded = false`, `coyote = 0`, `buffer = 0`.
4. Decrement `buffer` to a minimum of 0.
5. If not grounded, `vy = max(vy - Gravity, -MaxFallSpeed)`.
6. `x += speed`; `y += vy`.
7. Floor: if `vy <= 0`, the previous y was ≥ 0, the new y ≤ 0, and `course.HasFloorAt(x)`, then snap `y = 0`, `vy = 0`, `grounded = true`, `lastSafeX = x`. If grounded but no floor at x, set `grounded = false`.
8. Hazards: if `invuln == 0` and the hitbox overlaps any hazard at this tick, `alive = false`.
9. If `y < DeathFloorY`, `alive = false`.
10. Decrement `invuln` to a minimum of 0.

The floor is flat at y = 0 in v1; gaps are simply missing floor. There are no walls. `HasFloorAt(x)` tests the runner's centre x.

## 6. Course

The course is an endless chain of 10-unit chunks, generated lazily (3 chunks ahead) from the course seed.

**Chunk JSON**

```json
{
  "id": "t2_ceiling_squeeze",
  "tier": 2,
  "length": 10000,
  "gaps":     [{ "x": 4000, "w": 2500 }],
  "spikes":   [{ "x": 7000, "w": 800, "h": 700 }],
  "ceilings": [{ "x": 2000, "w": 3000, "y": 2600 }],
  "movers":   [{ "x": 6000, "w": 800, "h": 800, "yMin": 0, "yMax": 2400, "period": 120, "phase": 0 }]
}
```

- Spikes are hazards from y = 0 to `h`. Ceilings are hazards from `y` upwards. Movers are hazards whose bottom follows an integer triangle wave: `p = (tick + phase) % period`, `half = period / 2`, `y = yMin + (yMax - yMin) * (p < half ? p : period - p) / half`.
- Every chunk starts and ends with at least 1 unit of hazard-free floor, so chunks can join in any order.
- Movers use the run tick, so the player and every ghost see identical positions.

**Tiers and selection**

| Tier | Adds | Unlocked at |
| --- | --- | --- |
| 0 | Tutorial: flat floor, single spikes | first 3 chunks, fixed order |
| 1 | Gaps, spikes | 0 m |
| 2 | Low ceilings | 200 m |
| 3 | Double spikes | 400 m |
| 4 | Moving blocks | 700 m |

After the tutorial, each chunk is picked by the seeded RNG from all chunks with tier ≤ the current max tier, with chunks at the current max tier weighted 2×. The v1 library is 30 chunks, 6 per tier from 1 to 4 plus the tutorial set.

**Content version**

`ContentVersion` = FNV-1a hash of all chunk JSON plus every `SimConfig` value. It is saved with the ghost set; on load, a mismatch discards ghosts (best score and cosmetics are kept).

**Validation**

`ChunkValidator` proves every chunk is clearable, entering grounded, at base speed and at max speed. It runs a memoised search over (tick offset, y, vy, grounded, coyote, buffer) with tap and no-tap branches, and for mover chunks repeats for every entry phase across the period. It runs as an EditMode test and from an editor menu.

## 7. Ghosts

```csharp
public sealed class GhostRecord {
    public int RunIndex;
    public int[] TapTicks;
    public int[] ReviveTicks;
    public int DeathTick;
    public int SpeedPct;
    public int Kills;
    public int LastKillRun;   // run index of the player's run it most recently killed
}
```

**Recording.** `RunRecorder` appends the tick of each tap, the tick of each Continue, and the final death tick. A run with no taps still becomes a ghost.

**Replay.** Every ghost owns its own `RunnerState` and is stepped with the same `Runner.Step`, fed `tap = true` on its recorded ticks. At a revive tick it applies the same revive as the player did. At its death tick it fades for 18 ticks and then goes inactive. Ghosts never collide with each other.

**Collision with the player.** From tick `GhostGraceTicks` onward, if the player has no invulnerability and the player hitbox overlaps an active ghost's 80% hitbox, the player dies. That ghost's `Kills` goes up by 1 and `LastKillRun` is set to the current run index.

**Revive (Continue).** The player is placed at `x = lastSafeX`, `y = 0`, grounded, with `ReviveInvulnTicks` of invulnerability. The run keeps its tick count.

**Near-miss.** An active, post-grace ghost counts as a near-miss once per run when the AABB gap to the player is ≤ `NearMissDistance` without overlapping.

**Nemesis.** Recomputed after every death: the ghost with the highest `Kills`, minimum 2; ties go to the highest `LastKillRun`. Its name is "The " plus its run index as an ordinal word up to 20 ("The Seventh") and a numeric ordinal after that ("The 34th").

**Cap.** After adding a ghost, if there are more than 30, remove the lowest `RunIndex` that isn't the Nemesis.

**Banish.** Available when the player was killed by a ghost that isn't the Nemesis, no banish has been used this run, and fewer than 3 banishes have been used this high-score cycle. It requires a successful rewarded ad and removes that ghost.

**Run end.** The death screen holds the run as pending, since Continue can resume it. The run commits when the player restarts, banishes, or backgrounds the app. Commit order:

1. Compute the final score.
2. If it beats the best: store the best, note whether a Nemesis existed (for the "Nemesis defeated" moment), clear all ghosts, reset the cycle's banish count, pick a new seed, increment clears. The run is not added as a ghost.
3. Otherwise: add the run as a ghost, apply the cap, recompute the Nemesis.
4. Evaluate achievements, then save.

A run killed by the app closing mid-run is discarded.

## 8. Scoring

- `distance = maxX / 1000` (metres, integer division).
- `score = (distance + 5 × nearMisses) × (100 + 5 × ghostsAtStart) / 100`, integer maths.
- `ghostsAtStart` is the number of ghosts present at tick 0, so the multiplier tops out at ×2.5 with 30.

## 9. Game flow

| State | Behaviour | Next |
| --- | --- | --- |
| Boot | Load save, content, services | Title |
| Title | Idle course with ghosts standing at the start line; logo, best, icons | Tap → Running; icons → Store, Stats, Settings |
| Running | Sim steps; HUD shows score and ghost count | Death → Dying |
| Dying | 5-tick freeze, shatter effect | DeathScreen |
| DeathScreen | Pending run; offers; taps ignored for the first 250 ms | Tap → commit → Running; Continue → Running |
| Paused | App backgrounded mid-run; sim frozen | Resume → 3-2-1 → Running |

## 10. Persistence

Saved to `Application.persistentDataPath/echo_save.json`, written to a temp file then swapped in with `File.Replace`. Saves happen on commit, purchase and settings changes. Serialisation lives in Game; Core exposes plain data classes.

```json
{
  "schema": 1,
  "contentVersion": "a1b2c3d4",
  "best": 0, "clears": 0, "seed": 12345,
  "nextRunIndex": 1, "cycleBanishes": 0,
  "ghosts": [
    { "runIndex": 1, "tapTicks": [40, 97], "reviveTicks": [], "deathTick": 311,
      "speedPct": 100, "kills": 0, "lastKillRun": 0 }
  ],
  "stats": { "totalRuns": 0, "nemesesDefeated": 0, "worstNemesisKills": 0, "worstNemesisName": "" },
  "cosmetics": { "owned": ["runner_default"],
                 "equipped": { "runner": "runner_default", "trail": "trail_default", "palette": "palette_default" } },
  "achievements": [],
  "settings": { "music": true, "sfx": true, "haptics": true }
}
```

If the file fails to load, it is renamed to `.bak` and the game starts fresh.

## 11. Presentation

- **Camera:** orthographic, 9 units wide, player held 25% from the left edge, fixed vertical position.
- **Runner:** white silhouette sprite with view-only squash and stretch on jump and land.
- **Ghosts:** a pool of 31 views sharing one outline material, opacity set through a `MaterialPropertyBlock` from 1.0 (newest) down to 0.25 (oldest).
- **Nemesis:** red material, 1.1× scale, a taunt animation when it overtakes the player; name shown on the death screen.
- **Preview trails:** each frame, copy each visible ghost's state, step it 18 ticks ahead with its taps, and draw the arc with a `LineRenderer`. This never touches the real ghost.
- **Background:** hue sampled from a palette gradient by distance.
- **Effects:** particle shatter on death; on a new best, a 1-second "Nemesis defeated" banner (if one existed), then every ghost pops with a screen flash.
- **Haptics:** a short pulse on death only, respecting the setting.

## 12. Audio

Music is 6 stems at the same length and tempo, started together with `AudioSource.PlayScheduled`. Stem k (1–5) fades in over 0.5 s once 5k ghosts are on screen. Sound effects: jump, near-miss, death, ghost clear, Nemesis sting. Ghosts are silent.

## 13. Input

Touch press, left mouse button and Space all produce one tap. Taps are queued and consumed on the next sim tick; several taps within one tick count as one. On Title and DeathScreen, taps over UI buttons go to the button, not the game.

## 14. UI

| Screen | Contents |
| --- | --- |
| Title | Logo, "Tap to run", best score, Store / Stats / Settings icons |
| HUD | Score top-centre, ghost count top-right |
| Death | Score, best, killer line ("Killed by: run #7" or "The Seventh got you again (4)"), Banish and Continue when eligible, Share, "Tap to retry" |
| Store | Cosmetics grid, Supporter Pack, Restore Purchases |
| Stats | Total runs, Nemeses defeated, worst Nemesis, achievements |
| Settings | Music, sound effects, haptics, privacy options |

## 15. Services

```csharp
public interface IAdsService {
    bool IsRewardedReady(string placement);
    void ShowRewarded(string placement, Action<bool> onComplete);
}
public interface IIapService {
    bool Owns(string productId);
    void Purchase(string productId, Action<bool> onComplete);
    void Restore(Action<IReadOnlyList<string>> onComplete);
}
public interface IAnalytics { void Log(string evt, IReadOnlyDictionary<string, object> props = null); }
public interface IShareService { void ShareScreenshot(string caption); }
```

Development builds use mock implementations with a toggle to simulate failures. Real SDKs arrive in milestone 4.

| Kind | Id | Notes |
| --- | --- | --- |
| Rewarded placement | `banish_ghost` | Death screen only |
| Rewarded placement | `continue` | Death screen only |
| Non-consumable | `echo.supporter` | £2.99 Supporter Pack |
| Non-consumable | `echo.skins.neon`, `echo.skins.mono` | £0.99–£1.99 skin packs |

**Analytics events**

| Event | Properties |
| --- | --- |
| `run_start` | runIndex, ghosts, seed, clears |
| `run_end` | score, distance, nearMisses, ticks, cause (hazard / fall / ghost), killerRunIndex, killerIsNemesis |
| `new_best` | score, ghostsCleared, nemesisDefeated |
| `banish_offered`, `banish_used` | ghostRunIndex, cycleBanishes |
| `continue_offered`, `continue_used` | score, best |
| `iap_purchase` | productId |
| `session_start`, `session_end` | runs, duration |

These cover the Banish balancing checks in the Game Spec: new bests within 3 runs of a banish, banishes per session, and retention by banish use.

## 16. Cosmetics and achievements

Cosmetics are a ScriptableObject catalog in Game: id, type (runner, trail, palette), sprite or colours, and unlock rule (score milestone or product id). v1 ships 6 runner skins, 4 trails and 4 palettes.

Achievements are evaluated in Core from run-end data:

| Id | Condition |
| --- | --- |
| `crowded` | Survive 10 s with 20+ ghosts |
| `full_house` | Start a run with 30 ghosts |
| `grudge` | Die to the same ghost 3 times |
| `first_nemesis` | A Nemesis appears |
| `nemesis_down` | Beat your best while a Nemesis exists |
| `threading` | 10 near-misses in one run |
| `m500` / `m1000` | Reach 500 m / 1000 m |
| `five_clears` | Set a new best 5 times |
| `instant` | Die within 1 second of starting |

## 17. Testing

**EditMode (Core), run headless:**

- Determinism: 500 random tap schedules on random seeds, each simulated twice, with a state hash compared every tick.
- Replay: record a player run, replay it as a ghost, and check positions match every tick and death lands on the same tick, including with a revive.
- Jump arc: apex height and airtime match section 4.
- Coyote time and jump buffer at floor edges.
- Course: same seed gives same chunks; tiers unlock at the right distances.
- Chunk validator: every chunk clearable at base and max speed, every mover phase.
- Ghost rules: grace period, 80% hitbox, cap eviction skipping the Nemesis, Nemesis selection and ties, banish eligibility, commit order, clear on new best.
- Scoring formula, achievements, save round trip, content version mismatch discarding ghosts.

**PlayMode:** boot to Title; a 60-second scripted run with 30 ghosts; death to running again in under 1 second.

```
"$UNITY_PATH" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults ./TestResults/editmode.xml -logFile -
```

## 18. Performance budgets

| Measure | Budget |
| --- | --- |
| Frame rate | 60 fps on a 2019 mid-range Android phone |
| Sim cost | ≤ 0.5 ms per frame with 30 ghosts |
| Allocations | Zero GC allocations per frame while Running |
| Draw calls | ≤ 40 |
| Cold start to Title | ≤ 3 s |
| Install size | ≤ 80 MB |

## 19. Build and release

- iOS: IL2CPP, ARM64.
- Android: IL2CPP, ARM64, App Bundle, target API level as currently required by Google Play.
- Show a consent flow (UK GDPR / GDPR) before initialising the ads SDK; show the iOS tracking prompt only if the ad configuration needs it.
- Optional CI: GameCI on GitHub Actions running EditMode tests on every push and building both platforms on tags.

## 20. Deviations from the Game Spec

- **Share:** v1 shares a screenshot with score, ghost count and Nemesis name. The 5-second clip moves to v1.1, since video capture needs a native plugin and encoding work.

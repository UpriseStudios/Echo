# Echo 2.1 (Seasons): release checklist

Everything below is yours to do. Christmas starts on **1 December** (UK time), so aim to have 2.1 live by about
**20 November**, leaving time for App Store review. Seasons are looks only: nothing here changes how a run plays or what
the server replays.

## 1. Code

- [ ] Review and merge `daily-haunt/backend` into `main`, and tag it `v2.1.0`.
- [ ] Version is **2.1.0**, Android version code **18**. Check 18 is higher than the last upload in Play Console
      (Release > App bundle explorer); raise it if not.
- [ ] Saves move to **schema 5** (seasonal achievement progress and the equipped gravestone). Older saves migrate
      automatically, and the old file is kept beside the save as `echo_save.json.schema4.bak`.

## 2. Production backend

- [ ] Preview the deploy: `powershell -File Server/deploy-prod.ps1 -DryRun`.
- [ ] Deploy: `powershell -File Server/deploy-prod.ps1 -Confirm production`. This adds the Remote Config key
      **`seasons`** (the schedule) beside `daily_enabled` and `server_challenges_enabled`. The module changes only in
      code the server shares with the game; the daily rules are unchanged.
- [ ] Smoke test: `$env:ECHO_PROBE_PRODUCTION='yes'; dotnet run --project Server/Tools/ReplayProbe -- smoke production`.
- [ ] In the dashboard (Remote Config, production), check `seasons` is there. You can change its dates there at any
      time without an update. It's the same JSON as `Assets/Echo/Content/Seasons/seasons.json`. Each season has
      `id`, `start`, `end` (UK dates, `MM-dd` every year or `yyyy-MM-dd` for one year), `enabled` and `achievements`.
      The game reads it only after a player has signed in (Daily Haunt or a challenge) and keeps the last copy. Until
      then it uses the built-in schedule.

## 3. Store products

- [ ] **Google Play Console** > Monetise > Products > In-app products > Create:
  - Product ID `echo.pack.christmas`, non-consumable, **£1.99**.
  - Name: *Christmas Pack*. Description: *Four festive runners: Elf, Snowman, Gingerbread Runner and Nutcracker.*
  - Activate it.
- [ ] **App Store Connect** > In-App Purchases: the same product ID, non-consumable, £1.99 tier, with a review
      screenshot of the Skins screen during Christmas (force the season in the recording APK).
- [ ] It isn't part of the Season of Echo bundle; bundle owners don't get it. The Skins screen lists it only during
      Christmas and New Year, or once a player owns part of it.

## 4. Analytics (Unity Analytics > Event Manager, production)

Events or parameters that aren't defined are dropped. Integer = whole number, Boolean = true/false, String = text.

| Event | Parameters |
|---|---|
| `challenge_run` | `attempt` integer, `ghostCount` integer, `lite` boolean, `twist` string, `theirDistance` integer |
| `clip_saved` | `preset` string (gif, video, vertical), `saved` boolean |
| `season_active` | `season` string, `forced` boolean |
| `seasonal_achievement` | `id` string |
| `seasonal_skin_equipped` | `skin` string |

`challenge_run` and `clip_saved` came after 2.0.4, so they ship in 2.1 too; skip any you've already made.
`challenge_sent` keeps its parameters, but is now also sent when a clip is shared. `egg_collected` (Easter) comes
with 2.2.

## 5. Store listings and privacy

- [ ] **Play Data safety**: no change. Saving a clip writes to the player's own gallery; nothing is collected.
- [ ] **App Store App Privacy**: no change. iOS asks once before the first clip is saved to Photos. The text is
      *"Echo saves your clips to Photos so you can post them."* (`Editor/IosPhotoLibrary.cs`).
- [ ] The privacy policy needs no change.
- [ ] **What's new** (suggested):
      *Seasons are here! Snow, Santa hats and a red-nosed Nemesis all December, fireworks at New Year, and a
      heartbreaking Valentine's. Earn Santa, Rudolph and more with seasonal achievements, or get the Christmas Pack.
      Clips can now be saved straight to your gallery, in a new Vertical format with your own caption.*
- [ ] Check readability in `TestResults/Screenshots`: `season30_<season>_1..3.png` (a real run through a 30-ghost
      graveyard; in real play ghosts spread out and are met a few at a time) and `season30crowd_<season>.png` (the
      worst case, 15 ghosts piled on screen). Optional: December store screenshots from them once the art is final.

## 6. Test on phones

Use the recording APK (`Echo/Build/Android APK (recording, debug-signed)`) for looks, and a development-server bundle
on internal testing for anything online.

- [ ] Settings > Dev: season through **christmas**, **newyear**, **valentines**, **halloween**, **bonfire**: the
      effects stay behind the floor, the runner, ghosts and obstacles; hazards still read as hazards.
- [ ] Christmas: Santa hats, red-nosed Nemesis, snowman graves; the Christmas Pack shows in Skins and buys
      (internal testing licence).
- [ ] Achievements: in a development-server build with **Dev: daily** set to a December date, Daily Haunt runs
      count for Twelve Days and Lead the Way; Stats lists them under SEASONAL, outside the count.
- [ ] Clips: Reels > Clip > Vertical, add a hook, Make video, **Save to gallery** (Android 10+: Movies/Echo; Android
      8 and 9 are told to use Share), then **Share**.
- [ ] iOS: the first Save to gallery asks for Photos access; the clip appears in Photos. (The iOS build hasn't been
      tried yet.)

## 7. Art

All the seasonal art is generated by `Assets/Echo/Editor/Art` (`PixelSeasons`, `PixelSeasonalCast`) in the game's
pixel style, and is final. To change a look, change its drawing there and rebuild; SeasonArtTests checks every reskin
keeps the original's size and outline.

## 8. Known limits

- Saving to the gallery needs Android 10 or later; older phones use Share.
- A replayed reel shows the plain stone grave (the run's gravestone isn't recorded).
- Offline, seasonal achievements use the phone's date, so changing the clock could fake one. The rewards are
  cosmetic, and the seasons doc accepts this.
- Pumpkin's free Halloween week (25–31 October, from 2.0) stays until Halloween is switched on in the schedule.
  From then, Trick or Treat earns Pumpkin instead.

## 9. Next year

- [ ] By mid-October 2027: in Remote Config `seasons`, set Halloween and Bonfire Night to `"enabled": true` (and
      Halloween's `"achievements": true`). Their art ships in 2.1.
- [ ] Easter and April Fools come with 2.2 (egg hunt, Easter Chick); their entries stay switched off until then.

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

### Worlds (backdrops for every run)

Create all ten in **Google Play Console** (Monetise > Products > In-app products) and **App Store Connect** (In-App
Purchases), each **non-consumable**, then activate them. The game asks the store for every product at launch; any that
are missing show as "Unavailable" in Skins > Worlds and can't be bought.

| Product ID | Price | Name | Description |
|---|---|---|---|
| `echo.world.christmas` | £0.99 | Christmas Town | A snowy town at the North Pole, with Santa's sleigh in the sky. Wear it all year. |
| `echo.world.halloween` | £0.99 | Halloween Night | A haunted manor under a full moon, a pumpkin patch, bats and a witch. Wear it all year. |
| `echo.world.neon` | £0.99 | Neon Night | A rain-soaked city of neon signs, with hover cars overhead. |
| `echo.world.sunken` | £0.99 | Sunken Ruins | A temple under the sea: kelp, rising bubbles and a passing whale. |
| `echo.world.autumn` | £0.99 | Autumn Woods | Golden hills and falling leaves, with geese flying over. |
| `echo.world.beach` | £0.99 | Beach | Palm trees, a sparkling sea and a plane towing a banner across the sky. |
| `echo.world.desert` | £0.99 | Desert Dunes | Pyramids and saguaros on rolling dunes, with blowing sand and a circling vulture. |
| `echo.world.volcano` | £0.99 | Volcanic Waste | A ruined Roman town beneath an erupting volcano, with falling ash and flying lava. |
| `echo.world.toytown` | £0.99 | Toy Town | A wild cartoon world: candy hills, a rainbow, giant mushrooms and a striped balloon. |
| `echo.worlds.bundle` | £2.99 | Worlds Bundle | All nine worlds: Christmas Town, Halloween Night, Neon Night, Sunken Ruins, Autumn Woods, Beach, Desert Dunes, Volcanic Waste and Toy Town. |

- [ ] App Store review screenshot for each: Skins > Worlds in the recording APK (every world is unlocked there), or
      `TestResults/Screenshots/05_skins_worlds.png`.
- [ ] Rules the store copy must keep (a user decision):
  - Worlds are looks only: the backdrop (sky, scenery, drifting effects, the odd flyer in the sky), never how a run plays.
  - Christmas Town and Halloween Night show free to everyone during their season; buying one keeps it all year.
  - A world sold in the store is never won in a mode, and a mode's reward is never sold. The **Haunted Graveyard**
    (the Community Graveyard's reward, a first weekly top 10) has no product and must never get one.
  - The bundle's description names the nine worlds rather than promising future ones. A world added to the bundle
    later still reaches its owners: the game re-reads purchases at every launch.

## 4. Analytics (Unity Analytics > Event Manager, production)

Events or parameters that aren't defined are dropped. Integer = whole number, Boolean = true/false, String = text.

| Event | Parameters |
|---|---|
| `challenge_run` | `attempt` integer, `ghostCount` integer, `lite` boolean, `twist` string, `theirDistance` integer |
| `clip_saved` | `preset` string (gif, video, vertical), `saved` boolean |
| `season_active` | `season` string, `forced` boolean |
| `seasonal_achievement` | `id` string |
| `seasonal_skin_equipped` | `skin` string |
| `mode_opened` | `mode` string |
| `team_joined` | `team` string, `assigned` boolean |
| `team_contribution` | `metres` integer |
| `mode_reward_earned` | `mode` string, `reward` string |
| `onelife_started` | `date` string, `leaders` integer |
| `onelife_finished` | `distance` integer, `rank` integer |
| `onelife_abandoned` | `distance` integer, `rank` integer |
| `world_equipped` | `world` string, `bought` boolean |

`iap_purchase` (already defined) covers the world products; `productId` tells them apart.

`challenge_run` and `clip_saved` came after 2.0.4, so they ship in 2.1 too; skip any you've already made.
`challenge_sent` keeps its parameters, but is now also sent when a clip is shared. `egg_collected` (Easter) comes
with 2.2.

## 5. Store listings and privacy

- [ ] **Play Data safety**: no change. Saving a clip writes to the player's own gallery; nothing is collected.
- [ ] **App Store App Privacy**: no change. iOS asks once before the first clip is saved to Photos. The text is
      *"Echo saves your clips to Photos so you can post them."* (`Editor/IosPhotoLibrary.cs`).
- [ ] **Privacy policy**: it now covers event teams (Naughty vs Nice: your side, metres and days with your
      generated name, public totals and top contributor, rewards and badges; kept until a month after the result,
      removed by Delete online data). Publish the updated `web/privacy/` (Cloudflare Pages) before switching a mode
      on, and update the Play Data safety and App Store App Privacy answers if they list gameplay data shared publicly.
- [ ] **Competitive modes** ship switched off. To run Naughty vs Nice: in production Remote Config `modes`, set its
      `"enabled": true` (dates 1 to 24 December, result showing until New Year). Its rewards (Halo, Coal trail, team
      badges) come from the server. One Life: set its two windows' `"enabled": true` (Christmas Day, then Sundays
      through 2027). The production deploy creates the `onelife` leaderboard.
- [ ] **Play Data safety / App Store App Privacy for worlds**: no change. A world is a purchase like the skin packs;
      what's worn stays on the phone.
- [ ] **What's new** (suggested):
      *Seasons are here! A snowy town at the North Pole with Santa's sleigh overhead and Jingle Bells all December,
      Auld Lang Syne at New Year, and a heartbreaking Valentine's. Earn Santa, Rudolph and more with seasonal
      achievements, or get the Christmas Pack. NEW: Worlds! Change the backdrop of every run: Neon Night, Sunken Ruins,
      Autumn Woods, Beach, Desert Dunes, Volcanic Waste, Toy Town, Halloween Night and Christmas Town, in a redesigned
      store. The title now shows how fast your runs have become. Clips can now be saved straight to your
      gallery, in a new Vertical format with your own caption.*
- [ ] **Store listing**: consider a screenshot or two of the worlds in play (wear one in the recording APK, then
      record with Settings > Recording on).
- [ ] Check readability in `TestResults/Screenshots`: `season30_<season>_1..3.png` (a real run through a 30-ghost
      graveyard; in real play ghosts spread out and are met a few at a time) and `season30crowd_<season>.png` (the
      worst case, 15 ghosts piled on screen). Optional: December store screenshots from them once the art is final.

## 6. Test on phones

Use the recording APK (`Echo/Build/Android APK (recording, debug-signed)`) for looks, and a development-server bundle
on internal testing for anything online.

- [ ] Settings > Dev: season through **christmas**, **newyear**, **valentines**, **halloween**, **bonfire**: the
      effects stay behind the floor, the runner, ghosts and obstacles; hazards still read as hazards.
- [ ] Christmas: Santa hats, red-nosed Nemesis, snowman graves; the town with Santa's sleigh crossing the sky every
      40 s or so; Jingle Bells (it changes at the title or a run's start); the Christmas Pack shows in Skins and buys
      (internal testing licence). New Year: Auld Lang Syne.
- [ ] Gravestones: during Christmas, a gravestone you chose (the Heart) shows; the plain Stone gives way to the
      snowman.
- [ ] Store: the six tabs (Runners, Packs, Worlds, Style, Extras, Free); each paid tab opens with its bundle.
- [ ] Worlds (internal testing licence, on a release-signed build, not the recording APK where everything is
      unlocked): buy one, wear it, Restore on a second install; the bundle unlocks all nine. A world lasts the whole run: no Castle or
      Space comes over it. Settings > Seasons off
      keeps your world during a forced season; on shows the season's look (Valentine's keeps the world behind it).
      The Community Graveyard always wears its own look.
- [ ] Achievements: in a development-server build with **Dev: daily** set to a December date, Daily Haunt runs
      count for Twelve Days and Lead the Way; Stats lists them under SEASONAL, outside the count.
- [ ] Clips: Reels > Clip > Vertical, add a hook, Make video, **Save to gallery** (Android 10+: Movies/Echo; Android
      8 and 9 are told to use Share), then **Share**.
- [ ] iOS: the first Save to gallery asks for Photos access; the clip appears in Photos. (The iOS build hasn't been
      tried yet.)

## 7. Art

All the seasonal art is generated by `Assets/Echo/Editor/Art` (`PixelSeasons`, `PixelSeasonalCast`,
`PixelChristmasTown`, and the worlds in `PixelWorldsArt` and `PixelModeArt`) in the game's pixel style, and is final.
The carols are generated by `Assets/Echo/Editor/CarolMusic.cs` only when their files are missing, so a recording
dropped in under the same names (`xmas_stem*`, `newyear_stem*` in `Content/Audio`) is kept. To change a look, change its drawing there and rebuild; SeasonArtTests checks every reskin
keeps the original's size and outline.

## 8. Known limits

- Saving to the gallery needs Android 10 or later; older phones use Share.
- A replayed reel shows the plain stone grave (the run's gravestone isn't recorded), and the world worn now rather
  than the one worn then.
- A world worn out of season changes only the backdrop: Christmas Town in July has no Santa hats, snowman graves or
  Jingle Bells.
- Offline, seasonal achievements use the phone's date, so changing the clock could fake one. The rewards are
  cosmetic, and the seasons doc accepts this.
- Pumpkin's free Halloween week (25–31 October, from 2.0) stays until Halloween is switched on in the schedule.
  From then, Trick or Treat earns Pumpkin instead.

## 9. Next year

- [ ] By mid-October 2027: in Remote Config `seasons`, set Halloween and Bonfire Night to `"enabled": true` (and
      Halloween's `"achievements": true`). Their art ships in 2.1.
- [ ] Easter and April Fools come with 2.2 (egg hunt, Easter Chick); their entries stay switched off until then.

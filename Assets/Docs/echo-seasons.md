# Seasonal Events

## Calendar and ground rules

Echo gets a light visual makeover at key times of year, plus seasonal skins earned through limited-time achievements. Every change is cosmetic, so the game plays exactly the same all year.

| Event | Dates (UK local) | What changes |
| --- | --- | --- |
| Halloween | 24 Oct – 2 Nov | Purple-orange sky, pumpkin gravestones, cobwebs on ghosts. Free Pumpkin Head unlock. |
| Bonfire Night | 4 – 6 Nov | Fireworks bursting in the background |
| Christmas | 1 – 31 Dec | Falling snow, snow-capped obstacles, ghosts in Santa hats, snowman gravestones, the Nemesis gets a glowing red nose instead of horns. Santa and Rudolph skins. |
| New Year | 31 Dec – 1 Jan | Fireworks when the Daily Haunt board resets at midnight UTC |
| Valentine's | 13 – 15 Feb | The Nemesis wears a heart; the death screen reads "It's complicated." |
| Easter | Good Friday to Easter Monday (26 – 29 Mar 2027) | Spikes become painted eggs; an egg hunt in the main game; Easter Chick skin |
| April Fools | 1 Apr | Everyone runs as the Banana for the day (the player's own skin returns at midnight) |

Dates live in Remote Config, so they can be moved or extended without an update.

**Ground rules**

- **Looks only.** Seasonal art never changes a hitbox, an obstacle's shape or position, movement, or timing. Ghost replays and the server's replay checks depend on the game playing identically all year.
- **Reskinned hazards keep their outline.** A painted egg replacing a spike has exactly the spike's silhouette and stays in the hazard colour, so players read it as "don't touch" instantly.
- **Effects sit behind the action.** Snow, fireworks and cobwebs never cover the player, ghosts or obstacles. Check it with 30 ghosts on screen.
- **The Daily Haunt gets the visuals but nothing else.** No egg hunt, no seasonal pickups, so the course stays identical for everyone.
- **Seasonal skins come back every year.** Players who miss one can earn it next time, so there's no "gone forever" pressure.

## Seasonal skins and achievements

Each main event has a free skin earned through a seasonal achievement. Christmas, the biggest event, has two: Santa and Rudolph.

| Event | Achievement | Requirement | Reward |
| --- | --- | --- | --- |
| Halloween | Trick or Treat | Play 10 runs during Halloween | Pumpkin Head skin |
| Christmas | Twelve Days | Play the Daily Haunt on 12 different days in December | **Santa** skin |
| Christmas | Lead the Way | Pass 25 leader ghosts in the Daily Haunt during December | **Rudolph** skin (with a glowing nose that lights up the ground just ahead) |
| Christmas | Snowed Under | Have 30 ghosts active during December | Candy-cane ghost trail |
| New Year | First Footer | Finish a Daily Haunt run on 1 January | Fireworks trail |
| Valentine's | It's Complicated | Get killed by your Nemesis on 14 February | Heart gravestone |
| Easter | Egg Hunt | Find 10 hidden eggs during Easter | Easter Chick skin |

**Notes on the skins**

- **Rudolph's glowing nose** is purely cosmetic: a soft light just ahead of the runner. It must not make obstacles easier to see than with any other skin.
- **Santa and Rudolph as ghosts:** like every skin, ghosts wear whatever the player wore on that run, so a December graveyard fills with Santas and Rudolphs. That's the screenshot moment.
- **Pumpkin Head** stays a free Halloween unlock each year. The Monsters Pack gets a different fourth skin, so buyers aren't paying for something others got free.
- **Easter Chick** replaces the Bunny idea, because Bunny is already in the Critters Pack.

**Optional paid pack: Christmas Pack (£1.99)**, with Elf, Snowman, Gingerbread Runner and Nutcracker. It sells alongside the free Santa and Rudolph and returns each December.

**Rules for seasonal achievements**

- They form their own **Seasonal** group and don't count towards completion rewards (such as the Elementals) or the "30 achievements" total, so a player who starts in March can still finish everything else.
- Progress only counts during the event, and resets if not finished. Each achievement can be earned again next year if missed.
- Use the server's date when online (the game already has it from the Daily Haunt), and the device date offline. Changing the phone's clock could fake an offline seasonal unlock, but the reward is cosmetic, so that's an acceptable risk.
- Once earned, a seasonal skin is kept forever and can be worn all year.

**Easter egg hunt (main game only)**

- During Easter, up to 2 eggs appear per run in the main game, placed where you can reach them with a normal jump.
- Collecting one happens on contact and changes nothing about movement or scoring.
- 10 eggs across the event unlocks the Easter Chick. Collected eggs are saved on the phone.

## How it's built

A season is a bundle of art plus a date range. The game picks the active season at startup and when the date changes; nothing in the simulation knows seasons exist.

**Season schedule**

- A Remote Config key `seasons` holds the schedule as JSON: id, start and end (UK local dates), and whether the event's achievements are active.
- A copy of the same schedule ships in the build as a fallback for offline players and for when Remote Config can't be reached.
- Only one season is active at a time. Where events overlap (New Year inside Christmas), the more specific one wins.
- A development-only setting forces any season, for testing.

**Season themes**

Each season is a `SeasonTheme` asset that can override any of these, falling back to the normal art where it doesn't:

| Override | Examples |
| --- | --- |
| Background and palette | Snowy sky, Halloween purple-orange |
| Background effects layer | Snowfall, fireworks, falling leaves (always drawn behind gameplay) |
| Obstacle sprites | Snow-capped blocks, painted eggs (same outline as the original) |
| Gravestone sprite | Pumpkin, snowman, heart |
| Ghost accessory | Santa hat, cobwebs |
| Nemesis accessory | Red nose (Christmas), heart (Valentine's), replacing the horns |
| Death-screen line | "It's complicated." |
| Music stinger | A short festive variation on the existing track (optional) |

**Rules for the code**

- Seasonal code must not touch the simulation, course generation, collision or anything the server replays. Add a test that runs the same recorded runs with every season forced and checks the results match exactly.
- Reskinned obstacles reuse the original collider data; only the sprite changes.
- The egg hunt's eggs are visual pickups detected after the simulation step and never feed back into it. They're disabled in the Daily Haunt, friend challenges and replays.
- Seasonal achievements use the existing achievement system, in a new Seasonal group excluded from completion counts.

**Delivering the art**

- Ship each season's art in an app update at least two weeks before it starts, switched off until its dates. App review can be slow in December.
- Later, Unity's content delivery service (Cloud Content Delivery with Addressables) could deliver seasonal art without an update. It isn't needed for the first year.

**Analytics:** `season_active` (season id), `seasonal_achievement` (id), `seasonal_skin_equipped` (skin id), `egg_collected` (count).

## Release plan and Claude Code prompt

Halloween and Bonfire Night are too close to build properly while 2.0 is still being tested, so this year starts with Christmas. Their art can ship dormant in 2.1, ready for next year.

| Release | Contents | Live by |
| --- | --- | --- |
| 2.1 — Seasons | Season system, Christmas (with Santa, Rudolph and the optional Christmas Pack), New Year, Valentine's. Halloween and Bonfire Night art included but switched off until next year. | About 20 Nov 2026, giving App Store review time before 1 Dec |
| 2.2 | Easter (painted eggs, egg hunt, Easter Chick) and April Fools | Early March 2027 |

**Before starting:** export this tab as Markdown to `docs/echo-seasons.md`, and make sure 2.0 is merged so 2.1 builds on the released code.

```
Echo 2.0 is complete. Build the seasonal events system for 2.1 from docs/echo-seasons.md.

## Ground rules
1. Seasons are cosmetic only. Do not touch the simulation, course generation, collision, scoring or anything Cloud Code replays. Reskinned obstacles reuse the original collider data.
2. Add a test that replays recorded runs with every season forced and checks the results are identical to no season.
3. Seasonal effects always draw behind the player, ghosts and obstacles.
4. The schedule comes from a Remote Config key "seasons", with a built-in copy as the offline fallback. Add a development-only setting to force any season.
5. Seasonal achievements go in a new Seasonal group that doesn't count towards completion rewards or the 30-achievement total.
6. Use the server date when online and the device date offline for seasonal achievements.
7. Development environment only, as before. No new packages without asking.

## Phases (stop after each for my review)
Phase 1: Season system — schedule, SeasonTheme asset with the overrides in the doc, the forced-season dev setting, and the replay-equality test. Deliver one test theme.
Phase 2: Christmas and New Year — snow layer, snow-capped obstacle sprites, Santa-hat ghost accessory, snowman gravestones, red-nose Nemesis, New Year fireworks at the daily reset. Santa and Rudolph skins (Rudolph's glow is cosmetic only), the Twelve Days, Lead the Way, Snowed Under and First Footer achievements, and the optional Christmas Pack IAP product.
Phase 3: Valentine's, plus dormant Halloween and Bonfire Night themes and the Pumpkin Head unlock, switched off in the schedule until next year.
Phase 4: Release prep — screenshots of each theme with 30 ghosts on screen so I can check readability, analytics events, and a checklist of Remote Config and store changes.

Use placeholder art where final art isn't ready, and list every placeholder for me.

Start with Phase 1.
```

**Art you'll need for 2.1:** a snowy background and palette, snow-capped versions of each obstacle, a Santa hat accessory, a snowman gravestone, a red-nose Nemesis accessory, Santa and Rudolph skins on the standard 16-bit template, a candy-cane trail, a fireworks trail, and a heart gravestone. The Christmas Pack adds four more skins if you do it.

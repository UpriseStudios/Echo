# World: Time Run

## Overview and the eras

Time Run is a world where the course runs through history: from the age of dinosaurs to the far future, one era every 250 m. It fits Echo's idea of your past chasing you, and your ghosts and gravestones change with each era. Everything that changes is visual; the course plays exactly like any other world.

**First release: four eras**

To keep the art manageable, the first version has four eras, reusing the existing castle and city art for the middle two.

| Distance | Era | Look |
| --- | --- | --- |
| 0–250 m | Dino Land | Giant ferns, smoking volcanoes, a T-rex silhouette and pterodactyls in the far background, warm orange sky |
| 250–500 m | Medieval | The existing castle art, retinted to suit the sequence |
| 500–750 m | Today | The existing neon city rooftops |
| 750 m and beyond | Far Future | Chrome towers, flying cars, neon grid floor, two moons |

The Far Future era continues for the rest of the run, so it becomes the setting of every long run.

**The art style travels through time too**

Each era is drawn in the style of a different era of video games, so the graphics upgrade as you run. After the final time rift, the Far Future breaks out of pixel art entirely.

| Era | Art style |
| --- | --- |
| Dino Land | 8-bit: a tiny palette (4 to 6 colours), chunky pixels, no gradients, like an early handheld |
| Medieval | 16-bit: Echo's normal style |
| Today | 32-bit: richer palette, more parallax layers, softer shading, still pixel art |
| Far Future | Modern HD: smooth gradients, real-time lighting and glow, reflections on wet chrome, soft particles |

The runner, ghosts and hazards stay pixel sprites in every era, for readability and so every existing skin works. In the Far Future they get modern treatment instead of being redrawn: glow, rim lighting from the neon, and smooth motion trails. A pixel runner in an HD future reads exactly like what it is: a time traveller.

**Later: Ancient Egypt** (pyramids, sand, obelisks) slots in between Dino Land and Medieval, at which point each era covers 200 m.

**Time rifts**

- At each era boundary, a short "time rift": a 0.3-second ripple across the screen, a flash, and a whoosh. The new era's art is in place as soon as it ends.
- The rift is drawn behind the player and ghosts, never over them, and never hides an obstacle.
- A small era label appears in the corner for 2 seconds: "65,000,000 BC", "1200 AD", "2026", "3026".

## Ghosts, gravestones and hazards per era

This is what makes Time Run feel made for Echo: your ghosts and graves belong to whichever era they're standing in.

| Era | Ghosts look like | Gravestones look like | Nemesis |
| --- | --- | --- | --- |
| Dino Land | Fossil skeletons of the skin they wore, bone-white | Rough stone slabs with claw marks | A fossil with a horned dinosaur skull |
| Medieval | Classic translucent spectres (the standard ghost) | Weathered tombstones with crosses | Standard red horns |
| Today | Standard ghosts with a faint phone-screen glow | Modern headstones with flowers | Standard red horns |
| Far Future | Flickering cyan holograms with scanlines | Holographic memorial panels | A red hologram with glitching horns |

- **The look follows the ghost's position, not where it died.** A ghost standing in Dino Land is a fossil. This only matters at era boundaries, and keeps every era consistent.
- **Ghosts keep their skin's shape** in every era, so a Wizard ghost is a Wizard-shaped fossil or a Wizard-shaped hologram. Mixed-skin graveyards still read as mixed.
- **The Nemesis must stay instantly recognisable** in every era: always red, always horned.

**Hazards** keep exactly the same outlines and collision shapes as standard obstacles. Only the sprite changes.

| Obstacle | Dino Land | Medieval | Today | Far Future |
| --- | --- | --- | --- | --- |
| Spike | Volcanic rock shard | Iron spikes | Traffic cone | Energy spike, glowing |
| Hanging block | Cliff overhang with stalactites | Castle portcullis | Hanging billboard | Hovering metal platform |
| Gap | Lava pit (glow only, same depth) | Moat | Gap between rooftops | Void with neon grid below |
| Coin | Amber with a mosquito inside | Gold coin | Coin | Data chip |

Keep every hazard in the same hazard colour family the player already knows (or a clear warning tint), so a new era never makes a hazard harder to spot.

## Unlocks, achievements and marketing

**How players get it**

- Sold in the shop alongside the other shop worlds, at the top price point, since it's the most spectacular world in the game.
- **Free trial:** every player can play Time Run 3 times for free, so they see at least the first time rift before deciding. Show "2 free runs left" on the world card.
- Nothing in Time Run affects gameplay or scoring, so buying it never gives an advantage.

**Achievements** (in the main group; they count towards the achievement total once the world is owned)

| Achievement | Requirement | Reward |
| --- | --- | --- |
| Time Traveller | Reach the Far Future in a single run | Caveman skin |
| Year 3026 | Reach 1,500 m in Time Run | Chrono Android skin |
| Extinct | Die in Dino Land 25 times | Fossil gravestone style (usable in any world) |
| Fossil Record | Have 10 ghosts on screen at once in Dino Land | Amber coin trail |
| Paradox | Get killed by your own ghost in the Far Future | None, bragging rights only |

The Caveman and Chrono Android follow the standard 16-bit skin template, so they work as ghosts in every world too.

**Marketing hooks**

- "I ran from the dinosaurs to the year 3026 in one run."
- "My ghosts turn into fossils." A clip that shows ghosts changing from fossils to holograms across eras.
- "65 million years of bad decisions."
- The time rift itself makes a great 1-second moment: cut a clip right on a rift for a loop.

Save a run that crosses all four eras with lots of ghosts: it's the trailer for this world on its own.

## Build rules, art list and Claude Code prompt

**Rules for the build**

- **Looks only.** Eras, art styles, ghost looks and hazard sprites never change collision shapes, movement, timing or anything Cloud Code replays. A run in Time Run replays exactly like the same run in any other world.
- **Era by distance,** calculated from the simulation's distance, so every replay, challenge and ghost sees the same era in the same place.
- **Preload the next era** at 200 m into the current one, so a time rift never causes a stutter.
- **Readability first:** check every era with 30 ghosts on screen. The modern HD background must stay darker and calmer than the gameplay layer, so hazards and ghosts always stand out.
- **Far Future performance:** use Unity's 2D lights and a light bloom pass, with a target of 60 fps on a mid-range Android phone. Add a reduced-effects version (no bloom, fewer lights) that switches on automatically on weaker phones and in the Settings. The world must still look good without the effects.
- **Shop world rules:** the free-trial counter is stored server-side if online play is used, otherwise on the device. Competitive modes keep their own fixed worlds unless a mode says otherwise.

**Art list**

| Era | Backgrounds | Gameplay layer |
| --- | --- | --- |
| Dino Land (8-bit) | Sky, volcanoes with smoke, ferns, a T-rex and pterodactyls (background only) | Volcanic spike, cliff overhang, lava pit glow, amber coin, fossil ghost overlay, stone slab grave, fossil Nemesis |
| Medieval (16-bit) | Existing castle art, retinted | Iron spikes, portcullis, moat, existing graves |
| Today (32-bit) | Existing city, plus 2 extra parallax layers and soft window lighting | Traffic cone, billboard, rooftop gap, phone-glow ghost tint, modern headstone |
| Far Future (HD) | Chrome towers, flying cars, two moons, neon grid floor, rain reflections | Energy spike, hover platform, void grid, data chip, hologram ghost shader, holo memorial, glitch Nemesis |
| All | Time rift effect, era labels, world card art for the shop | Caveman and Chrono Android skins |

The Far Future background is the only HD art, so it can be painted or rendered at full resolution. Everything else stays on the pixel grid.

**Claude Code prompt**

Export this tab to `docs/echo-time-run.md` first.

```
Build the Time Run world from docs/echo-time-run.md. It is purely visual: do not change collision, movement, scoring, course generation or anything Cloud Code replays.

Stop after each phase for my review.

Phase 1: Era system
- Eras chosen by simulation distance (0-250 Dino Land, 250-500 Medieval, 500-750 Today, 750+ Far Future), the time rift effect behind gameplay, era labels, preloading at 200 m into each era.
- A test that replays recorded runs in Time Run and in a standard world and checks the results are identical.
- A dev-only setting to jump to any era.

Phase 2: Era-specific gameplay visuals
- Hazard sprites per era using the original collider data, era ghost looks chosen by the ghost's position (fossil, standard, phone glow, hologram), era gravestones, the Nemesis variants (always red and horned).
- Ghosts keep their skin's silhouette in every era.

Phase 3: Art styles
- 8-bit Dino Land, 16-bit Medieval (existing castle, retinted), 32-bit Today (existing city plus extra parallax and lighting), modern HD Far Future with 2D lights, bloom, reflections and the glow and motion-trail treatment for pixel sprites.
- A reduced-effects mode, automatic on weaker phones and selectable in Settings.
- Report frame rate on a mid-range Android phone in each era with 30 ghosts.

Phase 4: Shop and achievements
- Time Run as a shop world with 3 free trial runs, the five achievements and the Caveman and Chrono Android skins (standard skin template).
- Analytics: timerun_trial_started, timerun_purchased, era_reached (era).

Use placeholder art where final art isn't ready, list every placeholder, and give me screenshots of each era with 30 ghosts on screen.
```

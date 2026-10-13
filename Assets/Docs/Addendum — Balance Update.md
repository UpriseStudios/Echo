# Addendum — Balance Update

This update makes Echo fairer in three ways. Two changes help players who get stuck: a **Banish 10** offer when the ghost cap is full, and **Continue** offered once on every run. Both respond to tester feedback that Echo is hard. Friend challenges now send the challenger's ghosts too, so a challenge is as hard as the run it came from. This addendum also covers a quick check on control responsiveness, which 1 of 21 testers raised.

## Banish 10

When a player has a full graveyard of 30 ghosts, they can watch a rewarded ad to clear the 10 ghosts that have killed them most.

| Rule | Detail |
| --- | --- |
| When offered | On the death screen, when 30 ghosts are active after the run is committed |
| Replaces | The single-ghost Banish offer, while Banish 10 is available. If Banish 10 has already been used this cycle, the single Banish shows as normal. |
| Ad placement | `banish_ten` (rewarded) |
| Which ghosts | The 10 non-Nemesis ghosts with the most kills. Ties go to the oldest ghost (lowest run index). If fewer than 10 have kills, fill the rest with the oldest ghosts. |
| Nemesis | Never removed. The Nemesis can only be beaten by a new best or a Nemesis Duel. |
| Gravestones | Removed with their ghosts |
| Limit | Once per high-score cycle, tracked separately from the single Banish limit (3 per cycle). Resets on a new best. |
| Order of events | The dead run is committed as a ghost first (cap applied), then 10 ghosts are removed, leaving 20. |
| Effect | All 10 burst at once with the ghost-clear effect, plus a short message: "10 ghosts banished". |
| Score multiplier | Recalculates naturally on the next run from the remaining ghosts |
| Achievements | Banished ghosts don't count towards clear achievements (Exorcist, Mass Exorcism), which need a new best. |
| Ad failure | If no ad is available, the button is hidden. If the ad fails part-way, nothing is removed and the offer stays available. |

**Button text:** "Banish 10 ghosts" with a small ad icon. Sub-label: "The ones that keep getting you".

## Continue

Continue is offered once on every run, with no score condition.

| Rule | Detail |
| --- | --- |
| When offered | On every death, if Continue hasn't been used in this run |
| Removed condition | The "score over 50% of your best" requirement is removed |
| Effect | Unchanged: revive at the last safe floor position with 1 second of invulnerability |
| Not offered in | Nemesis Duels and friend challenges, so those stay pure skill tests |

## Death screen offers

The death screen shows at most two ad offers, in this order:

| Situation | Offers shown |
| --- | --- |
| Continue unused, 30 ghosts, Banish 10 available | Continue, Banish 10 |
| Continue unused, killed by a non-Nemesis ghost, single Banish available | Continue, Banish |
| Continue unused, no banish available | Continue |
| Continue already used this run | Banish 10 or Banish if available, otherwise none |

Tapping anywhere outside the buttons still restarts instantly, so the offers never slow down a retry.

## Analytics and guardrails

| Event | Properties |
| --- | --- |
| `banish_ten_offered` | ghostCount, deathsSinceBest |
| `banish_ten_used` | killsRemoved, nemesisPresent |
| `continue_offered` | score, best, runSeconds |
| `continue_used` | score, best, runSeconds |

| Measure | Healthy | Action if not |
| --- | --- | --- |
| Share of new bests within 3 runs of a Banish 10 | Under 50% | Only offer Banish 10 after 5 deaths in a row at 30 ghosts |
| Players who stop playing while at 30 ghosts | Falls after release | If it doesn't fall, being stuck isn't the main reason players leave; look at early-game difficulty instead |
| New bests after a Continue | Any; for information only | If very low, Continue is mostly saving weak runs, which is harmless |

## Friend challenges: send the full graveyard

A challenge now carries the exact haunted course the challenger faced: the course seed, every ghost active when their run started, and the run itself. Until now only the run was sent, so friends faced an empty course and won too easily.

| Rule | Detail |
| --- | --- |
| What's sent | Course seed, content version, any active weekly twist, every ghost active at the start of the challenger's run (taps, death point, skin, kills, Nemesis flag), and the challenger's run |
| Snapshot timing | The ghosts as they were when the challenger's run started, not after it ended |
| Challenger's run | Appears as a ghost waiting just before where the challenger died, like any other ghost, with a name tag ("Jamie"). It guards the distance to beat. |
| Target | The challenger's score, with distance shown underneath. Both players face the same ghosts, so the multiplier matches and scores compare fairly. |
| Nemesis | The challenger's Nemesis appears with its red horns and name ("Jamie's Nemesis: The Seventh"). It kills on contact; there is no duel. |
| Bounty ghost | Same course seed, so the same bounty ghost |
| Not available | Continue, Banish, Banish 10 and Nemesis Duel |
| Friend's failed attempts | Don't add ghosts, which would make it harder than the challenger's run. Retries are unlimited, and the result shows the attempt count: "Beat Jamie's score on attempt 4". |
| Weekly twist | The challenge plays under the twist that was active when it was recorded, with a banner saying so |
| Gallery runs | Favourite runs saved from now on also store their ghost snapshot, so challenges can be sent from the gallery. Runs saved before this update are sent as "Lite" challenges with no ghosts, and labelled as such. |
| Old links | Challenges sent before this update have no ghost data. They open as Lite challenges with a label. |
| Isolation | A challenge never changes the friend's own ghosts, best score, Nemesis or graveyard. Only challenge achievements count. |

**Sending the data**

- 30 ghosts plus a run is likely too long for a link. Store the challenge on a small server and share a short link or code instead. This can use the same small server planned for the daily course.
- Ghosts now appear a few seconds before where they died, so each ghost only needs its starting state at that point plus its final taps. That keeps each ghost to a few dozen bytes.
- Validate every challenge on open: at most 30 ghosts, a known content version, and sane tap counts.

**Analytics**

| Event | Properties |
| --- | --- |
| `challenge_sent` | ghostCount, hasNemesis, twist |
| `challenge_result` | won, attempts, ghostCount, lite |

A healthy challenge win rate is somewhere between about 30% and 60%. Well above that suggests the snapshot is incomplete or retries make it too easy to learn the course.

## Control responsiveness check

Only 1 of 21 testers raised this, but it's quick to rule out, and some players may feel it without saying so.

- [ ] Ask the tester which device they used. Low-end Android phones are the most likely cause.
- [ ] Confirm the jump fires on touch down, not on release.
- [ ] Confirm taps between frames are queued and never dropped, especially if the frame rate dips.
- [ ] Profile frame rate on a low-end Android phone with the ads SDK initialised. Dips below 60 fps feel like input lag.
- [ ] Check no invisible UI element (HUD, safe-area panel, transparent overlay) blocks taps in part of the screen.
- [ ] Confirm the jump buffer (100 ms) and coyote time are active in release builds, not just the editor.
- [ ] Ask whether they meant a delay before the jump, or that the jump itself feels floaty or uncontrollable. A fixed-height jump can read as "unresponsive" to some players.

If all of these pass, treat it as one player's preference and leave the controls alone.

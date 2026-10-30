# Echo 2.2 (Community Graveyard): release checklist

Everything below is yours to do. The Graveyard ships switched off; its first week is planned for **Monday 18 January
2027**, and turning it on is a Remote Config change, not an update.

## 1. Code

- [ ] Review and merge `daily-haunt/backend` into `main`, and tag it `v2.2.0`.
- [ ] Version is **2.2.0**, with an Android version code higher than 2.1's (check Play Console > App bundle explorer).
- [ ] Saves: the Graveyard adds a `graveyard` section to save schema 5 (unsent runs, the next run number, the last
      summary seen). If 2.1 has shipped with schema 5, nothing to migrate: the section is new and starts empty.

## 2. Production backend

- [ ] The UGS service account needs the **Scheduler** and **Triggers** roles in production too (it has them in
      development). Without them, deploy with `-SkipSchedules`; the first lobby of each day builds the ghosts instead.
- [ ] Preview: `powershell -File Server/deploy-prod.ps1 -DryRun`.
- [ ] Deploy: `powershell -File Server/deploy-prod.ps1 -Confirm production`. This adds:
  - the module endpoints `GetGraveyard`, `SubmitGraveyardRun` and `BuildGraveyardSet`;
  - the boards **`graveyard_daily`** (resets 00:00 UTC) and **`graveyard_weekly`** (resets Mondays 00:00 UTC), both
    keep-best with archives;
  - the schedule **`echo-graveyard-midnight`** and its trigger, which call `BuildGraveyardSet` at 00:00 UTC;
  - the `graveyard` window in the Remote Config key `modes`, switched **off**.
- [ ] Smoke test: `$env:ECHO_PROBE_PRODUCTION='yes'; dotnet run --project Server/Tools/ReplayProbe -- smoke production`.
- [ ] If UGS answers a leaderboard update with "unknown server error" (500), deploy again: it has been a passing fault
      in development, landing on different boards each time.

## 3. Going live

- [ ] In Remote Config (production) > `modes`, set the `graveyard` window's `enabled` to `true` and check its `start`
      is a Monday 00:00 UTC (weeks run Monday to Sunday).
- [ ] Update the privacy policy page (the Community Graveyard lines are already in `web/privacy/index.html`) and the
      store data forms: runs are stored 21 days and shown to other players as nameless ghosts.
- [ ] Add the mode to the store description and the release notes.
- [ ] Record launch clips with the recording APK (Settings > Dev: modes forces it open).
- [ ] Watch crash reports and server errors closely for the first 48 hours.

## 4. Analytics (Unity Analytics > Event Manager, production)

- [ ] `graveyard_opened`: `day` (int, 0 Monday), `size` (int), `rank` (int).
- [ ] `graveyard_run_end`: `distance` (int), `day` (int), `newBest` (bool).
- [ ] `graveyard_week_complete`: `total` (int), `rank` (int).
- [ ] `daily_run_watched` already exists; add the parameter `mode` (string: `daily`, `one_life` or `graveyard`), since
      the mode boards can be watched too (a rewarded ad on the `watch_run` placement, as for the Daily Haunt).
- [ ] `mode_reward_earned` already exists; the Graveyard sends `mode` = `graveyard` with `reward`
      `gravekeeper_<monday>` or `char_gravekeeper`.

## 5. Rewards

- [ ] **Gravekeeper badge**: the weekly top 10, shown beside their name for the following week.
- [ ] **Gravekeeper character**: the fourth week a player plays all 7 days. Granted by the server; nothing to set up.

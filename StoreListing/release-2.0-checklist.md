# Echo 2.0: production release checklist

Everything below is yours to do; nothing here has touched production. Work top to bottom: the release build
talks to **production**, so production must be ready before anyone installs it, including internal testers.

## 1. Code

- [ ] Review and merge `daily-haunt/backend` into `main`.
- [ ] Version is set to **2.0.0**, Android version code **12**. Check 12 is higher than the last upload in Play Console
      (Release > App bundle explorer); raise it if not.

## 2. Unity Cloud dashboard: production environment

All of this is in the **production** environment. Development stays as it is for testing.

- [ ] **Authentication**: Anonymous sign-in is enabled (it is by default).
- [ ] **Secret Manager** (production), two secrets:
  - [ ] `DAILY_SEED_SALT_PROD`: a **new** long random string, not the development one. Example:
        `openssl rand -base64 48`. Never change it after launch: it would change the day's course mid-day.
  - [ ] `LEADERBOARDS_ADMIN_KEY_PROD`: `keyId:secret` of a service account with the Leaderboards admin role
        (Administration > Service accounts > New, add the project role that allows deleting leaderboard scores, then
        create a key). Delete online data uses it to remove the player's live board score. Without it, everything else
        is still deleted and the score stays until midnight's archive.
  - [ ] Also add `LEADERBOARDS_ADMIN_KEY` in **development** (its own service account key is fine) so I can prove the
        score deletion with `ReplayProbe delete`.
- [ ] **Leaderboard start date**: in `Server/Config/daily.lb`, set `ResetConfig.Start` to midnight UTC on or after
      launch day (UGS needs it in the future when the leaderboard is created). It's 2026-10-15 for development.
- [ ] **CLI access**: the CLI must be logged in with Echo's service account (production roles for Cloud Code, Remote
      Config, Leaderboards and Access Control). If it's logged in for another project, set `UGS_CLI_SERVICE_KEY_ID` and
      `UGS_CLI_SERVICE_SECRET_KEY` in the deploying terminal instead of logging in again. The scripts always target Echo's
      project (from `ProjectSettings`), whatever project the CLI is set to.
- [ ] **Deploy to production** from the repo root:
  ```
  powershell -ExecutionPolicy Bypass -File Server/deploy-prod.ps1 -DryRun
  powershell -ExecutionPolicy Bypass -File Server/deploy-prod.ps1 -Confirm production [-SwitchesOff]
  ```
  It runs the server tests, packages the module, sets the first daily reset to the next midnight UTC and deploys the
  module, Remote Config, the leaderboard and the Access Control policy with `-p <Echo> -e production` (the CLI's saved
  settings are untouched).
- [ ] **Smoke check** (no runs submitted, nothing on the public board; the test player deletes its own data):
  ```
  $env:ECHO_PROBE_PRODUCTION = 'yes'; dotnet run --project Server/Tools/ReplayProbe -- smoke production
  ```
  Every line should PASS (or INFO for a switch left off): the seed salt, the content version, ReplayCheck refused,
  direct writes refused, Delete online data and the Leaderboards admin key.
- [ ] **Remote Config switches**: the deploy turns `daily_enabled` and `server_challenges_enabled` on. To launch the
      app first and the online features later, set both to `false` in the dashboard straight after deploying, then
      back to `true` when ready. Both the game and the server obey them within seconds.
- [ ] **Check production** in the dashboard:
  - Cloud Code > Modules: `EchoCloud` is listed.
  - Leaderboards: `daily` (Daily Haunt, descending, keep best, daily reset with archive).
  - Access Control: four statements, all Deny for Player: leaderboard writes, Cloud Save writes, Cloud Code
    scripts, and `urn:ugs:social:/*/names/*` (player names).

- [ ] **Analytics events** (Unity Analytics > Event Manager, in production; events or parameters that aren't defined
      are dropped). Integer = whole number, Boolean = true/false, String = text.

      New in 2.0:

      | Event | Parameters |
      |---|---|
      | `banish_ten_offered` | `ghostCount` integer, `deathsSinceBest` integer |
      | `banish_ten_used` | `killsRemoved` integer, `nemesisPresent` boolean |
      | `daily_opened` | `date` string, `rank` integer, `best` integer |
      | `daily_run_end` | `distance` integer, `best` integer, `rank` integer, `leadersPassed` integer, `newBest` boolean |
      | `daily_new_best` | `distance` integer, `rank` integer |
      | `daily_submit_failed` | `error` string |
      | `daily_banish_used` | `ghostRunIndex` integer, `left` integer |
      | `daily_run_watched` | `rank` integer, `own` boolean |
      | `clip_exported` | `preset` string (gif, video, vertical), `length` integer (seconds), `has_hook` boolean |
      | `clip_shared` | `preset` string |

      Changed in 2.0 (add the new parameters to the existing events):

      | Event | Add | Notes |
      |---|---|---|
      | `challenge_sent` | `ghostCount` integer, `hasNemesis` boolean, `twist` string, `lite` boolean | `distance` is no longer sent |
      | `challenge_opened` | `lite` boolean | still sends `distance`, `versionMatch` |
      | `challenge_result` | `attempts` integer, `ghostCount` integer, `lite` boolean | still sends `won`, `margin`, `killedByGhost`, `distance`, `theirDistance` |

      `banish_ten_*` were already in the first commit on `main`, but they came with the balance update, which ships in
      2.0; skip them if 1.4's dashboard already has them. Every other event is unchanged from 1.4.
- [ ] **Ad units (optional)**: Daily Haunt banish and "watch a run" have their own placements (`daily_banish`,
      `watch_run`). Until you create ad units for them in LevelPlay and put them in `EchoServiceConfig`
      (`DailyBanishAdUnit*`, `WatchRunAdUnit*`), they use the banish and unlock ad units, which works but mixes their
      reporting.

## 3. Website (Cloudflare Pages, project `echo-links`)

- [ ] Deploy `/web` to `echo-links` with the custom domain `echo.uprisestudios.co.uk`.
- [ ] `https://echo.uprisestudios.co.uk/.well-known/assetlinks.json` loads (it already holds the Play app signing
      SHA-256). Check it with Google's Digital Asset Links tester for `com.uprise.echo`.
- [ ] `https://echo.uprisestudios.co.uk/c/test/test` shows the "Open in Echo" page.
- [ ] `https://echo.uprisestudios.co.uk/privacy/` shows the current policy. Its wording is version-neutral (the
      online features are described as "in version 2.0 and later"), so it can go live at submission, before 2.0 is
      out. Update the effective date in `web/privacy/index.html` and `StoreListing/privacy-policy*.html` to the day you
      publish it.
- [ ] The published policy page (https://claude.ai/artifact/4a9fuGvRsQQJVJPwUPqzpb) still shows the 1.4 policy. Ask
      me to republish it from `StoreListing/privacy-policy.body.html` when you submit 2.0.
- [ ] The iOS placeholders in `web/.well-known/apple-app-site-association` can wait for the iOS release.

## 4. Build and test

- [ ] To test server changes on phones **before** production is ready: **Echo > Build > Android App Bundle (development
      server)**. It writes `Echo-devserver.aab`, talks to the development environment and shows **DEV SERVER** on the
      title. Use it on internal testing only, never on the production track.
- [ ] For release: **Echo > Build > Android App Bundle** (it needs your keystore passwords, so not from batch mode). No
      DEV SERVER tag on the title means it talks to production.
- [ ] Upload to an **internal testing** track and check on a phone:
  - Daily Haunt opens, plays and ranks; the board shows generated names.
  - Send a challenge, open its link on a second phone, race it.
  - Settings > Delete online data, then the Daily Haunt again: you start afresh with a new name.
  - Flip a Remote Config switch off and on: the lobby says the Daily Haunt is resting, then plays again.

## 5. Google Play Console

### Data safety (App content > Data safety). Changes for 2.0

Keep your existing answers for ads, analytics and purchases and add the following. "Collected" means it leaves the
phone to us. Unity Gaming Services processes it on our behalf as a service provider, which Play doesn't count as
"sharing".

| Data type (Play's name) | Collected | Shared | Optional? | Purposes |
|---|---|---|---|---|
| Personal info > **User IDs** (the random UGS player ID) | Yes | No | Optional: only when the player uses the Daily Haunt or challenges | App functionality, Account management, Fraud prevention, security and compliance |
| Personal info > **Name** (the challenge name the player types, stored with server challenges) | Yes | No | Optional | App functionality |
| App activity > **App interactions** (runs as tap timings, distances, daily attempts) | Yes (already declared for analytics: add the purpose) | No | Optional | App functionality, Fraud prevention, security and compliance (plus Analytics as before) |
| App activity > **Other user-generated content** (stored challenges: a run, its ghosts and a name others can open) | Yes | No | Optional | App functionality |

Security practices:
- Data is encrypted in transit: **Yes**.
- Users can request that their data be deleted: **Yes**, in the app (Settings > Delete online data), and by email
  for anything else.

Account deletion: Echo's online ID is created automatically and anonymously; players don't create an account, so
Play's account-deletion URL requirement shouldn't apply. If the form asks, give
`https://echo.uprisestudios.co.uk/privacy/#online`, which explains the in-app deletion.

### Elsewhere in Play Console
- [ ] **Privacy policy URL** (App content > Privacy policy): `https://echo.uprisestudios.co.uk/privacy/` (update it
      if it pointed elsewhere).
- [ ] **Content rating**: redo the questionnaire. New answers: players' content reaches other players (daily board
      names are generated, but challenge names are typed by players and shown to whoever opens the link; they pass a
      blocklist). Answer "users can interact / share content" accordingly.
- [ ] **Release notes**: the Daily Haunt (one course a day, today's top 10 haunt it as ghosts), fairer challenges
      (your friends race the ghosts you raced), Continue on every run, Banish 10.
- [ ] Roll out with a **staged rollout** (for example 10%), watch for a day, then widen.

## 6. Apple App Store (for the iOS release later)

App Privacy (App Store Connect > App Privacy). Add for 2.0:

| Apple category | Type | Linked to the user? | Tracking? | Purposes |
|---|---|---|---|---|
| Identifiers | **User ID** (random UGS player ID) | Yes (it is the online account's ID) | No | App Functionality |
| Contact Info | **Name** (the challenge name) | Yes | No | App Functionality |
| User Content | **Gameplay Content** (runs, daily attempts, stored challenges) | Yes | No | App Functionality |
| Usage Data | **Product Interaction** | as already declared for analytics | No | Analytics (as before) |

Also, for iOS: fill in the `apple-app-site-association` placeholders, turn on Associated Domains, and make sure the
privacy policy URL is set in App Store Connect. Apple requires in-app account deletion only for apps where users
create an account; Settings > Delete online data covers it anyway.

## 7. After launch

- [ ] Watch Cloud Code > Logs and error rates for `EchoCloud` for the first days.
- [ ] Kill switches: Remote Config `daily_enabled` / `server_challenges_enabled` set to `false` close the features in
      the game and on the server within seconds. Queued daily runs wait on the phone until the end of their day.

## Known limits (accepted)

- **Grace runs aren't ranked.** UGS archives are read-only, so a run finished just after midnight is replayed and
  kept as that day's ghost, but goes on neither board ("Midnight passed while you ran").
- **Archived daily boards keep a random ID, a generated name and a distance** for each player, and nobody can edit
  them. Once a player deletes their online data, nothing links those entries to them. The policy says so.
- **Two submissions sent at exactly the same moment** can both get past the 2-second rate limit. Each is still
  replayed honestly, so it gains a cheater nothing.
- **Uninstalling doesn't delete online data**, because the anonymous ID goes with the app. It is still deleted on
  schedule (daily records after 7 days, challenges after 30), and the policy tells players to use the button first.

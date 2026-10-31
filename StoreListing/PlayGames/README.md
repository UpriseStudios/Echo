# Google Play Games Services for Echo

Echo uses Play Games Services v2 for achievements, Game Stats, cloud save (Saved Games) and Play Games sign-in,
which also carries the player's online account (UGS) between devices. Until the steps below are done, the game
runs exactly as before: Play Games stays off and nothing is sent.

Play's Level Up guidelines ask for these (PGS v2 at start, 10+ achievements, 5 Game Stats and a progression stat,
cloud save with a conflict policy, Sidekick). Echo has 41 achievements (40+ is Play's "robust" mark), 5 repetitive
stats (one competitive) and a progression stat (best distance).

The files here are made by `python StoreListing/PlayGames/make_play_games.py` from the game's own catalog and art:

| File | Where it goes |
|---|---|
| `achievements.zip` | Play Console > Play Games Services > Achievements > Import |
| `game_stats_events.csv` | Play Console > Play Games Services > Game Stats > Events |
| `game_stats.zip` | Play Console > Play Games Services > Game Stats > Import stats |
| `preview.png` | Every icon on one sheet, for review (not uploaded) |

## 1. Play Console

1. **Grow users > Play Games Services > Setup and management > Configuration**: create a new Play Games Services
   project for Echo (a game that doesn't use Google APIs yet).
2. **Credentials > Add credential > Android**, package `com.uprise.echo`, one credential per signing key. Both
   Play SHA-1s are on **Test and release > App integrity > Play app signing > Settings** (the App signing page):
   - **App signing key certificate** SHA-1: every build installed from Play (internal testing included; Play
     re-signs them).
   - **Upload key certificate** SHA-1: builds you sign and install yourself (the release bundle via bundletool).
     Also `keytool -list -v -keystore "C:/Users/jamie/OneDrive/Documents/uprise-echo.keystore" -alias echo-ghost`.
   - Optional, the **debug key** SHA-1, to try Play Games in the recording APK:
     `keytool -list -v -keystore "%USERPROFILE%\.android\debug.keystore" -alias androiddebugkey -storepass android`.
3. **Credentials > Add credential > Game server**: create (or pick) an OAuth **Web application** client in Google
   Cloud. Keep its client ID and secret; the client ID goes into the game (step 3), the pair into UGS (step 4).
4. **Achievements > Import**: upload `achievements.zip`. Check the list (41, 920 points, 3 hidden).
5. **Saved Games**: turn on (Configuration > Saved Games: On).
6. **Game Stats**: upload `game_stats_events.csv` as the events, then `game_stats.zip`.
7. **Testers**: add your Google account so you can sign in before the configuration is published.
8. **Publish** the Play Games Services configuration (Review and publish).
9. **Sidekick**: Grow users > Play Games Services > Sidekick: turn it on (App Bundles need no code).

## 2. The game

1. Play Console > Achievements > **Get resources** > Android (XML). Save it as
   `Assets/Echo/Settings/PlayGamesResources.xml` (it holds only public ids).
2. In `Assets/Echo/Settings/Resources/EchoServiceConfig.asset`, set **Play Games Web Client Id** to the Game
   server client ID from step 1.3.
3. Unity: **Echo > Play Games > Import Resources**. It runs the plugin's setup (the `APP_ID` in the Android
   manifest, `GPGSIds`) and matches every Echo achievement to its Play id by name; it lists any it can't match.
4. Build as usual (`Echo/Build/Android App Bundle`).

## 3. Unity Gaming Services

Dashboard > Player Authentication > Identity providers > **Google Play Games**: add the Web client ID and secret
from step 1.3. Do it in both environments (development and production). Without it Play Games still works in the
game, and online play stays on its anonymous account as before.

## How it behaves

- **Sign-in**: after the privacy choice, Play Games signs in automatically. If the player isn't signed in to
  Play Games, Settings > Play Games offers it; nothing is ever asked mid-run.
- **Achievements**: every achievement the game awards is unlocked on Play (counted ones keep their steps), and
  everything already earned is sent on each sign-in, so older players get theirs.
- **Game Stats**: one `runEnded` event per finished run (main game, Daily Haunt and modes; never a replay) with
  distance, score, ghosts passed and near misses, and `progressUpdate` with the best distance when it rises.
- **Cloud save** (conflict policy): the whole save, compressed, in one Saved Game (`echo_save`), written after each
  finished run (at most once a minute) and when the game is put away. On sign-in the game compares the cloud save
  with this device's by runs played (then best distance):
  - no cloud save, or this device's has as many runs: this device's is kept and uploaded;
  - the cloud has more: the player is asked ("Progress found on Google Play Games: 312 runs, best 1,240 m. Use it?").
    Using it keeps this device's save as `echo_save.json.device.bak` and reloads the game with the cloud's.
  - Saves never merge; between two devices, Saved Games keeps the one with the longest play time.
- **Online account**: the first time an online feature signs in, a Play Games player links their anonymous UGS
  account to Play Games; on a new device, Play Games sign-in brings back the same online account (Daily Haunt
  history, challenges, badges). If the Play Games account was already linked elsewhere, the device switches to it.

# echo.uprisestudios.co.uk

The website behind Echo's challenge links (version 2.0), published with Cloudflare Pages (project `echo-links`,
custom domain `https://echo.uprisestudios.co.uk`). It is a static site: this folder is the build output.

| Path | What it is |
| --- | --- |
| `/.well-known/apple-app-site-association` | iOS Universal Links: lets `https://echo.uprisestudios.co.uk/c/...` open Echo |
| `/.well-known/assetlinks.json` | Android App Links: the same for Android (verified at install) |
| `/c/<ownerId>/<challengeId>` | A challenge link. With Echo installed the phone opens the game; otherwise `challenge.html` shows "Open in Echo" and the store buttons |
| `/` | A small Echo page with the store buttons |
| `/privacy/` | The privacy policy (a copy of `StoreListing/privacy-policy.html`; copy it again when that changes) |
| `_headers` | Serves `apple-app-site-association` (which has no file extension) as `application/json` |
| `_redirects` | Serves `challenge.html` for every `/c/*` address |

## Android first, iOS to follow

The site is ready to publish for Android: `assetlinks.json` is filled in and the pages link to Google Play. For iOS,
fill in the two placeholders below and add a "Download on the App Store" button to `index.html` and `challenge.html`
(each has a comment where it goes). Until then the Apple file is served but matches no app, which is harmless.

## Placeholders to fill in for iOS

| Placeholder | Where | Value |
| --- | --- | --- |
| `__APPLE_TEAM_ID__` | `apple-app-site-association` | Apple Developer account > Membership > Team ID (10 characters) |
| `__IOS_BUNDLE_ID__` | `apple-app-site-association` | The iOS bundle id; the project has `com.uprise.echo` |

`assetlinks.json` is filled in from Play Console's App integrity page (package `com.uprise.echo`, the Play app
signing key). It verifies builds installed from Google Play. A build you sideload yourself is signed with your upload
key instead, so its https links open in the browser; to have those open the game too, add the upload key's SHA-256
(App integrity > Upload key certificate) as a second fingerprint.

## Publishing

1. Cloudflare dashboard > Workers & Pages > Create > Pages > Direct Upload (or connect this repo with the
   build output directory set to `web` and no build command). Name the project `echo-links`.
2. Custom domains > add `echo.uprisestudios.co.uk`.
3. Check: `https://echo.uprisestudios.co.uk/.well-known/apple-app-site-association` returns the JSON with
   `Content-Type: application/json`, and `https://echo.uprisestudios.co.uk/.well-known/assetlinks.json` returns
   the JSON. Google's checker: https://developers.google.com/digital-asset-links/tools/generator
4. iOS also needs the Associated Domains capability on the App ID in the Apple Developer portal; the game's build
   adds the entitlement (`Assets/Echo/Editor/IosAssociatedDomains.cs`).

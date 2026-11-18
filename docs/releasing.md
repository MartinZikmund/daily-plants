# Versions, channels and releases

`main` is where the next version is brewing. `release/vX.Y` branches are where versions ship. Nothing on `main` ever
reaches a store.

The model comes from [uno-app-template](https://github.com/MartinZikmund/uno-app-template) (its `docs/versioning.md`,
`docs/release-pipeline.md` and `docs/release-runbook.md` go deeper). This page covers what Daily Plants does and where
it differs.

## Channels

Every build has an `AppChannel`, set in `Directory.Build.props`:

| | Dev | Prod |
|---|---|---|
| Built by | local builds, `main`, PRs | `release/v*` in CI, or `-p:AppChannel=Prod` |
| Name | Daily Plants Dev | Daily Plants |
| Android, iOS, macOS ID | `dev.mzikmund.dailyplants.dev` | `dev.mzikmund.dailyplants` |
| Windows package name | `dev.mzikmund.dailyplants.dev` | `5143MartinZikmund.DailyPlants` |
| Compile constant | `APP_CHANNEL_DEV` | none |

Different IDs mean a Dev build installs next to the store app instead of over it.

- **Windows.** `Package.appxmanifest` holds the Partner Center identity, and Prod ships it untouched.
  `StampWindowsDevIdentity` in `src/Directory.Build.targets` renames the generated manifest for Dev and puts the Dev
  title on the tile. The template stamps the Store identity from repo variables instead; here it's checked into the
  manifest, so there are no `WINDOWS_STORE_*` variables to set.
- **Android.** The launcher label is a string resource, so `src/MobileLabels.targets` appends " Dev" to it.
- **iOS.** `Info.plist` leaves `CFBundleDisplayName` out, so the SDK takes it from `ApplicationTitle`.
- **macOS.** The Mac App Store profile only covers the Prod ID, so `docs/store/macos/package-app.sh` always builds Prod
  and Dev builds of the Mac app are unsigned.

The store screenshot steps in `docs/store/` build Prod.

### Worktree builds

A Dev build from a linked git worktree gets its own identity on top of the Dev one, so two worktrees install and run
side by side. Nothing to switch on: `src/WorktreeIdentity.props` spots the worktree from the `.git` file, and
`src/WorktreeIdentity.targets` applies it. It's off in the main checkout, in CI (`CI` or `ContinuousIntegrationBuild`)
and on Prod.

| | Main checkout | Worktree `feature-x` |
|---|---|---|
| ID, Windows package name | `dev.mzikmund.dailyplants.dev` | `dev.mzikmund.dailyplants.dev.wtfeaturex<hash>` |
| Name (Windows, desktop) | Daily Plants Dev | Daily Plants Dev (feature-x) |
| Android label, iOS name | Daily Plants Dev | Daily Plants Dev [FX] |
| Title-bar badge | DEV | DEV · feature-x |
| Window title | unchanged | Daily Plants — feature-x |
| About page | version | version, then "Worktree: feature-x" |

The ID segment is `wt`, 8 letters of the name and a 6-digit hash, so it stays at 50 characters or fewer. The title tag
is cut so the Windows tile name stays at 40 or fewer. The name is git's admin folder for the worktree, so switching
branches keeps the identity, and `git worktree move` changes it.

- `-p:EnableWorktreeIdentity=false` builds the plain Dev identity from a worktree, `-p:AppWorktreeName=x` picks the name.
- The WebAssembly head has no install identity. Its build prints a port to serve it on, so worktrees don't share an
  origin.
- **Each channel and worktree has its own data.** The database and feed cache live under `%LOCALAPPDATA%`: `DailyPlants`
  for Prod, `DailyPlants.Dev` for Dev, `DailyPlants.Dev.<worktree>` for a worktree. A Dev or worktree build starts with an
  empty diary, and never touches the store app's. The suffix matters because the desktop head isn't packaged, and MSIX
  only gives a packaged app its own copy of an AppData folder that didn't exist before it first wrote there. Android
  and iOS sandbox each identity anyway.
- Clean up stray Windows registrations with `Get-AppxPackage dev.mzikmund.dailyplants.dev.wt*`.

`scripts/verify-worktree-identity.ps1` checks the rules on the real project. The tests cover them on a scratch one.

### Dev artwork

Dev builds stamp a gold **DEV** badge (blue **CI** on CI builds) onto the bottom centre of the app icon and splash at
build time, so there's no Dev artwork to draw. `src/DevAssets.targets` runs `ComposeDevAsset`
(`src/DevAssets.Task.cs`) on the `UnoIcon` foreground and `UnoSplashScreen` SVGs just before Uno.Resizetizer, and
lifts the badge inside the Android and iOS masks. The iOS background plate is left alone, so the icon stays opaque.
Prod builds use the artwork untouched. `-p:GenerateDevAssets=false` turns it off.

**Windows tiles, taskbar, Alt-Tab and splash.** `Package.appxmanifest` points at the hand-made PNGs in
`Platforms/Windows/`, not at Resizetizer's output, because Resizetizer draws every tile with one foreground scale and have their own padding per image. So Dev builds re-render that set: `GenerateDevWindowsAssets` badges
`Assets/Icons/icon_foreground.svg`, runs `assets/app-icon/export-windows-assets.cs` on it into
`obj/.../devassets/<hash>/windows/` and swaps those PNGs in under the same package paths. The badge sits at the centre of the logo, which is the image's edge on the unplated taskbar icons. The export runs once per badged, a few seconds. Prod ships the checked-in PNGs, and they copy when different rather than when newer, so a Prod after a Dev one doesn't keep the badged copies.

It doesn't reach everything:

- **The macOS bundle icon** comes from the plated PNGs in `Platforms/Desktop/macOS/AppIcon/`, so it stays plain too.
  The Mac app only ships Prod.

`scripts/verify-dev-assets.ps1 -IncludeAndroid` builds Dev, Prod and opt-out and compares the rendered PNGs.

## Versions

`version.json` on `main` is the next minor with a `-dev` tag, for example `0.8-dev`. The patch number is the git
height, so every commit is a new version and nothing is edited by hand.

| Store | Gets | Example for `0.8.42` |
|---|---|---|
| Microsoft Store | `X.Y.Z.0` | `0.8.42.0` |
| Google Play `versionCode` | `X * 10,000,000 + Y * 100,000 + Z` | `800042` |
| App Store, Mac App Store | `X.Y.Z` | `0.8.42` |

CI computes these once (`.github/actions/app-version`) and passes them on the command line with
`-p:CiVersionInjected=true`. A Prod CI build without them fails (`AssertStoreVersionStamped`), and each build workflow
reads the version back out of the package. Local builds let Nerdbank.GitVersioning pick the version.

### Why main is 0.8-dev and not 0.7-dev

The old pipeline pushed `0.7.x` builds from `main` to Play internal, TestFlight and the Microsoft Store, with Android
codes from NBGV's own formula, `minor << 16 + height` (`0.7.10` was `458762`). Starting at `0.8` keeps every number
above those: Android `800000+`, Apple `0.8.Z` > `0.7.10`, Windows `0.8.Z.0` > `0.7.10.0`, whatever the git height.
`VERSION_CODE_OFFSET` isn't needed.

## Workflows

| Workflow | When | What |
|---|---|---|
| `ci.yml` | PRs, `main` | version sanity, unit tests, Release builds of desktop, WebAssembly, Android and Windows |
| `build-main.yml` | push to `main` | packages Android, iOS, Windows and WebAssembly as Dev; macOS too if `ENABLE_MACOS_ON_MAIN` is `true`. Publishes nothing |
| `release.yml` | push to `release/v*` | packages everything as Prod and publishes (below) |
| `prepare-release.yml` | manual | cuts `release/vX.Y` from `main` and moves `main` to the next minor |
| `forward-merge.yml` | push to `release/v*` | keeps one open `release/vX.Y -> main` PR |
| `store-ops.yml` | manual | halt or walk a rollout, cancel a review, republish an old run |
| `store-health.yml` | weekly | opens an issue when a store is stuck or a credential stopped working |
| `static-web-apps-deploy.yml` | PRs | WebAssembly preview sites |
| `_build-*.yml` | called | one per head; the only place a head is packaged |

`release.yml` does these on its own: Play **internal**, TestFlight (iOS), App Store Connect (macOS), a Microsoft
Store **draft** (uploaded, certification not started), the Azure Static Web Apps production site, the `vX.Y.Z` tag and
a draft GitHub Release with the `.msixbundle`.

These wait for an approval in the run's *Review deployments*:

- `play-production`: Play production at 10 %
- `appstore-production`: App Store and Mac App Store review submissions
- `ms-store-production`: commits the Microsoft Store submission at 10 %

A missing secret skips its store with a warning instead of failing. Set `ENABLE_ANDROID`, `ENABLE_IOS`,
`ENABLE_WINDOWS`, `ENABLE_MACOS` or `ENABLE_WASM` to `false` to skip a head.

## Cut a release

1. Make sure CI on `main` is green.
2. **Actions > Cut release branch > Run workflow**, with `dry_run` ticked the first time.
3. Run it for real. It creates `release/v0.8`, moves `main` to `0.9-dev` and starts `release.yml`.
4. Try the build from Play internal and TestFlight, then approve the gated jobs.
5. Publish the draft GitHub Release.

## Ship a patch

A patch is a commit on `release/vX.Y`: fix it on `main` and cherry-pick, or open a PR against the release branch. The
push runs `release.yml` again with a higher patch number. Then merge the forward-merge PR, keeping `main`'s
`version.json` in the conflict.

If a bad build is already rolling out, halt it first:

```bash
gh workflow run store-ops.yml -f action=play-halt
gh workflow run store-ops.yml -f action=appstore-pause
gh workflow run store-ops.yml -f action=msstore-rollout-halt
```

Halting stops new installs, it doesn't roll anyone back. The fix has to be a higher version.

## Gotchas

- **Never edit an API-created submission in Partner Center.** The API can't commit it again afterwards.
- **Finalize staged rollouts** (`play-finalize`, `msstore-rollout-finalize`), or the next release is blocked.
- **The App Store allows one version in review per platform.** The submit job refuses instead of cancelling one.
- **The App Store version must exist** in App Store Connect, with the build attached, before `appstore-production` can
  submit it. The script submits a version, it doesn't create one.
- **Web production only moves on releases now.** `main` no longer deploys the site.

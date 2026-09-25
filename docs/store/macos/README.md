# Mac App Store listing and package

Daily Plants is on the Mac App Store as the macOS platform of the same App Store Connect app as iOS (bundle ID `dev.mzikmund.dailyplants`, universal purchase). The Mac app is the Uno Platform desktop head, packaged as a sandboxed, universal (Apple silicon and Intel) app bundle.

- `listing/<locale>.md` has the Mac text for one App Store language. It only has the fields each platform keeps its own copy of: `PromotionalText`, `Description` and `Keywords` (and `ReleaseNotes` from the second version on). The name, subtitle, privacy URL, categories, age rating and App Privacy belong to the whole app, so the iOS listing sets them. The text follows the iOS listing, with clicks instead of taps.
- `images/` holds the screenshots, in order, at 2880x1800. Every language shows the English ones.
- `screenshots/` builds those images, see below.
- `build-listing.cs` checks the text and the screenshot sizes against the Mac App Store's limits and writes the folders fastlane uploads.
- `Deliverfile` has the options for `fastlane deliver`.
- `package-app.sh` builds the signed installer package.

## Packaging

`.github/workflows/package-macos.yml` runs `package-app.sh` on every push to `main` and uploads the package to App Store Connect, like the iOS workflow does with TestFlight. The script:

1. publishes the desktop head for `osx-arm64` and `osx-x64` as app bundles,
2. merges them into one universal bundle,
3. embeds the Mac App Store provisioning profile,
4. signs it with the entitlements in `src/DailyPlants/Platforms/Desktop/macOS/Entitlements.plist` plus the application and team identifiers from the profile,
5. wraps it in an installer package signed for the App Store: `artifacts/store/macos/DailyPlants.pkg`.

The workflow's secrets are `MACOS_DISTRIBUTION_P12_BASE64` (the "Apple Distribution" certificate), `MACOS_INSTALLER_P12_BASE64` (the "3rd Party Mac Developer Installer" certificate), `MACOS_P12_PASSWORD` for both, and `MACOS_PROVISIONING_PROFILE_BASE64` (the "Daily Plants Mac App Store Profile"), plus the `APPSTORE_*` API key secrets it shares with iOS. The profile and the certificates expire in September 2027.

To build and upload from a Mac, with both identities in a keychain:

```sh
CODESIGN_KEY="Apple Distribution: Martin Zikmund (X8D8TDGSDY)" \
INSTALLER_KEY="3rd Party Mac Developer Installer: Martin Zikmund (X8D8TDGSDY)" \
  docs/store/macos/package-app.sh <path to .provisionprofile>
xcrun altool --upload-app -t macos -f artifacts/store/macos/DailyPlants.pkg --apiKey <key id> --apiIssuer <issuer id>
```

`altool --validate-app` with the same arguments runs App Store Connect's checks without uploading. The version is the three-part Nerdbank.GitVersioning version, so each commit gets a new build number.

The app is sandboxed with network access for the NutritionFacts.org feeds and read/write access to the files you pick for export and import. Everything else, including the database and settings, lives in the app's container in `~/Library/Containers/dev.mzikmund.dailyplants`.

## Updating the listing

1. Edit `listing/en-US.md`, then update the other languages to match.
2. Run `dotnet run build-listing.cs`. It writes `artifacts/store/macos/metadata` and `artifacts/store/macos/screenshots`. Add `-- --check-only` to only check them.
3. Upload from this folder with an App Store Connect API key that has the App Manager role:

   ```sh
   fastlane deliver --api_key_path ~/.appstoreconnect/asc_key.json --app_version <version>
   ```

   `--app_version` is the macOS version you're about to submit. The other options are in `Deliverfile`: macOS only, no build, no submission, and the screenshots replace the ones in App Store Connect.
4. In App Store Connect, pick the build on the macOS version and submit it for review.

## Screenshots

Each screenshot is a slide: a real capture of the Mac app in its window, standing on the windowsill from the Windows and iOS slides, with the same headlines.

1. **Publish** the app bundle: `dotnet publish src/DailyPlants/DailyPlants.csproj -c Release -f net10.0-desktop -r osx-arm64 -p:TargetFrameworks=net10.0-desktop -p:SelfContained=true -p:PackageFormat=app`.
2. **Capture** the app with `screenshots/capture-app.sh`. The terminal needs the Accessibility and Screen Recording permissions. The script backs up your Daily Plants data, seeds demo data with `../windows/screenshots/seed-demo-data.cs`, writes the settings the captures need, drives the app with `mac.swift` and restores your data afterwards. For sharp 2x captures it switches the main display to a 3840x1080 HiDPI mode while it runs and back afterwards; on another display, change the mode in the script. Leave the mouse alone for the minute it runs. The captures land in `screenshots/captures/`.
3. **Render** the slides with `screenshots/render-slides.sh`, which writes `images/1.png` to `6.png` with headless Edge or Chrome.

Skia draws the app, so macOS sees no accessibility tree and the script clicks by position, in points from the window's top-left corner. If a screen's layout changes, check the clicks in `capture-app.sh` and the crops in `screenshots/slides.html`, which are in capture pixels. Open `slides.html?slide=hero` (or `details`, `statistics`, `achievements`, `resources`, `dark`) in a browser to work on one.

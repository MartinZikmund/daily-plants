# App Store listing

The source for the Daily Plants listing in App Store Connect (bundle ID `dev.mzikmund.dailyplants`), uploaded with [fastlane deliver](https://docs.fastlane.tools/actions/deliver/).

- `listing/<locale>.md` has the text for one App Store language, named by its App Store locale code. Each `##` heading is an App Store field. `Keywords` is a `- ` list. The text follows the Windows listing, apart from the fields only the App Store has (Subtitle, PromotionalText, Keywords) and the high contrast theme, which iOS doesn't have. Bulgarian and Persian have no App Store locale, so they're Windows only.
- `screenshots/` builds the screenshots, see below. They're rendered into `artifacts/store/ios/images/iphone/` and `ipad/` rather than committed, since they're build output and each re-render would add megabytes to the history. Every language shows the English ones.
- `build-listing.cs` checks the text against the App Store's limits and writes the folders fastlane uploads. The URLs, copyright and categories are set in it too.
- `Deliverfile` has the options for `fastlane deliver`.

## Updating the listing

1. Edit `listing/en-US.md`, then update the other languages to match.
2. Render the slides with `screenshots/render-slides.sh` (see [Screenshots](#screenshots); the captures are committed, so this is all it takes), then run `dotnet run build-listing.cs`. It writes `artifacts/store/ios/metadata` and `artifacts/store/ios/screenshots`. Add `-- --check-only` to only check the text, which doesn't need the slides.
3. Upload from this folder with an App Store Connect API key that has the App Manager role:

   ```sh
   fastlane deliver --api_key_path ~/.appstoreconnect/asc_key.json --app_version <version>
   ```

   `--app_version` is the version you're about to submit. fastlane creates it in App Store Connect if it isn't there yet. The key file is the JSON fastlane expects (`key_id`, `issuer_id` and `key` with the `.p8` contents). The other options are in `Deliverfile`: no build, no submission, and the screenshots replace the ones in App Store Connect.
4. In App Store Connect, pick the build and submit it for review.

Text fields are replaced as a whole, and the screenshots already in App Store Connect are deleted before these are added. App Review contact details, the age rating and App Privacy aren't in these files, so they stay as they are in App Store Connect.

To see what App Store Connect has now, download it into a folder of the same shape:

```sh
fastlane deliver download_metadata --api_key_path ~/.appstoreconnect/asc_key.json -a dev.mzikmund.dailyplants -m <dir>
fastlane deliver download_screenshots --api_key_path ~/.appstoreconnect/asc_key.json -a dev.mzikmund.dailyplants -w <dir>
```

## Screenshots

Each screenshot is a slide: a real capture of the app in a device frame from the Windowsill design, with a headline. There's an iPhone set (6.9", 1320x2868) and an iPad set (13", landscape, 2752x2064), and every language uses the English ones.

1. **Build** the simulator app: `dotnet build src/DailyPlants/DailyPlants.csproj -c Release -f net10.0-ios -r iossimulator-arm64`.
2. **Capture** the app with `screenshots/capture-app.sh` (or `capture-app.sh iphone` for one device). It needs Xcode's iPhone 17 Pro Max and iPad Pro 13-inch (M5) simulators and [Maestro](https://maestro.dev) with Java 17 (`brew install mobile-dev-inc/tap/maestro openjdk@17`). The script installs the app, seeds demo data with `../windows/screenshots/seed-demo-data.cs`, sets the settings the captures need, cleans up the status bar and drives the app with `iphone.yaml`, `ipad.yaml` and `dark.yaml`. The captures land in `screenshots/captures/`, and with [oxipng](https://github.com/oxipng/oxipng) installed (`brew install oxipng`) the script shrinks them losslessly before you commit them. It replaces the app's data in those simulators.
3. **Render** the slides with `screenshots/render-slides.sh`, which writes `artifacts/store/ios/images/iphone/1.png` to `6.png` and the same for `ipad/` with headless Edge or Chrome.

The README's iPhone screenshots are small copies in `docs/images/`. After a recapture, refresh them from the rendered slides: `sips -s format jpeg -s formatOptions 82 -Z 900 artifacts/store/ios/images/iphone/<N>.png --out docs/images/iphone-<N>.jpg`.

Skia draws the app, so iOS sees no accessibility tree and the flows tap by position, in points. If a screen's layout changes, check the taps in the flows and the crops in `screenshots/slides.html`, which are in capture pixels. Open `slides.html?device=iphone&slide=hero` (or `ipad`, and `details`, `statistics`, `achievements`, `resources`, `dark`) in a browser to work on one. The headlines match the Microsoft Store slides.

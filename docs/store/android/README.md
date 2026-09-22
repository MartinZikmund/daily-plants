# Google Play listing

The source for the Daily Plants listing in Google Play Console (package `dev.mzikmund.dailyplants`), uploaded with [fastlane supply](https://docs.fastlane.tools/actions/supply/).

- `listing/<locale>.md` has the text for one Play language, named by its Play locale code (Hebrew is `iw-IL`). Each `##` heading is a Play field: `Title`, `ShortDescription`, `FullDescription` and `ReleaseNotes`. The text follows the App Store listing and its translations, with Bulgarian and Persian from the Microsoft Store, since Play has both.
- `images/phone/` and `images/tablet/` hold the screenshots, in order. The tablet set goes to both the 7" and 10" slots. `images/feature-graphic.png` is the banner above the listing and `images/icon.png` the store icon. Every language shows the English ones.
- `screenshots/` builds those images, see below.
- `build-listing.cs` checks the text and images against Play's limits and writes the folder fastlane uploads, plus the release notes CI sends with each build.
- `Supplyfile` has the options for `fastlane supply`.

## Updating the listing

1. Edit `listing/en-US.md`, then update the other languages to match.
2. Run `dotnet run build-listing.cs`. It writes `artifacts/store/android/metadata` and `artifacts/store/android/whatsnew`. Add `-- --check-only` to only check.
3. Upload from this folder with a key for a service account that can edit the store listing in Play Console (Users and permissions):

   ```sh
   fastlane supply --json_key ~/.googleplay/dailyplants.json --validate_only true
   fastlane supply --json_key ~/.googleplay/dailyplants.json
   ```

   The first command only asks Play to check the changes. The key is the service account's JSON key from Google Cloud (IAM > Service accounts > Keys). The other options are in `Supplyfile`: no build, no release notes.

The text fields and the English images are replaced as a whole. Languages that have no file here keep their text in Play, and the other languages only fall back to the English images if they have none of their own, so remove any old translated screenshots in Play Console.

To see what Play has now, download it into a folder of the same shape:

```sh
fastlane supply init --json_key ~/.googleplay/dailyplants.json --package_name dev.mzikmund.dailyplants --metadata_path <dir>
```

## Release notes

Play keeps "What's new" with each release, not with the listing, so `ReleaseNotes` doesn't go up with the listing. `package-android.yml` runs `build-listing.cs` and uploads `artifacts/store/android/whatsnew` with every build it sends to the internal track, and promoting that release carries the notes along. Play allows 500 characters per language and may refuse notes in a language the listing doesn't have yet, so upload the listing before a build that adds a language.

## Screenshots

Each screenshot is a slide: a real capture of the app in a device frame from the Windowsill design, with a headline. There's a phone set (9:16, 1440x2560) and a tablet set (16:9, 2560x1440), and every language uses the English ones.

1. **Build** the APK: `dotnet build src/DailyPlants/DailyPlants.csproj -c Release -f net10.0-android -r android-x64 -p:AndroidPackageFormat=apk`.
2. **Capture** the app with `screenshots/Capture-App.ps1` (or `-Device phone` for one). It needs the Android SDK with `system-images;android-37.0;google_apis;x86_64`, whose emulators allow `adb root`, which seeding the app's data needs. It creates the `dailyplants-phone` (Pixel 9 Pro XL) and `dailyplants-tablet` (Pixel Tablet) emulators if they're missing, reinstalls the app, seeds demo data with `../windows/screenshots/seed-demo-data.cs`, sets the settings the captures need, cleans up the status bar and drives the app with adb. The captures land in `screenshots/captures/`. It only talks to those emulators, so a phone connected for debugging is left alone.
3. **Render** the slides with `screenshots/Render-Slides.ps1`, which writes `images/phone/1.png` to `6.png`, the same for `images/tablet/`, the feature graphic and the icon with headless Edge.

Uno's Android accessibility tree only has the first item of each list and nothing inside dialogs, so the flows tap by name where they can and by position, in dp, everywhere else. If a screen's layout changes, check the taps in `Capture-App.ps1` (`-PrepareOnly` seeds the emulator and leaves the app open for that) and the crops in `screenshots/slides.html`, which are in capture pixels. Open `slides.html?device=phone&slide=hero` (or `tablet`, and `details`, `statistics`, `achievements`, `resources`, `dark`, or `?slide=feature` and `?slide=icon`) in a browser to work on one. The headlines match the Microsoft Store and App Store slides.

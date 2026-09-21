# Microsoft Store listing

The source for the Daily Plants listing in Partner Center (product `9NKK3K501RZG`).

- `listing/<lang>.md` has the text for one listing language. Each `##` heading is a Partner Center field. `Features`, `SearchTerms` and `ScreenshotCaptions` are `- ` lists.
- `images/<Field>.png` sets that image in every language, for example `images/StoreLogo300x300.png` or `images/DesktopScreenshot1.png`. Put a file in `images/<lang>/` to replace it for one language only.
- `screenshots/` builds the `DesktopScreenshot` images, see below.
- `Build-StoreListing.ps1` checks the text against the Store's limits and merges everything into a Partner Center export.

## Updating the listing

1. Edit `listing/en.md`, then update the other languages to match.
2. In Partner Center, open the app overview and select **Export listings**.
3. Run `./Build-StoreListing.ps1 -ExportPath <exported csv>`. It writes `artifacts/store/windows/store-listing`.
4. Select **Import listings > Import folder** and choose that folder.

Anything these files don't cover, such as trailers or hardware requirements, keeps the value from the export. Screenshots after the last `images/DesktopScreenshot<N>.png` are removed. Run the script without `-ExportPath` to only check the text.

## Screenshots

Each screenshot is a slide: a real capture of the app in a frame from the Windowsill design, with a headline. Every language uses the English slides, and only `ScreenshotCaptions` is translated.

1. **Capture** the app with `screenshots/Capture-App.ps1`. Deploy the Windows head first (F5 once), keep the app in English, set the display scale to 150% or more, and close the app. The script backs up your database and settings, seeds demo data with `seed-demo-data.cs`, drives the app through UI Automation and restores your data afterwards. It clicks one diary row with the mouse, so leave the mouse alone while it runs. The captures land in `screenshots/captures/`.
2. **Render** the slides with `screenshots/Render-Slides.ps1`, which writes `images/DesktopScreenshot1.png` to `6.png` with headless Edge.

The headlines, layouts and crops live in `screenshots/slides.html`. Open it in a browser with `?slide=hero` (or `details`, `statistics`, `achievements`, `resources`, `dark`) to work on one. The crops are in capture pixels, so check them after a recapture if a screen's layout changed. Keep the headlines in step with `ScreenshotCaptions`.

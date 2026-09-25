# Microsoft Store listing

The source for the Daily Plants listing in Partner Center (product `9NKK3K501RZG`).

- `listing/<lang>.md` has the text for one listing language. Each `##` heading is a Partner Center field. `Features`, `SearchTerms` and `ScreenshotCaptions` are `- ` lists.
- `images/<Field>.png` sets that image in every language, for example `images/StoreLogo300x300.png`. Put a file in `images/<lang>/` to replace it for one language only, including a screenshot.
- `screenshots/` builds the `DesktopScreenshot` images, see below. They're rendered into `artifacts/store/windows/images/` rather than committed, since they're build output and each re-render would add megabytes to the history.
- `Build-StoreListing.ps1` checks the text against the Store's limits and builds a folder for Partner Center's folder upload.

## Updating the listing

1. Edit `listing/en.md`, then update the other languages to match.
2. Render the slides with `screenshots/Render-Slides.ps1` (the captures are committed, so this is all it takes), then run `./Build-StoreListing.ps1`. It writes `artifacts/store/windows/store-listing` with `listing.csv` and the images it references.
3. In Partner Center, open the app overview, select **Import listings > Upload folder** and pick that `store-listing` folder.

The CSV only has rows for what these files cover, so anything else, such as trailers or hardware requirements, stays as it is in Partner Center. Text fields are replaced as a whole, so a feature or search term removed here is removed there too. Images only get added or replaced: an import can't remove one, so if you drop a screenshot, delete it in Partner Center as well. Run the script with `-CheckOnly` to only check the text, which doesn't need the slides.

## Screenshots

Each screenshot is a slide: a real capture of the app in a frame from the Windowsill design, with a headline. Every language uses the English slides, and only `ScreenshotCaptions` is translated.

1. **Capture** the app with `screenshots/Capture-App.ps1`. Deploy the Windows head first (F5 once), keep the app in English, set the display scale to 150% or more, and close the app. The script backs up your database and settings, seeds demo data with `seed-demo-data.cs`, drives the app through UI Automation and restores your data afterwards. It clicks one diary row with the mouse, so leave the mouse alone while it runs. The captures land in `screenshots/captures/`, and with [oxipng](https://github.com/oxipng/oxipng) on the path the script shrinks them losslessly before you commit them.
2. **Render** the slides with `screenshots/Render-Slides.ps1`, which writes `artifacts/store/windows/images/DesktopScreenshot1.png` to `6.png` with headless Edge.

The README's screenshots are small copies in `docs/images/` (`windows-<N>.jpg`, 1280 px wide). Refresh them from the rendered slides after a recapture.

The headlines, layouts and crops live in `screenshots/slides.html`. Open it in a browser with `?slide=hero` (or `details`, `statistics`, `achievements`, `resources`, `dark`) to work on one. The crops are in capture pixels, so check them after a recapture if a screen's layout changed. Keep the headlines in step with `ScreenshotCaptions`.

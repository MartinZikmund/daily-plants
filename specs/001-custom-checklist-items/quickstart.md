# Quickstart: Manual Verification — Custom User-Defined Checklist Items

**Branch**: `001-custom-checklist-items`
**Date**: 2026-04-27

This walkthrough lets a developer or QA reviewer manually verify the feature against every acceptance scenario, success criterion, and edge case. Run these on the **Windows (WinAppSDK) head** for the fastest feedback loop; spot-check the same flows on Android, iOS, WASM, and Desktop heads before release.

---

## Setup

1. Check out branch `001-custom-checklist-items`.
2. Build & run the Windows head from VS or `dotnet run`. The DB lives at `%LocalAppData%/DailyPlants/dailyplants.db` — delete it first to verify a clean install path, OR keep an existing DB to verify the `user_version 2 → 3` migration path. **Run both at least once.**
3. Confirm the app launches without errors. (DEBUG self-check `VerifyAchievementIsolationDebug` runs at startup — failures appear in Output / Debug console.)

---

## Story 1 (P1) — Add and track a personal daily item

**Maps to**: Spec User Story 1; FR-001 / FR-002 / FR-006 / FR-007 / FR-008 / FR-009; SC-001 / SC-002

1. Settings → expand "Custom Items" → **Add**.
2. Fill the dialog: Name = `Take vitamin D`, Description = `1000 IU after breakfast`, Servings = `1`, open **Icon picker**, select `pill`. Save.
3. ⏱ Time the operation from opening Settings to Save. **Expect ≤ 30 s (SC-001).**
4. Navigate to **Diary** for today.
   - **Expect**: a "Custom" section header below Tweaks. The item shows with the `pill` icon, name "Take vitamin D", and `0/1` badge.
4a. Tap the row's info button. **Expect**: flyout shows the description text "1000 IU after breakfast". Add a second item with no description; its info button is hidden (or its flyout has no description block). **Acceptance Scenarios 1 + 1a ✅**
5. Tap **+** once. Badge updates to `1/1`, color matches completion theme.
6. Navigate to yesterday, then back to today. Badge still shows `1/1`. **Acceptance Scenario 1 ✅**
7. Open the icon picker again on a new item — verify all 16 + default catalog glyphs render and one-tap selects. **Acceptance Scenario 2 ✅**
7a. **Emoji source**: open the icon picker, type `🌱` into the emoji input field. Form preview shows 🌱. Save. Diary row and Settings list both render 🌱. **Acceptance Scenario 2a ✅**
7b. Edit the same item, paste `🍎🥗` into the emoji field, save. Verify only 🍎 is retained. **Acceptance Scenario 2b ✅**
7c. Edit again and switch back to a catalog glyph. Save. Verify the emoji renderer is replaced by the catalog `BitmapIcon`. (Story 2 acceptance scenario about switching between sources.)
8. Add a second custom item but skip the icon entirely. Save. **Expect**: item renders with the `default` catalog icon. **Acceptance Scenario 4 ✅**
9. **Achievement isolation check (SC-002)**:
   - Ensure Daily Dozen and Tweaks are enabled but no items completed today.
   - Mark the custom item complete (`1/1`).
   - Open **Achievements** view. Confirm:
     - "Perfect day" count for today: **0**.
     - Streak counter: unchanged.
     - No achievement toast fires for the custom completion.
   - **Acceptance Scenario 5 ✅**

---

## Story 2 (P2) — Manage existing custom items

**Maps to**: Spec User Story 2; FR-003 / FR-004 / FR-005 / FR-010 / FR-011 / FR-012; SC-003

10. With the existing "Take vitamin D" item (ID-tagged in the DB), record servings on three different days.
11. **Rename**: Settings → Custom Items → edit the item → change name to `30 min walk` → Save.
    - Diary on each of those three days still shows servings tied to the same item; entry count unchanged.
    - **Acceptance Scenario 1 ✅**, **SC-003 ✅** (verify via DB if needed: `SELECT COUNT(*) FROM CustomItemEntries WHERE CustomItemId = ?` — same before/after).
12. **Change icon**: edit the item → open icon picker → pick a different glyph → Save. Diary now renders the new icon. Settings list shows the new icon. Historical entries on past days still show the new icon (because they read live from the item record). **Acceptance Scenario 2 ✅**
13. **Reorder**: add a second item with default sort order. Edit it, set `SortOrder = 50`. Verify it now appears **above** the first item in the diary's Custom section. Restart app → order persists. **Acceptance Scenario 3 ✅**
14. **Delete with history prompt**:
    - On an item with several entries, click Delete. **Expect**: dialog with three buttons — Keep history / Delete everything / Cancel.
    - Choose **Keep history**. Item disappears from active list. Diary no longer shows it for today. SQLite check: `SELECT COUNT(*) FROM CustomItemEntries WHERE CustomItemId = ?` returns the old count (orphan rows preserved).
    - On another item, choose **Delete everything**. Both `CustomItems` and `CustomItemEntries` rows for that ID are gone. **Acceptance Scenario 4 ✅**
15. **Length limit (name)**: try to enter a 61-char name. Save is blocked or input truncated to 60 with a visible warning. **Acceptance Scenario 5 ✅**
15a. **Length limit (description)**: paste a 600-character description. Save is blocked or input truncated to 500 with a visible character counter / warning. Empty description is always valid.
16. **Duplicate name**: create a third item named identically to an existing item. Save shows a non-blocking warning banner. Save proceeds. **Acceptance Scenario 6 ✅**

---

## Story 3 (P3) — Export and re-import

**Maps to**: Spec User Story 3; FR-013 / FR-014; SC-004

17. Export → choose JSON. Open the file in a text editor.
    - Confirm `"version": "1.1"`.
    - Confirm `customItems` array contains every active custom item (NOT the cascade-deleted one from step 14, but DOES contain the keep-history-deleted item's orphan entries — wait, the keep-history flow removed the `CustomItems` row, so its definition is NOT in `customItems`; only its orphan rows are in `customItemEntries`. Confirm both behaviors.)
    - Confirm `customItemEntries` array contains all entry rows including orphans.
    - **Acceptance Scenario 1 ✅**
18. Quit the app. Delete `%LocalAppData%/DailyPlants/dailyplants.db`. Relaunch (clean state). Settings → Import → select the JSON.
    - All custom items recreated with original names, icons, recommended servings, sort orders.
    - All historical entries restored.
    - Orphan entries (whose definitions were keep-history-deleted) are absent from the diary but present in the DB; a re-export reproduces them.
    - **Acceptance Scenario 2 ✅, SC-004 ✅**
19. Re-import the same file on top of the now-populated DB. **Expect**: no duplicates created, counts unchanged (upsert by Id). **Acceptance Scenario 3 ✅**

---

## Edge cases

- **Empty state (FR-007)**: with all custom items deleted, diary's Custom section header is hidden. Re-add one — header reappears.
- **RTL rendering**: switch app language to Hebrew (`he`) or Persian (`fa`). Open diary. Custom item names entered in Latin script render LTR; section header and dialog labels render RTL. No truncation or overlap.
- **Emoji cross-platform**: create one item with emoji `👨‍👩‍👧` (ZWJ family). Verify it renders as a single combined glyph on Windows / Android / iOS / macOS. Export, then import on a different platform — the emoji round-trips byte-identical. Visual rendering may differ between OSes (Apple Color Emoji vs Segoe UI Emoji vs Noto) and that is expected.
- **Emoji rejection**: open the editor, type a plain word like `hello` in the emoji field, save. The item should fall back to the default catalog icon (or whichever catalog glyph the user has currently selected on the catalog tab) rather than rendering literal text.
- **User-entered names not translated**: switch through 3 different languages. Name `Take vitamin D` always shows verbatim regardless of locale; surrounding chrome (`Add`, `Save`, `Custom`, `Recommended servings`, etc.) translates correctly. **SC-005 ✅**
- **Daily Dozen and Tweaks unchanged (SC-006)**: side-by-side compare diary screenshots from `main` and from this branch with no custom items configured. Layout, colors, and behavior of DD / Tweaks rows: byte-identical to a casual eye.
- **Migration path**: launch the app with a v2 DB (no custom-item tables). Confirm `user_version` advances to 3 and the two new tables exist (`SELECT name FROM sqlite_master WHERE type='table' AND name LIKE 'Custom%'`).

---

## Cross-platform spot-check

After Windows passes, run the following on each remaining head:

| Platform | Critical checks |
|---|---|
| Android | Add item, mark serving, restart app, history persists. Icon picker fits portrait viewport. |
| iOS | Same as Android + RTL flip works. |
| WASM | Add item, mark serving, refresh page, history persists (IndexedDB-backed SQLite). |
| Desktop (Skia) | Same as Windows; verify file-picker dialogs for export/import work in Skia host. |

---

## Done criteria

All acceptance scenarios checked. All success criteria verified. No regressions in existing Daily Dozen / Tweaks UI or achievement triggers.

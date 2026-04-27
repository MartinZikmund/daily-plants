# Feature Specification: Custom User-Defined Checklist Items

**Feature Branch**: `001-custom-checklist-items`
**Created**: 2026-04-27
**Status**: Draft
**Input**: GitHub Issue [#46](https://github.com/MartinZikmund/daily-plants/issues/46) — Allow users to define their own daily-tracked items (e.g., "Take vitamin D", "30 min walk") that appear in a separate Custom section of the diary, without affecting the official Daily Dozen / Tweaks completion logic or achievements.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Add and track a personal daily item (Priority: P1)

A user wants to track a habit beyond the official Daily Dozen and Tweaks (e.g., "Take vitamin D", "30 min walk", "Drink 2L water"). They open Settings, add a custom item with a name, a target number of servings, and pick an icon. The item then appears in a dedicated "Custom" section of the diary, where they can mark servings each day just like the Daily Dozen.

**Why this priority**: This is the core value of the feature — the entire issue exists to let users append their own tracked items. Without this, no other story is meaningful. It is the MVP slice: one user, one custom item, one day of tracking.

**Independent Test**: Add one custom item via Settings, navigate to the diary for today, mark its servings, navigate to the diary for yesterday and back to today — the marked servings persist. The Daily Dozen and Tweaks sections are unchanged in look and behavior.

**Acceptance Scenarios**:

1. **Given** the user has never created a custom item, **When** they open Settings and add an item named "Take vitamin D" with 1 recommended serving, an optional description "1000 IU after breakfast", and a chosen icon, **Then** the item is saved and the diary shows a new "Custom" section containing that item — rendered with the chosen icon — and 0 of 1 servings marked for today; tapping the row's info button shows the description text.
1a. **Given** the user creates a custom item without entering a description, **When** they save, **Then** the item is created successfully and the diary row's info button is hidden (or its flyout simply has no description block) — no empty placeholder text is shown.
2. **Given** the user is creating a new custom item, **When** they open the icon picker, **Then** they see two sources side-by-side: the full built-in catalog (10–20 glyphs) and an emoji input field. Selecting a catalog glyph or entering an emoji updates the form preview before save.
2a. **Given** the user is creating a custom item, **When** they enter the emoji 🌱 in the emoji input, **Then** the form preview shows 🌱 and saving the item makes 🌱 the icon shown in Settings and on the diary row.
2b. **Given** the user pastes a multi-character string `🍎🥗` into the emoji input, **When** they save, **Then** only the first emoji grapheme (🍎) is retained and rendered.
3. **Given** a custom item exists, **When** the user marks one serving of it on the diary, **Then** the serving is persisted for that date and survives navigating away and reopening the app.
4. **Given** a user adds a custom item without selecting an icon, **When** the item is saved, **Then** a default icon is used so the item still renders normally.
5. **Given** a user is viewing the diary on a day with the custom item fully completed but no Daily Dozen or Tweaks items completed, **When** they look at any "perfect day" indicator, streak counter, or achievement trigger, **Then** the day is NOT counted as a perfect day and no achievement is awarded based on the custom item.

---

### User Story 2 - Manage existing custom items (Priority: P2)

After creating one or more custom items, the user needs to refine them: rename, change the recommended servings, change the icon, reorder, or delete an item. They expect renames to preserve their tracked history (no data loss) and to be warned before destroying history.

**Why this priority**: Once users start creating custom items, they will inevitably want to edit and tidy them. Without management, the feature feels half-finished and creates frustration. It depends on Story 1 but is independently testable.

**Independent Test**: Given an existing custom item with a few days of history, rename it, change its recommended servings count, change its icon, move it above another item via sort order, then delete it and verify the user is prompted about history.

**Acceptance Scenarios**:

1. **Given** a custom item with several days of recorded servings, **When** the user renames it from "Walk" to "30 min walk" and edits its description, **Then** both the name and description update everywhere they are surfaced and prior days' servings remain attached to the same item.
2. **Given** an existing custom item with an icon, **When** the user opens the icon picker and either selects a different catalog glyph or enters a different emoji, **Then** the new icon is shown in both Settings and the diary on next render, and historical entries remain linked to the same item. Switching between a catalog glyph and an emoji and back is supported in either direction.
3. **Given** two custom items with sort orders 10 and 20, **When** the user changes the second item's sort order to 5, **Then** the second item now appears above the first in the diary's Custom section and the order persists across app restarts.
4. **Given** a custom item with recorded history, **When** the user attempts to delete it, **Then** they are presented with a choice to keep history (item removed from active list, prior entries retained) or fully remove the item and all its history.
5. **Given** the user enters a name longer than 60 characters, **When** they attempt to save, **Then** the save is blocked or the input is truncated to 60 characters with clear feedback.
6. **Given** the user enters a name that already exists on another custom item, **When** they attempt to save, **Then** they see a non-blocking warning that the name is duplicate but are allowed to proceed.

---

### User Story 3 - Export and re-import including custom items (Priority: P3)

A user who exports their data (e.g., to back up or move to a new device) expects their custom item definitions and the daily history of those items to be included. On import, the custom items are recreated and their history is restored.

**Why this priority**: Data portability is important for trust but not required to validate the feature itself. It is built on top of Stories 1 and 2 but tests an independent capability.

**Independent Test**: Create custom items, record several days of history, export, clear app data (or import on a clean device), import the export file, and verify all custom items and history are present and unchanged.

**Acceptance Scenarios**:

1. **Given** the user has custom items with multiple days of history, **When** they export their data, **Then** the export contains both the item definitions and the daily entries for those items.
2. **Given** an export file containing custom items and their history, **When** the user imports it on a device that has no custom items, **Then** the items are recreated with their original names, icons, recommended servings, and sort order, and historical entries are restored.
3. **Given** an export file containing custom items, **When** the user imports it on a device that already has the same items (matched by stable identifier), **Then** the import does not create duplicates and history is merged or restored without loss.

---

### Edge Cases

- **Empty state**: When no custom items exist, the Custom section is hidden from the diary so it does not add visual noise.
- **Renaming an item**: Item identity is preserved by a stable identifier, not the name; renaming never orphans existing history entries.
- **Deleting an item with history**: User is prompted with a clear binary choice — keep history (item removed from active tracking, entries retained for past-day views) or cascade delete everything.
- **Achievement isolation**: A day on which only custom items are completed (and no Daily Dozen / Tweaks) is NOT a perfect day. Streaks and milestones are unaffected by custom-item completion.
- **Long names**: Names longer than 60 characters are rejected or truncated with user feedback.
- **Long descriptions**: Descriptions longer than 500 characters are rejected or truncated with user feedback. Empty descriptions are treated as "no description" and never block save.
- **Duplicate names**: Permitted; a warning is shown at save time but does not block.
- **Right-to-left languages**: Custom item names entered in RTL scripts render correctly in both Settings and the diary.
- **Emoji rendering across platforms**: emoji icons render using the host OS's emoji font; appearance may differ by platform (Apple, Segoe UI Emoji, Noto Emoji) but the underlying value round-trips identically through export / import.
- **Emoji input edge inputs**: empty emoji field, whitespace, plain text characters, and combining-character-only input are rejected (form preview shows the default catalog icon and the emoji-source button is treated as not selected).
- **User-entered text not translated**: User-entered names appear verbatim regardless of the app's selected language; only the surrounding UI chrome is localized.
- **Reorder consistency**: Sort order is persisted per item so the order is stable across sessions and (after import) across devices.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Users MUST be able to create a custom daily-tracked item by providing a name and a recommended number of daily servings.
- **FR-001a**: Users MUST be able to optionally attach a free-form description to a custom item (e.g., notes on what counts as a serving, motivation, source link). When the description is empty, no description UI is shown for that item; when present, the description is shown in the item-detail flyout opened from the diary row's info button. The description MUST be limited to 500 characters and is NOT translated.
- **FR-002**: Users MUST be able to choose an icon for each custom item via an icon picker that exposes two sources: (a) a built-in catalog of 10–20 curated glyphs, and (b) a free-form emoji input where the user can type or paste a single emoji character. The icon picker is available at creation time and at any later edit. The chosen icon MUST be displayed alongside the item everywhere it appears (Settings list, diary Custom section, edit form preview). If the user does not pick one, a default catalog icon is applied automatically.
- **FR-002a**: When the user supplies an emoji, the system MUST accept any single Unicode emoji character or grapheme cluster (including multi-codepoint sequences such as ZWJ-joined family / flag / skin-tone emojis). If the user enters more than one emoji, only the first emoji grapheme is retained.
- **FR-003**: Users MUST be able to edit any field of an existing custom item (name, recommended servings, icon, sort order).
- **FR-004**: Users MUST be able to delete a custom item; if the item has recorded history, the system MUST prompt the user to choose between keeping the history (orphan retention) or fully removing item plus all history.
- **FR-005**: Users MUST be able to assign each custom item an explicit numeric sort order that controls its position in the diary's Custom section.
- **FR-006**: The diary MUST display custom items in a dedicated "Custom" section, visually distinct from and below the Daily Dozen and Tweaks sections.
- **FR-007**: The Custom section MUST be hidden from the diary when no custom items exist.
- **FR-008**: Daily tracking for custom items MUST behave identically to Daily Dozen items: the user marks servings per day, and entries persist per date and survive app restarts.
- **FR-009**: Custom items MUST NOT contribute to the "perfect day" calculation, streaks, achievements, or any milestone logic. Completing only custom items on a given day MUST NOT count toward any achievement.
- **FR-010**: The system MUST limit custom item names to 60 characters; longer input is rejected or truncated with clear feedback.
- **FR-011**: The system MUST allow duplicate custom item names but MUST warn the user (non-blocking) when a duplicate is detected at save time.
- **FR-012**: The system MUST identify each custom item by a stable identifier so that renaming preserves all historical entries linked to that item.
- **FR-013**: Export MUST include both custom item definitions and the full daily-entry history of those items.
- **FR-014**: Import MUST restore custom item definitions and entry history, recreating items by their stable identifier on a target device that lacks them, and avoiding duplicates on a device that already has them.
- **FR-015**: All UI labels introduced by this feature (Settings screens, diary section header, prompts, warnings) MUST be localized in all 21 languages currently supported by the app. User-entered custom item names MUST NOT be translated.
- **FR-016**: All custom-item functionality MUST work fully offline; no internet connectivity is required to add, edit, delete, track, export, or import custom items.

### Key Entities

- **Custom Item**: A user-defined tracked item. Attributes: stable identifier, display name (user-entered, ≤60 chars, not translated), optional description (user-entered free-form text, ≤500 chars, not translated, surfaced in the diary item-detail flyout when non-empty), recommended daily servings (positive integer), icon source (either a key from the built-in catalog or a single user-entered emoji grapheme, with a default catalog fallback), explicit sort order (numeric, used for diary ordering).
- **Custom Item Entry**: A record of progress on a Custom Item for a single calendar day. Attributes: date, reference to the Custom Item by stable identifier, servings completed.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can create their first custom item — including name, servings, and icon — in under 30 seconds from opening Settings.
- **SC-002**: 100% of "perfect day" evaluations and achievement triggers ignore custom items: in a verification run, a day on which only custom items are completed produces zero perfect-day flags and zero achievement awards.
- **SC-003**: Renaming a custom item with prior history preserves 100% of its historical entries (verified by entry count before/after rename).
- **SC-004**: Export-then-import on a clean device restores 100% of custom item definitions and 100% of their historical entries (verified by record-count and field-level comparison).
- **SC-005**: All UI strings introduced for this feature appear translated in each of the 21 supported languages — no fallback English strings on non-English language settings.
- **SC-006**: The diary's Daily Dozen and Tweaks sections render and behave identically before and after the feature is enabled (no visual regression, no change in completion logic for existing items).

## Assumptions

- Existing per-day persistence patterns used for the Daily Dozen will be reused for Custom Item Entries; no new storage paradigm is introduced at the user-facing level.
- The icon set ships with the app as a fixed curated collection of 10–20 glyphs; users cannot supply their own images in this version.
- "21 languages" refers to the same set of languages already supported by the rest of the app at the time of implementation.
- The achievement engine has a single, well-defined entry point that can be guarded so custom items are excluded from all achievement logic without scattered conditional checks.
- Custom items are personal to the device until the user explicitly exports them; there is no cloud sync requirement in this feature.
- Recommended daily servings is a positive integer (default 1); fractional servings are out of scope for this feature.
- The feature targets the existing supported platforms of the app — no new platforms are added by this feature.

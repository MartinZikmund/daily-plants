# Phase 0 Research: Custom User-Defined Checklist Items

**Branch**: `001-custom-checklist-items`
**Date**: 2026-04-27

The Technical Context in `plan.md` had no `NEEDS CLARIFICATION` markers; the spec resolved all ambiguity in writing. This document captures the design decisions made up-front so downstream phases (`/speckit-tasks`, `/speckit-implement`) inherit a stable rationale.

---

## Decision: Storage — parallel `CustomItems` / `CustomItemEntries` tables (not reuse `DailyEntries`)

- **Rationale**: The single hardest spec invariant is FR-009 (custom items MUST NOT contribute to "perfect day" or any achievement). The existing achievement engine — `SqliteDataService.GetCurrentStreakAsync`, `GetLongestStreakAsync`, `GetPerfectDaysCountAsync`, `GetItemCompletionCountAsync` — derives completion from `DailyEntries` filtered by `ChecklistDefinitions.GetEnabledItems(...)`. Putting custom rows in `DailyEntries` would force a foreign-key-style filter on every achievement query and on every future achievement type. Keeping custom entries in a separate table makes the isolation a structural property of the schema, not a discipline that future code must remember.
- **Alternatives considered**:
  - *Reuse `DailyEntries` with a nullable `CustomItemId` column*: rejected — every achievement query would need `WHERE ItemId IN (SELECT Id FROM ChecklistDefinitions...)` or equivalent, easy to forget when adding new achievements.
  - *Single polymorphic table with a `Source` discriminator*: same problem as above plus harder schema reads.
- **Consequence**: `IDataService` gains a parallel set of methods (`GetCustomItemsAsync`, `GetCustomItemEntryAsync`, `GetCustomItemEntriesForDateAsync`, `SaveCustomItemAsync`, `SaveCustomItemEntryAsync`, etc.). `AchievementService` and the existing statistics methods remain unchanged.

---

## Decision: Stable item identity — `string` GUID PK on `CustomItems`

- **Rationale**: FR-012 requires renames to preserve history. A separate stable identifier (independent of name) is needed. GUID strings match the existing `ItemId` shape used elsewhere (compare `DailyEntry.ItemId : string`). The string form imports/exports cleanly across devices and is human-distinguishable in logs.
- **Alternatives considered**:
  - *Auto-increment `int Id`*: rejected — incompatible with FR-014 (cross-device import without ID collisions).
  - *Slugified name as ID*: rejected — would break on rename.
- **Generation**: `Guid.NewGuid().ToString("N")` (32-char hex, no braces/dashes) at creation time, never regenerated.

---

## Decision: Schema migration — add tables in migration v3 (no destructive changes)

- **Rationale**: `SqliteDataService` already uses `PRAGMA user_version` migrations. Existing migrations cover up to v2 (Anti-Aging Eight removal). New custom-item tables go in v3.
- **Implementation pattern** (matches existing code):
  ```csharp
  if (version < 3)
  {
      await _connection.CreateTableAsync<CustomItemEntity>();
      await _connection.CreateTableAsync<CustomItemEntryEntity>();
      await _connection.ExecuteAsync(
          "CREATE UNIQUE INDEX IF NOT EXISTS idx_custom_entries_date_item ON CustomItemEntries (Date, CustomItemId)");
      await _connection.ExecuteAsync("PRAGMA user_version = 3");
  }
  ```
- **Note**: `CreateTableAsync<T>` is idempotent in sqlite-net, so calling it for new entities is safe even if v3 has already run.

---

## Decision: Icon — discriminated source (built-in catalog OR user emoji), with default catalog fallback

Icons have **two sources**, captured by an `IconType` enum + `IconValue` string:

| `IconType` | `IconValue` semantics | Renderer |
|---|---|---|
| `Catalog`  | catalog key (e.g., `"pill"`); falls back to `"default"` if unknown | `BitmapIcon` with `UriSource = ms-appx:///Assets/Icons/CustomItems/{key}.png` |
| `Emoji`    | exactly one emoji grapheme cluster (e.g., `"🌱"`, `"👨‍👩‍👧"`) | `TextBlock` rendering the emoji at icon-cell font size; OS emoji font does the work |

### Built-in catalog

- Same 16 PNGs + `default.png` as before. Spec ("small built-in set (10–20)") satisfied.
- Initial set: `pill`, `walk`, `run`, `water`, `sleep`, `meditate`, `yoga`, `book`, `journal`, `sun`, `bike`, `dumbbell`, `apple`, `tea`, `heart`, `star`, `default`.
- `CustomIconCatalog.AllKeys`, `.IsKnown(key)`, `.GetIconPath(key) → string` (with default fallback).

### Emoji input

- The picker dialog has two visual sources side-by-side: the catalog grid AND a single-line text input labeled "Or type an emoji". Either one selected updates the form's preview.
- **Validation**: the input accepts arbitrary text but at save time only the **first emoji grapheme cluster** is retained. Implementation: enumerate `System.Globalization.StringInfo.GetTextElementEnumerator(value)`, take the first element, then validate that element contains at least one code point in an emoji-related Unicode range (`\p{Extended_Pictographic}` or in the Emoji presentation set). If validation fails, the emoji source is treated as not selected and the catalog selection (or `"default"`) is used.
- **Storage**: `IconType = Emoji`, `IconValue = "<first emoji grapheme>"`. The grapheme cluster is stored as-is (UTF-8 in SQLite), preserving ZWJ sequences, skin tone modifiers, and variation selectors.
- **Default**: when the user has never selected anything, the new item is saved with `IconType = Catalog`, `IconValue = "default"` — single uniform fallback path.

### Rendering — `IconSourceElement` with a switched `IconSource`

WinUI's idiomatic pattern is `IconSourceElement` consuming an `IconSource` (`BitmapIconSource`, `FontIconSource`, `SymbolIconSource`, etc.). Both of our two sources fit cleanly:

- **Catalog** → `BitmapIconSource { UriSource = new Uri(CustomIconCatalog.GetIconPath(IconValue)), ShowAsMonochrome = false }` — matches how official Daily Dozen items already render in `DiaryView.xaml`.
- **Emoji** → `FontIconSource { Glyph = IconValue, FontFamily = <OS emoji font fallback chain> }` — emoji code points render correctly through `FontIcon` because the system falls back to the platform emoji font when the requested family lacks the glyph.

Wiring:

- A small static helper `CustomItemIconSourceFactory.Create(CustomItemIconType type, string value) → IconSource` returns the right `IconSource` subclass.
- In XAML, custom-item rows bind `<IconSourceElement IconSource="{x:Bind ViewModel.IconSource, Mode=OneWay}" />`. The view model exposes a single `IconSource IconSource` property recomputed whenever `IconType` / `IconValue` changes.
- Sizing: `IconSourceElement` inherits the surrounding `Width` / `Height` from the cell, so it slots into the existing 32 px (wide) / 24 px (narrow) layout without a custom control.

No new XAML control file is needed — this avoids the awkwardly named `EmojiIconPresenter`, the ad-hoc UserControl boilerplate, and a duplicate visual-state machine. All the logic lives in the factory + a view-model property.

### Alternatives considered

- *Single `IconKey` string with a prefix scheme (e.g., `"emoji:🌱"` vs `"catalog:pill"`)*: rejected — string prefixes are stringly-typed and easy to misparse. An explicit enum + value is clearer in code and JSON.
- *User-supplied images*: still rejected — opens size / format / sync / licensing concerns. Emoji satisfies the "personal expression" need without those costs.
- *Allow multiple emojis per item (e.g., 🍎🥗)*: rejected — keeps icon-cell layout uniform; users entering more than one emoji get the first one silently, matching common emoji-pickers.
- *Font glyph (Segoe Fluent)*: still rejected — same cross-platform availability issue.

---

## Decision: Achievement isolation — implicit via separate table (no extra guard logic)

- **Rationale**: Because custom entries never enter `DailyEntries`, every existing achievement query naturally excludes them. No new guard code is needed in `AchievementService` or anywhere in `SqliteDataService`'s statistics methods. SC-002 (verifiable: a day with only custom items completed produces zero perfect-day flags) becomes a structural certainty.
- **Verification**: a startup-only DEBUG self-check (`SqliteDataService.VerifyAchievementIsolationDebug`) asserts that the achievement-relevant query set (`SELECT DISTINCT ItemId FROM DailyEntries`) is a subset of `ChecklistDefinitions.AllItems.Select(i => i.Id)`. Catches a future regression where someone accidentally writes a custom-item ID into `DailyEntries`.
- **Alternatives considered**:
  - *Explicit blacklist filter in achievement queries*: rejected — adds maintenance burden and a chance of bugs when new achievement types are added.

---

## Decision: Deletion-with-history — modal prompt with binary choice

- **Rationale**: FR-004 requires the user to choose between keep-history (orphan rows retained) and cascade delete. A `ContentDialog` with two clearly labeled buttons is the established WinUI pattern and matches existing settings dialogs in the codebase.
- **Behavior**:
  - **Keep history**: `DELETE FROM CustomItems WHERE Id = ?`. `CustomItemEntries` rows remain orphaned (no FK constraint enforces deletion). Past-day diary views show those entries with the "default" icon and a fallback display name (the persisted name at deletion time, stored on `CustomItemEntries` only at deletion time? No — simpler: render entries with no matching item as "[deleted item]"). For v1, simply hide orphaned entries from the diary; they remain in the DB and round-trip through export.
  - **Delete everything**: `DELETE FROM CustomItemEntries WHERE CustomItemId = ?` followed by `DELETE FROM CustomItems WHERE Id = ?` inside a single transaction.
- **Alternatives considered**:
  - *Soft delete via `IsDeleted` column*: rejected — adds permanent filter overhead in every read query for a low-frequency operation.
  - *Always cascade (no prompt)*: rejected — directly violates FR-004.

---

## Decision: Diary section rendering — second `ListView` below the existing one, hidden when empty

- **Rationale**: The current `DiaryView.xaml` uses a single `ListView` bound to `DiaryViewModel.Items`. Adding a second `ListView` (bound to a new `CustomItems` ObservableCollection) below it is the smallest change and preserves the existing wide/narrow `DataTemplate` system. FR-007 (hide when empty) maps to a `Visibility` binding driven by collection count.
- **Layout**: insert a new `RowDefinition` (Auto) for a "Custom" section header + a new `RowDefinition` (Auto) for the custom list, between the existing checklist (Row 3, `*`) and the bottom of the grid. Outer grid changes from `*` last row to a `ScrollViewer` wrapping both lists, OR the existing `ListView` switches to non-scrolling and a parent `ScrollViewer` is added. Simpler option: convert Row 3 to a `ScrollViewer` containing a `StackPanel` with both lists.
- **Alternatives considered**:
  - *`CollectionViewSource` with grouping*: rejected — would force custom items to share a `DataTemplate` and `ItemTemplateSelector` with official items, blocking divergence (e.g., custom items don't have `MoreInfoUrl`, no merged children).
  - *Tab control*: rejected — hides custom items behind a tap, undermining the daily-tracking goal.

---

## Decision: Localization — new `resw` keys with the prefixes `SettingsView_CustomItems_*`, `DiaryView_CustomSection*`, `CustomItemEditor_*`

- **Rationale**: Matches existing key prefixes (e.g., `Settings_DailyDozen`, `Diary_Today`). Keeps localization grep-friendly. User-entered names are NEVER passed through `Localizer.GetString` — they bind directly to `CustomItem.Name`.
- **Initial key set** (final list confirmed during implementation):
  - `SettingsView_CustomItems_SectionHeader`
  - `SettingsView_CustomItems_SectionDescription`
  - `SettingsView_CustomItems_AddButton`
  - `SettingsView_CustomItems_EmptyState`
  - `SettingsView_CustomItems_DeletePrompt_Title`
  - `SettingsView_CustomItems_DeletePrompt_KeepHistory`
  - `SettingsView_CustomItems_DeletePrompt_DeleteAll`
  - `SettingsView_CustomItems_DeletePrompt_Cancel`
  - `DiaryView_CustomSection_Header`
  - `CustomItemEditor_Title_Add`
  - `CustomItemEditor_Title_Edit`
  - `CustomItemEditor_NameLabel`
  - `CustomItemEditor_NameTooLong` (used for >60 char feedback)
  - `CustomItemEditor_NameDuplicateWarning`
  - `CustomItemEditor_DescriptionLabel`
  - `CustomItemEditor_DescriptionPlaceholder` (e.g., "Optional notes")
  - `CustomItemEditor_DescriptionTooLong` (used for >500 char feedback)
  - `CustomItemEditor_ServingsLabel`
  - `CustomItemEditor_IconLabel`
  - `CustomItemEditor_SortOrderLabel`
  - `CustomItemEditor_Save`
  - `CustomItemEditor_Cancel`

---

## Decision: Export format — extend existing `ExportData` JSON with two new arrays

- **Rationale**: `ExportService.ExportToJsonAsync` already serializes a versioned `ExportData` record. Adding two new collection properties (`CustomItems`, `CustomItemEntries`) is backwards-compatible: older readers ignore unknown JSON properties; newer readers gracefully handle missing arrays from older exports.
- **Version bump**: `ExportData.Version` from `"1.0"` to `"1.1"` — non-breaking minor bump signaling new optional sections.
- **CSV export**: not extended in v1. CSV is a flat shape that matches the official daily dozen rows; mixing custom items risks confusing third-party spreadsheet consumers. Custom items round-trip via JSON only.
- **Import semantics**:
  - `customItems` rows are upserted by `Id` (string GUID): if missing, insert; if present, overwrite name / servings / icon / sortOrder fields.
  - `customItemEntries` rows are upserted by `(Date, CustomItemId)`: same pattern as existing `DailyEntries` import.
  - Skipping `customItems` in the import payload but including `customItemEntries` referencing unknown IDs is tolerated — the entry rows persist (orphaned), matching the deletion-with-history orphan model.

---

## Decision: Forward Compatibility — Server Sync (deferred feature)

The user has indicated that server sync is a likely future addition. This feature is designed so that adding sync later does not require a destructive schema migration or wire-format break.

### Already compatible (no change needed)

- **Stable string GUID `Id` on `CustomItem`** — collision-free across devices; the central enabling decision.
- **`(Date, CustomItemId)` unique index on `CustomItemEntries`** — natural sync key for entries; the local auto-increment `Id` is an implementation detail, never serialized.
- **JSON export / import shape** — already mirrors what a sync wire format would push and pull. The importer's upsert-by-id semantics is exactly the LWW merge a sync engine performs.
- **Orphan-tolerant reads** — keep-history delete already produces orphan rows; the codebase handles them. Sync engines mid-conflict produce the same shape, so no new code path is needed there.
- **Scalar fields only** (`Name`, `Description`, `RecommendedServings`, `IconType`, `IconValue`, `SortOrder`) — trivially mergeable last-write-wins per row.

### Add now (cheap, future-proof)

- **`UpdatedAt` UTC ISO 8601 column on both tables** — set by the data layer on every insert / upsert (`DateTime.UtcNow.ToString("O")`). Cost today: 4 columns + one stamp per write. Cost if deferred: a v4 migration that backfills timestamps for historical user data we no longer have. Useful pre-sync as diagnostic data ("when was this row last touched?").
- **`updatedAt` field in the export JSON** (optional, importer stamps `DateTime.UtcNow` if missing) — once exports include the timestamp, future sync uploads have a consistent vocabulary out of the gate.

### Deferred to the sync feature itself

- **Tombstones / `DeletedAt`** — not added now. Soft-delete invades every read path today for a feature that may never ship. When sync is built, the same migration that ships sync will add tombstone tracking AND the wire protocol that uses it; they belong together. The cost: deletions performed before sync-launch are not propagated to the server (the server simply doesn't learn about them). That is acceptable — the server becomes the source of truth from sync-launch onward.
- **Conflict resolution beyond LWW** — for v1 sync, last-write-wins on `UpdatedAt` per row is sufficient. Smarter handling (e.g., reconciling SortOrder reorderings on two devices, name-collision warnings post-merge) is sync-feature scope.
- **Sync transport / endpoints / auth** — entirely sync-feature scope.

### Outcome

The design is **forward-compatible by construction** (GUIDs, natural keys, orphan tolerance, scalar fields, idempotent JSON upsert) plus **one deliberate cheap investment** (`UpdatedAt`). Anything more (tombstones, conflict policy, transport) is properly scoped to the sync feature, which can land as additive migrations and additive JSON fields without breaking this feature's contract.

---

## Decision: Validation — name length client-side, duplicate-name warning at save

- **Rationale**: FR-010 (60-char limit) and FR-011 (allow duplicates with warning).
- **Pattern**: `CustomItemEditorViewModel` exposes `bool IsNameTooLong` and `bool IsDuplicateName` derived properties; the dialog Save button is enabled only when name is non-empty AND `!IsNameTooLong`. Duplicate name shows an inline warning banner but does not block save.
- **Reorder**: explicit numeric `SortOrder` matches the existing `ChecklistItem.SortOrder` convention noted in user memory ("explicit maintainable" — prefer numeric sort orders over implicit ordering). New custom items default to `SortOrder = (max existing sort order in CustomItems) + 100`.

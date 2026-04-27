# Tasks: Custom User-Defined Checklist Items

**Input**: Design documents from `/specs/001-custom-checklist-items/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/export-schema.md, quickstart.md

**Tests**: Not requested in spec. Validation is manual via `quickstart.md` plus a single in-process DEBUG self-check (`VerifyAchievementIsolationDebug`) — captured as a task, not a test phase.

**Organization**: Tasks are grouped by user story so each can be implemented and validated independently. Phase 1–2 are blocking prerequisites for all stories.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Maps to a user story from `spec.md` (US1, US2, US3)
- File paths are absolute to `src/DailyPlants/` unless noted

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add static asset folders and built-in icon catalog metadata that every later phase depends on. No SQLite, no UI yet.

- [ ] T001 [P] Create asset folder `src/DailyPlants/Assets/Icons/CustomItems/` and add 16 PNG glyphs + `default.png` per research.md ("Built-in catalog"): `pill.png`, `walk.png`, `run.png`, `water.png`, `sleep.png`, `meditate.png`, `yoga.png`, `book.png`, `journal.png`, `sun.png`, `bike.png`, `dumbbell.png`, `apple.png`, `tea.png`, `heart.png`, `star.png`, `default.png`. Match the size/format conventions of existing `Assets/Icons/Items/*.png`.
- [ ] T002 [P] Create `src/DailyPlants/Services/CustomIconCatalog.cs` exposing `IReadOnlyList<string> AllKeys`, `bool IsKnown(string key)`, `string GetIconPath(string key)` (returns `ms-appx:///Assets/Icons/CustomItems/{key}.png`, falling back to `default.png` for unknown keys). Constants are the 16 catalog keys + `"default"` from T001.
- [ ] T003 [P] Verify `DailyPlants.csproj` packs the new `Assets/Icons/CustomItems/**/*.png` files. Existing `<Content Include="Assets\**\*" />` glob should already cover them — confirm by inspecting the csproj; only edit if a more restrictive glob is in place.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Ship the schema, entities, domain models, and `IDataService` extensions that every user story depends on. Until this phase is complete, no story can render or persist anything.

**⚠️ CRITICAL**: All user-story phases (3–5) are blocked until this phase is complete.

- [ ] T004 Create SQLite entity `src/DailyPlants/Services/Entities/CustomItemEntity.cs` per data-model.md ("SQLite entity — CustomItemEntity"): `[Table("CustomItems")]`, `[PrimaryKey] string Id`, `string Name`, `string Description`, `int RecommendedServings`, `int IconType`, `string IconValue` (default `"default"`), `int SortOrder`, `string UpdatedAt`. Mirror the namespace and access-modifier conventions of `DailyEntryEntity.cs`.
- [ ] T005 [P] Create SQLite entity `src/DailyPlants/Services/Entities/CustomItemEntryEntity.cs` per data-model.md ("SQLite entity — CustomItemEntryEntity"): `[Table("CustomItemEntries")]`, `[PrimaryKey, AutoIncrement] int Id`, `string Date`, `string CustomItemId`, `int ServingsCompleted`, `string UpdatedAt`. Mirror `DailyEntryEntity.cs`.
- [ ] T006 [P] Create domain model `src/DailyPlants/Models/CustomItem.cs` as a `partial record` with `Id`, `Name`, `Description` (default `""`), `RecommendedServings`, `IconType`, `IconValue`, `SortOrder`, `UpdatedAt`. Add the `CustomItemIconType` enum (`Catalog = 0`, `Emoji = 1`) in the same file.
- [ ] T007 [P] Create domain model `src/DailyPlants/Models/CustomItemEntry.cs` per data-model.md with `int Id`, `DateOnly Date`, `string CustomItemId`, `int ServingsCompleted`, `DateTime UpdatedAt` (UTC).
- [ ] T008 Extend `src/DailyPlants/Services/IDataService.cs` with the seven new methods listed in data-model.md ("IDataService extension surface"): `GetCustomItemsAsync`, `GetCustomItemByIdAsync`, `SaveCustomItemAsync`, `DeleteCustomItemAsync(string id, bool cascadeEntries)`, `GetCustomItemEntriesForDateAsync(DateOnly)`, `GetCustomItemEntriesInRangeAsync(DateOnly, DateOnly)`, `SaveCustomItemEntryAsync`. Do NOT modify existing achievement-relevant signatures (`GetCurrentStreakAsync`, `GetLongestStreakAsync`, `GetPerfectDaysCountAsync`, `GetItemCompletionCountAsync`).
- [ ] T009 Implement the seven new `IDataService` methods in `src/DailyPlants/Services/SqliteDataService.cs`. Use the existing `DailyEntries` upsert pattern (`INSERT … ON CONFLICT(Date, CustomItemId) DO UPDATE`) for `SaveCustomItemEntryAsync` per data-model.md ("Persistence pattern"). Stamp `UpdatedAt = DateTime.UtcNow.ToString("O")` on every write regardless of caller-supplied value. Implement `DeleteCustomItemAsync` as a single transaction when `cascadeEntries = true`.
- [ ] T010 Add migration v3 to `SqliteDataService.RunMigrationsAsync` per research.md ("Schema migration"): `if (version < 3) { CreateTableAsync<CustomItemEntity>(); CreateTableAsync<CustomItemEntryEntity>(); CREATE UNIQUE INDEX idx_custom_entries_date_item ON CustomItemEntries (Date, CustomItemId); PRAGMA user_version = 3; }`. Also add idempotent `CreateTableAsync<...>` calls in `InitializeAsync` for fresh installs (mirrors the existing pattern for `DailyEntryEntity`).
- [ ] T011 Add `CustomItemService.cs` + `ICustomItemService.cs` in `src/DailyPlants/Services/` exposing `Task<IReadOnlyList<CustomItem>> GetAllAsync()`, `Task<CustomItem> CreateAsync(...)`, `Task UpdateAsync(CustomItem item)`, `Task DeleteAsync(string id, bool cascadeEntries)`. Inside `CreateAsync`: generate `Id = Guid.NewGuid().ToString("N")`, compute `SortOrder = (max existing SortOrder) + 100`, set `UpdatedAt`. Apply validation rules from data-model.md ("Validation rules") server-side as a defensive layer behind the editor ViewModel. Wrap `IDataService` calls.
- [ ] T012 [P] Add static helper `src/DailyPlants/Services/CustomItemIconSourceFactory.cs` exposing `static IconSource Create(CustomItemIconType type, string value)`. For `Catalog`: return `BitmapIconSource { UriSource = new Uri(CustomIconCatalog.GetIconPath(value)), ShowAsMonochrome = false }`. For `Emoji`: return `FontIconSource { Glyph = value, FontFamily = <emoji fallback chain> }`. Per research.md ("Rendering — IconSourceElement").
- [ ] T013 Register `ICustomItemService → CustomItemService` in the host builder where existing services are registered (search for `IDataService` registration in `App.xaml.cs` / `Startup.cs` — wire the new service alongside).
- [ ] T014 Add DEBUG-only self-check method `VerifyAchievementIsolationDebug` on `SqliteDataService` per research.md ("Achievement isolation"): asserts `SELECT DISTINCT ItemId FROM DailyEntries` is a subset of `ChecklistDefinitions.AllItems.Select(i => i.Id)`. Wire a call to it from `App.xaml.cs` startup inside `#if DEBUG`. Logs (do not throw in retail).

**Checkpoint**: Schema, models, services, and DI are wired. UI work for all three user stories can now proceed in parallel.

---

## Phase 3: User Story 1 - Add and track a personal daily item (Priority: P1) 🎯 MVP

**Goal**: A user can create one custom item via Settings (name, optional description, servings, icon) and mark its servings each day in a new "Custom" diary section. Achievement engine remains unaffected.

**Independent Test**: Per quickstart.md Story 1 (steps 1–9). Add one item, mark its servings, navigate days, then verify Daily Dozen / Tweaks / streak / perfect-day are unchanged on a day with only the custom item completed.

### Implementation for User Story 1

- [ ] T015 [US1] Create `src/DailyPlants/ViewModels/CustomItemEditorViewModel.cs` (new file) — backing for the add/edit dialog. Use `[ObservableProperty]` for `Name`, `Description`, `RecommendedServings`, `IconType`, `IconValue`, `SortOrder`. Derived properties: `IsNameTooLong` (>60), `IsDescriptionTooLong` (>500), `IsDuplicateName`, `IsValid`, `IconSource` (recomputed via `CustomItemIconSourceFactory` whenever `IconType`/`IconValue` change). `[RelayCommand]` `SaveAsync` (canExecute: `IsValid`) calls `ICustomItemService.CreateAsync` or `UpdateAsync`. Per research.md ("Validation").
- [ ] T016 [P] [US1] Create `src/DailyPlants/Views/IconPickerControl.xaml(.cs)` (new UserControl). Two visual sources side-by-side per research.md ("Emoji input"): a catalog grid (bound to `CustomIconCatalog.AllKeys` rendering each via `CustomItemIconSourceFactory`) and a single-line emoji input (`TextBox`). Selecting a catalog glyph or typing in the emoji input updates the parent ViewModel's `IconType`/`IconValue`. Emoji input applies the StringInfo-based "first-grapheme + Extended_Pictographic check" validation at save time (T015 wires the actual reduction inside the ViewModel; the control just feeds the raw string in).
- [ ] T017 [US1] Create `src/DailyPlants/Views/CustomItemEditorDialog.xaml(.cs)` (new ContentDialog). Layout: name `TextBox` (with character counter / inline `IsNameTooLong` warning), description `TextBox` (multiline, `MaxLength=500`, with character counter / inline `IsDescriptionTooLong` warning), recommended-servings `NumberBox` clamped to ≥1, embedded `IconPickerControl`, sort-order `NumberBox`, duplicate-name warning banner bound to `IsDuplicateName`. Save button bound to `SaveCommand`. Use `Localizer.GetString("CustomItemEditor_*")` keys per research.md ("Localization").
- [ ] T018 [US1] Modify `src/DailyPlants/ViewModels/DiaryViewModel.cs` to load custom-item entries for the currently-displayed date into a separate `ObservableCollection<CustomItemRowViewModel>` (define `CustomItemRowViewModel` inside the same file or as a sibling). Bind `+`/`–` commands to `IDataService.SaveCustomItemEntryAsync`. Hide-when-empty: bind `CustomSectionVisibility = CustomItems.Count > 0 ? Visible : Collapsed`. Do NOT touch the existing Daily-Dozen / Tweaks `Items` collection. Sort by `CustomItem.SortOrder`.
- [ ] T019 [US1] Modify `src/DailyPlants/Views/DiaryView.xaml` to render the new "Custom" section below the existing checklist per research.md ("Diary section rendering"). Replace the row-3 `*` `RowDefinition` strategy with a `ScrollViewer` containing a `StackPanel` of two `ListView`s: existing items + custom items. Add a `TextBlock` section header bound to `Localizer.GetString("DiaryView_CustomSection_Header")`, visible only when `CustomItems.Count > 0`. The custom `ListView`'s `DataTemplate` mirrors the existing template's `+`/`–` controls but uses `IconSourceElement` bound to `IconSource` (no `MoreInfoUrl`, no merged children).
- [ ] T020 [US1] Add a description-flyout/info-button to the custom-item row template in `DiaryView.xaml` (and matching narrow template if applicable). Visible only when `CustomItem.Description` is non-empty (binding via `StringEmptyToVisibilityConverter` or a `BoolToVisibilityConverter` on `HasDescription`). Tapping shows a `Flyout` rendering the description text. Per spec acceptance scenario 1a.
- [ ] T021 [US1] In `src/DailyPlants/ViewModels/SettingsViewModel.cs`, add: `ObservableCollection<CustomItem> CustomItems`, `[RelayCommand] AddCustomItemAsync` (opens `CustomItemEditorDialog` with a fresh ViewModel), `[RelayCommand] EditCustomItemAsync(CustomItem item)`, `[RelayCommand] DeleteCustomItemAsync(CustomItem item)` (Story 2 will replace this with the full prompt — for US1 just call `DeleteAsync(item.Id, cascadeEntries: true)`). Load `CustomItems` from `ICustomItemService.GetAllAsync()` on view-model init.
- [ ] T022 [US1] Modify `src/DailyPlants/Views/SettingsView.xaml` to add a new "Custom Items" `SettingsExpander` section below the existing settings sections. Inside: a list bound to `CustomItems` (each row shows icon via `IconSourceElement`, name, recommended servings, edit/delete buttons), an Add button bound to `AddCustomItemCommand`, an empty-state `TextBlock` shown when `CustomItems.Count == 0`. Localize headers and empty-state text via `SettingsView_CustomItems_*` keys.
- [ ] T023 [P] [US1] Add the new resw keys listed in research.md ("Localization") to `src/DailyPlants/Strings/en/Resources.resw` (English source of truth): `SettingsView_CustomItems_SectionHeader`, `SettingsView_CustomItems_SectionDescription`, `SettingsView_CustomItems_AddButton`, `SettingsView_CustomItems_EmptyState`, `DiaryView_CustomSection_Header`, `CustomItemEditor_Title_Add`, `CustomItemEditor_Title_Edit`, `CustomItemEditor_NameLabel`, `CustomItemEditor_NameTooLong`, `CustomItemEditor_DescriptionLabel`, `CustomItemEditor_DescriptionPlaceholder`, `CustomItemEditor_DescriptionTooLong`, `CustomItemEditor_ServingsLabel`, `CustomItemEditor_IconLabel`, `CustomItemEditor_SortOrderLabel`, `CustomItemEditor_Save`, `CustomItemEditor_Cancel`. (Story-2 keys for the delete prompt + duplicate warning are added in T030.)
- [ ] T024 [US1] Verify Story 1 acceptance scenarios 1, 1a, 2, 2a, 2b, 3, 4, 5 manually using quickstart.md steps 1–9. Confirm achievement isolation via the DEBUG self-check (T014) firing without errors and via Achievements view showing 0 perfect days when only the custom item is complete.

**Checkpoint**: A user can add one custom item, mark its servings, and verify achievement isolation. MVP complete and shippable.

---

## Phase 4: User Story 2 - Manage existing custom items (Priority: P2)

**Goal**: Users can rename, change description / servings / icon / sort order, and delete (with keep-history vs cascade prompt) custom items. Renames preserve history.

**Independent Test**: Per quickstart.md Story 2 (steps 10–16). Rename a tracked item → its history count is unchanged. Reorder → diary order persists across restart. Delete → user is prompted with three buttons.

### Implementation for User Story 2

- [ ] T025 [US2] In `CustomItemEditorViewModel` (T015), wire up "edit existing" mode: constructor variant accepting an existing `CustomItem` populates all fields and stores `Id` for the eventual `UpdateAsync` call. `SaveAsync` branches on whether `Id` is present.
- [ ] T026 [US2] In `SettingsViewModel.EditCustomItemAsync` (T021), open `CustomItemEditorDialog` in edit mode (passes the selected `CustomItem`). On save, refresh `CustomItems` from `ICustomItemService.GetAllAsync()` so the list reflects new sort order.
- [ ] T027 [US2] Replace the temporary delete logic in `SettingsViewModel.DeleteCustomItemAsync` (T021) with the full keep-history flow per research.md ("Deletion-with-history"): show a `ContentDialog` with three buttons — Keep history, Delete everything, Cancel. Map button to `ICustomItemService.DeleteAsync(id, cascadeEntries: false | true)`. Cancel is a no-op. Skip the prompt and just call `DeleteAsync(id, cascadeEntries: true)` if the item has zero entries (call `IDataService.GetCustomItemEntriesInRangeAsync` over a wide range, or add a small `HasEntriesAsync(id)` helper if cleaner).
- [ ] T028 [US2] Filter orphaned entries out of the diary load path in `DiaryViewModel` (T018): only keep `CustomItemEntry` rows whose `CustomItemId` matches a current `CustomItem`. Orphans remain in the DB and round-trip through export per research.md ("Deletion-with-history" → "For v1, simply hide orphaned entries").
- [ ] T029 [US2] Surface a duplicate-name non-blocking warning banner in `CustomItemEditorDialog` (T017). Wire `IsDuplicateName` in `CustomItemEditorViewModel` (T015) to recompute by checking other `CustomItems` (excluding the currently edited Id). Bind `Visibility` of an `InfoBar` to that bool. Save remains enabled. Per FR-011.
- [ ] T030 [P] [US2] Add Story-2 resw keys to `src/DailyPlants/Strings/en/Resources.resw`: `SettingsView_CustomItems_DeletePrompt_Title`, `SettingsView_CustomItems_DeletePrompt_KeepHistory`, `SettingsView_CustomItems_DeletePrompt_DeleteAll`, `SettingsView_CustomItems_DeletePrompt_Cancel`, `CustomItemEditor_NameDuplicateWarning`.
- [ ] T031 [US2] Verify Story 2 acceptance scenarios 1–6 manually using quickstart.md steps 10–16. Verify SC-003 by counting `CustomItemEntries` rows for an item before and after a rename (via `SqliteDataService` debug query or DB browser).

**Checkpoint**: Custom items are fully manageable. Stories 1 and 2 work side-by-side without regressions.

---

## Phase 5: User Story 3 - Export and re-import including custom items (Priority: P3)

**Goal**: Custom item definitions and their entries are included in JSON export at v1.1 and round-trip cleanly through import (upsert semantics, orphan tolerance).

**Independent Test**: Per quickstart.md Story 3 (steps 17–19). Export → wipe DB → import → row counts and field values identical pre/post.

### Implementation for User Story 3

- [ ] T032 [US3] Modify the `ExportData` DTO inside `src/DailyPlants/Services/ExportService.cs` (or its dedicated file if extracted) per contracts/export-schema.md: bump `Version` from `"1.0"` to `"1.1"`, add `IReadOnlyList<CustomItemExportDto> CustomItems` and `IReadOnlyList<CustomItemEntryExportDto> CustomItemEntries`. Define the two new DTOs in the same file matching the contract field shapes (note `iconType` is the **string** `"catalog"`/`"emoji"` on the wire, mapped from the int enum on serialize/deserialize).
- [ ] T033 [US3] Modify `ExportService.ExportToJsonAsync` to populate the two new arrays from `IDataService.GetCustomItemsAsync()` and a new `GetAllCustomItemEntriesAsync()` helper (add to `IDataService`/`SqliteDataService` if not already present — orphan-inclusive). Field order follows record property declaration per contract.
- [ ] T034 [US3] Modify `ExportService.ImportFromJsonAsync` per contracts/export-schema.md ("Import semantics"): process `customItems` first, then `customItemEntries`. Apply the per-field defensive defaults / clamps from the contract (truncate `name` to 60, `description` to 500, default missing `description` to `""`, clamp `recommendedServings` to ≥1, unknown `iconType` → `"catalog"`/`"default"`, two-grapheme emoji → first grapheme, missing `updatedAt` → `DateTime.UtcNow.ToString("O")`). Surface truncation/clamping warnings in the existing `ImportResult` shape. Upsert by `id` for items and `(date, customItemId)` for entries.
- [ ] T035 [P] [US3] If the project still has the CSV export path active, explicitly skip custom items there (no new columns, no new rows). Per contracts/export-schema.md ("Non-goals for v1"). One-line guard with a code comment is enough — no UI change.
- [ ] T036 [US3] Verify Story 3 acceptance scenarios 1–3 and SC-004 manually using quickstart.md steps 17–19. Walk through the contract's "Validation tests" matrix at least for: 0-items export, round-trip, v1.0 file import, unknown-id entry import, 80-char name truncate, 600-char description truncate, missing description field, two-grapheme emoji, unknown iconType.

**Checkpoint**: All three user stories independently functional. Feature-complete.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Localization for non-English locales, RTL spot-check, cross-platform spot-check, doc updates.

- [ ] T037 [P] Translate every resw key added in T023 + T030 across the 20 non-English locales: `bg`, `ca`, `cs`, `de`, `el`, `es`, `fa`, `fr`, `he`, `hu`, `it`, `pl`, `pt-BR`, `pt-PT`, `ro`, `ru`, `sk`, `uk`, `zh-Hans`, `zh-Hant`. File path: `src/DailyPlants/Strings/{locale}/Resources.resw` for each. Per FR-015 / SC-005.
- [ ] T038 RTL spot-check per quickstart.md Edge cases: switch app language to `he` then `fa`, open Diary + Settings + the custom-item editor; confirm chrome flips RTL while user-entered names remain LTR. No truncation or layout overlap.
- [ ] T039 Cross-platform spot-check per quickstart.md "Cross-platform spot-check" matrix: Android, iOS, WASM, Desktop (Skia). Critical checks: add item, mark serving, restart / refresh, history persists. Verify icon picker fits portrait viewport on Android/iOS. Verify file-picker dialogs in Skia for export/import.
- [ ] T040 Migration spot-check per quickstart.md "Edge cases → Migration path": launch with a v2 DB (no custom-item tables); confirm `PRAGMA user_version` advances to 3 and both new tables exist (`SELECT name FROM sqlite_master WHERE type='table' AND name LIKE 'Custom%'`).
- [ ] T041 SC-006 regression check: side-by-side compare diary screenshots from `main` and from this branch with no custom items configured — Daily Dozen / Tweaks rendering and behavior must be byte-identical to a casual eye.
- [ ] T042 [P] Update `CLAUDE.md` if the added DEBUG self-check, the v3 migration, or the parallel-table pattern are worth pinning for future work in this repo (one-line hooks only — no expansion).

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: no dependencies — start immediately.
- **Phase 2 (Foundational)**: depends on Phase 1 (asset files referenced by `CustomIconCatalog`). **Blocks Phases 3, 4, 5.**
- **Phase 3 (US1 — MVP)**: depends on Phase 2.
- **Phase 4 (US2)**: depends on Phase 2; reuses files created in Phase 3 (`CustomItemEditorViewModel`, `CustomItemEditorDialog`, `SettingsViewModel`, `DiaryViewModel`). Best done after Phase 3 in single-developer workflow; can run in parallel with Phase 3 in multi-developer workflow if developers coordinate on those shared files.
- **Phase 5 (US3)**: depends on Phase 2 only. Touches different files (`ExportService.cs`) and can run fully in parallel with Phases 3 and 4.
- **Phase 6 (Polish)**: depends on all desired user-story phases being complete.

### Within-Phase Task Dependencies

- T009 (`SqliteDataService` impl) depends on T004, T005, T008.
- T010 (migration v3) depends on T004, T005.
- T011 (`CustomItemService`) depends on T009.
- T013 (DI registration) depends on T011.
- T014 (DEBUG self-check) depends on `SqliteDataService` already loading (any time after T010).
- T017 (editor dialog) depends on T015 (VM) and T016 (icon picker control).
- T019 (Diary XAML) depends on T018 (Diary VM changes).
- T021 (Settings VM) depends on T011, T015.
- T022 (Settings XAML) depends on T021.
- T025–T029 (US2) depend on T015, T017, T021 from US1.
- T028 (orphan filter) depends on T018.
- T033, T034 (export/import) depend on T032.
- T037 (locale translations) depends on T023 and T030.

### Parallel Opportunities

- All Phase 1 tasks (T001, T002, T003) can run in parallel.
- In Phase 2: T004, T005, T006, T007 are independent (different new files) → run in parallel. T012 is independent of the data-access stack → can also run in parallel with T004–T007.
- In Phase 3: T015, T016, T023 are independent files. T018 (DiaryViewModel) and T021 (SettingsViewModel) edit different files and can run in parallel after T015 lands.
- In Phase 5: T032 is sequential (DTO file), but once it lands, T035 (CSV guard) is independent and can run in parallel with T033/T034.
- In Phase 6: T037 (translations) and T042 (CLAUDE.md note) are independent of everything else.

---

## Parallel Example: User Story 1 Foundation

```bash
# After Phase 2 lands, kick off the US1 file-creation tasks in parallel:
Task: "Create CustomItemEditorViewModel.cs (T015)"
Task: "Create IconPickerControl.xaml(.cs) (T016)"
Task: "Add en resw keys for editor + section (T023)"

# After T015 + T016 land, the dialog itself:
Task: "Create CustomItemEditorDialog.xaml(.cs) (T017)"

# In parallel with the dialog, the diary integration (different files):
Task: "Modify DiaryViewModel.cs to load custom items (T018)"
Task: "Modify SettingsViewModel.cs to add CustomItems collection (T021)"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1: Setup (T001–T003).
2. Phase 2: Foundational (T004–T014). **Critical — blocks everything below.**
3. Phase 3: User Story 1 (T015–T024).
4. **Stop and validate**: walk quickstart.md Story 1; verify SC-002 (achievement isolation). Ship if needed.

### Incremental Delivery

1. Setup + Foundational ready (Phases 1–2).
2. Add Story 1 (Phase 3) → manual quickstart Story 1 → MVP demo.
3. Add Story 2 (Phase 4) → manual quickstart Story 2 → demo.
4. Add Story 3 (Phase 5) → manual quickstart Story 3 → demo.
5. Polish (Phase 6) → ship.

### Single-Developer Order (recommended)

Single developer in this repo: do Phase 1 → Phase 2 → Phase 3 → Phase 4 → Phase 5 → Phase 6 sequentially. Skipping Phase 4 to ship Phase 3 + 5 separately is also viable since US1 + US3 don't share dialog files; US2 layers cleanly on top of US1.

---

## Notes

- `[P]` tasks edit different files; same-file tasks are sequential by construction.
- Phase 3 tasks T015 + T016 create new files and so are independent; T017 imports both and so is sequential to them.
- Achievement isolation (SC-002) is enforced **structurally** by the parallel-table schema (Phase 2) — no per-task guard logic exists or is needed elsewhere.
- The DEBUG self-check (T014) is the only "test" — it lives in `SqliteDataService` itself and runs at startup in DEBUG only.
- Commit at logical task groupings (e.g., per phase, or per ViewModel + View pair).
- Stop at any checkpoint to validate independently — that's the whole point of the user-story phasing.

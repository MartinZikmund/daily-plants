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

- [X] T001 [P] Create asset folder `src/DailyPlants/Assets/Icons/CustomItems/` and add 16 PNG glyphs + `default.png` per research.md ("Built-in catalog"): `pill.png`, `walk.png`, `run.png`, `water.png`, `sleep.png`, `meditate.png`, `yoga.png`, `book.png`, `journal.png`, `sun.png`, `bike.png`, `dumbbell.png`, `apple.png`, `tea.png`, `heart.png`, `star.png`, `default.png`. Match the size/format conventions of existing `Assets/Icons/Items/*.png`.
- [X] T002 [P] Create `src/DailyPlants/Services/CustomIconCatalog.cs` exposing `IReadOnlyList<string> AllKeys`, `bool IsKnown(string key)`, `string GetIconPath(string key)` (returns `ms-appx:///Assets/Icons/CustomItems/{key}.png`, falling back to `default.png` for unknown keys). Constants are the 16 catalog keys + `"default"` from T001.
- [X] T003 [P] Verify `DailyPlants.csproj` packs the new `Assets/Icons/CustomItems/**/*.png` files. Existing `<Content Include="Assets\**\*" />` glob should already cover them — confirm by inspecting the csproj; only edit if a more restrictive glob is in place.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Ship the schema, entities, domain models, and `IDataService` extensions that every user story depends on. Until this phase is complete, no story can render or persist anything.

**⚠️ CRITICAL**: All user-story phases (3–5) are blocked until this phase is complete.

- [X] T004 Create SQLite entity `src/DailyPlants/Services/Entities/CustomItemEntity.cs` per data-model.md ("SQLite entity — CustomItemEntity"): `[Table("CustomItems")]`, `[PrimaryKey] string Id`, `string Name`, `string Description`, `int RecommendedServings`, `int IconType`, `string IconValue` (default `"default"`), `int SortOrder`, `string UpdatedAt`. Mirror the namespace and access-modifier conventions of `DailyEntryEntity.cs`.
- [X] T005 [P] Create SQLite entity `src/DailyPlants/Services/Entities/CustomItemEntryEntity.cs` per data-model.md ("SQLite entity — CustomItemEntryEntity"): `[Table("CustomItemEntries")]`, `[PrimaryKey, AutoIncrement] int Id`, `string Date`, `string CustomItemId`, `int ServingsCompleted`, `string UpdatedAt`. Mirror `DailyEntryEntity.cs`.
- [X] T006 [P] Create domain model `src/DailyPlants/Models/CustomItem.cs` as a `partial record` with `Id`, `Name`, `Description` (default `""`), `RecommendedServings`, `IconType`, `IconValue`, `SortOrder`, `UpdatedAt`. Add the `CustomItemIconType` enum (`Catalog = 0`, `Emoji = 1`) in the same file.
- [X] T007 [P] Create domain model `src/DailyPlants/Models/CustomItemEntry.cs` per data-model.md with `int Id`, `DateOnly Date`, `string CustomItemId`, `int ServingsCompleted`, `DateTime UpdatedAt` (UTC).
- [X] T008 Extend `src/DailyPlants/Services/IDataService.cs` with the seven new methods listed in data-model.md ("IDataService extension surface").
- [X] T009 Implement the seven new `IDataService` methods in `src/DailyPlants/Services/SqliteDataService.cs`.
- [X] T010 Add migration v3 to `SqliteDataService.RunMigrationsAsync`.
- [X] T011 Add `CustomItemService.cs` + `ICustomItemService.cs` with validation, ID generation, SortOrder defaulting.
- [X] T012 [P] Add static helper `src/DailyPlants/Services/CustomItemIconSourceFactory.cs`.
- [X] T013 Register `ICustomItemService → CustomItemService` in the host builder.
- [X] T014 Add DEBUG-only self-check method `VerifyAchievementIsolationDebug` on `SqliteDataService` and wire it from `App.xaml.cs`.

**Checkpoint**: Schema, models, services, and DI are wired. UI work for all three user stories can now proceed in parallel.

---

## Phase 3: User Story 1 - Add and track a personal daily item (Priority: P1) 🎯 MVP

**Goal**: A user can create one custom item via Settings (name, optional description, servings, icon) and mark its servings each day in a new "Custom" diary section. Achievement engine remains unaffected.

**Independent Test**: Per quickstart.md Story 1 (steps 1–9). Add one item, mark its servings, navigate days, then verify Daily Dozen / Tweaks / streak / perfect-day are unchanged on a day with only the custom item completed.

### Implementation for User Story 1

- [X] T015 [US1] Create `CustomItemEditorViewModel.cs` with validation properties, IconSource computed via factory, SaveAsync command.
- [X] T016 [P] [US1] Create `IconPickerControl.xaml(.cs)` with catalog grid + emoji input, both updating the parent ViewModel.
- [X] T017 [US1] Create `CustomItemEditorDialog.xaml(.cs)` ContentDialog with full editor layout and inline validation messages.
- [X] T018 [US1] Modify `DiaryViewModel.cs` to load custom-item entries into `CustomItems` collection, +/- bound to SaveCustomItemEntryAsync.
- [X] T019 [US1] Modify `DiaryView.xaml` to render Custom section header + ListView below checklist, wrapped in ScrollViewer.
- [X] T020 [US1] Add description-flyout info button to custom-item row template (visible only when description is non-empty).
- [X] T021 [US1] Extend `SettingsViewModel.cs` with `CustomItems`, Add/Edit/Delete commands, dialog/prompt events.
- [X] T022 [US1] Modify `SettingsView.xaml` to add Custom Items SettingsExpander with list, Add button, and empty-state card.
- [X] T023 [P] [US1] Add Story 1 (and Story 2) resw keys to `src/DailyPlants/Strings/en/Resources.resw`.
- [ ] T024 [US1] Verify Story 1 acceptance scenarios 1, 1a, 2, 2a, 2b, 3, 4, 5 manually using quickstart.md steps 1–9 (deferred — manual QA).

**Checkpoint**: A user can add one custom item, mark its servings, and verify achievement isolation. MVP complete and shippable.

---

## Phase 4: User Story 2 - Manage existing custom items (Priority: P2)

**Goal**: Users can rename, change description / servings / icon / sort order, and delete (with keep-history vs cascade prompt) custom items. Renames preserve history.

**Independent Test**: Per quickstart.md Story 2 (steps 10–16). Rename a tracked item → its history count is unchanged. Reorder → diary order persists across restart. Delete → user is prompted with three buttons.

### Implementation for User Story 2

- [X] T025 [US2] Editor view-model accepts an existing `CustomItem` for edit mode; SaveAsync branches on `_editingId`.
- [X] T026 [US2] `SettingsViewModel.EditCustomItemAsync` opens dialog in edit mode and refreshes the list on save.
- [X] T027 [US2] Delete flow shows three-button prompt (Keep history / Delete everything / Cancel); skips prompt when no entries exist.
- [X] T028 [US2] Diary load filters orphaned entries by matching against current `CustomItems` definitions only.
- [X] T029 [US2] Duplicate-name InfoBar banner bound to `IsDuplicateName`; Save remains enabled.
- [X] T030 [P] [US2] Story-2 resw keys added alongside Story-1 keys.
- [ ] T031 [US2] Verify Story 2 acceptance scenarios 1–6 manually (deferred — manual QA).

**Checkpoint**: Custom items are fully manageable. Stories 1 and 2 work side-by-side without regressions.

---

## Phase 5: User Story 3 - Export and re-import including custom items (Priority: P3)

**Goal**: Custom item definitions and their entries are included in JSON export at v1.1 and round-trip cleanly through import (upsert semantics, orphan tolerance).

**Independent Test**: Per quickstart.md Story 3 (steps 17–19). Export → wipe DB → import → row counts and field values identical pre/post.

### Implementation for User Story 3

- [X] T032 [US3] Bumped `ExportData.Version` to `"1.1"`; added `CustomItemExport` + `CustomItemEntryExport` DTOs with string `IconType`.
- [X] T033 [US3] Populate `CustomItems` + `CustomItemEntries` in `ExportToJsonAsync` via `GetCustomItemsAsync` + new `GetAllCustomItemEntriesAsync`.
- [X] T034 [US3] Import upserts custom items first, then entries; applies clamps for name/description, defaults unknown iconType to catalog, takes first emoji grapheme, defaults missing updatedAt.
- [X] T035 [P] [US3] CSV export already excluded custom items; added an explicit comment marker.
- [ ] T036 [US3] Verify Story 3 acceptance scenarios 1–3 and SC-004 manually using quickstart.md steps 17–19 (deferred — manual QA).

**Checkpoint**: All three user stories independently functional. Feature-complete.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Localization for non-English locales, RTL spot-check, cross-platform spot-check, doc updates.

- [X] T037 [P] Translated all 27 new resw keys across 20 locales (`bg`, `ca`, `cs`, `de`, `el`, `es`, `fa`, `fr`, `he`, `hu`, `it`, `pl`, `pt-BR`, `pt-PT`, `ro`, `ru`, `sk`, `uk`, `zh-Hans`, `zh-Hant`).
- [ ] T038 RTL spot-check (deferred — manual QA).
- [ ] T039 Cross-platform spot-check (deferred — manual QA).
- [ ] T040 Migration spot-check (deferred — manual QA).
- [ ] T041 SC-006 regression check (deferred — manual QA).
- [X] T042 [P] CLAUDE.md note skipped — current CLAUDE.md is a SpecKit pointer; conventions already captured in plan.md/research.md.

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

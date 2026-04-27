# Implementation Plan: Custom User-Defined Checklist Items

**Branch**: `001-custom-checklist-items` | **Date**: 2026-04-27 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/001-custom-checklist-items/spec.md`

## Summary

Allow users to define their own daily-tracked items that appear in a dedicated "Custom" section in the diary, fully isolated from the official Daily Dozen / Tweaks completion logic and achievement system. Approach: store custom item definitions in a new `CustomItems` table and their daily history in a parallel `CustomItemEntries` table — the parallel table guarantees achievement queries never see custom rows without scattered conditional filters. Surface CRUD in `SettingsView` (add / edit / delete with history-handling prompt / reorder via numeric `SortOrder`) and render via a second collection control in `DiaryView` below the existing checklist. Extend `ExportService` with `customItems` and `customItemEntries` JSON sections. Icons are sourced from either a small built-in PNG catalog (16 + default) or a single user-entered emoji grapheme; the choice is captured by an `IconType` discriminator + an `IconValue` string column.

## Technical Context

**Language/Version**: C# / .NET 10 (TFMs: `net10.0-android`, `net10.0-ios`, `net10.0-windows10.0.26100`, `net10.0-browserwasm`, `net10.0-desktop`)
**Primary Dependencies**: Uno Platform (Uno.Sdk; UnoFeatures: Hosting, Mvvm, SkiaRenderer, Svg, Toolkit), CommunityToolkit.WinUI.Controls.SettingsControls, MZikmund.Toolkit.WinUI, sqlite-net-e (sqlite-net ORM async), SourceGear.sqlite3
**Storage**: Local SQLite (`%LocalAppData%/DailyPlants/dailyplants.db`); existing `SqliteDataService` with `PRAGMA user_version` migrations; new tables `CustomItems` and `CustomItemEntries` added in migration v3
**Testing**: No test project currently in repo. Validation will be manual + a small in-process self-check method invoked at startup in DEBUG to verify achievement isolation invariants (see research.md). If a test project is added later, the same checks port directly.
**Target Platform**: Single-project Uno (`UnoSingleProject=true`) shipping Android, iOS, Windows (WinAppSDK), WASM, and Desktop (Skia) heads
**Project Type**: Cross-platform desktop / mobile app (single Uno project)
**Performance Goals**: 60 fps UI on diary scroll; sub-100 ms add/edit save round-trip on local SQLite; sub-50 ms diary load for ≤50 custom items
**Constraints**: Fully offline; no network calls for any custom-item flow; UI labels localized in all 21 currently shipping languages; user-entered names NOT translated; supports RTL scripts (Hebrew, Persian)
**Scale/Scope**: Personal-device app. Realistic upper bounds: ≤50 custom items per user, ≤10 years of daily entries (~3,650 entries per item, ~180k rows total in `CustomItemEntries` extreme case). Well within SQLite comfort zone. ≤20 icons in built-in set. 21 locales × ~12 new strings ≈ 250 string entries to add.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

The repository's `.specify/memory/constitution.md` is the unmodified Spec Kit template (placeholder principles only) — no ratified gates exist for this project yet. As such, no constitutional checks fail. The plan is consistent with the project's documented preferences captured in memory and CLAUDE.md:

- **Explicit numeric sort orders, named constants over implicit ordering** — followed: `CustomItem.SortOrder` is an explicit numeric column, no list-position ordering.
- **No premature abstractions / no scope creep** — followed: parallel `CustomItemEntries` table reused via `IDataService` extensions; no generic "checklist abstraction" refactor of `ChecklistDefinitions`.
- **Maintain existing patterns** — followed: same entity / model / service split as `DailyEntryEntity` / `DailyEntry` / `IDataService`; same `Localizer.GetString` localization path; same `[ObservableProperty]` / `[RelayCommand]` MVVM patterns as existing ViewModels.

**Result: PASS** (no violations to track in Complexity Tracking).

## Project Structure

### Documentation (this feature)

```text
specs/001-custom-checklist-items/
├── plan.md                # This file
├── research.md            # Phase 0 — design decisions & rationale
├── data-model.md          # Phase 1 — entities, schema, relationships
├── quickstart.md          # Phase 1 — manual verification walkthrough
├── contracts/
│   └── export-schema.md   # JSON export format extension
├── checklists/
│   └── requirements.md    # Spec quality checklist (from /speckit-specify)
└── tasks.md               # Created by /speckit-tasks (NOT this command)
```

### Source Code (repository root)

This is a single-project Uno app. The feature is delivered entirely inside `src/DailyPlants/`. New files added; no folder restructuring.

```text
src/DailyPlants/
├── Models/
│   ├── CustomItem.cs                       # NEW — definition record
│   └── CustomItemEntry.cs                  # NEW — daily entry model
├── Services/
│   ├── CustomItemService.cs                # NEW — CRUD wrapper, name validation, deletion-with-history flow
│   ├── ICustomItemService.cs               # NEW
│   ├── CustomIconCatalog.cs                # NEW — built-in IconKey list + default
│   ├── CustomItemIconSourceFactory.cs      # NEW — builds an IconSource (BitmapIconSource | FontIconSource) from (IconType, IconValue)
│   ├── IDataService.cs                     # MODIFIED — add CustomItem/Entry methods
│   ├── SqliteDataService.cs                # MODIFIED — implement new methods, migration v3
│   ├── ExportService.cs                    # MODIFIED — add customItems / customItemEntries sections
│   └── Entities/
│       ├── CustomItemEntity.cs             # NEW
│       └── CustomItemEntryEntity.cs        # NEW
├── ViewModels/
│   ├── SettingsViewModel.cs                # MODIFIED — custom-items collection + CRUD commands
│   ├── CustomItemEditorViewModel.cs        # NEW — add/edit dialog backing
│   └── DiaryViewModel.cs                   # MODIFIED — load custom entries into separate collection
├── Views/
│   ├── SettingsView.xaml(.cs)              # MODIFIED — new "Custom Items" expander section
│   ├── DiaryView.xaml(.cs)                 # MODIFIED — second list rendering "Custom" section header + items
│   ├── CustomItemEditorDialog.xaml(.cs)    # NEW — ContentDialog with name / servings / icon picker
│   └── IconPickerControl.xaml(.cs)         # NEW — two-source picker: built-in catalog grid + emoji input
├── Assets/Icons/CustomItems/               # NEW — 10–20 PNGs (vitamin, walk, water, sleep, …) + default.png
└── Strings/
    ├── en/Resources.resw                   # MODIFIED — add SettingsView_CustomItems_*, DiaryView_CustomSection*, CustomItemEditor_*
    └── (each of 20 other locales)/Resources.resw  # MODIFIED — same keys translated
```

**Structure Decision**: Single-project Uno layout retained. All changes localized to `src/DailyPlants/` with file additions following the existing Models / Services / Entities / ViewModels / Views split. Built-in icon set lives at `Assets/Icons/CustomItems/` to keep it visually and logically separate from the official `Assets/Icons/Items/` set.

## Complexity Tracking

> No constitutional violations to justify. Section intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| _(none)_  | _(none)_   | _(none)_                            |

# Changelog

All notable changes to Daily Plants will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

Checklist content re-checked against NutritionFacts.org and the official Daily Dozen apps.

### Changed
- Vitamin B12 no longer counts toward the daily total, perfect days or streaks, as in the
  official apps. It stays on the list and past entries are kept. Days that only missed B12
  now count as complete, so streaks can grow but never shrink.
- Stay Hydrated is its own row again instead of a sixth glass on Beverages. Past days keep
  their counts and stay complete or incomplete exactly as before.
- Item details now show safety guidance and more serving options.

### Fixed
- Exercise Timing no longer suggests "afternoon", and now covers diabetes and prediabetes.
- Added missing safety guidance for B12 over 65, vinegar, hydration, fluid limits and mild
  Trendelenburg.
- Added cayenne as the alternative to ginger, and the real rules for time-restricted eating
  and the nightly fast.
- Corrected the metric cruciferous serving, and Italian, Polish and Ukrainian imperial servings
  that showed metric units.
- Replaced four dead NutritionFacts.org links and added links to the tweaks that had none.
- Reworded health claims the sources do not support as written.

## [0.5.0] - 2026-04-13

First feature update since the initial 0.1 public release. Focuses on more
flexible tracking, broader localization, and a smoother Windows install.

### Added
- Metric and imperial unit support for items with measurable servings.
- Ability to disable individual checklist items you don't want to track.
- Additional UI and checklist localizations.
- Self-contained WinAppSDK packaging on Windows — the app no longer depends
  on a separately installed Windows App Runtime.

### Changed
- Smart merging of overlapping items across the Daily Dozen, Twenty-One Tweaks,
  and Anti-Aging Eight checklists, so a single serving can satisfy multiple
  programs without double-counting.
- Aligned item names and ordering across the three checklists for a more
  consistent experience when combining them.

### Fixed
- Version number resolution in packaged builds.

## [0.1.66] - Initial public release

Initial Microsoft Store release of Daily Plants — daily nutrition tracking
based on Dr. Michael Greger's Daily Dozen, Twenty-One Tweaks, and
Anti-Aging Eight, with streaks, achievements, weight tracking, and full
local-only storage.

---

## Release Notes Format

Each release includes:
- **Added** — New features
- **Changed** — Changes to existing functionality
- **Deprecated** — Features to be removed in future versions
- **Removed** — Features removed in this version
- **Fixed** — Bug fixes
- **Security** — Security-related changes

[0.5.0]: https://github.com/MartinZikmund/daily-plants/releases/tag/v0.5.0
[0.1.66]: https://github.com/MartinZikmund/daily-plants/releases/tag/v0.1.66

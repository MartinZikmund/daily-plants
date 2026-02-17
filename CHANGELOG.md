# Changelog

All notable changes to Daily Dozen will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Comprehensive documentation (README, CONTRIBUTING, SECURITY)
- GitHub issue and PR templates
- TODO tracking for app store release preparation

### Changed
- Updated README with full build instructions and architecture overview

## [1.0.0] - 2024-XX-XX

### Added
- **Core Tracking**
  - Daily Dozen checklist (12 food groups from "How Not to Die")
  - Twenty-One Tweaks checklist (weight-loss accelerators from "How Not to Diet")
  - Anti-Aging Eight checklist (longevity nutrition from "How Not to Age")
  - Smart merge of overlapping items across checklists
  - Date navigation to view and edit past entries

- **Statistics Dashboard**
  - Current streak tracking
  - Longest streak records
  - Historical completion trends
  - Per-item statistics

- **Weight Tracking**
  - Optional weight logging
  - Weight history view
  - Metric and imperial unit support

- **Achievement System**
  - Milestone achievements for consistent tracking
  - Achievement notifications
  - Achievement history view

- **Data Management**
  - Export to JSON format
  - Export to CSV format
  - Import from JSON backup
  - Date range filtering for exports

- **Customization**
  - Light theme
  - Dark theme
  - System theme (follows OS setting)
  - Enable/disable individual checklists

- **Localization**
  - English language support
  - Czech language support

- **Cross-Platform Support**
  - Windows (WinUI)
  - Android
  - iOS
  - macOS
  - Linux (Desktop/GTK)
  - WebAssembly

### Technical
- Built with Uno Platform 6.x
- .NET 9.0 target framework
- SQLite local database storage
- MVVM architecture with CommunityToolkit.Mvvm
- Fluent Design System UI

---

## Version History Summary

| Version | Date | Highlights |
|---------|------|------------|
| 1.0.0 | TBD | Initial public release |

---

## Release Notes Format

Each release includes:
- **Added** - New features
- **Changed** - Changes to existing functionality
- **Deprecated** - Features to be removed in future versions
- **Removed** - Features removed in this version
- **Fixed** - Bug fixes
- **Security** - Security-related changes

[Unreleased]: https://github.com/user/daily-dozen/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/user/daily-dozen/releases/tag/v1.0.0

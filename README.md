# Daily Plants

A cross-platform nutrition tracking app based on Dr. Michael Greger's evidence-based nutrition recommendations from his books *How Not to Die*, *How Not to Diet*, and *How Not to Age*.

[![CI](https://github.com/user/daily-dozen/actions/workflows/ci.yml/badge.svg)](https://github.com/user/daily-dozen/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

<!--
[![Google Play](https://img.shields.io/badge/Google_Play-Download-green?logo=google-play)](https://play.google.com/store/apps/details?id=dev.mzikmund.dailyplants)
[![App Store](https://img.shields.io/badge/App_Store-Download-blue?logo=apple)](https://apps.apple.com/app/daily-plants)
[![Microsoft Store](https://img.shields.io/badge/Microsoft_Store-Download-blue?logo=microsoft)](https://apps.microsoft.com/store/detail/daily-plants)
-->

![Daily Plants app showing nutrition checklist tracking](assets/screenshot.jpg)

## About

Daily Plants helps you track your daily nutrition goals with three evidence-based checklists:

- **Daily Dozen** - 12 food groups to include every day from *How Not to Die*
- **Twenty-One Tweaks** - Weight-loss accelerators from *How Not to Diet*
- **Anti-Aging Eight** - Longevity-focused nutrition from *How Not to Age*

All data is stored locally on your device. No account required, no tracking, no ads.

## Features

- Track servings across three nutrition checklists
- Smart merge of overlapping items across checklists
- Date navigation to review and edit past entries
- Statistics dashboard with streaks and trends
- Optional weight tracking
- Achievement system for motivation
- Export/import data (JSON and CSV formats)
- Light, dark, and system theme support
- Multi-language support (English, Czech)
- Works offline - all data stored locally

## Supported Platforms

| Platform | Status |
|----------|--------|
| Windows | Supported |
| Android | Supported |
| iOS | Supported |
| macOS | Supported |
| Linux | Supported |
| WebAssembly | Supported |

## Building from Source

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later
- [Visual Studio 2022](https://visualstudio.microsoft.com/) (17.8+) with:
  - .NET Multi-platform App UI development workload
  - Or: [Uno Platform extension](https://marketplace.visualstudio.com/items?itemName=unoplatform.uno-platform-addin-2022)
- For Android: Android SDK (API 21+)
- For iOS/macOS: macOS with Xcode 15+
- For WebAssembly: No additional requirements

### Clone the Repository

```bash
git clone https://github.com/user/daily-dozen.git
cd daily-dozen
```

### Build and Run

#### Windows

```bash
cd src/DailyPlants
dotnet build -f net10.0-windows10.0.26100
dotnet run -f net10.0-windows10.0.26100
```

#### Android

```bash
cd src/DailyPlants
dotnet build -f net10.0-android
# Deploy to connected device or emulator
dotnet build -f net10.0-android -t:Install
```

#### iOS (macOS required)

```bash
cd src/DailyPlants
dotnet build -f net10.0-ios
```

#### WebAssembly

```bash
cd src/DailyPlants
dotnet build -f net10.0-browserwasm
dotnet run -f net10.0-browserwasm
```

#### Desktop (Skia/GTK)

```bash
cd src/DailyPlants
dotnet build -f net10.0-desktop
dotnet run -f net10.0-desktop
```

### Using Visual Studio

1. Open `DailyPlants.slnx`
2. Select your target framework from the dropdown
3. Press F5 to build and run

## Architecture

The app is built with [Uno Platform](https://platform.uno/) using modern .NET patterns:

```
src/DailyPlants/
├── Models/           # Data models (ChecklistItem, DailyEntry, etc.)
├── ViewModels/       # MVVM ViewModels using CommunityToolkit.Mvvm
├── Views/            # XAML pages and controls
├── Services/         # Business logic and data access
├── Controls/         # Reusable XAML controls
├── Converters/       # XAML value converters
├── Helpers/          # Utility classes
├── Strings/          # Localization resources
└── Platforms/        # Platform-specific code
```

### Key Technologies

- **Uno Platform** - Cross-platform UI framework
- **WinUI/Fluent Design** - UI design system
- **CommunityToolkit.Mvvm** - MVVM implementation
- **Microsoft.Data.Sqlite** - Local database
- **Microsoft.Extensions.Hosting** - Dependency injection

## Data Storage

All data is stored locally using SQLite:

- **Windows**: `%LOCALAPPDATA%\DailyPlants\dailyplants.db`
- **Android**: App's private storage
- **iOS**: App's Documents directory
- **macOS/Linux**: `~/.local/share/DailyPlants/dailyplants.db`

## Contributing

Contributions are welcome! Please read our [Contributing Guidelines](CONTRIBUTING.md) before submitting a pull request.

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for a history of notable changes to this project.

## Security

For security concerns, please see our [Security Policy](SECURITY.md).

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details. Third-party license notices are available in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Acknowledgments

- [Dr. Michael Greger](https://nutritionfacts.org/) for the Daily Dozen, Twenty-One Tweaks, and Anti-Aging Eight concepts
- [NutritionFacts.org](https://nutritionfacts.org/) for evidence-based nutrition information
- [Uno Platform](https://platform.uno/) for the cross-platform framework
- The open-source community for the amazing tools and libraries

## Disclaimer

This app is not affiliated with or endorsed by Dr. Michael Greger or NutritionFacts.org. The nutrition information is based on publicly available content from the referenced books and website. This app is not a substitute for professional medical advice.

---

Made with Uno Platform

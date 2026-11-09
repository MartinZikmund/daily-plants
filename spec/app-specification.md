# Daily Dozen App Specification

## Overview

A cross-platform Uno Platform app for tracking daily nutrition based on Dr. Michael Greger's recommendations from his books. The app supports multiple checklists and provides statistics to help users maintain healthy habits.

---

## Target Platforms

- Windows (WinUI)
- Android
- iOS
- WebAssembly
- macOS
- Linux

All platforms will be supported from initial release using Uno Platform's Skia renderer.

---

## Checklists

The authoritative wording, serving sizes and counts are in the official Daily Dozen apps
([iOS](https://github.com/nutritionfactsorg/daily-dozen-ios), [Android](https://github.com/nutritionfactsorg/daily-dozen-android))
and on [NutritionFacts.org](https://nutritionfacts.org/daily-dozen/). `ChecklistContentTests` pins the counts and safety text.

### 1. Daily Dozen (How Not to Die)

| Item | Daily Servings | Serving Size Example |
|------|----------------|---------------------|
| Beans | 3 | ½ cup cooked or ¼ cup hummus |
| Berries | 1 | ½ cup fresh/frozen or ¼ cup dried |
| Other Fruits | 3 | 1 medium fruit or ¼ cup dried |
| Greens | 2 | 1 cup raw or ½ cup cooked |
| Cruciferous Vegetables | 1 | ½ cup chopped or 1 tbsp horseradish |
| Other Vegetables | 2 | ½ cup non-leafy vegetables |
| Flaxseed | 1 | 1 tbsp ground |
| Nuts and Seeds | 1 | ¼ cup nuts or seeds, or 2 tbsp nut or seed butter |
| Herbs and Spices | 1 | ¼ tsp turmeric |
| Whole Grains | 3 | ½ cup hot cereal or 1 slice whole grain bread |
| Beverages | 5 | 12 oz water, tea, or coffee |
| Exercise | 1 | 90 min moderate or 40 min vigorous |

Vitamin B12 (at least 2,000 mcg weekly or 50 mcg daily; at least 1,000 mcg daily over 65) is listed alongside
these for convenience. As in the official apps, it does not count toward the daily total, perfect days or streaks.

### 2. Twenty-One Tweaks (How Not to Diet)

| Tweak | Daily checks |
|-------|--------------|
| **At each meal** | |
| Preload with water | 3 |
| Preload with "negative calorie" foods | 3 |
| Incorporate vinegar (2 tsp) | 3 |
| Enjoy undistracted meals | 3 |
| Follow the twenty-minute rule | 3 |
| **Every day** | |
| Black cumin (¼ tsp) | 1 |
| Garlic powder (¼ tsp) | 1 |
| Ground ginger (1 tsp) or cayenne (½ tsp) | 1 |
| Nutritional yeast (2 tsp) | 1 |
| Cumin (½ tsp with lunch and dinner) | 2 |
| Green tea (3 cups) | 3 |
| Stay hydrated | 1 |
| Deflour your diet | 1 |
| Front-load your calories | 1 |
| Time-restrict your eating | 1 |
| Optimize exercise timing | 1 |
| Weigh yourself twice a day | 2 |
| Complete your implementation intentions | 3 |
| **Every night** | |
| Fast after 7:00 PM | 1 |
| Get sufficient sleep | 1 |
| Experiment with mild Trendelenburg | 1 |

The Anti-Aging Eight from *How Not to Age* is not a checklist in the app: most of it (caloric restriction,
xenohormesis, NAD+ and so on) is a strategy rather than something to tick off each day.

---

## User Experience

### Checklist Selection

- Users can enable/disable each checklist independently
- Default: Daily Dozen enabled
- Settings page to manage active checklists

### Items in More Than One Checklist

Nothing is merged. Beverages and Stay Hydrated look alike but measure different things: glasses drunk versus
urine colour. Each checklist item has its own row and its own stored count. Rows written by versions that did
merge them (a sixth glass stored as `stay_hydrated`) read correctly as they are, so no migration is involved.

### Main Tracking View

```
┌─────────────────────────────────────┐
│  ←  January 11, 2026  (Today)   →   │
├─────────────────────────────────────┤
│  [Daily Dozen]  [21 Tweaks]         │  (tabs/pivot)
├─────────────────────────────────────┤
│                                     │
│  Beans             ●●●○○○  3/3      │
│  Berries           ●○○○○○  1/1      │
│  Other Fruits      ●●○○○○  2/3      │
│  Greens            ●●○○○○  2/2      │
│  Cruciferous       ○○○○○○  0/1      │
│  ...                                │
│                                     │
│  ─── Progress: 65% ────────────     │
│                                     │
└─────────────────────────────────────┘
```

### Date Navigation

- Current date displayed prominently at top
- Left arrow: Go to previous day
- Right arrow: Go to next day (disabled if already on today)
- Tapping date opens calendar picker for quick navigation
- All historical entries are editable

### Item Detail View

When tapping an item:
- Full description and health benefits
- Serving size examples with images (if available)
- Quick +/- buttons for serving count
- Link to NutritionFacts.org for more information

---

## Statistics Dashboard

### Overview Stats

- **Today's Progress**: Completion percentage per checklist
- **This Week**: Average daily completion
- **This Month**: Average daily completion

### Streaks

- Current streak (consecutive days with 100% completion)
- Longest streak ever
- Per-item streaks (e.g., "30-day beans streak")

### Historical Trends

- Line/bar charts showing completion over time
- Filter by: Week, Month, Year, All Time
- Per-checklist breakdown
- Highlight best/worst performing items

### Weight Tracking (if enabled)

- Weight entry with date
- Trend chart (line graph)
- BMI calculation (optional, requires height input)
- Goal weight tracking

---

## Data Management

### Local Storage

- SQLite database for all tracking data
- Schema:
  - `DailyEntries` - date, checklist_id, item_id, servings_completed
  - `WeightEntries` - date, weight, notes
  - `UserSettings` - enabled_checklists, theme, units, etc.

### Export

- **JSON**: Full structured export, preserves all data
- **CSV**: Spreadsheet-compatible, one row per day
- Export options:
  - All data
  - Date range selection
  - Specific checklist only

### Import

- **JSON**: Restore from previous export
- **CSV**: Import historical data
- Merge strategy: Ask user when duplicates found
- Validation before import with preview

---

## Design System

### Theme

- Base: Fluent Design (WinUI default)
- Primary color: Green (`#4CAF50` or similar health/nature green)
- Secondary: Light green for accents
- Support both light and dark modes

### Typography

- Use Fluent type ramp
- Clear hierarchy for checklist items
- Readable serving counts

### Layout

- Responsive design for all screen sizes
- Phone: Single column, bottom navigation
- Tablet/Desktop: Optional side navigation, wider content area

---

## Navigation Structure

```
Bottom Navigation (Mobile) / NavigationView (Desktop)
│
├── Today (Main tracking view)
│   └── Date picker + checklists
│
├── Statistics
│   ├── Overview
│   ├── Streaks
│   ├── Trends
│   └── Weight (if enabled)
│
├── Settings
│   ├── Active Checklists
│   ├── Weight Tracking toggle
│   ├── Units (metric/imperial)
│   ├── Theme (light/dark/system)
│   ├── Export Data
│   └── Import Data
│
└── About
    ├── App info
    └── Links to NutritionFacts.org
```

---

## Technical Architecture

### Project Structure

```
DailyPlants/
├── Models/
│   ├── ChecklistItem.cs
│   ├── DailyEntry.cs
│   ├── WeightEntry.cs
│   └── UserSettings.cs
├── ViewModels/
│   ├── MainViewModel.cs
│   ├── TodayViewModel.cs
│   ├── StatisticsViewModel.cs
│   └── SettingsViewModel.cs
├── Views/
│   ├── TodayPage.xaml
│   ├── StatisticsPage.xaml
│   ├── SettingsPage.xaml
│   └── AboutPage.xaml
├── Services/
│   ├── IDataService.cs
│   ├── SqliteDataService.cs
│   ├── IExportService.cs
│   ├── ExportService.cs
│   └── ChecklistDefinitions.cs
├── Controls/
│   ├── ChecklistItemControl.xaml
│   ├── ServingIndicator.xaml
│   └── DateNavigator.xaml
└── Themes/
    └── ColorPaletteOverride.xaml
```

### Dependencies to Add

- `Microsoft.Data.Sqlite` - SQLite database
- `LiveChartsCore.SkiaSharpView.Uno.WinUI` - Charts (or similar)
- Consider: `Uno.Toolkit.UI` for enhanced controls

### UnoFeatures to Add

```xml
<UnoFeatures>
  Hosting;
  Mvvm;
  SkiaRenderer;
  Toolkit;         <!-- Add for enhanced controls -->
</UnoFeatures>
```

**Note:** Using Fluent design (WinUI default), NOT Material. The Toolkit provides additional controls while maintaining Fluent styling.

---

## Future Considerations (Not in v1)

- Push notifications/reminders
- Home screen widgets
- Cloud sync (optional account)
- Social sharing (share streaks)
- Apple Health / Google Fit integration
- Barcode scanning for food logging
- Meal planning suggestions

---

## Data Sources

- Daily Dozen: [NutritionFacts.org Daily Dozen](https://nutritionfacts.org/daily-dozen/)
- 21 Tweaks: "How Not to Diet" book and the official apps
- [iOS](https://github.com/nutritionfactsorg/daily-dozen-ios) and [Android](https://github.com/nutritionfactsorg/daily-dozen-android) app sources for reference

---

## Implementation Phases

### Phase 1: Foundation
- Set up project structure and navigation
- Create data models and SQLite service
- Implement checklist definitions

### Phase 2: Core Tracking
- Today page with date navigation
- Checklist item display and editing
- Basic daily entry persistence

### Phase 3: Statistics
- Statistics dashboard
- Streaks calculation
- Charts and trends

### Phase 4: Weight & Polish
- Weight tracking feature
- Item detail views with education
- Export/Import functionality

### Phase 5: Refinement
- Theming and visual polish
- Performance optimization
- Testing across all platforms

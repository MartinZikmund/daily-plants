# Windowsill — the Daily Plants design system

The resolved visual identity for Daily Plants. This document is the contract:
implement against these tokens and component specs rather than re-deriving them.

Direction study artifacts (visual reference, not normative):

- [Lookbook — three directions](https://claude.ai/code/artifact/420e0f4c-47e3-4a2d-b83e-f8a98f423b01)
- [Windowsill spins — how far colour travels](https://claude.ai/code/artifact/e3ce6b3d-98db-4566-aac0-7a70aaf71f90)
- [The list problem + typography](https://claude.ai/code/artifact/85299c83-cced-4913-9973-8847b28306ea)
- [Dark, Statistics, Achievements, celebration](https://claude.ai/code/artifact/929e1cd7-2abe-47d1-950a-b9fabd7fbb0e)
- [The design system](https://claude.ai/code/artifact/52b624db-76cf-40ec-a7f9-1c623ac6b276)

---

## The idea in one line

A cookbook you have owned for ten years. Warm linen ground, hairline rules
instead of cards, a page that holds still. The only saturated colour on screen
comes from the food itself.

---

## Colour

Defined in `Styles/Colors.xaml`. Nine tokens, each with one meaning. Never
introduce a tenth without deciding what it means.

| Brush | Light | Dark | Used for |
|---|---|---|---|
| `DpGroundBrush` | `#FBFAF5` | `#191A15` | The page. Linen, not white. |
| `DpSurfaceBrush` | `#F3F1E9` | `#20221B` | Title bar, nav rail, raised panels. |
| `DpHairlineBrush` | `#E6E3D8` | `#2E3128` | Row rules, group dividers, chart grid. |
| `DpInkBrush` | `#23271F` | `#E9EAE1` | Item names, headings, numbers. |
| `DpInkMutedBrush` | `#767C6C` | `#959B8A` | Serving sizes, captions, axis labels. |
| `DpGreenBrush` | `#4C7A56` | `#82B189` | Filled dots, progress, accent fills. |
| `DpGreenInkBrush` | `#3A6244` | `#A3CBA8` | Green **text**. Never use `DpGreenBrush` for type. |
| `DpClayBrush` | `#B9724C` | `#D19670` | Misses, goal lines, "below half". Reserved. |
| `DpParadeBrush` | `#3A6244` | `#2A4A33` | The inverted day card, once daily. |

Rules:

- Reference brushes with `{ThemeResource}` at usage sites, never `{StaticResource}`.
  `{StaticResource}` does not update on theme change.
- `Colors.xaml` also overrides `SystemAccentColor` and its six variants, so stock
  WinUI controls inherit the palette for free. Prefer that over restyling controls.
- All three theme dictionary keys exist: `Light`, `Dark`, `HighContrast`.
  Never `x:Key="Default"`.
- The `HighContrast` dictionary redirects to system colour brushes only. No
  hardcoded colours, no accents, no opacity.

### Bugs this replaces

- `DiaryView.xaml` — `Foreground="Black"` on the increment glyph, in **both**
  data templates. Invisible in dark.
- `StatisticsView.xaml` — `Foreground="Gold"` on the longest-streak icon. Fails
  High Contrast.

---

## Typography

Defined in `Styles/Typography.xaml`. Fraunces for four display sizes, Karla for
everything else. Both ship as `Content` under `Assets/Fonts/`.

| Style | Face | Used for |
|---|---|---|
| `DpDisplayTextBlockStyle` | Fraunces 600 | The parade headline, and nothing else. |
| `DpTitleTextBlockStyle` | Fraunces 600 | Page titles, the date. |
| `DpSubtitleTextBlockStyle` | Fraunces 600 | Panel headings, stat values. |
| `DpTallyTextBlockStyle` | Fraunces 600 | "14 of 21". Green ink. |
| `DpBodyStrongTextBlockStyle` | Karla 600 | Item names. |
| `DpBodyTextBlockStyle` | Karla 400 | Running text, buttons. |
| `DpCaptionTextBlockStyle` | Karla 400 | Serving sizes, axis labels. Muted ink. |
| `DpOverlineTextBlockStyle` | Karla 600, 10px, `CharacterSpacing="140"` | Group headers. |

Rules:

- `BasedOn` styles override `FontFamily` only. Never re-declare an inherited size
  or weight.
- WinUI has no tabular-figures setter. Anywhere digits count up or stack — the
  tally, stat values, chart labels — set a `MinWidth` at the usage site so the
  layout does not shuffle as glyph widths change.
- Minimum size is 12px. `SemiBold`, never `Bold`.

---

## Shape, space, motion

**Shape.** One radius: 8px, via `ControlCornerRadius`. Rows are *not* cards —
a 1px `DpHairlineBrush` rule separates them. Two deliberate exceptions: circles
for serving dots and the + button, and `OverlayCornerRadius` for flyouts and
dialogs, left at whatever WinUI says.

**Space.** 4px grid, no exceptions. Row padding `2,12`. Group header 16px above,
8px below. Canvas padding 20px. Two-column gap 24px. Use `RowSpacing` /
`ColumnSpacing`, never spacer elements. Use `MinHeight`, not fixed heights.

**Motion.**

- A dot fills in **180ms**. That is the only animation on an ordinary tap.
- Completing an item moves it from "Still to go" to "Done today": hold in place
  **400ms**, then animate the move over **300ms**. Rows must never teleport under
  a finger.
- The parade staggers 21 plate squares at **30ms** each.
- Under reduced motion: no confetti, no stagger, no count-up. Cut to the final
  state, and regroup rows on next navigation instead of animating.

---

## Components

### Serving row

`28px` icon · name + serving size · dots + add button.

- Icon: Icons8 **Fluent** SVG at 28×28.
- Name: `DpBodyStrongTextBlockStyle`.
- Serving size: `DpCaptionTextBlockStyle`, one line, `CharacterEllipsis`.
- Dots: one per recommended serving, 10px circle, 1.4px `DpGreenBrush` border,
  filled `DpGreenBrush` when earned.
- Add: 25px circle, 1.4px `DpGreenBrush` border, `+` glyph in `DpGreenBrush`.
- Completed rows in the "Done today" group render at 48% opacity.

Every icon-only button needs `AutomationProperties.Name`. Tooltips are not names.

### Group header

`DpOverlineTextBlockStyle` label · 1px hairline rule filling remaining width ·
count in `DpGreenInkBrush`. Two groups on the Diary: **Still to go** and
**Done today**. "Done today" is collapsible.

### Tally

`DpTallyTextBlockStyle` reading "14 of 21", then a 3px `DpHairlineBrush` track
with a `DpGreenBrush` fill. Bottom border is a hairline rule.

### Two-column layout

Each group lays its rows out in two columns with a 24px gap on wide windows,
one column below that. `ContentFrame`'s cap is raised to `MaxWidth="1400"`.

**The breakpoint is 1300, and it is window width, not content width.** An
earlier 900 was measured wrong and clipped the serving controls off the right
edge: two columns need `2 × 380 + 24` of *content*, but the nav pane (~320),
frame margins (112) and canvas padding (40) come off the window first. Verified
empirically at 933 DIPs (one column, nothing clipped) and 1600 DIPs (two
columns). If the row template or `TwoColumnMinItemWidth` changes, re-derive it.

### Parade

Fires once per day, the first time every serving is complete.

`DpParadeBrush` card, `DpParadeInkBrush` text, `DpDisplayTextBlockStyle`
headline, 21 plate squares staggered in, confetti drifting once. Any badge
earned appears as a toast beneath. Needs a settings toggle.

---

## Assets

- **Item icons** — Icons8 **Fluent** (flat gradient SVG), replacing the 3D
  Fluency raster set. **All 39 are exported** to `Assets/Icons/Items/Fluent/`.
  Audited: no duplicate artwork, no gradient-id collisions across files, no `~`
  characters in ids (fragile in XAML resource pipelines), no dangling `url(#…)`
  references.

  Not yet wired up — `ChecklistDefinitions.IconPath` still points at the `.png`
  files. Flipping needs `BitmapIcon` swapped for `Image` + `SvgImageSource`,
  since `BitmapIcon` does not render SVG.

  **Six are approximations**, because Icons8 Fluent has no literal match. Worth
  a review pass before release:

  | Item | Shown as | Why |
  |---|---|---|
  | `black_cumin` | Black pepper | No nigella/black-seed icon exists |
  | `cumin` | Curry bowl | No cumin icon exists |
  | `nutritional_yeast` | Salt shaker | No yeast icon; sprinkling reads right |
  | `nightly_trendelenburg` | Occupied bed | Nothing depicts elevated legs |
  | `undistracted_meals` | Do-not-disturb | Closest available "focus" metaphor |
  | `exercise_timing` | Runner | Loses the clock; reads the same as `exercise` |

  **A caution for anyone re-running the export:** the Icons8 MCP's
  `get_icon_svg` returned artwork for the *wrong* icon id under concurrent load.
  Three files (`garlic_powder`, `ground_ginger`, `more_berries`) were written
  cross-assigned — a bottle, a pea pod and a pear — and none of the exporting
  agents caught it. It was only found by rendering every icon side by side and
  looking. Always verify visually, and prefer sequential fetches.
- **Achievement badges** — 23, still the old 3D set. They need the same Fluent
  pass or the two sets visibly disagree.
- **Fonts** — in place at `Assets/Fonts/`. Both are **variable** fonts, so a single file
  carries every weight and `FontWeight="SemiBold"` resolves within the family.
  Verified internal family names are `Fraunces` and `Karla`, matching the `#Family`
  references in `Typography.xaml`.
  Not yet subset: Fraunces is 384 KB and Karla 94 KB. That is a real cost on the
  WASM head and should be trimmed to Latin + the fractions used in serving sizes
  (½ ¼ ⅓) before release.
- **App icon** — deferred until the in-app design is finished.

### Licensing — unresolved

`THIRD-PARTY-NOTICES.md` credits only Segoe Fluent Icons. The Icons8 artwork —
39 items, 23 badges, and `Assets/Svg/applogo.svg` — is not credited anywhere.
Icons8's free tier requires a visible attribution link. Settle before store
submission.

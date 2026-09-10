# TeachingTip learning flow

**Date:** 2026-09-10
**Status:** Approved design, not yet implemented
**Issue:** Variant of #45 (First-run onboarding tutorial)

## Problem

Daily Plants opens on a Diary page that assumes you already know three things: that
tapping a serving logs it, that the bar across the top is the day rather than the week,
and that the date header moves. None of that is signposted. Issue #45 proposes a
first-run tutorial; a carousel of screens shown before the user has touched anything
teaches in the abstract, and is skipped by most of the people who most need it.

Instead: coach marks on the real UI, using `TeachingTip`, taught where the behaviour
lives.

## Goals

- A new user learns how to log a serving and where to read their day, on first launch.
- A returning user with a gap in their history discovers that past days are editable, at
  the moment that fact is useful to them.
- The flow is replayable on demand, for demos and for anyone who skipped it.
- Adding a fourth tip later costs one enum entry, one XAML block and a set of strings.

## Non-goals

- No separate onboarding page, carousel, or route. Nothing new in the navigation.
- No tip explaining merged checklist items, streak semantics, or the resources feed.
  These were considered and deliberately cut; see Future tips.
- No remote configuration, no analytics on tip engagement.

## Shape

Hybrid. A two-step tour on the Diary at first launch covers the basics; anything else is
contextual and fires when it becomes relevant. The tour is short enough that nobody feels
held hostage, and the contextual tip arrives with a reason to exist.

## Tip inventory

| Id | Kind | Target | Trigger | Buttons |
|----|------|--------|---------|---------|
| `diary-log-serving` | Tour 1/2 | First realized `ChecklistItemControl` in `StillToGoRepeater` | First Diary load with the tip unseen | Next / Skip |
| `diary-day-progress` | Tour 2/2 | `TallyTrack` | Next on tour step 1 | Got it |
| `diary-past-days` | Contextual | `DatePickerButton` | Diary load, tip unseen, tour not pending, and the most recent entry is older than yesterday | Got it |

Ids are stable strings, persisted as strings. The `TipId` enum's ordinals are never
written to storage, so reordering or inserting enum members cannot shuffle what a user
has already been shown.

## Architecture

### `Services/Tips/`

- **`TipId`** — enum, with an extension mapping each member to its stable string id.
- **`ITipService`** — `bool ShouldShow(TipId id)`, `void MarkSeen(params TipId[] ids)`,
  `void Reset()`.
- **`TipService`** — implements the above over `IAppPreferences.SeenTips`.

### Preference

One new member on `IAppPreferences`:

```csharp
/// <summary>
/// Comma-separated ids of the teaching tips already shown. One value rather than a flag
/// per tip, so a new tip does not mean a new preference key.
/// </summary>
string SeenTips { get; set; }
```

Stored through the existing `_preferences.Get`/`Set` pair in `AppPreferences`, following
`DisabledItemIds`. The settings doubles in the test project gain the same member.

### Data

No new `IDataService` member. The design originally proposed a `GetMostRecentEntryDateAsync`
returning `DateOnly?`; `GetDatesWithEntriesAsync` already answers the question, so adding a
second way to ask it would have been duplication for no gain. The call only happens while
the contextual tip is still unseen, so its cost stops the moment the tip has been shown.

### Sequencing

Sequencing lives in `DiaryViewModel`, not in the view:

- `ActiveTip` — `TipId?`, observable. Null means no tip is showing.
- `TipNextCommand` — marks `ActiveTip` seen and advances to the next tour step, or clears.
- `TipSkipCommand` — marks every remaining tour tip seen and clears `ActiveTip`.
- `EvaluateTipsAsync(bool canPointAtARow = true)` — called on Diary load. Picks the tip to
  show, if any. The view passes whether a checklist row is realized, because the ViewModel
  cannot see the visual tree and step one has nothing to point at without one.

`DiaryView.xaml` holds three `TeachingTip` elements. Each binds `IsOpen` to whether
`ActiveTip` matches its id, and contributes only its target, title, subtitle and buttons.
The view carries no ordering logic, which is what makes the flow testable without UI.

All three set `IsLightDismissEnabled="False"`, so a stray tap on the page background
cannot silently consume a tip.

### Suppression rules

1. Contextual tips never fire while any tour tip is unseen. The tour drains first, in a
   later session if the user closed the app part way through.
2. Evaluation runs exactly once per Diary load. `DayCompleteParade` and
   `AchievementNotification` are both raised by in-session actions taken *after* that
   load, so a tip and a celebration cannot compete for the screen without extra
   machinery to prevent it. If the two are ever seen to collide in practice — most
   likely returning to the Diary while a Shell-level achievement toast is still
   animating — add the gate then, rather than building it on speculation.
3. At most one tip is open at a time, enforced by `ActiveTip` being a single value.

### Skip semantics

Skip ends the tour and nothing more. It marks `diary-log-serving` and
`diary-day-progress` seen; `diary-past-days` stays eligible and still fires when it
becomes relevant. Skip is read as "not now, let me look around", which is what people
mean by it, and the contextual tip is the one most worth preserving.

### Replay

Settings gains a "Show tips again" button calling `ITipService.Reset()`, which clears
`SeenTips`. The tour replays on the next Diary load. This exists as much for demos and
talks as for users.

## Error handling

- **Malformed `SeenTips`.** Parsed defensively: blank and whitespace segments are ignored,
  and ids that do not map to a known `TipId` are *preserved* on write rather than dropped,
  so running an older build does not replay tips a newer build already showed.
- **Missing target.** If no checklist row has realized, the view calls evaluation with
  `canPointAtARow: false`; the tour sits that load out and is *not* marked seen. Tour step
  two and the contextual tip target elements that are always present, so neither depends
  on realization.
- **Data failure.** If `GetMostRecentEntryDateAsync` throws, the contextual tip is skipped
  for that session and left unseen. A teaching tip is never worth surfacing an error over.

## Localization

Eleven new keys, translated into all 21 locales:

`Tip_LogServing_Title`, `Tip_LogServing_Subtitle`, `Tip_DayProgress_Title`,
`Tip_DayProgress_Subtitle`, `Tip_PastDays_Title`, `Tip_PastDays_Subtitle`, `Tip_Next`,
`Tip_Skip`, `Tip_GotIt`, `Settings_ShowTipsAgain`, `Settings_ShowTipsAgainDescription`.

## Files touched

- `Services/Tips/TipId.cs`, `ITipService.cs`, `TipService.cs` — new
- `Services/Settings/IAppPreferences.cs`, `AppPreferences.cs` — `SeenTips`
- `ViewModels/DiaryViewModel.cs` — `ActiveTip`, commands, evaluation
- `Views/DiaryView.xaml` — three `TeachingTip` elements
- `ViewModels/SettingsViewModel.cs`, `Views/SettingsView.xaml` — replay button
- `App.xaml.cs` — DI registration for `ITipService`
- `Strings/*/Resources.resw` — 21 locales
- Test project — new tests, plus `SeenTips` on the settings doubles

## Testing

Test-driven, with the fix and its test as separate commits per the usual rule.

**`TipService`**

- An unseen id reports `ShouldShow` true; a seen one reports false.
- `MarkSeen` round-trips through the preference and is idempotent.
- Unknown ids already in the stored value survive a `MarkSeen` write.
- Blank and whitespace segments parse without throwing.
- `Reset` clears the value and makes every tip eligible again.

**`DiaryViewModel`**

- With nothing seen, evaluation opens tour step 1.
- `TipNextCommand` on step 1 marks it seen and opens step 2.
- `TipSkipCommand` on step 1 marks both tour tips seen and opens nothing.
- The contextual tip does not open while a tour tip is unseen.
- With the tour complete and the most recent entry older than yesterday, the contextual
  tip opens.
- With the tour complete and an entry for yesterday, nothing opens.
- With everything seen, nothing opens.
- A throwing `GetMostRecentEntryDateAsync` opens nothing and leaves the tip unseen.

**`SettingsViewModel`**

- "Show tips again" makes every tip eligible again, and touches nothing else the user
  configured.

## Risks

1. **`ItemsRepeater` realization timing.** *Resolved by construction rather than by a
   fallback target.* Evaluation is triggered from `ElementPrepared` for the first row of
   `StillToGoRepeater`, which is also where the target is assigned; a load with no rows
   instead evaluates with `canPointAtARow: false`. Either way the tip never opens without
   something to point at. Still worth watching on a real device.
2. **Cross-head rendering.** `TeachingTip` is a full mux port in shared `Uno.UI`, so it is
   present on every head, but its placement and tail behaviour should be smoke-checked on
   WebAssembly and Android early rather than after everything else is built.
3. **Translation volume.** Eleven keys across 21 locales is the bulk of the diff and
   should land as its own commit, as in previous branches.

## Future tips

Cut from this pass, listed so the reasoning is not lost:

- **Merged items count in two lists.** The most confusing behaviour in the app, but it
  needs a trigger tied to `MergeRules` and a target inside a virtualized repeater. Worth
  adding once the realization question above is settled.
- **Streaks count perfect days.** Belongs on Statistics, and only makes sense once a user
  has enough history for a streak to mean anything.
- **The resources feed exists.** Lowest value of the three; the teaser is already visible.

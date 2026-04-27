# Phase 1 Data Model: Custom User-Defined Checklist Items

**Branch**: `001-custom-checklist-items`
**Date**: 2026-04-27

Two new entities are introduced. They live in their own SQLite tables and **never co-mingle with the existing `DailyEntries` table** — that separation is the structural guarantee that achievement / streak / perfect-day logic ignores custom items (research.md → "Achievement isolation").

---

## Entity: `CustomItem`

User-defined daily-tracked item. Visible in Settings (CRUD list) and Diary (Custom section).

### Domain model — `Models/CustomItem.cs` (NEW)

```csharp
namespace DailyPlants.Models;

public partial record CustomItem
{
    /// <summary>
    /// Stable unique identifier (GUID, "N" format — 32 hex chars, no dashes).
    /// Generated once at creation, never changes. Renames preserve this ID
    /// so historical CustomItemEntry rows stay attached.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// User-entered display name. NEVER translated. ≤60 chars.
    /// Duplicates across items allowed (warned but not blocked at save).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Optional user-entered free-form description (e.g., serving notes, motivation).
    /// NEVER translated. ≤500 chars. Empty string means "no description" — UI hides
    /// the description block in that case rather than showing an empty placeholder.
    /// </summary>
    public string Description { get; init; } = "";

    /// <summary>
    /// Number of servings recommended per day. Positive integer.
    /// </summary>
    public required int RecommendedServings { get; init; }

    /// <summary>
    /// Icon source discriminator. Catalog = built-in glyph keyed by IconValue;
    /// Emoji = single emoji grapheme cluster stored verbatim in IconValue.
    /// </summary>
    public required CustomItemIconType IconType { get; init; }

    /// <summary>
    /// When IconType = Catalog: a key from CustomIconCatalog (e.g., "pill", "default").
    /// When IconType = Emoji:   a single emoji grapheme cluster (e.g., "🌱", "👨‍👩‍👧").
    /// </summary>
    public required string IconValue { get; init; }

    /// <summary>
    /// Explicit numeric sort order for diary display.
    /// New items default to (max existing) + 100.
    /// </summary>
    public required int SortOrder { get; init; }

    /// <summary>
    /// UTC timestamp of the last write to this row (ISO 8601 round-trip "O" format).
    /// Set on every insert/update by the data layer — callers do NOT set it.
    /// Forward-compatibility hook for future last-write-wins server sync.
    /// </summary>
    public required DateTime UpdatedAt { get; init; }
}

public enum CustomItemIconType
{
    Catalog = 0,  // default for new items when no choice has been made
    Emoji   = 1,
}
```

### SQLite entity — `Services/Entities/CustomItemEntity.cs` (NEW)

```csharp
[Table("CustomItems")]
internal class CustomItemEntity
{
    [PrimaryKey] // string PK; not auto-increment
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    /// <summary>Optional free-form text, ≤500 chars. Empty string when no description.</summary>
    public string Description { get; set; } = "";

    public int RecommendedServings { get; set; }

    /// <summary>0 = Catalog (default), 1 = Emoji. Persisted as int for forward compatibility.</summary>
    public int IconType { get; set; } = 0;

    /// <summary>Catalog key OR emoji grapheme; interpretation depends on IconType.</summary>
    public string IconValue { get; set; } = "default";

    public int SortOrder { get; set; }

    /// <summary>UTC ISO 8601 ("O" format). Set by the data layer on every write.</summary>
    public string UpdatedAt { get; set; } = "";
}
```

### Validation rules (enforced in `CustomItemService` / `CustomItemEditorViewModel`)

| Rule | Source | Enforcement |
|---|---|---|
| `Name` non-empty after trim | FR-001 | Save button disabled when empty |
| `Name.Length ≤ 60` | FR-010 | Save blocked; inline warning shown |
| `Name` may duplicate another item's `Name` | FR-011 | Non-blocking warning at save time |
| `Description.Length ≤ 500` | FR-001a | Save blocked; inline character counter / warning shown. Empty allowed. |
| `RecommendedServings ≥ 1` | Spec assumption | Save blocked; numeric input clamped |
| `IconType` ∈ {Catalog, Emoji} | research.md | Enum; deserialization clamps unknown ints to Catalog |
| When `IconType = Catalog`: `IconValue` from `CustomIconCatalog.AllKeys` (or `"default"`) | research.md | Editor picker only allows valid keys; unknown keys at import → render `default.png` (no DB rewrite) |
| When `IconType = Emoji`: `IconValue` is a single emoji grapheme cluster | FR-002a | At save time, take `StringInfo.GetTextElementEnumerator(input).MoveNext()`'s first element and verify it contains an Extended_Pictographic codepoint; if not, fall back to `IconType = Catalog`, `IconValue = "default"` |
| `Id` matches `^[0-9a-f]{32}$` | research.md | Generated only by `CustomItemService.CreateAsync`; never user-supplied |
| `UpdatedAt` is set on every write | research.md → "Forward Compatibility" | `SqliteDataService.SaveCustomItem*Async` stamps `DateTime.UtcNow.ToString("O")` regardless of caller-supplied value |

### Lifecycle / state transitions

- **Create**: `Id = Guid.NewGuid().ToString("N")`; `SortOrder = (max in CustomItems) + 100`; row inserted.
- **Edit**: any field except `Id` may change; `Id` is the WHERE-clause anchor.
- **Delete (keep history)**: `DELETE FROM CustomItems WHERE Id = ?`. `CustomItemEntries` rows for this `Id` remain (orphaned). Diary hides orphans; export still serializes them.
- **Delete (cascade)**: inside a transaction — `DELETE FROM CustomItemEntries WHERE CustomItemId = ?` then `DELETE FROM CustomItems WHERE Id = ?`.

---

## Entity: `CustomItemEntry`

A user's progress on a `CustomItem` for a single calendar day. Mirrors the shape of `DailyEntry` but lives in its own table.

### Domain model — `Models/CustomItemEntry.cs` (NEW)

```csharp
namespace DailyPlants.Models;

public class CustomItemEntry
{
    /// <summary>
    /// Database primary key (auto-increment). Not exposed across devices.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The date of this entry.
    /// </summary>
    public required DateOnly Date { get; set; }

    /// <summary>
    /// Foreign key to CustomItem.Id (string GUID).
    /// May reference an item that no longer exists (orphan after "keep history" delete).
    /// </summary>
    public required string CustomItemId { get; set; }

    /// <summary>
    /// Number of servings completed for this custom item on this date.
    /// </summary>
    public int ServingsCompleted { get; set; }

    /// <summary>
    /// UTC timestamp of the last write to this row (ISO 8601 "O" format).
    /// Set by the data layer on every insert/upsert — callers do NOT set it.
    /// Forward-compatibility hook for future last-write-wins server sync.
    /// </summary>
    public required DateTime UpdatedAt { get; set; }
}
```

### SQLite entity — `Services/Entities/CustomItemEntryEntity.cs` (NEW)

```csharp
[Table("CustomItemEntries")]
internal class CustomItemEntryEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Date { get; set; } = "";          // yyyy-MM-dd

    public string CustomItemId { get; set; } = "";  // FK to CustomItems.Id

    public int ServingsCompleted { get; set; }

    /// <summary>UTC ISO 8601 ("O" format). Set by the data layer on every upsert.</summary>
    public string UpdatedAt { get; set; } = "";
}
```

### Indexes

```sql
CREATE UNIQUE INDEX IF NOT EXISTS idx_custom_entries_date_item
ON CustomItemEntries (Date, CustomItemId);
```

Mirrors `idx_daily_entries_date_item` so `INSERT … ON CONFLICT(Date, CustomItemId) DO UPDATE` upsert works for the save path.

### Persistence pattern (mirrors `SqliteDataService.SaveEntryAsync`)

```csharp
var nowUtc = DateTime.UtcNow.ToString("O");
await _connection.ExecuteAsync(
    "INSERT INTO CustomItemEntries (Date, CustomItemId, ServingsCompleted, UpdatedAt) VALUES (?, ?, ?, ?) " +
    "ON CONFLICT(Date, CustomItemId) DO UPDATE SET ServingsCompleted = ?, UpdatedAt = ?",
    dateStr, entry.CustomItemId, entry.ServingsCompleted, nowUtc,
    entry.ServingsCompleted, nowUtc);
```

### No FK constraint enforced at the DB level

`sqlite-net-e` does not enable `PRAGMA foreign_keys = ON` by default, and the project does not set it. Orphaned `CustomItemEntries` rows are an intended state (FR-004 keep-history option), so leaving FKs disabled is correct. The application layer treats orphans as "hidden" in the diary but persists them.

---

## Schema migration: `user_version 2 → 3`

Added in `SqliteDataService.RunMigrationsAsync`:

```csharp
if (version < 3)
{
    // v3: Custom user-defined checklist items.
    await _connection.CreateTableAsync<CustomItemEntity>();
    await _connection.CreateTableAsync<CustomItemEntryEntity>();
    await _connection.ExecuteAsync(
        "CREATE UNIQUE INDEX IF NOT EXISTS idx_custom_entries_date_item ON CustomItemEntries (Date, CustomItemId)");
    await _connection.ExecuteAsync("PRAGMA user_version = 3");
}
```

`CreateTableAsync<T>` calls in `InitializeAsync` are extended in parallel so the tables also exist on a fresh install (matches the existing pattern for `DailyEntryEntity` etc.).

---

## `IDataService` extension surface

New methods (additive — no signature changes to existing methods):

```csharp
// Custom item definitions
Task<IReadOnlyList<CustomItem>> GetCustomItemsAsync();
Task<CustomItem?> GetCustomItemByIdAsync(string id);
Task SaveCustomItemAsync(CustomItem item);                          // upsert by Id
Task DeleteCustomItemAsync(string id, bool cascadeEntries);         // keep-history vs cascade

// Custom item entries
Task<IReadOnlyList<CustomItemEntry>> GetCustomItemEntriesForDateAsync(DateOnly date);
Task<IReadOnlyList<CustomItemEntry>> GetCustomItemEntriesInRangeAsync(DateOnly startDate, DateOnly endDate);
Task SaveCustomItemEntryAsync(CustomItemEntry entry);               // upsert by (Date, CustomItemId)
```

The achievement-relevant statistics methods (`GetCurrentStreakAsync`, `GetLongestStreakAsync`, `GetPerfectDaysCountAsync`, `GetItemCompletionCountAsync`) are NOT modified. They continue to read only `DailyEntries`, which by construction never contains custom-item rows.

---

## Relationships diagram

```text
CustomItems (1) ───< (many, may orphan) CustomItemEntries
   id (string PK)                          customItemId (string, no FK constraint)
   name                                    date (yyyy-MM-dd)
   description (optional, ≤500)            servingsCompleted
   recommendedServings                     updatedAt (ISO 8601 UTC)
   iconType (int: 0=Catalog, 1=Emoji)
   iconValue (string: catalog key OR emoji grapheme)
   sortOrder
   updatedAt (ISO 8601 UTC)

DailyEntries  ──────────────────────────────  unchanged, untouched by this feature
EarnedAchievements ──────────────────────────  unchanged, untouched by this feature
WeightEntries ──────────────────────────────  unchanged, untouched by this feature
```

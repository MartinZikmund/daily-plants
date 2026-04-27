# Contract: Export JSON Schema (v1.1)

**Branch**: `001-custom-checklist-items`
**Date**: 2026-04-27

This feature extends the existing `ExportData` JSON contract produced by `ExportService.ExportToJsonAsync` and consumed by `ExportService.ImportFromJsonAsync`. The shape is additive and backwards-compatible.

---

## Version

| Field | Old | New |
|---|---|---|
| `version` | `"1.0"` | `"1.1"` |

A v1.0 reader encountering a v1.1 export silently ignores the new arrays. A v1.1 reader encountering a v1.0 export treats the new arrays as empty (no custom items / entries to import).

---

## New top-level fields

```jsonc
{
  "version": "1.1",
  "exportDate": "2026-04-27T12:34:56.789Z",
  "dailyEntries":  [ /* unchanged */ ],
  "weightEntries": [ /* unchanged */ ],
  "settings":      { /* unchanged */ },

  // === NEW in v1.1 ===
  "customItems": [
    {
      "id":                  "9b4f3e1c2a8d4f5e9c0b1a2d3e4f5a6b",
      "name":                "Take vitamin D",
      "description":         "1000 IU after breakfast",
      "recommendedServings": 1,
      "iconType":            "catalog",
      "iconValue":           "pill",
      "sortOrder":           100,
      "updatedAt":           "2026-04-27T10:15:30.1234567Z"
    },
    {
      "id":                  "1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d",
      "name":                "Plants",
      "description":         "",
      "recommendedServings": 1,
      "iconType":            "emoji",
      "iconValue":           "🌱",
      "sortOrder":           200,
      "updatedAt":           "2026-04-27T10:16:42.7654321Z"
    }
  ],
  "customItemEntries": [
    {
      "date":              "2026-04-27",
      "customItemId":      "9b4f3e1c2a8d4f5e9c0b1a2d3e4f5a6b",
      "servingsCompleted": 1,
      "updatedAt":         "2026-04-27T11:02:15.9876543Z"
    }
  ]
}
```

---

## `customItems` array — element schema

| Field | Type | Required | Constraints |
|---|---|---|---|
| `id` | string | yes | 32-char lowercase hex (`Guid.NewGuid().ToString("N")`) |
| `name` | string | yes | non-empty after trim, ≤60 chars (importer truncates if longer) |
| `description` | string | no | ≤500 chars (importer truncates if longer). Missing or `null` is treated as `""` |
| `recommendedServings` | integer | yes | ≥1 (importer clamps to 1 if lower) |
| `iconType` | string | yes | `"catalog"` or `"emoji"`. Unknown / missing values default to `"catalog"` for forward compatibility |
| `iconValue` | string | yes | When `iconType = "catalog"`: catalog key (unknown values resolve to `"default"` at render time without rewriting the DB). When `iconType = "emoji"`: a single emoji grapheme cluster; if more than one grapheme is present, the importer keeps only the first |
| `sortOrder` | integer | yes | any signed 32-bit integer |
| `updatedAt` | string | no | ISO 8601 UTC timestamp ("O" round-trip format). Forward-compat hook for future server sync. Missing on import → importer stamps `DateTime.UtcNow`. The importer may also overwrite the supplied value with the moment of import to mark the local row as freshly written. |

---

## `customItemEntries` array — element schema

| Field | Type | Required | Constraints |
|---|---|---|---|
| `date` | string | yes | `yyyy-MM-dd` |
| `customItemId` | string | yes | should match a `customItems[].id` in the same payload OR an existing `Id` on the target device; if neither, the row is still imported (orphan) and hidden in the diary |
| `servingsCompleted` | integer | yes | ≥0 |
| `updatedAt` | string | no | ISO 8601 UTC timestamp; same forward-compat semantics as on `customItems`. Missing → importer stamps `DateTime.UtcNow`. |

---

## Import semantics

- **`customItems`**: upsert by `id`. If an item with that `id` exists locally, all writable fields (`name`, `recommendedServings`, `iconKey`, `sortOrder`) are overwritten with the imported values.
- **`customItemEntries`**: upsert by `(date, customItemId)` — same pattern as the existing `dailyEntries` import. Entries referencing an unknown `customItemId` are still inserted (orphan); they will be hidden in the diary but round-trip through future exports.
- **Order independence**: importer should process `customItems` before `customItemEntries` so that within a single import the entries are guaranteed to find their parent definitions; however, due to the orphan-tolerant model, the order is not strictly required for correctness.

---

## Export semantics

- All current `CustomItems` rows are serialized (none are filtered out).
- All `CustomItemEntries` rows are serialized — including orphans whose parent `CustomItems` row has been deleted (keep-history mode). This preserves history end-to-end across export/import cycles.
- Field order in JSON follows the schema above (driven by record property declaration order in C#).

---

## Validation tests (manual, until a test project is added)

| Scenario | Expected |
|---|---|
| Export with 0 custom items, 0 entries | `customItems: []`, `customItemEntries: []` present (empty arrays, not `null` / missing) |
| Export → wipe DB → import (round-trip) | `customItems` rowcount + `customItemEntries` rowcount identical pre/post |
| Import v1.0 file (no custom fields) | No errors; counts of custom rows unchanged on target |
| Import payload with entries referencing unknown id | Row inserted; diary hides it; subsequent export includes it |
| Import payload with `name` length 80 | Row inserted with name truncated to 60; warning surfaced in `ImportResult` |
| Import payload with `description` length 600 | Row inserted with description truncated to 500; warning surfaced |
| Import payload missing `description` field (e.g., older v1.1 exports before this change) | Row inserted with `description = ""` |
| Import payload with `iconType: "emoji"`, `iconValue: "🍎"` | Item renders the apple emoji on the diary; round-trip preserves the exact bytes |
| Import payload with `iconType: "emoji"`, `iconValue: "🍎🥗"` (two graphemes) | Importer keeps only `🍎`; subsequent export emits the single grapheme |
| Import payload with unknown `iconType: "svg"` | Defaults to `iconType = "catalog"`, `iconValue = "default"`; warning surfaced |

---

## Non-goals for v1

- CSV export does NOT include custom items. Custom items are JSON-only.
- No conflict-resolution UX on import — last-write-wins via upsert. Future enhancement if multi-device sync is added.

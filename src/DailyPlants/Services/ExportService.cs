using System.Text;
using System.Text.Json;
using DailyPlants.Models;
using DailyPlants.Services.Settings;

namespace DailyPlants.Services;

/// <summary>
/// Service for exporting and importing tracking data.
/// </summary>
public class ExportService : IExportService
{
    private readonly IDataService _dataService;
    private readonly IAppPreferences _appPreferences;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExportService(IDataService dataService, IAppPreferences appPreferences)
    {
        _dataService = dataService;
        _appPreferences = appPreferences;
    }

    public async Task<string> ExportToJsonAsync()
    {
        var exportData = new ExportData
        {
            Version = "1.1",
            ExportDate = DateTime.UtcNow
        };

        // Export all daily entries in a single range query
        var allEntries = await _dataService.GetEntriesInRangeAsync(DateOnly.MinValue, DateOnly.MaxValue);
        foreach (var entry in allEntries)
        {
            exportData.DailyEntries.Add(new DailyEntryExport
            {
                Date = entry.Date.ToString("yyyy-MM-dd"),
                ItemId = entry.ItemId,
                ServingsCompleted = entry.ServingsCompleted
            });
        }

        // Export weight entries
        var weightEntries = await _dataService.GetAllWeightEntriesAsync();
        foreach (var entry in weightEntries)
        {
            exportData.WeightEntries.Add(new WeightEntryExport
            {
                Date = entry.Date.ToString("yyyy-MM-dd"),
                Weight = entry.Weight,
                Notes = entry.Notes
            });
        }

        // Export settings
        exportData.Settings = new UserSettingsExport
        {
            DailyDozenEnabled = _appPreferences.DailyDozenEnabled,
            TwentyOneTweaksEnabled = _appPreferences.TwentyOneTweaksEnabled,
            WeightTrackingEnabled = _appPreferences.WeightTrackingEnabled,
            UseMetricUnits = _appPreferences.UseMetricUnits,
            HeightCm = _appPreferences.HeightCm,
            GoalWeight = _appPreferences.GoalWeight,
            ThemePreference = _appPreferences.ThemePreference
        };

        // Export custom items + entries (orphans included)
        var customItems = await _dataService.GetCustomItemsAsync();
        foreach (var item in customItems)
        {
            exportData.CustomItems.Add(new CustomItemExport
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                RecommendedServings = item.RecommendedServings,
                IconType = item.IconType == CustomItemIconType.Emoji ? "emoji" : "catalog",
                IconValue = item.IconValue,
                SortOrder = item.SortOrder,
                UpdatedAt = item.UpdatedAt.ToString("O"),
            });
        }

        var customEntries = await _dataService.GetAllCustomItemEntriesAsync();
        foreach (var entry in customEntries)
        {
            exportData.CustomItemEntries.Add(new CustomItemEntryExport
            {
                Date = entry.Date.ToString("yyyy-MM-dd"),
                CustomItemId = entry.CustomItemId,
                ServingsCompleted = entry.ServingsCompleted,
                UpdatedAt = entry.UpdatedAt.ToString("O"),
            });
        }

        return JsonSerializer.Serialize(exportData, JsonOptions);
    }

    public async Task<string> ExportToCsvAsync()
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("Date,ItemId,ItemName,ServingsCompleted,RecommendedServings");

        // CSV intentionally excludes custom items per contracts/export-schema.md ("Non-goals for v1").
        // Export all entries in a single range query
        var allEntries = await _dataService.GetEntriesInRangeAsync(DateOnly.MinValue, DateOnly.MaxValue);
        foreach (var entry in allEntries)
        {
            var item = ChecklistDefinitions.GetItemById(entry.ItemId);
            var itemName = item?.Name ?? entry.ItemId;
            var recommended = item?.RecommendedServings ?? 0;

            sb.AppendLine($"{entry.Date:yyyy-MM-dd},{entry.ItemId},{EscapeCsv(itemName)},{entry.ServingsCompleted},{recommended}");
        }

        return sb.ToString();
    }

    public async Task<ImportResult> ImportFromJsonAsync(string json)
    {
        try
        {
            var importData = JsonSerializer.Deserialize<ExportData>(json, JsonOptions);
            if (importData == null)
            {
                return new ImportResult
                {
                    Success = false,
                    ErrorMessage = "Invalid JSON format"
                };
            }

            int entriesImported = 0;
            int weightEntriesImported = 0;
            int customItemsImported = 0;
            int customEntriesImported = 0;
            var warnings = new List<string>();

            // Import daily entries
            foreach (var entry in importData.DailyEntries)
            {
                if (DateOnly.TryParse(entry.Date, out var date))
                {
                    await _dataService.SaveEntryAsync(new DailyEntry
                    {
                        Date = date,
                        ItemId = entry.ItemId,
                        ServingsCompleted = entry.ServingsCompleted
                    });
                    entriesImported++;
                }
            }

            // Import weight entries
            foreach (var entry in importData.WeightEntries)
            {
                if (DateOnly.TryParse(entry.Date, out var date))
                {
                    await _dataService.SaveWeightEntryAsync(new WeightEntry
                    {
                        Date = date,
                        Weight = entry.Weight,
                        Notes = entry.Notes
                    });
                    weightEntriesImported++;
                }
            }

            // Import custom items first (so entries find their parents within the same payload).
            foreach (var ci in importData.CustomItems)
            {
                if (string.IsNullOrWhiteSpace(ci.Id) || string.IsNullOrWhiteSpace(ci.Name))
                {
                    warnings.Add($"Skipped custom item with missing id or name.");
                    continue;
                }

                var name = ci.Name.Trim();
                if (name.Length > 60)
                {
                    warnings.Add($"Truncated custom item name '{name[..Math.Min(20, name.Length)]}...' to 60 chars.");
                    name = name[..60];
                }

                var description = ci.Description ?? "";
                if (description.Length > 500)
                {
                    warnings.Add($"Truncated description on '{name}' to 500 chars.");
                    description = description[..500];
                }

                var servings = Math.Max(1, ci.RecommendedServings);

                CustomItemIconType iconType;
                string iconValue;
                if (string.Equals(ci.IconType, "emoji", StringComparison.OrdinalIgnoreCase))
                {
                    var grapheme = CustomItemService.TakeFirstEmojiGrapheme(ci.IconValue);
                    if (grapheme is null)
                    {
                        warnings.Add($"Custom item '{name}' had invalid emoji; using default catalog icon.");
                        iconType = CustomItemIconType.Catalog;
                        iconValue = CustomIconCatalog.DefaultKey;
                    }
                    else
                    {
                        iconType = CustomItemIconType.Emoji;
                        iconValue = grapheme;
                    }
                }
                else
                {
                    if (!string.Equals(ci.IconType, "catalog", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrEmpty(ci.IconType))
                    {
                        warnings.Add($"Unknown iconType '{ci.IconType}' on '{name}'; defaulting to catalog.");
                    }
                    iconType = CustomItemIconType.Catalog;
                    iconValue = CustomIconCatalog.IsKnown(ci.IconValue ?? "")
                        ? ci.IconValue!
                        : CustomIconCatalog.DefaultKey;
                }

                var updatedAt = ParseUpdatedAt(ci.UpdatedAt);

                await _dataService.SaveCustomItemAsync(new CustomItem
                {
                    Id = ci.Id,
                    Name = name,
                    Description = description,
                    RecommendedServings = servings,
                    IconType = iconType,
                    IconValue = iconValue,
                    SortOrder = ci.SortOrder,
                    UpdatedAt = updatedAt,
                });
                customItemsImported++;
            }

            // Custom item entries (orphan-tolerant)
            foreach (var entry in importData.CustomItemEntries)
            {
                if (!DateOnly.TryParse(entry.Date, out var date)) continue;
                if (string.IsNullOrWhiteSpace(entry.CustomItemId)) continue;

                await _dataService.SaveCustomItemEntryAsync(new CustomItemEntry
                {
                    Date = date,
                    CustomItemId = entry.CustomItemId,
                    ServingsCompleted = Math.Max(0, entry.ServingsCompleted),
                    UpdatedAt = ParseUpdatedAt(entry.UpdatedAt),
                });
                customEntriesImported++;
            }

            // Import settings (optional - don't overwrite if not provided)
            if (importData.Settings != null)
            {
                _appPreferences.DailyDozenEnabled = importData.Settings.DailyDozenEnabled;
                _appPreferences.TwentyOneTweaksEnabled = importData.Settings.TwentyOneTweaksEnabled;
                _appPreferences.WeightTrackingEnabled = importData.Settings.WeightTrackingEnabled;
                _appPreferences.UseMetricUnits = importData.Settings.UseMetricUnits;
                _appPreferences.HeightCm = importData.Settings.HeightCm;
                _appPreferences.GoalWeight = importData.Settings.GoalWeight;
                _appPreferences.ThemePreference = importData.Settings.ThemePreference;
            }

            return new ImportResult
            {
                Success = true,
                EntriesImported = entriesImported,
                WeightEntriesImported = weightEntriesImported,
                CustomItemsImported = customItemsImported,
                CustomItemEntriesImported = customEntriesImported,
                Warnings = warnings,
            };
        }
        catch (JsonException ex)
        {
            return new ImportResult
            {
                Success = false,
                ErrorMessage = $"JSON parse error: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new ImportResult
            {
                Success = false,
                ErrorMessage = $"Import failed: {ex.Message}"
            };
        }
    }

    private static DateTime ParseUpdatedAt(string? value)
    {
        if (string.IsNullOrEmpty(value)) return DateTime.UtcNow;
        return DateTime.TryParse(
            value,
            null,
            System.Globalization.DateTimeStyles.RoundtripKind | System.Globalization.DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed
            : DateTime.UtcNow;
    }

    public async Task<ImportResult> ImportFromCsvAsync(string csv)
    {
        try
        {
            var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2)
            {
                return new ImportResult
                {
                    Success = false,
                    ErrorMessage = "CSV file is empty or has no data rows"
                };
            }

            int entriesImported = 0;

            // Skip header row
            for (int i = 1; i < lines.Length; i++)
            {
                var parts = ParseCsvLine(lines[i]);
                if (parts.Length >= 4)
                {
                    var dateStr = parts[0].Trim();
                    var itemId = parts[1].Trim();
                    var servingsStr = parts[3].Trim();

                    if (DateOnly.TryParse(dateStr, out var date) &&
                        int.TryParse(servingsStr, out var servings))
                    {
                        await _dataService.SaveEntryAsync(new DailyEntry
                        {
                            Date = date,
                            ItemId = itemId,
                            ServingsCompleted = servings
                        });
                        entriesImported++;
                    }
                }
            }

            return new ImportResult
            {
                Success = true,
                EntriesImported = entriesImported
            };
        }
        catch (Exception ex)
        {
            return new ImportResult
            {
                Success = false,
                ErrorMessage = $"CSV import failed: {ex.Message}"
            };
        }
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString());
        return result.ToArray();
    }
}

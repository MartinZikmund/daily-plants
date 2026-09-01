using System.Globalization;
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
    /// <summary>
    /// Upper bound on servings accepted from a file. The largest recommended count in any
    /// checklist is well under this; anything above it is corrupt rather than ambitious.
    /// </summary>
    private const int MaxReasonableServings = 1000;

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
            Version = ExportFormat.CurrentVersion,
            ExportDate = DateTime.UtcNow
        };

        // Export all daily entries in a single range query
        var allEntries = await _dataService.GetEntriesInRangeAsync(DateOnly.MinValue, DateOnly.MaxValue);
        foreach (var entry in allEntries)
        {
            exportData.DailyEntries.Add(new DailyEntryExport
            {
                Date = IsoDate.ToStorage(entry.Date),
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
                Date = IsoDate.ToStorage(entry.Date),
                Weight = entry.Weight,
                Notes = entry.Notes
            });
        }

        // Export earned achievements — without these a restore silently wipes every badge
        var achievements = await _dataService.GetEarnedAchievementsAsync();
        foreach (var achievement in achievements)
        {
            exportData.Achievements.Add(new EarnedAchievementExport
            {
                AchievementId = achievement.AchievementId,
                EarnedAt = IsoDate.TimestampToStorage(achievement.EarnedAt),
                HasBeenSeen = achievement.HasBeenSeen
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
            ThemePreference = _appPreferences.ThemePreference,
            Language = _appPreferences.Language,
            DisabledItemIds = _appPreferences.DisabledItemIds
        };

        return JsonSerializer.Serialize(exportData, JsonOptions);
    }

    public async Task<string> ExportToCsvAsync()
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("Date,ItemId,ItemName,ServingsCompleted,RecommendedServings");

        // Export all entries in a single range query
        var allEntries = await _dataService.GetEntriesInRangeAsync(DateOnly.MinValue, DateOnly.MaxValue);
        foreach (var entry in allEntries)
        {
            var item = ChecklistDefinitions.GetItemById(entry.ItemId);
            var itemName = item?.Name ?? entry.ItemId;
            var recommended = item?.RecommendedServings ?? 0;

            sb.AppendLine($"{IsoDate.ToStorage(entry.Date)},{entry.ItemId},{EscapeCsv(itemName)},{entry.ServingsCompleted},{recommended}");
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
                return Failed("Invalid JSON format");
            }

            if (!ExportFormat.IsSupported(importData.Version))
            {
                return Failed($"Unsupported export format version: {importData.Version}");
            }

            // 1.0 files stored weights and heights in whichever unit the exporting user had
            // selected; every version since stores kilograms and centimetres.
            var storedInImperial = importData.Version == ExportFormat.LegacyUnitsVersion
                && importData.Settings?.UseMetricUnits == false;

            var entriesImported = 0;
            var weightEntriesImported = 0;
            var achievementsImported = 0;
            var entriesSkipped = 0;

            // One unit of work: a failure part way through must not leave the database
            // half-overwritten, since import upserts straight over existing entries.
            await _dataService.RunInTransactionAsync(async () =>
            {
                foreach (var entry in importData.DailyEntries)
                {
                    if (!IsValidEntry(entry, out var date))
                    {
                        entriesSkipped++;
                        continue;
                    }

                    await _dataService.SaveEntryAsync(new DailyEntry
                    {
                        Date = date,
                        ItemId = entry.ItemId,
                        ServingsCompleted = entry.ServingsCompleted
                    });
                    entriesImported++;
                }

                foreach (var entry in importData.WeightEntries)
                {
                    if (!IsoDate.TryParse(entry.Date, out var weightDate) || entry.Weight <= 0)
                    {
                        entriesSkipped++;
                        continue;
                    }

                    await _dataService.SaveWeightEntryAsync(new WeightEntry
                    {
                        Date = weightDate,
                        Weight = storedInImperial
                            ? entry.Weight.DisplayToKilograms(useMetric: false)
                            : entry.Weight,
                        Notes = entry.Notes
                    });
                    weightEntriesImported++;
                }

                foreach (var achievement in importData.Achievements)
                {
                    if (AchievementDefinitions.GetById(achievement.AchievementId) is null)
                    {
                        entriesSkipped++;
                        continue;
                    }

                    // Coercing an unreadable timestamp to "now" would silently rewrite the
                    // badge's history, so the row is refused instead.
                    if (!IsoDate.TryParseTimestamp(achievement.EarnedAt, out var earnedAt))
                    {
                        entriesSkipped++;
                        continue;
                    }

                    await _dataService.SaveEarnedAchievementAsync(new EarnedAchievement
                    {
                        AchievementId = achievement.AchievementId,
                        EarnedAt = earnedAt,
                        HasBeenSeen = achievement.HasBeenSeen
                    });
                    achievementsImported++;
                }

                // Settings are optional - a file without them leaves the current ones alone.
                if (importData.Settings is { } settings)
                {
                    ApplySettings(settings, storedInImperial);
                }
            });

            return new ImportResult
            {
                Success = true,
                EntriesImported = entriesImported,
                WeightEntriesImported = weightEntriesImported,
                AchievementsImported = achievementsImported,
                EntriesSkipped = entriesSkipped
            };
        }
        catch (JsonException ex)
        {
            return Failed($"JSON parse error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Failed($"Import failed: {ex.Message}");
        }
    }

    public async Task<ImportResult> ImportFromCsvAsync(string csv)
    {
        try
        {
            var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2)
            {
                return Failed("CSV file is empty or has no data rows");
            }

            var entriesImported = 0;
            var entriesSkipped = 0;

            await _dataService.RunInTransactionAsync(async () =>
            {
                // Skip header row
                for (int i = 1; i < lines.Length; i++)
                {
                    var parts = ParseCsvLine(lines[i]);
                    if (parts.Length < 4)
                    {
                        entriesSkipped++;
                        continue;
                    }

                    if (!int.TryParse(parts[3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var servings))
                    {
                        entriesSkipped++;
                        continue;
                    }

                    var candidate = new DailyEntryExport
                    {
                        Date = parts[0].Trim(),
                        ItemId = parts[1].Trim(),
                        ServingsCompleted = servings
                    };

                    if (!IsValidEntry(candidate, out var date))
                    {
                        entriesSkipped++;
                        continue;
                    }

                    await _dataService.SaveEntryAsync(new DailyEntry
                    {
                        Date = date,
                        ItemId = candidate.ItemId,
                        ServingsCompleted = servings
                    });
                    entriesImported++;
                }
            });

            return new ImportResult
            {
                Success = true,
                EntriesImported = entriesImported,
                EntriesSkipped = entriesSkipped
            };
        }
        catch (Exception ex)
        {
            return Failed($"CSV import failed: {ex.Message}");
        }
    }

    private void ApplySettings(UserSettingsExport settings, bool storedInImperial)
    {
        _appPreferences.DailyDozenEnabled = settings.DailyDozenEnabled;
        _appPreferences.TwentyOneTweaksEnabled = settings.TwentyOneTweaksEnabled;
        _appPreferences.WeightTrackingEnabled = settings.WeightTrackingEnabled;
        _appPreferences.UseMetricUnits = settings.UseMetricUnits;
        _appPreferences.ThemePreference = settings.ThemePreference;

        _appPreferences.HeightCm = settings.HeightCm is { } height && storedInImperial
            ? height.DisplayToCentimetres(useMetric: false)
            : settings.HeightCm;

        _appPreferences.GoalWeight = settings.GoalWeight is { } goal && storedInImperial
            ? goal.DisplayToKilograms(useMetric: false)
            : settings.GoalWeight;

        if (settings.Language is not null)
        {
            _appPreferences.Language = settings.Language;
        }

        if (settings.DisabledItemIds is not null)
        {
            _appPreferences.DisabledItemIds = settings.DisabledItemIds;
        }
    }

    /// <summary>
    /// Rejects rows the app cannot represent: bad dates, unknown items, and servings
    /// outside a sane range. Import upserts, so an unchecked row silently overwrites
    /// good data with nonsense.
    /// </summary>
    private static bool IsValidEntry(DailyEntryExport entry, out DateOnly date)
    {
        if (!IsoDate.TryParse(entry.Date, out date))
        {
            return false;
        }

        if (entry.ServingsCompleted < 0 || entry.ServingsCompleted > MaxReasonableServings)
        {
            return false;
        }

        return ChecklistDefinitions.GetItemById(entry.ItemId) is not null;
    }

    private static ImportResult Failed(string message) => new()
    {
        Success = false,
        ErrorMessage = message
    };

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

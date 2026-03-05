using System.Text;
using System.Text.Json;
using DailyPlants.Models;

namespace DailyPlants.Services;

/// <summary>
/// Service for exporting and importing tracking data.
/// </summary>
public class ExportService : IExportService
{
    private readonly IDataService _dataService;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExportService(IDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task<string> ExportToJsonAsync()
    {
        var exportData = new ExportData
        {
            Version = "1.0",
            ExportDate = DateTime.UtcNow
        };

        // Get all dates with entries
        var datesWithEntries = await _dataService.GetDatesWithEntriesAsync();

        // Export daily entries
        foreach (var date in datesWithEntries)
        {
            var entries = await _dataService.GetEntriesForDateAsync(date);
            foreach (var entry in entries)
            {
                exportData.DailyEntries.Add(new DailyEntryExport
                {
                    Date = entry.Date.ToString("yyyy-MM-dd"),
                    ItemId = entry.ItemId,
                    ServingsCompleted = entry.ServingsCompleted
                });
            }
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
        var settings = await _dataService.GetSettingsAsync();
        exportData.Settings = new UserSettingsExport
        {
            DailyDozenEnabled = settings.DailyDozenEnabled,
            TwentyOneTweaksEnabled = settings.TwentyOneTweaksEnabled,
            AntiAgingEightEnabled = settings.AntiAgingEightEnabled,
            WeightTrackingEnabled = settings.WeightTrackingEnabled,
            UseMetricUnits = settings.UseMetricUnits,
            HeightCm = settings.HeightCm,
            GoalWeight = settings.GoalWeight,
            ThemePreference = settings.ThemePreference
        };

        return JsonSerializer.Serialize(exportData, JsonOptions);
    }

    public async Task<string> ExportToCsvAsync()
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("Date,ItemId,ItemName,ServingsCompleted,RecommendedServings");

        // Get all dates with entries
        var datesWithEntries = await _dataService.GetDatesWithEntriesAsync();

        foreach (var date in datesWithEntries.OrderBy(d => d))
        {
            var entries = await _dataService.GetEntriesForDateAsync(date);
            foreach (var entry in entries)
            {
                var item = ChecklistDefinitions.GetItemById(entry.ItemId);
                var itemName = item?.Name ?? entry.ItemId;
                var recommended = item?.RecommendedServings ?? 0;

                sb.AppendLine($"{date:yyyy-MM-dd},{entry.ItemId},{EscapeCsv(itemName)},{entry.ServingsCompleted},{recommended}");
            }
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

            // Import settings (optional - don't overwrite if not provided)
            if (importData.Settings != null)
            {
                var settings = new UserSettings
                {
                    DailyDozenEnabled = importData.Settings.DailyDozenEnabled,
                    TwentyOneTweaksEnabled = importData.Settings.TwentyOneTweaksEnabled,
                    AntiAgingEightEnabled = importData.Settings.AntiAgingEightEnabled,
                    WeightTrackingEnabled = importData.Settings.WeightTrackingEnabled,
                    UseMetricUnits = importData.Settings.UseMetricUnits,
                    HeightCm = importData.Settings.HeightCm,
                    GoalWeight = importData.Settings.GoalWeight,
                    ThemePreference = importData.Settings.ThemePreference
                };
                await _dataService.SaveSettingsAsync(settings);
            }

            return new ImportResult
            {
                Success = true,
                EntriesImported = entriesImported,
                WeightEntriesImported = weightEntriesImported
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
                        int.TryParse(servingsStr, out var servings) &&
                        ChecklistDefinitions.GetItemById(itemId) != null)
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

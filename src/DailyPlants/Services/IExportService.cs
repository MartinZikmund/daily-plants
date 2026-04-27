using DailyPlants.Models;

namespace DailyPlants.Services;

/// <summary>
/// Interface for data export and import operations.
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Exports all data to JSON format.
    /// </summary>
    Task<string> ExportToJsonAsync();

    /// <summary>
    /// Exports daily entries to CSV format.
    /// </summary>
    Task<string> ExportToCsvAsync();

    /// <summary>
    /// Imports data from JSON format.
    /// </summary>
    Task<ImportResult> ImportFromJsonAsync(string json);

    /// <summary>
    /// Imports daily entries from CSV format.
    /// </summary>
    Task<ImportResult> ImportFromCsvAsync(string csv);
}

/// <summary>
/// Result of an import operation.
/// </summary>
public class ImportResult
{
    public bool Success { get; init; }
    public int EntriesImported { get; init; }
    public int WeightEntriesImported { get; init; }
    public int CustomItemsImported { get; init; }
    public int CustomItemEntriesImported { get; init; }
    public List<string> Warnings { get; init; } = [];
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Data structure for JSON export/import. Version "1.1" adds customItems / customItemEntries.
/// </summary>
public class ExportData
{
    public string Version { get; set; } = "1.1";
    public DateTime ExportDate { get; set; } = DateTime.UtcNow;
    public List<DailyEntryExport> DailyEntries { get; set; } = [];
    public List<WeightEntryExport> WeightEntries { get; set; } = [];
    public UserSettingsExport? Settings { get; set; }

    public List<CustomItemExport> CustomItems { get; set; } = [];
    public List<CustomItemEntryExport> CustomItemEntries { get; set; } = [];
}

public class CustomItemExport
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int RecommendedServings { get; set; } = 1;
    public string IconType { get; set; } = "catalog";
    public string IconValue { get; set; } = "default";
    public int SortOrder { get; set; }
    public string? UpdatedAt { get; set; }
}

public class CustomItemEntryExport
{
    public string Date { get; set; } = "";
    public string CustomItemId { get; set; } = "";
    public int ServingsCompleted { get; set; }
    public string? UpdatedAt { get; set; }
}

public class DailyEntryExport
{
    public string Date { get; set; } = "";
    public string ItemId { get; set; } = "";
    public int ServingsCompleted { get; set; }
}

public class WeightEntryExport
{
    public string Date { get; set; } = "";
    public double Weight { get; set; }
    public string? Notes { get; set; }
}

public class UserSettingsExport
{
    public bool DailyDozenEnabled { get; set; }
    public bool TwentyOneTweaksEnabled { get; set; }
    public bool WeightTrackingEnabled { get; set; }
    public bool UseMetricUnits { get; set; }
    public double? HeightCm { get; set; }
    public double? GoalWeight { get; set; }
    public int ThemePreference { get; set; }
}

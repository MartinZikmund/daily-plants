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
    public int AchievementsImported { get; init; }

    /// <summary>
    /// Rows of any kind that the file contained but the app refused, counted together:
    /// daily entries with an unparseable date, an unknown item ID or out-of-range servings;
    /// weight rows with an unparseable date or a non-positive weight; and achievements with
    /// an unknown ID or an unreadable timestamp. Surfaced so a partial import is never silent.
    /// </summary>
    public int EntriesSkipped { get; init; }

    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Data structure for JSON export/import.
/// </summary>
public class ExportData
{
    /// <summary>
    /// Format version. "1.0" stored weights and heights in whichever unit the exporting
    /// user had selected; "1.1" always stores kilograms and centimetres.
    /// </summary>
    public string Version { get; set; } = ExportFormat.CurrentVersion;
    public DateTime ExportDate { get; set; } = DateTime.UtcNow;
    public List<DailyEntryExport> DailyEntries { get; set; } = [];
    public List<WeightEntryExport> WeightEntries { get; set; } = [];
    public List<EarnedAchievementExport> Achievements { get; set; } = [];
    public UserSettingsExport? Settings { get; set; }
}

/// <summary>
/// Known export format versions.
/// </summary>
public static class ExportFormat
{
    public const string LegacyUnitsVersion = "1.0";
    public const string CurrentVersion = "1.1";

    public static bool IsSupported(string? version) =>
        version is null or LegacyUnitsVersion or CurrentVersion;
}

public class EarnedAchievementExport
{
    public string AchievementId { get; set; } = "";
    public string EarnedAt { get; set; } = "";
    public bool HasBeenSeen { get; set; }
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
    public string? Language { get; set; }
    public string? DisabledItemIds { get; set; }
}

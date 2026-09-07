namespace DailyPlants.Services;

/// <summary>
/// Where the on-device log is written. Resolved once at startup so the Serilog file sink and the
/// database-failure dialog always name the same file.
/// </summary>
public static class LogFile
{
    /// <summary>The sink rolls at this size rather than by date, so <see cref="Resolve"/> stays the live file.</summary>
    public const long MaxBytes = 512 * 1024;

    /// <summary>How many rolled files to keep beside the live one.</summary>
    public const int RetainedFiles = 3;

    /// <summary>
    /// The log file path, or null on a head with no writable location (wasm). A null path means
    /// file logging is simply off - the other providers still receive everything.
    /// </summary>
    public static string? Resolve()
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DailyPlants",
                "logs");

            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "app.log");
        }
        catch (Exception)
        {
            return null;
        }
    }
}

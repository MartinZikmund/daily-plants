using System.Globalization;
using System.Text;

namespace DailyPlants.Services;

/// <summary>
/// Minimal rolling file log. The app ships with no telemetry by design, so this is the
/// only way a user-reported crash can be diagnosed: the file stays on the device and is
/// only shared if the user chooses to attach it to a bug report.
/// </summary>
/// <remarks>
/// Every operation swallows its own errors. Logging must never be the thing that breaks
/// the app, and file access is unavailable or restricted on some heads (WebAssembly).
/// </remarks>
public static class AppLog
{
    private const long MaxLogBytes = 512 * 1024;

    private static readonly object Gate = new();
    private static string? _logFilePath;

    /// <summary>The current log file, or null when logging is unavailable.</summary>
    public static string? LogFilePath => _logFilePath;

    public static void Initialize()
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DailyPlants",
                "logs");

            Directory.CreateDirectory(directory);
            _logFilePath = Path.Combine(directory, "app.log");
        }
        catch (Exception)
        {
            // No writable location on this platform; logging stays disabled.
            _logFilePath = null;
        }
    }

    public static void Info(string message) => Write("INFO", message, exception: null);

    public static void Error(string message, Exception? exception) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        if (_logFilePath is not { } path) return;

        try
        {
            var line = new StringBuilder()
                .Append(DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture))
                .Append(" [").Append(level).Append("] ")
                .Append(message);

            if (exception is not null)
            {
                line.AppendLine().Append(exception);
            }

            lock (Gate)
            {
                RollIfTooLarge(path);
                File.AppendAllText(path, line.AppendLine().ToString());
            }
        }
        catch (Exception)
        {
            // Logging is best effort and must never surface to the user.
        }
    }

    private static void RollIfTooLarge(string path)
    {
        var file = new FileInfo(path);
        if (!file.Exists || file.Length < MaxLogBytes) return;

        var previous = path + ".1";
        if (File.Exists(previous))
        {
            File.Delete(previous);
        }

        File.Move(path, previous);
    }
}

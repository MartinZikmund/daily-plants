using System.Diagnostics;

namespace DailyPlants.Helpers;

/// <summary>
/// Opens a URL in the system browser. On wasm the call must not sit behind an await
/// after the tap, or the browser blocks the popup - callers already hold the Uri, so don't add one.
/// </summary>
public static class BrowserLauncher
{
    /// <summary>Returns true when the OS accepted the URL. Logs and returns false otherwise; never throws.</summary>
    public static async Task<bool> OpenAsync(string url, ILogger? logger = null)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            logger?.LogError("Refusing to launch a non-absolute URL: {Url}", url);
            return false;
        }

        try
        {
            if (await Windows.System.Launcher.LaunchUriAsync(uri))
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Launcher.LaunchUriAsync failed for {Uri}", uri);
        }

        return TryShellExecute(uri, logger);
    }

    // The Skia desktop head is not a documented Launcher target; shelling out is the documented fallback.
    private static bool TryShellExecute(Uri uri, ILogger? logger)
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Opening {Uri} through the shell failed", uri);
            return false;
        }
    }
}

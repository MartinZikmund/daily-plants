using System.Diagnostics;

namespace DailyPlants.Helpers;

/// <summary>
/// Opens a folder in the system file manager. Only the desktop-class heads have one to open -
/// Android and iOS keep app data private and wasm has no filesystem at all - so callers should
/// gate the affordance on <see cref="IsSupported"/> rather than offering a button that cannot work.
/// </summary>
public static class FolderLauncher
{
    /// <summary>Whether this head has a file manager to hand the folder to.</summary>
    public static bool IsSupported =>
        OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();

    /// <summary>Returns true when the OS accepted the folder. Logs and returns false otherwise; never throws.</summary>
    public static async Task<bool> OpenAsync(string path, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            logger?.LogError("Refusing to open a folder that is not there: {Path}", path);
            return false;
        }

        try
        {
            if (await Windows.System.Launcher.LaunchFolderPathAsync(path))
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Launcher.LaunchFolderPathAsync failed for {Path}", path);
        }

        return TryShellExecute(path, logger);
    }

    // The Skia desktop head is not a documented Launcher target; shelling out is the documented fallback.
    private static bool TryShellExecute(string path, ILogger? logger)
    {
        if (!IsSupported)
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Opening {Path} through the shell failed", path);
            return false;
        }
    }
}

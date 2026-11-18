namespace DailyPlants.Infrastructure;

/// <summary>
/// What kind of build this is. The worktree name comes from a generated part (src/WorktreeIdentity.targets).
/// </summary>
public static partial class AppEnvironment
{
#if APP_CHANNEL_DEV && APP_CI_BUILD
    private const string ChannelLabel = "CI";
#elif APP_CHANNEL_DEV
    private const string ChannelLabel = "DEV";
#else
    private const string ChannelLabel = "";
#endif

    public static bool IsDevChannel { get; } = ChannelLabel.Length > 0;

    /// <summary>A Dev build made on CI, badged "CI" in blue like its app icon (src/DevAssets.targets).</summary>
    public static bool IsCiBuild { get; } = ChannelLabel == "CI";

    /// <summary>The Dev badge text, e.g. "DEV" or "DEV · feature-x". Empty in Prod.</summary>
    public static string ChannelBadgeLabel { get; } = FormatChannelBadge(ChannelLabel, WorktreeName);

    internal static string FormatChannelBadge(string channel, string worktree) =>
        channel.Length > 0 && worktree.Length > 0 ? $"{channel} · {worktree}" : channel;

    /// <summary>
    /// Where the database and feed cache live. Prod keeps %LocalAppData%\DailyPlants. Dev and each worktree get their own
    /// folder: the desktop head isn't packaged, and MSIX only isolates AppData folders that didn't already exist.
    /// </summary>
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        FormatDataFolderName(IsDevChannel, WorktreeName));

    internal static string FormatDataFolderName(bool isDevChannel, string worktree)
    {
        if (!isDevChannel)
        {
            return "DailyPlants";
        }

        if (worktree.Length == 0)
        {
            return "DailyPlants.Dev";
        }

        char[] invalid = Path.GetInvalidFileNameChars();
        return $"DailyPlants.Dev.{new string(worktree.Select(c => invalid.Contains(c) ? '-' : c).ToArray())}";
    }
}

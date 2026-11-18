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
}

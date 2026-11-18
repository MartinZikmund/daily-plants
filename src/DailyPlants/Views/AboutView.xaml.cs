using System.Globalization;
using System.Reflection;
using DailyPlants.Helpers;
using DailyPlants.Infrastructure;

namespace DailyPlants.Views;

public sealed partial class AboutView : Page
{
    public AboutView()
    {
        this.InitializeComponent();

        VersionText.Text = GetInformationalVersion();

        if (AppEnvironment.WorktreeName is { Length: > 0 } worktree)
        {
            WorktreeText.Text = string.Format(CultureInfo.CurrentCulture, Localizer.GetString("About_Worktree"), worktree);
            WorktreeText.Visibility = Visibility.Visible;
        }
    }

    // NerdBank.GitVersioning stamps AssemblyInformationalVersionAttribute at build time
    // (e.g. "1.2.3+abcdef0"); trim the commit suffix for display.
    private static string GetInformationalVersion()
    {
        var informationalVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (string.IsNullOrEmpty(informationalVersion))
        {
            return "Unknown";
        }

        var plusIndex = informationalVersion.IndexOf('+');
        return plusIndex >= 0 ? informationalVersion[..plusIndex] : informationalVersion;
    }
}

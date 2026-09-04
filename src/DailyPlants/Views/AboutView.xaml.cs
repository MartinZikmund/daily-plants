using System.Reflection;

namespace DailyPlants.Views;

public sealed partial class AboutView : Page
{
    public AboutView()
    {
        this.InitializeComponent();

        VersionText.Text = GetInformationalVersion();
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

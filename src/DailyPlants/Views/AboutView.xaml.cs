using System.Reflection;

namespace DailyPlants.Views;

public sealed partial class AboutView : Page
{
    public AboutView()
    {
        this.InitializeComponent();

        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "unknown";

        VersionText.Text = version;
    }
}

using System.Reflection;
using MZikmund.Toolkit.WinUI.Extensions;

namespace DailyPlants.Views;

public sealed partial class AboutView : Page
{
    public AboutView()
    {
        this.InitializeComponent();

        var version = Package.Current.Id.Version.ToVersionString();
        VersionText.Text = version;
    }
}

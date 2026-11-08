using System.Xml.Linq;

namespace DailyPlants.Tests.Packaging;

[TestClass]
public class AppResourcesTests
{
    private const string AppScheme = "ms-appx:///";

    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [TestMethod]
    public void App_MergesOnlyDictionariesThatExist()
    {
        var (_, missing) = MergedDictionaries();

        missing.Should().BeEmpty("a merged dictionary that does not exist crashes the app at startup");
    }

    // Goes away with the source copy, see src/DailyPlants/ThirdParty/CommunityToolkit/README.md
    [DataTestMethod]
    [DataRow("SettingsCard")]
    [DataRow("SettingsExpander")]
    public void SettingsControlsCopy_HasAnImplicitStyleReachableFromTheApp(string control)
    {
        var implicitStyleTargets = MergedDictionaries().ProjectFiles
            .SelectMany(file => XDocument.Load(file).Root!.Elements(Presentation + "Style"))
            .Where(style => style.Attribute(Xaml + "Key") is null)
            .Select(style => style.Attribute("TargetType")?.Value);

        implicitStyleTargets.Should().Contain($"local:{control}", "a control without a style has no template, so it renders nothing");
    }

    private static (List<string> ProjectFiles, List<string> Missing) MergedDictionaries()
    {
        var projectRoot = Path.Combine(RepoRoot(), "src", "DailyPlants");
        List<string> projectFiles = new();
        List<string> missing = new();
        Queue<string> pending = new([Path.Combine(projectRoot, "App.xaml")]);

        while (pending.TryDequeue(out var current))
        {
            var sources = XDocument.Load(current)
                .Descendants(Presentation + "ResourceDictionary")
                .Select(dictionary => dictionary.Attribute("Source")?.Value)
                .OfType<string>();

            foreach (var source in sources)
            {
                source.Should().StartWith(AppScheme);
                var path = source[AppScheme.Length..];

                var file = Path.Combine(projectRoot, path.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(file))
                {
                    projectFiles.Add(file);
                    pending.Enqueue(file);
                }
                else if (!IsInReferencedAssembly(path))
                {
                    missing.Add(source);
                }
            }
        }

        return (projectFiles, missing);
    }

    private static bool IsInReferencedAssembly(string path) =>
        File.Exists(Path.Combine(AppContext.BaseDirectory, $"{path.Split('/')[0]}.dll"));

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DailyPlants.slnx")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the tests run from inside the repository");
        return directory!.FullName;
    }
}

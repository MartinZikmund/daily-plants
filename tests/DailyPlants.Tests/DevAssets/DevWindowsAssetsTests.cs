using System.Xml.Linq;
using DailyPlants.Tests.Packaging;

namespace DailyPlants.Tests.DevAssets;

/// <summary>
/// Runs the Windows part of src/DevAssets.targets in a scratch project, with a fake exporter that copies the art it's
/// given into each PNG it "renders".
/// </summary>
[TestClass]
public sealed class DevWindowsAssetsTests
{
    const string Art = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100"><circle cx="50" cy="50" r="40" /></svg>""";

    static readonly string[] WindowsImages = ["AppIcon.scale-100.png", "SplashScreen.scale-100.png"];

    DirectoryInfo _project = null!;

    [TestInitialize]
    public void Setup()
    {
        _project = Directory.CreateTempSubdirectory("dev-windows-assets-");
        DirectoryInfo windows = Directory.CreateDirectory(Path.Combine(_project.FullName, "Platforms", "Windows"));
        foreach (string image in WindowsImages)
        {
            File.WriteAllText(Path.Combine(windows.FullName, image), "original");
        }

        Directory.CreateDirectory(Path.Combine(_project.FullName, "Assets"));
        File.WriteAllText(Path.Combine(_project.FullName, "Assets", "art.svg"), Art);
        File.WriteAllText(Path.Combine(_project.FullName, "Assets", "other.png"), "other");
    }

    [TestCleanup]
    public void Cleanup() => _project.Delete(recursive: true);

    [TestMethod]
    public void Prod_ShipsTheCheckedInImages()
    {
        List<ContentItem> content = Run("-p:AppChannel=Prod");

        content.Select(c => c.Path).Should().BeEquivalentTo(@"Platforms\Windows\AppIcon.scale-100.png", @"Platforms\Windows\SplashScreen.scale-100.png", @"Assets\other.png");
        content.Should().OnlyContain(c => c.Link == "");
    }

    [TestMethod]
    [DataRow("Prod")]
    [DataRow("Dev")]
    public void WindowsImages_CopyWhenDifferent(string channel)
    {
        List<ContentItem> content = Run($"-p:AppChannel={channel}");

        content.Where(c => c.TargetPath.StartsWith(@"Platforms\Windows\", StringComparison.Ordinal))
            .Should().HaveCount(2).And.OnlyContain(c => c.CopyToOutputDirectory == "IfDifferent");
        content.Single(c => c.Path == @"Assets\other.png").CopyToOutputDirectory.Should().Be("PreserveNewest");
    }

    [TestMethod]
    public void Dev_SwapsEveryWindowsImageForTheBadgedRender()
    {
        List<ContentItem> content = Run("-p:AppChannel=Dev");

        List<ContentItem> windows = content.Where(c => c.TargetPath.StartsWith(@"Platforms\Windows\", StringComparison.Ordinal)).ToList();
        windows.Select(c => c.TargetPath).Should().BeEquivalentTo(WindowsImages.Select(image => $@"Platforms\Windows\{image}"));
        foreach (ContentItem image in windows)
        {
            Path.IsPathRooted(image.Path).Should().BeTrue();
            Path.GetFileName(image.Path).Should().Be(Path.GetFileName(image.TargetPath));
            File.ReadAllText(image.Path).Should().Contain("dev-badge", "the exporter gets the badged art");
        }

        content.Single(c => c.Path == @"Assets\other.png").TargetPath.Should().Be(@"Assets\other.png");
    }

    [TestMethod]
    public void Dev_ImageTheExporterSkips_StaysOriginalWithAWarning()
    {
        List<ContentItem> content = Run(out string output, "-p:AppChannel=Dev", "-p:FakeExporterSkips=SplashScreen.scale-100.png");

        output.Should().Contain("DEVASSETS004").And.Contain("SplashScreen.scale-100.png");
        content.Should().Contain(c => c.Path == @"Platforms\Windows\SplashScreen.scale-100.png");
        content.Should().NotContain(c => c.Path == @"Platforms\Windows\AppIcon.scale-100.png");
    }

    [TestMethod]
    public void Dev_OptedOut_ShipsTheCheckedInImages()
    {
        List<ContentItem> content = Run("-p:AppChannel=Dev", "-p:GenerateDevAssets=false");

        content.Should().OnlyContain(c => !Path.IsPathRooted(c.Path));
    }

    List<ContentItem> Run(params string[] properties) => Run(out _, properties);

    List<ContentItem> Run(out string output, params string[] properties)
    {
        // Writes each image it's asked for, except FakeExporterSkips, containing the art.
        string exporter = Path.Combine(_project.FullName, "exporter", "export.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(exporter)!);
        string skip = properties.FirstOrDefault(p => p.StartsWith("-p:FakeExporterSkips=", StringComparison.Ordinal))?.Split('=')[1] ?? "";
        File.WriteAllText(exporter, $$"""
            Directory.CreateDirectory(args[1]);
            foreach (string name in new[] { {{string.Join(", ", WindowsImages.Where(i => i != skip).Select(i => $"\"{i}\""))}} })
            {
                File.Copy(args[0], Path.Combine(args[1], name), true);
            }
            """);

        string project = Path.Combine(_project.FullName, "App.proj");
        string report = Path.Combine(_project.FullName, "content.xml");
        File.WriteAllText(project, $"""
            <Project>
              <PropertyGroup>
                <TargetPlatformIdentifier>windows</TargetPlatformIdentifier>
                <IntermediateOutputPath>obj\</IntermediateOutputPath>
                <DevWindowsAssetsArt>Assets\art.svg</DevWindowsAssetsArt>
                <DevWindowsAssetsExporter>{exporter}</DevWindowsAssetsExporter>
              </PropertyGroup>
              <ItemGroup>
                <Content Include="Platforms\Windows\*.png;Assets\*.png" CopyToOutputDirectory="PreserveNewest" />
              </ItemGroup>
              <Import Project="{Path.Combine(BuildFiles.RepoRoot, "src", "DevAssets.targets")}" />
              <Target Name="AssignTargetPaths" />
              <Target Name="Report" DependsOnTargets="AssignTargetPaths">
                <WriteLinesToFile File="{report}" Overwrite="true"
                                  Lines="&lt;Items&gt;;@(Content->'&lt;Content Path=&quot;%(Identity)&quot; Link=&quot;%(Link)&quot; Copy=&quot;%(CopyToOutputDirectory)&quot; /&gt;', ';');&lt;/Items&gt;" />
              </Target>
            </Project>
            """);

        var (exitCode, log) = BuildFiles.RunMSBuild(project, "Report", properties.Where(p => !p.StartsWith("-p:FakeExporterSkips=", StringComparison.Ordinal)));
        exitCode.Should().Be(0, log);
        output = log;

        return XDocument.Load(report).Root!.Elements("Content")
            .Select(e => new ContentItem((string)e.Attribute("Path")!, (string)e.Attribute("Link")!, (string)e.Attribute("Copy")!))
            .ToList();
    }

    // TargetPath is where the item lands in the package, as AssignTargetPath works it out.
    sealed record ContentItem(string Path, string Link, string CopyToOutputDirectory)
    {
        public string TargetPath => Link.Length > 0 ? Link : Path;
    }
}

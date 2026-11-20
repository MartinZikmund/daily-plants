namespace DailyPlants.Tests.Packaging;

[TestClass]
public class MobileLabelsTests
{
    private const string Strings = """
        <?xml version="1.0" encoding="utf-8"?>
        <resources>
        	<string name="app_name">Daily Plants</string>
        	<string name="ApplicationName">DailyPlants</string>
        	<string name="other">Daily Plants</string>
        </resources>
        """;

    [TestMethod]
    public void ApplyMobileLabelAndroid_DevChannel_AppendsDevToBothLabels()
    {
        var staged = RunLabels("Dev");

        staged.Should().Contain("""<string name="app_name">Daily Plants Dev</string>""")
            .And.Contain("""<string name="ApplicationName">DailyPlants Dev</string>""")
            .And.Contain("""<string name="other">Daily Plants</string>""");
    }

    [TestMethod]
    public void ApplyMobileLabelAndroid_ProdAfterDev_RestoresTheStoreLabels()
    {
        var staged = RunLabels("Dev", "Prod");

        staged.Should().Contain("""<string name="app_name">Daily Plants</string>""")
            .And.Contain("""<string name="ApplicationName">DailyPlants</string>""");
    }

    // Builds the channels in order in one obj/ folder, like switching channels locally, and returns the staged resource.
    private static string RunLabels(params string[] channels)
    {
        var directory = Directory.CreateTempSubdirectory("mobile-labels-");
        try
        {
            var values = Directory.CreateDirectory(Path.Combine(directory.FullName, "Platforms", "Android", "Resources", "values"));
            File.WriteAllText(Path.Combine(values.FullName, "Strings.xml"), Strings);

            // Named like the app head, which the targets are scoped to.
            var project = Path.Combine(directory.FullName, "DailyPlants.proj");
            File.WriteAllText(project, $"""
                <Project>
                  <PropertyGroup>
                    <TargetPlatformIdentifier>android</TargetPlatformIdentifier>
                    <IntermediateOutputPath>obj/</IntermediateOutputPath>
                  </PropertyGroup>
                  <Import Project="{Path.Combine(BuildFiles.RepoRoot, "src", "MobileLabels.targets")}" />
                  <Target Name="_CompileResources" />
                </Project>
                """);

            foreach (var channel in channels)
            {
                var (exitCode, output) = BuildFiles.RunMSBuild(project, "_CompileResources", [$"-p:AppChannel={channel}"]);
                exitCode.Should().Be(0, output);
            }

            return File.ReadAllText(Path.Combine(directory.FullName, "obj", "res", "values", "strings.xml"));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}

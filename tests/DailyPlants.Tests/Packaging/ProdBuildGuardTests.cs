using System.Xml.Linq;

namespace DailyPlants.Tests.Packaging;

[TestClass]
public class ProdBuildGuardTests
{
    private static readonly XNamespace Foundation = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
    private static readonly XNamespace Uap = "http://schemas.microsoft.com/appx/manifest/uap/windows10";

    [TestMethod]
    [DataRow("0.8.42", "0")]
    [DataRow("0.8.42", "800042")]
    [DataRow("1.0.0", "1")]
    public void AssertStoreVersionStamped_VersionInjected_Passes(string displayVersion, string version)
    {
        var result = RunGuard("AssertStoreVersionStamped", ProdCi("-p:CiVersionInjected=true", $"-p:ApplicationDisplayVersion={displayVersion}", $"-p:ApplicationVersion={version}"));

        result.ExitCode.Should().Be(0, result.Output);
    }

    [TestMethod]
    public void AssertStoreVersionStamped_ApplicationVersionNotInjected_Fails()
    {
        var result = RunGuard("AssertStoreVersionStamped", ProdCi("-p:CiVersionInjected=true", "-p:ApplicationDisplayVersion=0.8.42"));

        result.ExitCode.Should().NotBe(0);
        result.Output.Should().Contain("ApplicationVersion was not passed");
    }

    [TestMethod]
    public void AssertStoreVersionStamped_DisplayVersionNotInjected_Fails()
    {
        var result = RunGuard("AssertStoreVersionStamped", ProdCi("-p:CiVersionInjected=true", "-p:ApplicationVersion=0"));

        result.ExitCode.Should().NotBe(0);
        result.Output.Should().Contain("ApplicationDisplayVersion was not passed");
    }

    [TestMethod]
    public void AssertStoreVersionStamped_CiVersionInjectedMissing_Fails()
    {
        var result = RunGuard("AssertStoreVersionStamped", ProdCi("-p:ApplicationDisplayVersion=0.8.42", "-p:ApplicationVersion=0"));

        result.ExitCode.Should().NotBe(0);
        result.Output.Should().Contain("CiVersionInjected");
    }

    [TestMethod]
    public void AssertStoreVersionStamped_DevChannel_DoesNotRequireInjection()
    {
        var result = RunGuard("AssertStoreVersionStamped", ["-p:ContinuousIntegrationBuild=true", "-p:AppChannel=Dev"]);

        result.ExitCode.Should().Be(0, result.Output);
    }

    [TestMethod]
    public void StampWindowsDevIdentity_DevChannel_RenamesTheStoreIdentity()
    {
        var result = RunGuard("StampWindowsDevIdentity", Windows("Dev"));

        result.ExitCode.Should().Be(0, result.Output);
        result.Manifest.Name.Should().Be("dev.mzikmund.dailyplants.dev");
        result.Manifest.Publisher.Should().Be("CN=Store", "the Dev certificate is generated from whatever publisher the manifest has");
        result.Manifest.DisplayNames.Should().AllBe("Daily Plants Dev");
    }

    [TestMethod]
    public void StampWindowsDevIdentity_ProdChannel_ShipsTheStoreIdentity()
    {
        var result = RunGuard("StampWindowsDevIdentity", Windows("Prod"));

        result.ExitCode.Should().Be(0, result.Output);
        result.Manifest.Name.Should().Be("Store.App");
        result.Manifest.DisplayNames.Should().AllBe("ms-resource:AppName");
    }

    [TestMethod]
    public void NbgvAndroidTarget_CiVersionInjected_KeepsTheInjectedVersion()
    {
        var output = RunNbgvAndroidTarget("-p:CiVersionInjected=true", "-p:ApplicationDisplayVersion=0.8.172", "-p:ApplicationVersion=800172");

        output.Should().Contain("ANDROID_VERSION=800172|0.8.172");
    }

    [TestMethod]
    public void NbgvAndroidTarget_NoInjection_LetsNbgvDeriveTheVersion()
    {
        var output = RunNbgvAndroidTarget();

        output.Should().Contain($"ANDROID_VERSION={(8 << 16) + 172}|0.8.172-dev");
    }

    private static string[] ProdCi(params string[] properties) => ["-p:ContinuousIntegrationBuild=true", "-p:AppChannel=Prod", .. properties];

    private static string[] Windows(string channel) =>
        ["-p:TargetPlatformIdentifier=windows", $"-p:AppChannel={channel}", "-p:ApplicationId=dev.mzikmund.dailyplants.dev", "-p:ApplicationTitle=Daily Plants Dev"];

    private static GuardResult RunGuard(string target, string[] properties)
    {
        var directory = Directory.CreateTempSubdirectory("prod-build-guard-");
        try
        {
            var manifest = Path.Combine(directory.FullName, "Package.appxmanifest");
            File.WriteAllText(manifest, $"""
                <Package xmlns="{Foundation}" xmlns:uap="{Uap}">
                  <Identity Name="Store.App" Publisher="CN=Store" Version="0.0.0.0" />
                  <Properties><DisplayName>ms-resource:AppName</DisplayName></Properties>
                  <Applications>
                    <Application Id="App">
                      <uap:VisualElements DisplayName="ms-resource:AppName">
                        <uap:DefaultTile ShortName="ms-resource:AppName" />
                      </uap:VisualElements>
                    </Application>
                  </Applications>
                </Package>
                """);

            // The real props and targets, with the SDKs' evaluation-time defaults between them.
            var project = Path.Combine(directory.FullName, "guard.proj");
            File.WriteAllText(project, $"""
                <Project>
                  <Import Project="{BuildFiles.Props}" />
                  <PropertyGroup>
                    <ApplicationVersion Condition="'$(ApplicationVersion)' == ''">1</ApplicationVersion>
                    <ApplicationDisplayVersion Condition="'$(ApplicationDisplayVersion)' == ''">1.0.0</ApplicationDisplayVersion>
                  </PropertyGroup>
                  <ItemGroup>
                    <AppxManifest Include="Package.appxmanifest" />
                  </ItemGroup>
                  <Import Project="{BuildFiles.Targets}" />
                </Project>
                """);

            var (exitCode, output) = BuildFiles.RunMSBuild(project, target, properties);

            var root = XDocument.Load(manifest).Root!;
            var identity = root.Element(Foundation + "Identity")!;
            List<string> displayNames =
            [
                (string)root.Descendants(Foundation + "DisplayName").Single(),
                (string)root.Descendants(Uap + "VisualElements").Single().Attribute("DisplayName")!,
                (string)root.Descendants(Uap + "DefaultTile").Single().Attribute("ShortName")!,
            ];
            return new(exitCode, output, new((string)identity.Attribute("Name")!, (string)identity.Attribute("Publisher")!, displayNames));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    // Runs the real NBGV targets (only GetBuildVersion is stubbed) up to Android's _GetAndroidPackageName,
    // and returns the version the Android SDK would then read.
    private static string RunNbgvAndroidTarget(params string[] properties)
    {
        var directory = Directory.CreateTempSubdirectory("nbgv-android-");
        try
        {
            var project = Path.Combine(directory.FullName, "android.proj");
            File.WriteAllText(project, $"""
                <Project>
                  <Import Project="{BuildFiles.Props}" />
                  <PropertyGroup>
                    <TargetPlatformIdentifier>android</TargetPlatformIdentifier>
                    <ApplicationVersion Condition="'$(ApplicationVersion)' == ''">1</ApplicationVersion>
                    <ApplicationDisplayVersion Condition="'$(ApplicationDisplayVersion)' == ''">1.0.0</ApplicationDisplayVersion>
                    <BuildVersion>0.8.172.0</BuildVersion>
                    <BuildNumber>172</BuildNumber>
                    <Version>0.8.172-dev</Version>
                  </PropertyGroup>
                  <Import Project="{BuildFiles.NbgvTargets()}" />
                  <Target Name="GetBuildVersion" />
                  <Target Name="_GetAndroidPackageName" />
                  <Target Name="Report" AfterTargets="_GetAndroidPackageName">
                    <Message Importance="high" Text="ANDROID_VERSION=$(ApplicationVersion)|$(ApplicationDisplayVersion)" />
                  </Target>
                </Project>
                """);

            var (exitCode, output) = BuildFiles.RunMSBuild(project, "_GetAndroidPackageName", properties);

            exitCode.Should().Be(0, output);
            return output;
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private sealed record ManifestIdentity(string Name, string Publisher, IReadOnlyList<string> DisplayNames);

    private sealed record GuardResult(int ExitCode, string Output, ManifestIdentity Manifest);
}

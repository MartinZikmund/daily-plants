using System.Text.RegularExpressions;

namespace DailyPlants.Tests.Packaging;

[TestClass]
public partial class WorkflowTests
{
    [TestMethod]
    [DataRow("_build-android.yml")]
    [DataRow("_build-ios.yml")]
    [DataRow("_build-windows.yml")]
    [DataRow("_build-macos.yml")]
    [DataRow("_build-wasm.yml")]
    public void BuildWorkflow_EveryPackagingCommand_InjectsTheVersion(string workflow)
    {
        var commands = PackagingCommand().Matches(BuildFiles.Workflow(workflow)).Select(match => match.Value).ToList();

        commands.Should().NotBeEmpty();
        commands.Should().AllSatisfy(command => command.Should()
            .Contain("-p:CiVersionInjected=true")
            .And.Contain("-p:ApplicationDisplayVersion=")
            .And.Contain("-p:ApplicationVersion=")
            .And.Contain("-p:AppChannel="));
    }

    // The Mac App Store build goes through the script, which has to pass the same properties.
    [TestMethod]
    public void MacPackageScript_InjectsTheVersionAndBuildsProd()
    {
        var script = File.ReadAllText(Path.Combine(BuildFiles.RepoRoot, "docs", "store", "macos", "package-app.sh"));

        script.Should().Contain("-p:CiVersionInjected=true").And.Contain("-p:ApplicationDisplayVersion=$APP_VERSION");
        PackagingCommand().Matches(script).Should().AllSatisfy(match => match.Value.Should().Contain("-p:AppChannel=Prod"));
    }

    // Anything that reaches a store lives in release.yml or the break-glass store-ops.yml.
    [TestMethod]
    [DataRow("build-main.yml")]
    [DataRow("ci.yml")]
    [DataRow("_build-android.yml")]
    [DataRow("_build-ios.yml")]
    [DataRow("_build-windows.yml")]
    [DataRow("_build-macos.yml")]
    [DataRow("_build-wasm.yml")]
    public void NonReleaseWorkflow_NeverPublishes(string workflow)
    {
        var yaml = BuildFiles.Workflow(workflow);

        yaml.Should().NotContainAny("upload-google-play", "upload-testflight-build", "msstore", "static-web-apps-deploy", "environment:");
    }

    [TestMethod]
    public void ReleaseWorkflow_RunsOnlyOnReleaseBranches()
    {
        var on = OnBlock(BuildFiles.Workflow("release.yml"));

        on.Should().Contain("""branches: [ "release/v**" ]""").And.NotContain("main");
    }

    [TestMethod]
    public void StaticWebAppsWorkflow_OnlyDeploysPullRequestPreviews()
    {
        var on = OnBlock(BuildFiles.Workflow("static-web-apps-deploy.yml"));

        on.Should().Contain("pull_request:").And.NotContain("push:");
    }

    // $ErrorActionPreference does not stop on a native non-zero exit, so a failed
    // `msstore submission update` would still be followed by `msstore submission publish`.
    [TestMethod]
    [DataRow("release.yml")]
    [DataRow("store-ops.yml")]
    public void WorkflowScripts_RunningMsstore_StopOnNativeFailure(string workflow)
    {
        var scripts = BuildFiles.Workflow(workflow).Split("run: |")
            .Skip(1)
            .Where(script => script.Contains("msstore reconfigure"))
            .ToList();

        scripts.Should().NotBeEmpty();
        scripts.Should().AllSatisfy(script => script.Should().Contain("$PSNativeCommandUseErrorActionPreference = $true"));
    }

    // `winapp sign` has no --cert-password; the unknown option made it fail, and the failure was ignored.
    [TestMethod]
    public void WindowsSignStep_UsesWinappSignSyntax()
    {
        var script = SigningScript();

        script.Should().NotContain("--cert-password");
        script.Should().MatchRegex(@"winapp sign ""[^""]+"" \$pfx --password ");
    }

    // A password-less Get-PfxCertificate prompts on stdin and hangs the job.
    [TestMethod]
    public void WindowsSignStep_NeverPromptsForThePfxPassword() =>
        SigningScript().Should().MatchRegex(@"Get-PfxCertificate [^\r\n]*-Password ");

    [TestMethod]
    public void WindowsSignStep_StopsOnNativeFailureWithinATimeout()
    {
        SigningScript().Should().Contain("$PSNativeCommandUseErrorActionPreference = $true");
        SigningStep().Should().MatchRegex(@"timeout-minutes: \d+");
    }

    private static string OnBlock(string yaml)
    {
        var start = yaml.IndexOf("\non:", StringComparison.Ordinal);
        var end = yaml.IndexOf("\nconcurrency:", start, StringComparison.Ordinal);
        if (end < 0)
        {
            end = yaml.IndexOf("\nenv:", start, StringComparison.Ordinal);
        }

        return yaml[start..end];
    }

    private static string SigningStep()
    {
        var yaml = BuildFiles.Workflow("_build-windows.yml");
        var start = yaml.IndexOf("- name: Sign the Dev bundle", StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1);
        var end = yaml.IndexOf("\n      - ", start + 1, StringComparison.Ordinal);
        return yaml[start..end];
    }

    private static string SigningScript()
    {
        var step = SigningStep();
        return step[step.IndexOf("run: |", StringComparison.Ordinal)..];
    }

    [GeneratedRegex(@"dotnet (?:publish|build)\b(?:[^\n]*\\\r?\n)*[^\n]*")]
    private static partial Regex PackagingCommand();
}

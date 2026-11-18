using System.Text.RegularExpressions;

namespace DailyPlants.Tests.Packaging;

/// <summary>
/// Runs src/WorktreeIdentity.props and .targets in a scratch repository whose .git is faked, so detection is tested too.
/// </summary>
[TestClass]
public partial class WorktreeIdentityTests
{
    private const string DevId = "dev.mzikmund.dailyplants.dev";
    private const string DevTitle = "Daily Plants Dev";

    [TestMethod]
    public void LinkedWorktree_Dev_SuffixesTheIdAndTitle()
    {
        var identity = Evaluate(Git.Worktree("feature-x"));

        identity.Package.Should().MatchRegex(@"^dev\.mzikmund\.dailyplants\.dev\.wtfeaturex[0-9a-f]{6}$");
        identity.Title.Should().Be("Daily Plants Dev (feature-x)");
    }

    [TestMethod]
    public void LinkedWorktree_IdSegment_IsStable()
    {
        // StableStringHash must not change with the SDK, or every worktree install gets orphaned with its data.
        // The value is the one the app template documents for this name.
        Evaluate(Git.Worktree("worktree-identity")).Package.Should().Be($"{DevId}.wtworktree1b71ff");
    }

    [TestMethod]
    public void RelativeGitdir_IsDetected() =>
        Evaluate(Git.RelativeWorktree("feature-x")).Title.Should().Be("Daily Plants Dev (feature-x)");

    [TestMethod]
    public void MainCheckout_KeepsTheDevIdentity()
    {
        var identity = Evaluate(Git.MainCheckout);

        identity.Should().Be(new Identity(DevId, DevTitle));
    }

    [TestMethod]
    public void Submodule_KeepsTheDevIdentity() =>
        Evaluate(Git.Submodule).Should().Be(new Identity(DevId, DevTitle));

    [TestMethod]
    [DataRow("-p:AppChannel=Prod")]
    [DataRow("-p:CI=true")]
    [DataRow("-p:ContinuousIntegrationBuild=true")]
    [DataRow("-p:EnableWorktreeIdentity=false")]
    public void LinkedWorktree_SwitchedOff_IsNotSuffixed(string property)
    {
        var identity = Evaluate(Git.Worktree("feature-x"), property);

        identity.Package.Should().NotContain(".wt");
        identity.Title.Should().NotContain("feature-x");
    }

    [TestMethod]
    public void Prod_IgnoresAnExplicitWorktreeName() =>
        Evaluate(Git.MainCheckout, "-p:AppChannel=Prod", "-p:AppWorktreeName=oops")
            .Should().Be(new Identity("dev.mzikmund.dailyplants", "Daily Plants"));

    [TestMethod]
    public void WorktreePrefix_IsDroppedFromTheTitleButNotTheId()
    {
        var identity = Evaluate(Git.Worktree("worktree-identity"));

        identity.Package.Should().StartWith($"{DevId}.wtworktree");
        identity.Title.Should().Be("Daily Plants Dev (identity)");
    }

    [TestMethod]
    [DataRow("agent-acf0db2bf96b42bc5")]
    [DataRow("a-really-long-worktree-name-for-a-feature-nobody-will-finish")]
    [DataRow("Ünïcode & <xml> (v2)")]
    public void AnyWorktreeName_FitsTheStoreLimits(string name)
    {
        var identity = Evaluate(Git.MainCheckout, $"-p:AppWorktreeName={name}");

        // MSIX: Identity/@Name up to 50 characters, uap:DefaultTile/@ShortName up to 40.
        identity.Package.Length.Should().BeLessThanOrEqualTo(50);
        identity.Package.Should().MatchRegex(@"^[a-z][a-z0-9.]*\.wt[a-z0-9]{14}$");
        identity.Title.Length.Should().BeLessThanOrEqualTo(40);
        identity.Title.Should().MatchRegex(@"^Daily Plants Dev \([A-Za-z0-9 ._-]+\)$");
    }

    [TestMethod]
    public void Ios_UsesTheShortTag() =>
        Evaluate(Git.Worktree("feature-x"), "-p:TargetPlatformIdentifier=ios").Title.Should().Be("Daily Plants Dev [FX]");

    [TestMethod]
    public void GenerateWorktreeInfo_LinkedWorktree_WritesTheName() =>
        GenerateWorktreeInfo(Git.Worktree("worktree-identity")).Should().Contain("""WorktreeName = "identity";""");

    [TestMethod]
    public void GenerateWorktreeInfo_MainCheckout_WritesAnEmptyName() =>
        GenerateWorktreeInfo(Git.MainCheckout).Should().Contain("""WorktreeName = "";""");

    private static Identity Evaluate(Action<string> git, params string[] properties)
    {
        var output = Run(git, "Report", properties);
        var match = ReportPattern().Match(output);
        match.Success.Should().BeTrue(output);

        return new(match.Groups["id"].Value, match.Groups["title"].Value);
    }

    private static string GenerateWorktreeInfo(Action<string> git)
    {
        string? generated = null;
        Run(git, "GenerateWorktreeInfo", [], project =>
            generated = File.ReadAllText(Directory.GetFiles(Path.Combine(project, "obj"), "AppEnvironment.Worktree.*.g.cs").Single()));

        return generated!;
    }

    private static string Run(Action<string> git, string target, string[] properties, Action<string>? inspect = null)
    {
        var repo = Directory.CreateTempSubdirectory("worktree-identity-");
        try
        {
            File.WriteAllText(Path.Combine(repo.FullName, "version.json"), "{}");
            git(repo.FullName);

            var src = Directory.CreateDirectory(Path.Combine(repo.FullName, "src"));
            foreach (var file in new[] { "WorktreeIdentity.props", "WorktreeIdentity.targets" })
            {
                File.Copy(Path.Combine(BuildFiles.RepoRoot, "src", file), Path.Combine(src.FullName, file));
            }

            // Named like the app head, which GenerateWorktreeInfo is scoped to. The channel values mirror DailyPlants.csproj.
            var head = Directory.CreateDirectory(Path.Combine(src.FullName, "DailyPlants"));
            var project = Path.Combine(head.FullName, "DailyPlants.proj");
            File.WriteAllText(project, $"""
                <Project>
                  <PropertyGroup>
                    <AppChannel Condition="'$(AppChannel)' == ''">Dev</AppChannel>
                    <IntermediateOutputPath>obj/</IntermediateOutputPath>
                  </PropertyGroup>
                  <Import Project="../WorktreeIdentity.props" />
                  <PropertyGroup Condition="'$(AppChannel)' == 'Prod'">
                    <ApplicationTitle>Daily Plants</ApplicationTitle>
                    <ApplicationId>dev.mzikmund.dailyplants</ApplicationId>
                  </PropertyGroup>
                  <PropertyGroup Condition="'$(AppChannel)' == 'Dev'">
                    <ApplicationTitle>{DevTitle}</ApplicationTitle>
                    <ApplicationId>{DevId}</ApplicationId>
                  </PropertyGroup>
                  <Import Project="../WorktreeIdentity.targets" />
                  <Target Name="Report">
                    <Message Importance="high" Text="IDENTITY=$(ApplicationId)|$(ApplicationTitle)|" />
                  </Target>
                </Project>
                """);

            // Ambient CI variables would switch the feature off for every case.
            var (exitCode, output) = BuildFiles.RunMSBuild(project, target, ["-p:CI=", "-p:ContinuousIntegrationBuild=", .. properties]);
            exitCode.Should().Be(0, output);
            inspect?.Invoke(head.FullName);

            return output;
        }
        finally
        {
            repo.Delete(recursive: true);
        }
    }

    [GeneratedRegex(@"IDENTITY=(?<id>[^|]*)\|(?<title>[^|]*)\|")]
    private static partial Regex ReportPattern();

    private sealed record Identity(string Package, string Title);

    /// <summary>Lays out the repository's .git the way each kind of checkout has it.</summary>
    private static class Git
    {
        public static void MainCheckout(string repo) => Directory.CreateDirectory(Path.Combine(repo, ".git"));

        public static void Submodule(string repo) =>
            File.WriteAllText(Path.Combine(repo, ".git"), "gitdir: ../parent/.git/modules/daily-plants");

        public static Action<string> Worktree(string name) =>
            repo => File.WriteAllText(Path.Combine(repo, ".git"), $"gitdir: {Path.Combine(Path.GetTempPath(), "main", ".git", "worktrees", name)}");

        public static Action<string> RelativeWorktree(string name) =>
            repo => File.WriteAllText(Path.Combine(repo, ".git"), $"gitdir: ../main/.git/worktrees/{name}");
    }
}

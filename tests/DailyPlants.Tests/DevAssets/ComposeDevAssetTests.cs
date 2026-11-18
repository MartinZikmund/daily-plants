using DailyPlants.Build;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using DailyPlants.Tests.TestDoubles;

namespace DailyPlants.Tests.DevAssets;

[TestClass]
public sealed class ComposeDevAssetTests
{
    const string Logo = """<svg xmlns="http://www.w3.org/2000/svg" width="450" height="450" viewBox="0 0 50 50"><rect width="50" height="50" fill="#7a67f8" /></svg>""";

    string _project = null!;
    FakeBuildEngine _engine = null!;

    [TestInitialize]
    public void Setup()
    {
        _project = Path.Combine(Path.GetTempPath(), "devassets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_project, "Assets"));
        File.WriteAllText(Path.Combine(_project, "Assets", "icon_foreground.svg"), Logo);
        File.WriteAllText(Path.Combine(_project, "Assets", "splash_screen.svg"), Logo);
        _engine = new FakeBuildEngine();
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_project, recursive: true);

    [TestMethod]
    public void Execute_Icon_PointsForegroundFileAtHashedCopy()
    {
        TaskItem icon = new("Assets/icon.svg");
        icon.SetMetadata("ForegroundFile", "Assets/icon_foreground.svg");
        icon.SetMetadata("ForegroundScale", "0.6");

        ITaskItem result = Run("Icon", "windows", icon).Single();

        result.ItemSpec.Should().Be("Assets/icon.svg");
        result.GetMetadata("ForegroundScale").Should().Be("0.6");
        string badged = result.GetMetadata("ForegroundFile");
        Path.GetFileName(badged).Should().Be("icon_foreground.svg");
        Path.GetFileName(Path.GetDirectoryName(badged)).Should().MatchRegex("^[0-9a-f]{12}$");
        Path.GetDirectoryName(Path.GetDirectoryName(badged)).Should().Be(Path.Combine(_project, "obj", "devassets"));
        File.ReadAllText(badged).Should().Contain("dev-badge");
    }

    [TestMethod]
    public void Execute_Splash_ReplacesItemSpecAndKeepsMetadata()
    {
        TaskItem splash = new("Assets/splash_screen.svg");
        splash.SetMetadata("BaseSize", "300,300");
        splash.SetMetadata("Scale", "0.85");

        ITaskItem result = Run("Splash", "android", splash).Single();

        Path.IsPathRooted(result.ItemSpec).Should().BeTrue();
        Path.GetFileName(result.ItemSpec).Should().Be("splash_screen.svg");
        result.GetMetadata("BaseSize").Should().Be("300,300");
        result.GetMetadata("Scale").Should().Be("0.85");
    }

    [TestMethod]
    public void Execute_SameInputTwice_ReusesTheFile()
    {
        TaskItem splash = new("Assets/splash_screen.svg");
        string first = Run("Splash", "windows", splash).Single().ItemSpec;
        DateTime written = File.GetLastWriteTimeUtc(first);

        string second = Run("Splash", "windows", splash).Single().ItemSpec;

        second.Should().Be(first);
        File.GetLastWriteTimeUtc(second).Should().Be(written);
    }

    [TestMethod]
    public void Execute_DifferentPlacement_GetsDifferentHash()
    {
        TaskItem splash = new("Assets/splash_screen.svg");

        string windows = Run("Splash", "windows", splash).Single().ItemSpec;
        string android = Run("Splash", "android", splash).Single().ItemSpec;

        android.Should().NotBe(windows);
    }

    [TestMethod]
    public void Execute_MissingSource_WarnsAndKeepsTheItem()
    {
        TaskItem splash = new("Assets/missing.svg");

        ITaskItem result = Run("Splash", "windows", splash).Single();

        result.Should().BeSameAs(splash);
        _engine.Warnings.Should().ContainSingle(w => w.Code == "DEVASSETS001");
    }

    [TestMethod]
    public void Execute_MalformedSource_WarnsAndKeepsTheItem()
    {
        File.WriteAllText(Path.Combine(_project, "Assets", "splash_screen.svg"), "<svg");
        TaskItem splash = new("Assets/splash_screen.svg");

        ITaskItem result = Run("Splash", "windows", splash).Single();

        result.Should().BeSameAs(splash);
        _engine.Warnings.Should().ContainSingle(w => w.Code == "DEVASSETS001");
    }

    [TestMethod]
    public void Execute_SourceAlreadyBadged_LeavesItemUntouched()
    {
        ITaskItem badged = Run("Splash", "windows", new TaskItem("Assets/splash_screen.svg")).Single();

        ITaskItem again = Run("Splash", "windows", badged).Single();

        again.Should().BeSameAs(badged);
    }

    [TestMethod]
    public void Execute_IconWithoutForegroundFile_LeavesItemUntouched()
    {
        TaskItem icon = new("Assets/icon_foreground.svg");

        Run("Icon", "windows", icon).Single().Should().BeSameAs(icon);
    }

    [TestMethod]
    public void Execute_BadgeCannotFitMask_WarnsDevAssets002()
    {
        TaskItem icon = new("Assets/icon.svg");
        icon.SetMetadata("ForegroundFile", "Assets/icon_foreground.svg");
        icon.SetMetadata("ForegroundScale", "3");

        Run("Icon", "android", icon);

        _engine.Warnings.Should().ContainSingle(w => w.Code == "DEVASSETS002");
    }

    [TestMethod]
    public void Execute_NoLabel_DrawsDevBadge()
    {
        string badged = Run("Splash", "windows", new TaskItem("Assets/splash_screen.svg")).Single().ItemSpec;

        File.ReadAllText(badged).Should().Contain(BadgeLabel.Dev.Fill);
    }

    [TestMethod]
    [DataRow("CI")]
    [DataRow("ci")]
    public void Execute_CiLabel_DrawsCiBadge(string label)
    {
        string badged = Run("Splash", "windows", label, new TaskItem("Assets/splash_screen.svg")).Single().ItemSpec;

        File.ReadAllText(badged).Should().Contain(BadgeLabel.CI.Fill);
        _engine.Warnings.Should().BeEmpty();
    }

    [TestMethod]
    public void Execute_UnknownLabel_WarnsAndDrawsDevBadge()
    {
        string badged = Run("Splash", "windows", "BETA", new TaskItem("Assets/splash_screen.svg")).Single().ItemSpec;

        _engine.Warnings.Should().ContainSingle(w => w.Code == "DEVASSETS003");
        File.ReadAllText(badged).Should().Contain(BadgeLabel.Dev.Fill);
    }

    ITaskItem[] Run(string kind, string platform, params ITaskItem[] assets) => Run(kind, platform, null, assets);

    ITaskItem[] Run(string kind, string platform, string? label, params ITaskItem[] assets)
    {
        ComposeDevAsset task = new()
        {
            BuildEngine = _engine,
            Assets = assets,
            Kind = kind,
            TargetPlatform = platform,
            ProjectDirectory = _project,
            OutputRoot = Path.Combine("obj", "devassets"),
        };
        if (label is not null)
        {
            task.Label = label;
        }

        task.Execute().Should().BeTrue();
        return task.Result;
    }
}

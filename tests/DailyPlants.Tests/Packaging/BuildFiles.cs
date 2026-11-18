using System.Diagnostics;
using System.Xml.Linq;

namespace DailyPlants.Tests.Packaging;

/// <summary>
/// Runs the repository's real props and targets in a scratch project, so the build logic is tested without a full app build.
/// </summary>
internal static class BuildFiles
{
    public static string RepoRoot { get; } = FindRepoRoot();

    public static string Props => Path.Combine(RepoRoot, "Directory.Build.props");

    public static string Targets => Path.Combine(RepoRoot, "src", "Directory.Build.targets");

    public static string Workflow(string name) => File.ReadAllText(Path.Combine(RepoRoot, ".github", "workflows", name));

    public static string NbgvTargets()
    {
        var version = XDocument.Load(Path.Combine(RepoRoot, "Directory.Packages.props"))
            .Descendants("PackageVersion")
            .Single(package => (string?)package.Attribute("Include") == "Nerdbank.GitVersioning")
            .Attribute("Version")!.Value;
        var packages = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");

        return Path.Combine(packages, "nerdbank.gitversioning", version, "build", "Nerdbank.GitVersioning.targets");
    }

    public static (int ExitCode, string Output) RunMSBuild(string project, string target, IEnumerable<string> properties)
    {
        ProcessStartInfo startInfo = new("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(project)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in new[] { "msbuild", project, "-nologo", "-nodeReuse:false", $"-t:{target}" }.Concat(properties))
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        var error = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, output + error.Result);
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "version.json")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root (version.json) not found.");
    }
}

using System.Buffers.Binary;
using System.Diagnostics;
using System.Xml.Linq;

namespace DailyPlants.Tests.Packaging;

/// <summary>
/// Dev builds swap Platforms/Windows for export-windows-assets.cs run on the badged art, so the script has to render
/// exactly that set, and the set has to cover everything the manifest points at.
/// </summary>
[TestClass]
public sealed class WindowsAssetsTests
{
    static readonly XNamespace Foundation = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
    static readonly XNamespace Uap = "http://schemas.microsoft.com/appx/manifest/uap/windows10";

    static string App => Path.Combine(BuildFiles.RepoRoot, "src", "DailyPlants");

    static string WindowsFolder => Path.Combine(App, "Platforms", "Windows");

    [TestMethod]
    public void Manifest_EveryImage_HasCheckedInPngs()
    {
        XDocument manifest = XDocument.Load(Path.Combine(App, "Package.appxmanifest"));
        List<string> images = [.. manifest.Descendants(Foundation + "Logo").Select(e => e.Value)];
        images.AddRange(manifest.Descendants().Where(e => e.Name.Namespace == Uap).SelectMany(e => e.Attributes())
            .Where(a => a.Name.LocalName.EndsWith("Logo", StringComparison.Ordinal) || a.Name.LocalName == "Image")
            .Select(a => a.Value));

        images.Should().HaveCount(7);
        foreach (string image in images)
        {
            image.Should().StartWith(@"Platforms\Windows\");
            Directory.GetFiles(WindowsFolder, Path.GetFileNameWithoutExtension(image) + ".scale-*.png").Should().NotBeEmpty(image);
        }
    }

    [TestMethod]
    public void ExportScript_RendersExactlyTheCheckedInSet()
    {
        DirectoryInfo output = Directory.CreateTempSubdirectory("windows-assets-");
        try
        {
            string script = Path.Combine(BuildFiles.RepoRoot, "assets", "app-icon", "export-windows-assets.cs");
            ProcessStartInfo startInfo = new("dotnet")
            {
                WorkingDirectory = Path.GetDirectoryName(script)!,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (string argument in new[] { "run", script, "--", Path.Combine(App, "Assets", "Icons", "icon_foreground.svg"), output.FullName })
            {
                startInfo.ArgumentList.Add(argument);
            }

            using Process process = Process.Start(startInfo)!;
            Task<string> error = process.StandardError.ReadToEndAsync();
            string log = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            process.ExitCode.Should().Be(0, log + error.Result);

            Dictionary<string, (int, int)> rendered = Sizes(output.FullName);
            rendered.Should().BeEquivalentTo(Sizes(WindowsFolder));
        }
        finally
        {
            output.Delete(recursive: true);
        }
    }

    static Dictionary<string, (int Width, int Height)> Sizes(string folder) =>
        Directory.GetFiles(folder, "*.png").ToDictionary(file => Path.GetFileName(file), PngSize);

    // The IHDR chunk: width and height, big-endian, at bytes 16 and 20.
    static (int Width, int Height) PngSize(string path)
    {
        byte[] header = new byte[24];
        using FileStream stream = File.OpenRead(path);
        stream.ReadExactly(header);
        return (BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(16)), BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(20)));
    }
}

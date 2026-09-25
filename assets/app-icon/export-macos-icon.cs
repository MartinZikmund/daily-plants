#:package Svg.Skia@3.0.6

using SkiaSharp;
using Svg.Skia;

// Renders the macOS app icon sizes: the art on a green rounded square, laid out on Apple's icon grid
// (an 824 px plate with a soft shadow on a 1024 px canvas).
if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: dotnet run export-macos-icon.cs -- <art.svg> <output folder>");
    return 1;
}

string svgPath = args[0];
string outDir = args[1];
Directory.CreateDirectory(outDir);

using SKSvg svg = new();
SKPicture? picture = svg.Load(svgPath);
if (picture is null)
{
    Console.Error.WriteLine($"Could not load {svgPath}");
    return 1;
}

SKRect viewBox = picture.CullRect;
SKColor plateColor = SKColor.Parse("#4C7A56");
const float Canvas = 1024f, Plate = 824f, Corner = 185f, Art = .8f;

int written = 0;
foreach (int points in new[] { 16, 32, 128, 256, 512 })
{
    foreach (int scale in new[] { 1, 2 })
    {
        int size = points * scale;
        SKImageInfo info = new(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
        using SKSurface surface = SKSurface.Create(info);
        SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.Scale(size / Canvas);

        SKRect plate = SKRect.Create((Canvas - Plate) / 2f, (Canvas - Plate) / 2f, Plate, Plate);
        using (SKPaint shadow = new() { IsAntialias = true, Color = new SKColor(0, 0, 0, 76), ImageFilter = SKImageFilter.CreateBlur(14, 14) })
        {
            canvas.DrawRoundRect(SKRect.Create(plate.Left, plate.Top + 10, Plate, Plate), Corner, Corner, shadow);
        }
        using (SKPaint fill = new() { IsAntialias = true, Color = plateColor })
        {
            canvas.DrawRoundRect(plate, Corner, Corner, fill);
        }

        float side = Plate * Art;
        canvas.Translate((Canvas - side) / 2f, (Canvas - side) / 2f);
        canvas.Scale(side / Math.Max(viewBox.Width, viewBox.Height));
        canvas.Translate(-viewBox.Left, -viewBox.Top);
        canvas.DrawPicture(picture);

        using SKImage image = surface.Snapshot();
        using SKData png = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(Path.Combine(outDir, $"icon{points}x{points}@{scale}x.png"), png.ToArray());
        written++;
    }
}

Console.WriteLine($"{written} images written to {outDir}");
return 0;

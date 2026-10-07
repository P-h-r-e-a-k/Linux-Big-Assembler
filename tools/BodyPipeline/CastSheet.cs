using System.Text.Json;
using LbaBodyStudio;
using SkiaSharp;

namespace BodyPipeline;

// castsheet <game folder> <out.png> [columns] [per page]: every race car a game folder's build made (its RACECARS.JSON: the cars made by
// hand and the cast's, RaceTrackCharacterCars) on one sheet -- each seen three-quarters from the front and above, its number, its name,
// who drives it and the island -- and the same in pages beside it (<out>_1.png, ...).
//
// Drawn with Skia rather than System.Drawing, which is Windows-only from .NET 7 on: Skia is what the rest of this port writes pictures
// with (FlatBitmap), and unlike the little digit font the other contact sheets use, these labels are real names, so they need real text.
internal static class CastSheet
{
    private sealed record Entry(int Number, string Name, string Driver, string Island, int Entity, int Generic, int Body, int Small);

    public static int Run(string folder, string output, int columns = 8, int perPage = 48)
    {
        var entries = JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(Path.Combine(folder, "RACECARS.JSON"))) ?? new();
        var bodies = new Hqr(Path.Combine(folder, "BODY.HQR"));
        var texture = new Hqr(Path.Combine(folder, "RESS.HQR")).Read(6);
        var palette = Generator.Palette(folder);
        const int W = 320, H = 236, Text = 74;
        var pictures = new List<(Entry Entry, FlatImage? Picture)>();
        foreach (var e in entries)
        {
            try
            {
                var model = Body.Read(bodies.Read(e.Body), 2, allowStatic: true);
                model.TexturePage = texture;
                pictures.Add((e, View(model, palette, W, H)));
            }
            catch (Exception error) { Console.WriteLine($"  {e.Number} {e.Name}: {error.Message}"); pictures.Add((e, null)); }
        }
        Save(pictures, output, columns, W, H, Text, $"The race cars: {entries.Count}, by number");
        var pages = (pictures.Count + perPage - 1) / perPage;
        for (var p = 0; p < pages; p++)
        {
            var part = pictures.Skip(p * perPage).Take(perPage).ToList();
            var name = Path.Combine(Path.GetDirectoryName(output) ?? ".", $"{Path.GetFileNameWithoutExtension(output)}_{p + 1}.png");
            Save(part, name, columns, W, H, Text, $"The race cars, {part[0].Entry.Number}-{part[^1].Entry.Number} (page {p + 1} of {pages})");
        }
        Console.WriteLine($"{output}: {entries.Count} cars; {pages} pages");
        return 0;
    }

    private static readonly uint Backdrop = Argb.Pack(40, 60, 90);

    // A body three-quarters from the front and above (as carviews' first view).
    private static FlatImage View(Body model, uint[] palette, int w, int h)
    {
        const float Yaw = 0.65f, Pitch = 0.45f;
        var world = model.World();
        var turned = world.Select(p =>
        {
            var q = new System.Numerics.Vector3(p.X * MathF.Cos(Yaw) + p.Z * MathF.Sin(Yaw), p.Y, -p.X * MathF.Sin(Yaw) + p.Z * MathF.Cos(Yaw));
            return new System.Numerics.Vector3(q.X, q.Y * MathF.Cos(Pitch) + q.Z * MathF.Sin(Pitch), -q.Y * MathF.Sin(Pitch) + q.Z * MathF.Cos(Pitch));
        }).ToArray();
        var lowest = turned.Min(p => p.Y);
        var copy = new Body { Game = 2, Lit = model.Lit, Static = model.Static, Header = model.Header, Textures = model.Textures, TexturePage = model.TexturePage, LightScale = model.LightScale };
        copy.Bones.AddRange(model.Bones); copy.Faces.AddRange(model.Faces); copy.Lines.AddRange(model.Lines); copy.Spheres.AddRange(model.Spheres);
        copy.Vertices.AddRange(turned);
        copy.SetWorld(turned.Select(p => p with { Y = p.Y - lowest }).ToArray());
        return Renderer.Render(copy, palette, w, h, 0, false, background: Backdrop, gridLine: Backdrop);
    }

    // Segoe UI is the Windows build's face; on Linux fontconfig substitutes whichever of these is installed.
    private static SKTypeface Face(bool bold)
        => new[] { "Segoe UI", "Noto Sans", "DejaVu Sans", "Liberation Sans" }
               .Select(f => SKTypeface.FromFamilyName(f, bold ? SKFontStyle.Bold : SKFontStyle.Normal))
               .FirstOrDefault(t => t is not null && !string.Equals(t.FamilyName, SKTypeface.Default.FamilyName, StringComparison.Ordinal))
           ?? SKTypeface.Default;

    private static SKPaint Pen(SKTypeface face, float size, SKColor colour)
        => new() { Typeface = face, TextSize = size, Color = colour, IsAntialias = true, SubpixelText = true };

    // System.Drawing's StringTrimming.EllipsisCharacter: Skia has no trimming of its own.
    private static string Fit(string text, SKPaint paint, float width)
    {
        if (paint.MeasureText(text) <= width) return text;
        for (var n = text.Length - 1; n > 0; n--)
        {
            var cut = text[..n] + "…";
            if (paint.MeasureText(cut) <= width) return cut;
        }
        return "…";
    }

    // Draws with the top of the text at `top`, as System.Drawing's DrawString does; Skia's own origin is the baseline.
    private static void Draw(SKCanvas canvas, string text, SKPaint paint, float x, float top)
        => canvas.DrawText(text, x, top - paint.FontMetrics.Ascent, paint);

    private static void Save(List<(Entry Entry, FlatImage? Picture)> cars, string output, int columns, int w, int h, int text, string title)
    {
        const int Top = 56;
        var rows = (cars.Count + columns - 1) / columns;
        var info = new SKImageInfo(columns * w, Top + rows * (h + text), SKColorType.Bgra8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("Could not make the sheet.");
        var canvas = surface.Canvas;
        canvas.Clear(new SKColor(24, 34, 52));

        using var bold = Face(bold: true);
        using var plain = Face(bold: false);
        using var titlePen = Pen(bold, 22, SKColors.White);
        using var namePen = Pen(bold, 14, SKColors.White);
        using var smallPen = Pen(plain, 12, new SKColor(215, 225, 240));
        using var dimPen = Pen(plain, 12, new SKColor(150, 170, 200));
        using var numberPen = Pen(bold, 20, SKColors.Black);
        using var badge = new SKPaint { Color = new SKColor(255, 200, 40, 200), IsAntialias = true };
        using var panel = new SKPaint { Color = new SKColor(32, 46, 70) };
        using var line = new SKPaint { Color = new SKColor(24, 34, 52), Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true };

        Draw(canvas, title, titlePen, 12, 12);
        for (var i = 0; i < cars.Count; i++)
        {
            var (e, picture) = cars[i];
            float x = i % columns * w, y = Top + i / columns * (h + text);
            if (picture is not null)
            {
                var pictureInfo = new SKImageInfo(picture.Width, picture.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
                using var image = SKImage.FromPixelCopy(pictureInfo, picture.Bgra, picture.Width * 4);
                if (image is not null) canvas.DrawImage(image, x, y);
            }
            canvas.DrawRect(x + 6, y + 6, 58, 30, badge);
            var number = e.Number.ToString();
            Draw(canvas, number, numberPen, x + 6 + (58 - numberPen.MeasureText(number)) / 2, y + 6 + (30 - (numberPen.FontMetrics.Descent - numberPen.FontMetrics.Ascent)) / 2);
            canvas.DrawRect(x, y + h, w, text, panel);
            Draw(canvas, Fit(e.Name, namePen, w - 14), namePen, x + 8, y + h + 6);
            Draw(canvas, Fit($"Driver: {e.Driver}", smallPen, w - 14), smallPen, x + 8, y + h + 28);
            Draw(canvas, Fit(e.Island, dimPen, w - 14), dimPen, x + 8, y + h + 47);
            canvas.DrawRect(x, y, w, h + text, line);
        }

        using var snapshot = surface.Snapshot();
        using var data = snapshot.Encode(SKEncodedImageFormat.Png, 100) ?? throw new InvalidOperationException("Could not encode the sheet as PNG.");
        using var stream = File.Create(output);
        data.SaveTo(stream);
    }
}

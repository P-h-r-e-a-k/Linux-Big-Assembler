using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Text.Json;
using LbaBodyStudio;

namespace BodyPipeline;

// castsheet <game folder> <out.png> [columns] [per page]: every race car a game folder's build made (its RACECARS.JSON: the cars made by
// hand and the cast's, RaceTrackCharacterCars) on one sheet -- each seen three-quarters from the front and above, its number, its name,
// who drives it and the island -- and the same in pages beside it (<out>_1.png, ...).
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
        var pictures = new List<(Entry Entry, Bitmap? Picture)>();
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
        foreach (var (_, picture) in pictures) picture?.Dispose();
        Console.WriteLine($"{output}: {entries.Count} cars; {pages} pages");
        return 0;
    }

    // A body three-quarters from the front and above (as carviews' first view).
    private static Bitmap View(Body model, Color[] palette, int w, int h)
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
        return Renderer.Render(copy, palette, w, h, 0, false, background: Color.FromArgb(40, 60, 90), gridLine: Color.FromArgb(40, 60, 90));
    }

    private static void Save(List<(Entry Entry, Bitmap? Picture)> cars, string output, int columns, int w, int h, int text, string title)
    {
        const int Top = 56;
        var rows = (cars.Count + columns - 1) / columns;
        using var sheet = new Bitmap(columns * w, Top + rows * (h + text));
        using var g = Graphics.FromImage(sheet);
        g.Clear(Color.FromArgb(24, 34, 52));
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var titleFont = new Font("Segoe UI", 22, FontStyle.Bold, GraphicsUnit.Pixel);
        using var nameFont = new Font("Segoe UI", 14, FontStyle.Bold, GraphicsUnit.Pixel);
        using var smallFont = new Font("Segoe UI", 12, FontStyle.Regular, GraphicsUnit.Pixel);
        using var numberFont = new Font("Segoe UI", 20, FontStyle.Bold, GraphicsUnit.Pixel);
        g.DrawString(title, titleFont, Brushes.White, 12, 12);
        var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        for (var i = 0; i < cars.Count; i++)
        {
            var (e, picture) = cars[i];
            int x = i % columns * w, y = Top + i / columns * (h + text);
            if (picture is not null) g.DrawImageUnscaled(picture, x, y);
            using (var badge = new SolidBrush(Color.FromArgb(200, 255, 200, 40))) g.FillRectangle(badge, x + 6, y + 6, 58, 30);
            g.DrawString(e.Number.ToString(), numberFont, Brushes.Black, new RectangleF(x + 6, y + 6, 58, 30), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
            using (var panel = new SolidBrush(Color.FromArgb(32, 46, 70))) g.FillRectangle(panel, x, y + h, w, text);
            g.DrawString(e.Name, nameFont, Brushes.White, new RectangleF(x + 8, y + h + 6, w - 14, 20), format);
            using (var light = new SolidBrush(Color.FromArgb(215, 225, 240))) g.DrawString($"Driver: {e.Driver}", smallFont, light, new RectangleF(x + 8, y + h + 28, w - 14, 18), format);
            using (var dim = new SolidBrush(Color.FromArgb(150, 170, 200))) g.DrawString(e.Island, smallFont, dim, new RectangleF(x + 8, y + h + 47, w - 14, 18), format);
            using (var line = new Pen(Color.FromArgb(24, 34, 52), 2)) g.DrawRectangle(line, x, y, w, h + text);
        }
        sheet.Save(output, ImageFormat.Png);
    }
}

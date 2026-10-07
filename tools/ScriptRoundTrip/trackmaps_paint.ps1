# trackmaps_paint.ps1 <folder>: draws each map's <name>.txt (written by "ScriptRoundTrip trackmaps") over its <name>.png, in place --
# the raised road's deck, the lap's line, the markers and arrows, the start line and the checkpoints, the title and the notes.
param([Parameter(Mandatory = $true)][string]$folder)
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;

public static class TrackMapPainter
{
    static float N(string s) { return float.Parse(s, CultureInfo.InvariantCulture); }

    // text with a dark halo, so it reads on any ground
    static void Text(Graphics g, string text, float x, float y, Font font, Color colour, bool centre)
    {
        var size = g.MeasureString(text, font);
        if (centre) { x -= size.Width / 2; y -= size.Height / 2; }
        using (var path = new GraphicsPath())
        {
            path.AddString(text, font.FontFamily, (int)font.Style, g.DpiY * font.SizeInPoints / 72, new PointF(x, y), StringFormat.GenericDefault);
            using (var halo = new Pen(Color.FromArgb(220, 0, 0, 0), font.Size / 3.5f) { LineJoin = LineJoin.Round }) g.DrawPath(halo, path);
            using (var fill = new SolidBrush(colour)) g.FillPath(fill, path);
        }
    }

    // the raised road's deck: the other levels' faint, then this level's -- each all its edges, then all its asphalt over them (one
    // piece at a time left the pieces' round ends showing through)
    static void Roads(Graphics g, string[] lines)
    {
        foreach (var faint in new[] { true, false })
            foreach (var edge in new[] { true, false })
                foreach (var raw in lines)
                {
                    var p = raw.Split(' ');
                    if (p[0] != "road" || (p[6] == "1") != faint) continue;
                    float w = Math.Max(2, N(p[5]));
                    var colour = edge ? (faint ? Color.FromArgb(40, 230, 230, 230) : Color.FromArgb(255, 235, 235, 235))
                                      : (faint ? Color.FromArgb(28, 60, 60, 60) : Color.FromArgb(255, 78, 78, 74));
                    using (var pen = new Pen(colour, edge ? w + 4 : w) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                        g.DrawLine(pen, N(p[1]), N(p[2]), N(p[3]), N(p[4]));
                }
    }

    public static void Paint(string png, string plan)
    {
        Bitmap bmp;
        using (var source = new Bitmap(png)) bmp = new Bitmap(source);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            var title = new Font("Segoe UI", 20, FontStyle.Bold, GraphicsUnit.Pixel);
            var note = new Font("Segoe UI", 15, FontStyle.Regular, GraphicsUnit.Pixel);
            var label = new Font("Segoe UI", 15, FontStyle.Bold, GraphicsUnit.Pixel);
            var lines = File.ReadAllLines(plan);
            Roads(g, lines);
            foreach (var raw in lines)
            {
                var p = raw.Split(' ');
                switch (p[0])
                {
                    case "title": Text(g, string.Join(" ", p, 3, p.Length - 3), N(p[1]), N(p[2]), title, Color.White, false); break;
                    case "note": Text(g, string.Join(" ", p, 3, p.Length - 3), N(p[1]), N(p[2]), note, Color.FromArgb(235, 235, 235), false); break;
                    case "road": break;   // (drawn first, below: Roads)
                    case "line":
                        using (var pen = new Pen(Color.FromArgb(int.Parse(p[9]), int.Parse(p[6]), int.Parse(p[7]), int.Parse(p[8])), N(p[5])))
                            g.DrawLine(pen, N(p[1]), N(p[2]), N(p[3]), N(p[4]));
                        break;
                    case "dot":
                        using (var b = new SolidBrush(Color.FromArgb(int.Parse(p[4]), int.Parse(p[5]), int.Parse(p[6]))))
                        using (var o = new Pen(Color.Black, 1.5f))
                        {
                            float r = N(p[3]);
                            g.FillEllipse(b, N(p[1]) - r, N(p[2]) - r, 2 * r, 2 * r);
                            g.DrawEllipse(o, N(p[1]) - r, N(p[2]) - r, 2 * r, 2 * r);
                        }
                        break;
                    case "label": Text(g, p[3], N(p[1]), N(p[2]), label, Color.FromArgb(255, 230, 0), true); break;
                    case "arrow":
                    {
                        float x = N(p[1]), y = N(p[2]), dx = N(p[3]), dy = N(p[4]), s = N(p[5]);
                        var pts = new[] {
                            new PointF(x + dx * s, y + dy * s),
                            new PointF(x - dx * s * 0.6f - dy * s * 0.6f, y - dy * s * 0.6f + dx * s * 0.6f),
                            new PointF(x - dx * s * 0.2f, y - dy * s * 0.2f),
                            new PointF(x - dx * s * 0.6f + dy * s * 0.6f, y - dy * s * 0.6f - dx * s * 0.6f) };
                        using (var b = new SolidBrush(Color.FromArgb(240, 255, 255, 255))) g.FillPolygon(b, pts);
                        using (var o = new Pen(Color.FromArgb(200, 0, 0, 0), 1.5f)) g.DrawPolygon(o, pts);
                        break;
                    }
                    case "start":
                    {
                        // black and white, dashed
                        using (var w = new Pen(Color.White, 7)) g.DrawLine(w, N(p[1]), N(p[2]), N(p[3]), N(p[4]));
                        using (var k = new Pen(Color.Black, 7) { DashPattern = new float[] { 1, 1 } }) g.DrawLine(k, N(p[1]), N(p[2]), N(p[3]), N(p[4]));
                        break;
                    }
                    case "cp":
                        using (var o = new Pen(Color.FromArgb(200, 0, 0, 0), 7)) g.DrawLine(o, N(p[1]), N(p[2]), N(p[3]), N(p[4]));
                        using (var r = new Pen(Color.FromArgb(230, 30, 30), 4)) g.DrawLine(r, N(p[1]), N(p[2]), N(p[3]), N(p[4]));
                        break;
                    case "tag":
                    {
                        var colour = p[3] == "0" ? Color.White : Color.FromArgb(255, 80, 80);
                        Text(g, string.Join(" ", p, 6, p.Length - 6), N(p[1]) + 6, N(p[2]) - 10, label, colour, false);
                        break;
                    }
                }
            }
        }
        bmp.Save(png, System.Drawing.Imaging.ImageFormat.Png);
        bmp.Dispose();
    }
}
'@
Get-ChildItem (Join-Path $folder '*.txt') | Where-Object { $_.Name -ne 'maps.txt' } | ForEach-Object {
    $png = [IO.Path]::ChangeExtension($_.FullName, '.png')
    if (Test-Path $png) { [TrackMapPainter]::Paint($png, $_.FullName); "painted $png" }
}

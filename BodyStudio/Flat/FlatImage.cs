using System.Numerics;

namespace LbaBodyStudio;

// A plain BGRA picture (no System.Drawing), so the flat-sheet code runs the same in the app and in the command-line tests.
public sealed class FlatImage
{
    public int Width { get; }
    public int Height { get; }
    public byte[] Bgra { get; }

    public FlatImage(int width, int height, byte[]? bgra = null)
    {
        Width = Math.Max(1, width); Height = Math.Max(1, height);
        Bgra = bgra ?? new byte[Width * Height * 4];
        if (Bgra.Length != Width * Height * 4) throw new ArgumentException("Pixel buffer does not match the size.");
    }

    public int Index(int x, int y) => (y * Width + x) * 4;
    public byte Alpha(int x, int y) => Bgra[Index(x, y) + 3];
    public (byte R, byte G, byte B, byte A) Pixel(int x, int y) { var i = Index(x, y); return (Bgra[i + 2], Bgra[i + 1], Bgra[i], Bgra[i + 3]); }
    public void Set(int x, int y, byte r, byte g, byte b, byte a = 255) { var i = Index(x, y); Bgra[i] = b; Bgra[i + 1] = g; Bgra[i + 2] = r; Bgra[i + 3] = a; }

    public FlatImage Clone() => new(Width, Height, (byte[])Bgra.Clone());

    // The pixel as one packed 0xAARRGGBB value (see Argb), and its setter.
    public uint Argb(int x, int y) { var i = Index(x, y); return (uint)(Bgra[i + 3] << 24 | Bgra[i + 2] << 16 | Bgra[i + 1] << 8 | Bgra[i]); }
    public void SetArgb(int x, int y, uint argb) => Set(x, y, LbaBodyStudio.Argb.R(argb), LbaBodyStudio.Argb.G(argb), LbaBodyStudio.Argb.B(argb), LbaBodyStudio.Argb.A(argb));

    // A picture from a packed-ARGB pixel buffer (the renderer's): in memory a little-endian 0xAARRGGBB int is exactly the bytes B, G, R, A.
    public static FlatImage FromArgb(int width, int height, int[] argb)
    {
        var image = new FlatImage(width, height);
        if (argb.Length != image.Width * image.Height) throw new ArgumentException("Pixel buffer does not match the size.");
        if (BitConverter.IsLittleEndian) Buffer.BlockCopy(argb, 0, image.Bgra, 0, image.Bgra.Length);
        else for (var i = 0; i < argb.Length; i++) { var c = argb[i]; image.Bgra[i * 4] = (byte)c; image.Bgra[i * 4 + 1] = (byte)(c >> 8); image.Bgra[i * 4 + 2] = (byte)(c >> 16); image.Bgra[i * 4 + 3] = (byte)(c >> 24); }
        return image;
    }

    public FlatImage Crop(int x, int y, int width, int height)
    {
        var result = new FlatImage(width, height);
        for (var row = 0; row < result.Height; row++)
            for (var col = 0; col < result.Width; col++)
            {
                int sx = x + col, sy = y + row;
                if (sx < 0 || sy < 0 || sx >= Width || sy >= Height) continue;
                Array.Copy(Bgra, Index(sx, sy), result.Bgra, result.Index(col, row), 4);
            }
        return result;
    }

    public FlatImage FlipHorizontal()
    {
        var result = new FlatImage(Width, Height);
        for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
                Array.Copy(Bgra, Index(x, y), result.Bgra, result.Index(Width - 1 - x, y), 4);
        return result;
    }

    // Area-averaged resize (premultiplied by alpha, so transparent pixels do not bleed colour).
    public FlatImage Resize(int width, int height)
    {
        var result = new FlatImage(width, height);
        for (var y = 0; y < result.Height; y++)
        {
            double y0 = y * (double)Height / result.Height, y1 = (y + 1) * (double)Height / result.Height;
            for (var x = 0; x < result.Width; x++)
            {
                double x0 = x * (double)Width / result.Width, x1 = (x + 1) * (double)Width / result.Width;
                double b = 0, g = 0, r = 0, a = 0, weight = 0;
                for (var sy = (int)Math.Floor(y0); sy < Math.Ceiling(y1) && sy < Height; sy++)
                {
                    var wy = Math.Min(sy + 1, y1) - Math.Max(sy, y0);
                    for (var sx = (int)Math.Floor(x0); sx < Math.Ceiling(x1) && sx < Width; sx++)
                    {
                        var w = wy * (Math.Min(sx + 1, x1) - Math.Max(sx, x0));
                        var i = Index(sx, sy);
                        var alpha = Bgra[i + 3] / 255.0;
                        b += Bgra[i] * alpha * w; g += Bgra[i + 1] * alpha * w; r += Bgra[i + 2] * alpha * w; a += alpha * w; weight += w;
                    }
                }
                if (weight <= 0 || a <= 0) continue;
                result.Set(x, y, (byte)Math.Round(r / a), (byte)Math.Round(g / a), (byte)Math.Round(b / a), (byte)Math.Round(a / weight * 255));
            }
        }
        return result;
    }

    // Draws `other` into this picture at (x, y), over what is there.
    public void Paste(FlatImage other, int x, int y)
    {
        for (var row = 0; row < other.Height; row++)
            for (var col = 0; col < other.Width; col++)
            {
                int tx = x + col, ty = y + row;
                if (tx < 0 || ty < 0 || tx >= Width || ty >= Height) continue;
                var s = other.Index(col, row);
                var a = other.Bgra[s + 3];
                if (a == 0) continue;
                Array.Copy(other.Bgra, s, Bgra, Index(tx, ty), 4);
            }
    }

    // Bounding box of the pixels whose alpha is above 127; null when there are none.
    public (int X, int Y, int Width, int Height)? OpaqueBounds()
    {
        int minX = Width, minY = Height, maxX = -1, maxY = -1;
        for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
                if (Alpha(x, y) > 127) { minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y); }
        return maxX < 0 ? null : (minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    public int OpaqueCount() { var n = 0; for (var i = 3; i < Bgra.Length; i += 4) if (Bgra[i] > 127) n++; return n; }

    // Intersection over union of two pictures' silhouettes (same size); 1 = identical.
    public static double SilhouetteOverlap(FlatImage a, FlatImage b)
    {
        if (a.Width != b.Width || a.Height != b.Height) throw new ArgumentException("Sizes differ.");
        long both = 0, either = 0;
        for (var i = 3; i < a.Bgra.Length; i += 4)
        {
            var x = a.Bgra[i] > 127; var y = b.Bgra[i] > 127;
            if (x && y) both++;
            if (x || y) either++;
        }
        return either == 0 ? 1 : both / (double)either;
    }
}

// Packed 0xAARRGGBB colours (what System.Drawing.Color.ToArgb() gave; alpha 0xFF for an opaque colour): the form every colour in
// Body Studio takes -- palettes, the renderer's theme and background colours, and sampled picture pixels.
public static class Argb
{
    public static uint Pack(int r, int g, int b, int a = 255) => (uint)((a & 0xFF) << 24 | (r & 0xFF) << 16 | (g & 0xFF) << 8 | (b & 0xFF));
    public static byte A(uint argb) => (byte)(argb >> 24);
    public static byte R(uint argb) => (byte)(argb >> 16);
    public static byte G(uint argb) => (byte)(argb >> 8);
    public static byte B(uint argb) => (byte)argb;
    // `colour` blended towards white by `amount` (0 .. 1), the way ControlPaint.Light lightened a button colour.
    public static uint Lighten(uint argb, float amount) => Pack((int)(R(argb) + (255 - R(argb)) * amount), (int)(G(argb) + (255 - G(argb)) * amount), (int)(B(argb) + (255 - B(argb)) * amount), A(argb));
}

// sRGB <-> CIE Lab, for colour distances that follow what the eye sees.
public static class LabColour
{
    public static Vector3 FromRgb(byte r, byte g, byte b)
    {
        static double Lin(double c) { c /= 255; return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4); }
        double lr = Lin(r), lg = Lin(g), lb = Lin(b);
        var x = (lr * 0.4124 + lg * 0.3576 + lb * 0.1805) / 0.95047;
        var y = lr * 0.2126 + lg * 0.7152 + lb * 0.0722;
        var z = (lr * 0.0193 + lg * 0.1192 + lb * 0.9505) / 1.08883;
        static double F(double t) => t > 0.008856 ? Math.Cbrt(t) : 7.787 * t + 16.0 / 116;
        double fx = F(x), fy = F(y), fz = F(z);
        return new Vector3((float)(116 * fy - 16), (float)(500 * (fx - fy)), (float)(200 * (fy - fz)));
    }

    public static float Distance(Vector3 a, Vector3 b) => Vector3.Distance(a, b);
}

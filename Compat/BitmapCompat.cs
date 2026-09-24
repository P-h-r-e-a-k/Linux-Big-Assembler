using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace LBAAssembler;

// WPF's BitmapSource.Create / WriteableBitmap over Avalonia's WriteableBitmap. Every picture the editor makes is
// 32-bit BGRA (an 8-bit paletted frame from the native renderer is expanded here), at 96 dpi, unpremultiplied.
public enum WpfPixelFormat { Bgra32, Pbgra32, Indexed8, Bgr32 }

public static class PixelFormats
{
    public const WpfPixelFormat Bgra32 = WpfPixelFormat.Bgra32, Pbgra32 = WpfPixelFormat.Pbgra32, Indexed8 = WpfPixelFormat.Indexed8, Bgr32 = WpfPixelFormat.Bgr32;
}

public sealed class BitmapPalette
{
    public IReadOnlyList<Color> Colors { get; }
    public BitmapPalette(IList<Color> colors) => Colors = colors.ToArray();
}

public static class BitmapFactory
{
    public static WriteableBitmap Writeable(int width, int height)
        => new(new PixelSize(Math.Max(1, width), Math.Max(1, height)), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);

    // BitmapSource.Create(width, height, dpiX, dpiY, format, palette, pixels, stride): pixels is a byte[] (BGRA or
    // 8-bit indices with a palette) or an int[] of packed ARGB.
    public static WriteableBitmap Create(int width, int height, double dpiX, double dpiY, WpfPixelFormat format, BitmapPalette? palette, Array pixels, int stride)
    {
        var bitmap = Writeable(width, height);
        if (format == WpfPixelFormat.Indexed8)
        {
            var indices = (byte[])pixels;
            var colors = palette?.Colors ?? Array.Empty<Color>();
            var table = new int[256];
            for (var i = 0; i < 256; i++) { var c = i < colors.Count ? colors[i] : default; table[i] = (c.A << 24) | (c.R << 16) | (c.G << 8) | c.B; }
            var row = new int[width];
            using var fb = bitmap.Lock();
            for (var y = 0; y < height; y++)
            {
                var src = y * stride;
                for (var x = 0; x < width; x++) row[x] = table[indices[src + x]];
                Marshal.Copy(row, 0, fb.Address + y * fb.RowBytes, width);
            }
            return bitmap;
        }
        if (pixels is int[] argb) bitmap.WritePixels(new PixelRect(0, 0, width, height), argb, stride, 0);
        else bitmap.WritePixels(new PixelRect(0, 0, width, height), (byte[])pixels, stride, 0);
        return bitmap;
    }

    // A real copy of `rect` of `source` (WPF's CroppedBitmap was a view; a copy is simpler and the pictures are small).
    public static WriteableBitmap Crop(Bitmap source, PixelRect rect)
    {
        var bounds = new PixelRect(0, 0, source.PixelSize.Width, source.PixelSize.Height);
        rect = rect.Intersect(bounds);
        var result = Writeable(rect.Width, rect.Height);
        if (rect.Width <= 0 || rect.Height <= 0) return result;
        using var fb = result.Lock();
        source.CopyPixels(rect, fb.Address, fb.RowBytes * fb.Size.Height, fb.RowBytes);
        return result;
    }

    public static WriteableBitmap FromBgra(int width, int height, byte[] bgra) => Create(width, height, 96, 96, WpfPixelFormat.Bgra32, null, bgra, width * 4);

    // Loads a PNG/BMP/JPEG through Avalonia's decoder.
    public static Bitmap Load(string path)
    {
        using var stream = File.OpenRead(path);
        return new Bitmap(stream);
    }

    public static Bitmap Load(Stream stream) => new(stream);

    // The picture as one BGRA buffer (WPF's CopyPixels with a full rectangle).
    public static byte[] ToBgra(Bitmap bitmap)
    {
        var w = bitmap.PixelSize.Width; var h = bitmap.PixelSize.Height;
        var bytes = new byte[w * h * 4];
        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try { bitmap.CopyPixels(new PixelRect(0, 0, w, h), handle.AddrOfPinnedObject(), bytes.Length, w * 4); }
        finally { handle.Free(); }
        return bytes;
    }

    // Scales a picture by an integer or fractional factor (WPF's TransformedBitmap with a ScaleTransform). Done with Skia on the
    // picture's own pixels: Avalonia's Bitmap.CreateScaledBitmap only accepts a decoded (immutable) bitmap and throws "Invalid
    // source bitmap type" for the WriteableBitmaps every picture here is drawn into.
    public static Bitmap Scale(Bitmap source, double scaleX, double scaleY, BitmapInterpolationMode mode = BitmapInterpolationMode.HighQuality)
    {
        int width = source.PixelSize.Width, height = source.PixelSize.Height;
        var size = new PixelSize(Math.Max(1, (int)Math.Round(width * scaleX)), Math.Max(1, (int)Math.Round(height * scaleY)));
        var pixels = ToBgra(source);
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            using var from = new SkiaSharp.SKBitmap();
            from.InstallPixels(new SkiaSharp.SKImageInfo(width, height, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Unpremul), handle.AddrOfPinnedObject(), width * 4);
            using var to = new SkiaSharp.SKBitmap(new SkiaSharp.SKImageInfo(size.Width, size.Height, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Unpremul));
            var quality = mode switch
            {
                BitmapInterpolationMode.None => SkiaSharp.SKFilterQuality.None,
                BitmapInterpolationMode.LowQuality => SkiaSharp.SKFilterQuality.Low,
                BitmapInterpolationMode.MediumQuality => SkiaSharp.SKFilterQuality.Medium,
                _ => SkiaSharp.SKFilterQuality.High,
            };
            if (!from.ScalePixels(to, quality)) throw new InvalidOperationException("Skia couldn't scale the picture.");
            var result = new byte[size.Width * size.Height * 4];
            Marshal.Copy(to.GetPixels(), result, 0, result.Length);
            return FromBgra(size.Width, size.Height, result);
        }
        finally { handle.Free(); }
    }
}

public static class BitmapCompat
{
    extension(Bitmap bitmap)
    {
        public int PixelWidth => bitmap.PixelSize.Width;
        public int PixelHeight => bitmap.PixelSize.Height;

        // WPF's CopyPixels(byte[] / int[] buffer, stride, offset).
        public void CopyPixels(byte[] pixels, int stride, int offset)
        {
            var h = bitmap.PixelSize.Height;
            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try { bitmap.CopyPixels(new PixelRect(0, 0, bitmap.PixelSize.Width, h), handle.AddrOfPinnedObject() + offset, pixels.Length - offset, stride); }
            finally { handle.Free(); }
        }
        public void CopyPixels(int[] pixels, int stride, int offset)
        {
            var h = bitmap.PixelSize.Height;
            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try { bitmap.CopyPixels(new PixelRect(0, 0, bitmap.PixelSize.Width, h), handle.AddrOfPinnedObject() + offset * 4, (pixels.Length - offset) * 4, stride); }
            finally { handle.Free(); }
        }
        public void CopyPixels(PixelRect rect, byte[] pixels, int stride, int offset)
        {
            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try { bitmap.CopyPixels(rect, handle.AddrOfPinnedObject() + offset, pixels.Length - offset, stride); }
            finally { handle.Free(); }
        }
    }

    extension(WriteableBitmap bitmap)
    {
        // WPF's WritePixels(rect, buffer, stride, offset): `rect` names the destination, the buffer starts at `offset`.
        public void WritePixels(PixelRect rect, byte[] pixels, int stride, int offset)
        {
            using var fb = bitmap.Lock();
            var w = Math.Min(rect.Width, fb.Size.Width - rect.X); var h = Math.Min(rect.Height, fb.Size.Height - rect.Y);
            for (var y = 0; y < h; y++) Marshal.Copy(pixels, offset + y * stride, fb.Address + (rect.Y + y) * fb.RowBytes + rect.X * 4, w * 4);
        }
        public void WritePixels(PixelRect rect, int[] pixels, int stride, int offset)
        {
            using var fb = bitmap.Lock();
            var w = Math.Min(rect.Width, fb.Size.Width - rect.X); var h = Math.Min(rect.Height, fb.Size.Height - rect.Y);
            for (var y = 0; y < h; y++) Marshal.Copy(pixels, offset + y * (stride / 4), fb.Address + (rect.Y + y) * fb.RowBytes + rect.X * 4, w);
        }
        public void WritePixels(PixelRect rect, uint[] pixels, int stride, int offset)
        {
            using var fb = bitmap.Lock();
            var w = Math.Min(rect.Width, fb.Size.Width - rect.X); var h = Math.Min(rect.Height, fb.Size.Height - rect.Y);
            for (var y = 0; y < h; y++)
            {
                var row = new int[w];
                Buffer.BlockCopy(pixels, (offset + y * (stride / 4)) * 4, row, 0, w * 4);
                Marshal.Copy(row, 0, fb.Address + (rect.Y + y) * fb.RowBytes + rect.X * 4, w);
            }
        }
        // WPF's WritePixels(sourceRect, buffer, stride, destX, destY): `rect` is where in the buffer, (destX, destY) where in the bitmap.
        public void WritePixels(PixelRect sourceRect, byte[] pixels, int stride, int destX, int destY)
        {
            using var fb = bitmap.Lock();
            var w = Math.Min(sourceRect.Width, fb.Size.Width - destX); var h = Math.Min(sourceRect.Height, fb.Size.Height - destY);
            for (var y = 0; y < h; y++) Marshal.Copy(pixels, (sourceRect.Y + y) * stride + sourceRect.X * 4, fb.Address + (destY + y) * fb.RowBytes + destX * 4, w * 4);
        }
        public void WritePixels(PixelRect sourceRect, int[] pixels, int stride, int destX, int destY)
        {
            using var fb = bitmap.Lock();
            var w = Math.Min(sourceRect.Width, fb.Size.Width - destX); var h = Math.Min(sourceRect.Height, fb.Size.Height - destY);
            for (var y = 0; y < h; y++) Marshal.Copy(pixels, (sourceRect.Y + y) * (stride / 4) + sourceRect.X, fb.Address + (destY + y) * fb.RowBytes + destX * 4, w);
        }
        public void WritePixels(PixelRect sourceRect, uint[] pixels, int stride, int destX, int destY)
        {
            using var fb = bitmap.Lock();
            var w = Math.Min(sourceRect.Width, fb.Size.Width - destX); var h = Math.Min(sourceRect.Height, fb.Size.Height - destY);
            for (var y = 0; y < h; y++)
            {
                var row = new int[w];
                Buffer.BlockCopy(pixels, ((sourceRect.Y + y) * (stride / 4) + sourceRect.X) * 4, row, 0, w * 4);
                Marshal.Copy(row, 0, fb.Address + (destY + y) * fb.RowBytes + destX * 4, w);
            }
        }
    }
}

using System.IO;
using SkiaSharp;

namespace LbaBodyStudio;

// FlatImage <-> picture files, through SkiaSharp (the decoder and PNG encoder Avalonia itself ships with): no System.Drawing.
public static class FlatBitmap
{
    // Any picture SkiaSharp decodes (PNG, JPEG, BMP, GIF, WebP ...), as straight (unpremultiplied) BGRA.
    public static FlatImage Load(string path)
    {
        using var codec = SKCodec.Create(path) ?? throw new InvalidDataException($"Not a picture this tool reads: {path}");
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        using var bitmap = SKBitmap.Decode(codec, info) ?? throw new InvalidDataException($"Could not decode the picture: {path}");
        var image = new FlatImage(bitmap.Width, bitmap.Height);
        var source = bitmap.GetPixelSpan();
        var rowBytes = image.Width * 4;
        for (var y = 0; y < image.Height; y++) source.Slice(y * bitmap.RowBytes, rowBytes).CopyTo(image.Bgra.AsSpan(y * rowBytes, rowBytes));
        return image;
    }

    public static void Save(FlatImage image, string path)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        using var picture = SKImage.FromPixelCopy(info, image.Bgra, image.Width * 4) ?? throw new InvalidOperationException("Could not build the picture.");
        using var data = picture.Encode(SKEncodedImageFormat.Png, 100) ?? throw new InvalidOperationException("Could not encode the picture as PNG.");
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }
}

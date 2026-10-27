using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace LBAAssembler.Terrain;

// A picture read as a heightmap: one value a pixel, row by row from the top-left -- a level from 0 (black) to 1 (white), or, from a file
// of numbers (CSV), a world height as it is -- and NaN where the pixel is transparent (the ground there is left as it is).
internal sealed class HeightmapPicture
{
    public int Width { get; init; }
    public int Height { get; init; }
    public double[] Values { get; init; } = Array.Empty<double>();
    // Values are world heights (a CSV), not 0..1 levels: the black and white heights don't apply
    public bool Absolute { get; init; }
    // how many levels the file has: 8 bits (256) or 16 bits (65,536)
    public int Bits { get; init; } = 16;
    // what the file was, for the dialog ("PNG, 16-bit greyscale")
    public string Kind { get; init; } = "";
    // the black and white heights the file says it was exported with (the PNG's "LBA heightmap" text): Import uses them again
    public (int Black, int White)? Range { get; init; }
    public bool HasTransparency => Values.Any(double.IsNaN);

    public double At(int x, int y) => Values[y * Width + x];
}

// Reading and writing heightmap files without WPF (the headless tests use it too): PNG (greyscale, colour or palette; 1 to 16 bits; with or
// without transparency; interlaced or not -- read as grey), raw 16-bit heights (.r16 / .raw, little-endian, as terrain tools write them),
// and numbers in a text file (.csv: world heights, one row of the picture a line).
internal static class HeightmapFile
{
    public const int MaxSide = 4096;
    public const string TextKey = "LBA heightmap";
    private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

    // The file formats Import reads (the file dialog's filter).
    public const string ReadFilter = "Heightmaps (*.png;*.r16;*.raw;*.csv)|*.png;*.r16;*.raw;*.csv|PNG pictures (*.png)|*.png|Raw 16-bit heights (*.r16;*.raw)|*.r16;*.raw|Heights as numbers (*.csv)|*.csv";

    // `expected`: the island's own size, which settles what a raw file of no stated size is.
    public static HeightmapPicture Load(string path, (int Width, int Height)? expected = null)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(Signature)) return DecodePng(bytes);
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".r16" or ".raw" => DecodeRaw(bytes, expected),
            ".csv" or ".txt" => DecodeCsv(Encoding.UTF8.GetString(bytes)),
            ".png" => throw new InvalidDataException("The file is not a PNG picture (its first bytes aren't a PNG's)."),
            _ => throw new InvalidDataException($"A heightmap is a PNG picture, a raw 16-bit file (.r16) or numbers (.csv); {Path.GetFileName(path)} is none of them."),
        };
    }

    // ---- PNG --------------------------------------------------------------------------------------------------------------------------------

    public static HeightmapPicture DecodePng(byte[] png)
    {
        if (png.Length < 8 || !png.AsSpan(0, 8).SequenceEqual(Signature)) throw new InvalidDataException("Not a PNG file.");
        int width = 0, height = 0, depth = 0, colour = 0, interlace = 0;
        byte[]? palette = null, transparency = null;
        var compressed = new MemoryStream();
        var text = new Dictionary<string, string>();
        var pos = 8;
        while (pos + 12 <= png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(pos));
            if (length < 0 || pos + 12L + length > png.Length) throw new InvalidDataException("The PNG file is cut short.");
            var type = Encoding.ASCII.GetString(png, pos + 4, 4);
            var data = png.AsSpan(pos + 8, length);
            if (type == "IEND") break;
            switch (type)
            {
                case "IHDR":
                    if (length < 13) throw new InvalidDataException("The PNG header is too short.");
                    width = BinaryPrimitives.ReadInt32BigEndian(data); height = BinaryPrimitives.ReadInt32BigEndian(data[4..]);
                    depth = data[8]; colour = data[9]; interlace = data[12];
                    break;
                case "PLTE": palette = data.ToArray(); break;
                case "tRNS": transparency = data.ToArray(); break;
                case "IDAT": compressed.Write(data); break;
                case "tEXt":
                {
                    var zero = data.IndexOf((byte)0);
                    if (zero > 0) text[Encoding.Latin1.GetString(data[..zero])] = Encoding.Latin1.GetString(data[(zero + 1)..]);
                    break;
                }
                case "iTXt":
                {
                    // keyword \0 flag method language \0 translated \0 text (only uncompressed text is read)
                    var zero = data.IndexOf((byte)0);
                    if (zero <= 0 || zero + 3 > data.Length || data[zero + 1] != 0) break;
                    var rest = data[(zero + 3)..];
                    var language = rest.IndexOf((byte)0); if (language < 0) break;
                    rest = rest[(language + 1)..];
                    var translated = rest.IndexOf((byte)0); if (translated < 0) break;
                    text[Encoding.Latin1.GetString(data[..zero])] = Encoding.UTF8.GetString(rest[(translated + 1)..]);
                    break;
                }
            }
            pos += 12 + length;
        }
        if (width <= 0 || height <= 0) throw new InvalidDataException("The PNG file has no picture header.");
        if (width > MaxSide || height > MaxSide) throw new InvalidDataException($"The picture is {width} × {height}: heightmaps up to {MaxSide} pixels a side are read.");
        var channels = colour switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => throw new InvalidDataException($"PNG colour type {colour} is not one PNG has.") };
        var allowed = colour switch { 0 => depth is 1 or 2 or 4 or 8 or 16, 3 => depth is 1 or 2 or 4 or 8, _ => depth is 8 or 16 };
        if (!allowed) throw new InvalidDataException($"A PNG of colour type {colour} can't be {depth} bits deep.");
        if (colour == 3 && palette is null) throw new InvalidDataException("The palette PNG has no palette.");

        compressed.Position = 0;
        byte[] raw;
        using (var z = new ZLibStream(compressed, CompressionMode.Decompress))
        using (var output = new MemoryStream())
        {
            try { z.CopyTo(output); }
            catch (InvalidDataException) { throw new InvalidDataException("The PNG picture data is damaged."); }
            raw = output.ToArray();
        }

        var bitsPerPixel = channels * depth;
        var bytesPerPixel = Math.Max(1, bitsPerPixel / 8);
        var max = (1 << depth) - 1;
        var values = new double[width * height];
        var passes = interlace == 1
            ? new (int X, int Y, int StepX, int StepY)[] { (0, 0, 8, 8), (4, 0, 8, 8), (0, 4, 4, 8), (2, 0, 4, 4), (0, 2, 2, 4), (1, 0, 2, 2), (0, 1, 1, 2) }
            : new[] { (0, 0, 1, 1) };
        var offset = 0;
        var samples = new int[4];
        foreach (var (px, py, sx, sy) in passes)
        {
            var passWidth = (width - px + sx - 1) / sx; var passHeight = (height - py + sy - 1) / sy;
            if (passWidth <= 0 || passHeight <= 0) continue;
            var stride = (passWidth * bitsPerPixel + 7) / 8;
            var previous = new byte[stride]; var row = new byte[stride];
            for (var r = 0; r < passHeight; r++)
            {
                if (offset + 1 + stride > raw.Length) throw new InvalidDataException("The PNG picture data is cut short.");
                var filter = raw[offset];
                Array.Copy(raw, offset + 1, row, 0, stride);
                offset += 1 + stride;
                Unfilter(row, previous, filter, bytesPerPixel);
                var y = py + r * sy;
                for (var i = 0; i < passWidth; i++)
                {
                    for (var k = 0; k < channels; k++) samples[k] = Sample(row, i * channels + k, depth);
                    values[y * width + px + i * sx] = Level(samples, colour, max, palette, transparency);
                }
                (previous, row) = (row, previous);
            }
        }

        (int, int)? range = null;
        if (text.TryGetValue(TextKey, out var said)) range = ParseRange(said);
        var kind = colour switch { 0 => "greyscale", 2 => "colour (read as grey)", 3 => "palette colour (read as grey)", 4 => "greyscale with transparency", _ => "colour with transparency (read as grey)" };
        if (transparency is not null && colour is 0 or 2 or 3) kind += ", some of it transparent";
        return new HeightmapPicture
        {
            Width = width, Height = height, Values = values, Bits = colour == 3 ? 8 : depth,
            Kind = $"PNG, {depth}-bit {kind}{(interlace == 1 ? ", interlaced" : "")}", Range = range,
        };
    }

    // "black=-120 white=3400 ..." -> the two heights
    public static (int Black, int White)? ParseRange(string text)
    {
        int? black = null, white = null;
        foreach (var part in text.Split(new[] { ' ', ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0 || !int.TryParse(part[(eq + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) continue;
            switch (part[..eq].ToLowerInvariant()) { case "black": black = v; break; case "white": white = v; break; }
        }
        return black is { } b && white is { } w && w != b ? (b, w) : null;
    }

    private static int Sample(byte[] row, int index, int depth)
    {
        switch (depth)
        {
            case 16: return (row[index * 2] << 8) | row[index * 2 + 1];
            case 8: return row[index];
            default:
                var bit = index * depth;
                return (row[bit >> 3] >> (8 - depth - (bit & 7))) & ((1 << depth) - 1);
        }
    }

    // A pixel's level (0..1), NaN when it is transparent. Colour is read as its brightness (grey pixels exactly as their value).
    private static double Level(int[] s, int colour, int max, byte[]? palette, byte[]? trns)
    {
        double Grey(int r, int g, int b) => r == g && g == b ? r / (double)max : (0.299 * r + 0.587 * g + 0.114 * b) / max;
        switch (colour)
        {
            case 0:
                if (trns is { Length: >= 2 } && BinaryPrimitives.ReadUInt16BigEndian(trns) == s[0]) return double.NaN;
                return s[0] / (double)max;
            case 2:
                if (trns is { Length: >= 6 } && BinaryPrimitives.ReadUInt16BigEndian(trns) == s[0] && BinaryPrimitives.ReadUInt16BigEndian(trns.AsSpan(2)) == s[1]
                    && BinaryPrimitives.ReadUInt16BigEndian(trns.AsSpan(4)) == s[2]) return double.NaN;
                return Grey(s[0], s[1], s[2]);
            case 3:
            {
                var index = s[0];
                if (trns is not null && index < trns.Length && trns[index] < 128) return double.NaN;
                if (index * 3 + 2 >= palette!.Length) return 0;
                int r = palette[index * 3], g = palette[index * 3 + 1], b = palette[index * 3 + 2];
                return r == g && g == b ? r / 255.0 : (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;
            }
            case 4: return s[1] * 2 < max ? double.NaN : s[0] / (double)max;
            default: return s[3] * 2 < max ? double.NaN : Grey(s[0], s[1], s[2]);
        }
    }

    private static void Unfilter(byte[] row, byte[] previous, int filter, int bpp)
    {
        switch (filter)
        {
            case 0: return;
            case 1: for (var i = bpp; i < row.Length; i++) row[i] = (byte)(row[i] + row[i - bpp]); return;
            case 2: for (var i = 0; i < row.Length; i++) row[i] = (byte)(row[i] + previous[i]); return;
            case 3: for (var i = 0; i < row.Length; i++) row[i] = (byte)(row[i] + (((i >= bpp ? row[i - bpp] : 0) + previous[i]) >> 1)); return;
            case 4: for (var i = 0; i < row.Length; i++) row[i] = (byte)(row[i] + Paeth(i >= bpp ? row[i - bpp] : 0, previous[i], i >= bpp ? previous[i - bpp] : 0)); return;
            default: throw new InvalidDataException($"The PNG uses a row filter ({filter}) PNG doesn't have.");
        }
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c; var pa = Math.Abs(p - a); var pb = Math.Abs(p - b); var pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    // A greyscale PNG of `values` (0..1 levels, NaN = transparent: then the picture has an alpha channel), 8 or 16 bits, with text chunks.
    public static byte[] EncodePng(int width, int height, double[] values, int bits, IReadOnlyDictionary<string, string>? text = null)
    {
        if (bits is not (8 or 16)) throw new ArgumentOutOfRangeException(nameof(bits));
        var alpha = values.Any(double.IsNaN);
        var bytesPerSample = bits / 8;
        var bpp = bytesPerSample * (alpha ? 2 : 1);
        var stride = width * bpp;
        var max = (1 << bits) - 1;
        using var file = new MemoryStream();
        file.Write(Signature);
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width); BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = (byte)bits; header[9] = (byte)(alpha ? 4 : 0);
        Chunk(file, "IHDR", header);
        if (text is not null)
            foreach (var (key, value) in text) Chunk(file, "tEXt", Encoding.Latin1.GetBytes(key + "\0" + value));

        using var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.SmallestSize, true))
        {
            var row = new byte[stride]; var previous = new byte[stride];
            var candidate = new byte[stride]; var best = new byte[stride];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var v = values[y * width + x];
                    var level = double.IsNaN(v) ? 0 : (int)Math.Round(Math.Clamp(v, 0, 1) * max);
                    var at = x * bpp;
                    if (bits == 16) { row[at] = (byte)(level >> 8); row[at + 1] = (byte)level; }
                    else row[at] = (byte)level;
                    if (alpha)
                    {
                        var a = double.IsNaN(v) ? 0 : max;
                        if (bits == 16) { row[at + 2] = (byte)(a >> 8); row[at + 3] = (byte)a; }
                        else row[at + 1] = (byte)a;
                    }
                }
                // the row filter that leaves the smallest numbers (the usual heuristic)
                var bestFilter = 0; long bestScore = long.MaxValue;
                for (var f = 0; f < 5; f++)
                {
                    long score = 0;
                    for (var i = 0; i < stride; i++)
                    {
                        int a = i >= bpp ? row[i - bpp] : 0, b = previous[i], c = i >= bpp ? previous[i - bpp] : 0;
                        var d = (byte)(row[i] - f switch { 0 => 0, 1 => a, 2 => b, 3 => (a + b) >> 1, _ => Paeth(a, b, c) });
                        candidate[i] = d;
                        score += d < 128 ? d : 256 - d;
                    }
                    if (score < bestScore) { bestScore = score; bestFilter = f; (best, candidate) = (candidate, best); }
                }
                z.WriteByte((byte)bestFilter);
                z.Write(best);
                (previous, row) = (row, previous);
            }
        }
        Chunk(file, "IDAT", compressed.ToArray());
        Chunk(file, "IEND", Array.Empty<byte>());
        return file.ToArray();
    }

    private static void Chunk(Stream s, string type, byte[] data)
    {
        Span<byte> four = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(four, data.Length);
        s.Write(four);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes); s.Write(data);
        var crc = 0xFFFFFFFFu;
        foreach (var b in typeBytes) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        foreach (var b in data) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        BinaryPrimitives.WriteUInt32BigEndian(four, crc ^ 0xFFFFFFFFu);
        s.Write(four);
    }

    private static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(n =>
    {
        var c = (uint)n;
        for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    // ---- raw 16-bit -------------------------------------------------------------------------------------------------------------------------

    // Unsigned 16-bit levels, little-endian (Windows byte order), row by row; the size is the island's when the length fits it, else a square.
    // A file of one byte a pixel (an 8-bit raw) is read too when its length says so.
    public static HeightmapPicture DecodeRaw(byte[] bytes, (int Width, int Height)? expected)
    {
        int width, height, bytesPerPixel;
        if (expected is { } e && bytes.Length == e.Width * e.Height * 2) (width, height, bytesPerPixel) = (e.Width, e.Height, 2);
        else if (expected is { } e8 && bytes.Length == e8.Width * e8.Height) (width, height, bytesPerPixel) = (e8.Width, e8.Height, 1);
        else if (bytes.Length % 2 == 0 && IsSquare(bytes.Length / 2, out var side)) (width, height, bytesPerPixel) = (side, side, 2);
        else if (IsSquare(bytes.Length, out var side8)) (width, height, bytesPerPixel) = (side8, side8, 1);
        else throw new InvalidDataException($"A raw heightmap has no size of its own: its length ({bytes.Length:N0} bytes) must be the island's size{(expected is { } x ? $" ({x.Width} × {x.Height} × 2 bytes)" : "")} or a square.");
        if (width > MaxSide || height > MaxSide) throw new InvalidDataException($"The raw heightmap is {width} × {height}: up to {MaxSide} pixels a side are read.");
        var values = new double[width * height];
        for (var i = 0; i < values.Length; i++)
            values[i] = bytesPerPixel == 2 ? BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(i * 2)) / 65535.0 : bytes[i] / 255.0;
        return new HeightmapPicture { Width = width, Height = height, Values = values, Bits = bytesPerPixel * 8, Kind = $"raw {bytesPerPixel * 8}-bit heights" + (bytesPerPixel == 2 ? ", little-endian" : "") };
    }

    private static bool IsSquare(int n, out int side)
    {
        side = (int)Math.Round(Math.Sqrt(n));
        return side > 1 && side * side == n;
    }

    public static byte[] EncodeRaw(double[] values)
    {
        var bytes = new byte[values.Length * 2];
        for (var i = 0; i < values.Length; i++)
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(i * 2), (ushort)(double.IsNaN(values[i]) ? 0 : Math.Round(Math.Clamp(values[i], 0, 1) * 65535)));
        return bytes;
    }

    // ---- numbers ----------------------------------------------------------------------------------------------------------------------------

    // World heights, one line a row; separated by commas, semicolons, tabs or spaces. An empty cell (or "-") leaves the ground there as it is.
    public static HeightmapPicture DecodeCsv(string text)
    {
        var rows = new List<double[]>();
        foreach (var rawLine in text.Replace("\r", "").Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var separators = line.Contains(',') ? new[] { ',' } : line.Contains(';') ? new[] { ';' } : line.Contains('\t') ? new[] { '\t' } : new[] { ' ' };
            var cells = line.Split(separators, separators[0] == ' ' ? StringSplitOptions.RemoveEmptyEntries : StringSplitOptions.None);
            var row = new double[cells.Length];
            for (var i = 0; i < cells.Length; i++)
            {
                var cell = cells[i].Trim();
                if (cell.Length == 0 || cell == "-") { row[i] = double.NaN; continue; }
                if (!double.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture, out row[i]))
                    throw new InvalidDataException($"Row {rows.Count + 1} of the heights has \"{cell}\", which isn't a number.");
            }
            rows.Add(row);
        }
        if (rows.Count == 0) throw new InvalidDataException("The file has no heights in it.");
        var width = rows.Max(r => r.Length); var height = rows.Count;
        if (width > MaxSide || height > MaxSide) throw new InvalidDataException($"The heights are {width} × {height}: up to {MaxSide} a side are read.");
        var values = new double[width * height];
        Array.Fill(values, double.NaN);
        for (var y = 0; y < height; y++) Array.Copy(rows[y], 0, values, y * width, rows[y].Length);
        return new HeightmapPicture { Width = width, Height = height, Values = values, Absolute = true, Bits = 16, Kind = "heights as numbers (world units)" };
    }

    public static string EncodeCsv(int width, int height, double[] heights)
    {
        var text = new StringBuilder();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (x > 0) text.Append(',');
                var h = heights[y * width + x];
                if (!double.IsNaN(h)) text.Append(((int)Math.Round(h)).ToString(CultureInfo.InvariantCulture));
            }
            text.Append("\r\n");
        }
        return text.ToString();
    }
}

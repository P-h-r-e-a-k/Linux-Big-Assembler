using LBAAssembler.Assets;

namespace ScriptRoundTrip;

// lba2sprite <game folder> <png prefix> <scale> <sprite>...: LBA2 sprites (SPRITES.HQR) as pictures, <prefix><sprite>.png, drawn with the
// main palette (RESS.HQR 0), and the palette ramps each uses (index / 16: how many pixels) -- what a body made after one is coloured with.
// lba2sprite <game folder> <png> palette: the palette itself, a ramp to a row.
internal static class Lba2SpriteCommand
{
    public static int Run(string[] args)
    {
        var library = Environment.GetEnvironmentVariable("RAW") == "1" ? GphLibrary.Lba2RawSprites(args[1]) : GphLibrary.Lba2Sprites(args[1]);   // (RAW=1: SPRIRAW.HQR, the effects')
        var palette = library.LoadPalette();
        if (args[3] == "palette")
        {
            // (lba2sprite <game> <png> palette: the main palette as 16 ramps of 16, a ramp to a row, 24 pixels a colour)
            var swatch = new byte[384 * 384 * 4];
            for (var i = 0; i < 384 * 384; i++)
            {
                var c = (i / 384 / 24) * 16 + (i % 384) / 24;
                swatch[i * 4] = palette[c * 3 + 2]; swatch[i * 4 + 1] = palette[c * 3 + 1]; swatch[i * 4 + 2] = palette[c * 3]; swatch[i * 4 + 3] = 255;
            }
            DoorRender.WritePng(args[2], swatch, 384, 0, 0, 384, 384, 1);
            return 0;
        }
        var scale = int.Parse(args[3]);
        foreach (var number in args.Skip(4).Select(int.Parse))
        {
            var image = library.Read(number);
            var path = $"{args[2]}{number}.png";
            DoorRender.WritePng(path, image.ToBgra(palette), image.Width, 0, 0, image.Width, image.Height, scale);
            var ramps = Enumerable.Range(0, image.Pixels.Length).Where(i => image.Opaque[i]).GroupBy(i => image.Pixels[i] >> 4)
                .OrderByDescending(g => g.Count()).Select(g => $"{g.Key * 16}: {g.Count()}");
            Console.WriteLine($"sprite {number}: {image.Width} x {image.Height}, offset ({image.OffsetX}, {image.OffsetY}) -> {path}");
            Console.WriteLine($"  ramps {string.Join(", ", ramps)}");
        }
        return 0;
    }
}

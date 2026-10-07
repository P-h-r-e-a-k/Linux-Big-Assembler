using LBAAssembler.Lba1;

namespace LBAAssembler.Terrain.Polar;

// The faces of LBA1's bricks as textures for an LBA2 island (PolarTerrain's ground atlas, PolarObjects' object atlas). LBA1 draws a brick as
// an isometric sprite: the top of its cell as a diamond -- top point (24, 0), right (48, 12), bottom (24, 24), left (0, 12) less the sprite's
// hot spot -- and, below it, the two sides LBA1's camera sees: the +x side (from the right point down to the bottom one) and the +z side
// (from the left point to the bottom one), a layer (15 pixels) tall. The other two sides (-x, -z) LBA1 never shows, so no brick has them:
// an object turned round in LBA2 shows the opposite side's texture there instead.
internal static class PolarTextures
{
    public enum Face { Top, SideX, SideZ }

    // The part of its cell a brick fills, across: u along x, v along z, 0..1 (a thin post's a small square in the middle).
    public readonly record struct Box(double U0, double U1, double V0, double V1)
    {
        public static readonly Box Full = new(0, 1, 0, 1);
        public bool IsFull => this == Full;
    }

    // A face's tile size in pixels (u along the face, v down it): 32 across a cell (LBA1 draws a cell's edge some 27 pixels long; the retail
    // islands' tiles are 32 to a cell), a side 16 down a layer.
    public const int TileSize = 32;
    public static (int W, int H) Size(Face face) => face == Face.Top ? (TileSize, TileSize) : (TileSize, TileSize / 2);

    // A brick's sprite as LBA1 palette indices (-1 where it draws nothing), and its hot spot.
    public sealed class Sprite
    {
        public int Width, Lines, HotX, HotY;
        public int[] Pixels = Array.Empty<int>();

        public static Sprite Decode(byte[]? data)
        {
            var s = new Sprite();
            if (data is null || data.Length < 4) return s;
            s.Width = data[0]; s.Lines = data[1]; s.HotX = (sbyte)data[2]; s.HotY = (sbyte)data[3];
            s.Pixels = new int[s.Width * s.Lines];
            Array.Fill(s.Pixels, -1);
            var p = 4;
            for (var line = 0; line < s.Lines && p < data.Length; line++)
            {
                int runs = data[p++], x = 0;
                for (var k = 0; k < runs && p < data.Length; k++)
                {
                    int control = data[p++], length = (control & 0x3F) + 1;
                    switch (control >> 6)
                    {
                        case 0: x += length; break;
                        case 1: for (var i = 0; i < length && p < data.Length; i++, x++) { var c = data[p++]; if (x < s.Width) s.Pixels[line * s.Width + x] = c; } break;
                        default: { var c = data[p++]; for (var i = 0; i < length; i++, x++) if (x < s.Width) s.Pixels[line * s.Width + x] = c; break; }
                    }
                }
            }
            return s;
        }

        // The colour at a point of the brick's picture (sprite coordinates before the hot spot), or -1.
        public int At(double sx, double sy)
        {
            int x = (int)Math.Floor(sx - HotX), y = (int)Math.Floor(sy - HotY);
            return x < 0 || y < 0 || x >= Width || y >= Lines ? -1 : Pixels[y * Width + x];
        }

        // How much of what the brick draws is lone pixels (none of their four neighbours drawn): a dithered wisp of mist or sparkle, LBA1's
        // way to draw something half seen through, which no solid object is.
        public double Scattered
        {
            get
            {
                int drawn = 0, lone = 0;
                bool D(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Lines && Pixels[y * Width + x] >= 0;
                for (var y = 0; y < Lines; y++)
                    for (var x = 0; x < Width; x++)
                    {
                        if (!D(x, y)) continue;
                        drawn++;
                        if (!D(x - 1, y) && !D(x + 1, y) && !D(x, y - 1) && !D(x, y + 1)) lone++;
                    }
                return drawn == 0 ? 1 : (double)lone / drawn;
            }
        }

        // How much of a face the brick draws (0..1).
        public double Coverage(Face face) => Coverage(face, Box.Full);
        public double Coverage(Face face, Box box)
        {
            var (w, h) = Size(face); var drawn = 0;
            for (var j = 0; j < h; j++) for (var i = 0; i < w; i++) if (At(Where(face, (i + 0.5) / w, (j + 0.5) / h, box)) >= 0) drawn++;
            return (double)drawn / (w * h);
        }

        // The box of its cell the brick fills, read from the outline it draws. A box's outline is the hull of its corners: its leftmost and
        // rightmost points (u0 - v1, u1 - v0), its top and bottom ones (u0 + v0, u1 + v1) -- three sums that fix its middle and its width
        // and depth together, (u1 - u0) + (v1 - v0), but not each alone: of the boxes they allow, the one whose outline is most like the
        // brick's. A brick most like its whole cell, or like no box at all (a curve), fills its cell -- unless it draws little of it.
        // (the last fit's likenesses, whole cell and best box, for the study commands)
        public static (double Full, double Best, Box Box) LastFit;
        public Box Footprint()
        {
            var drawn = new HashSet<(int, int)>();
            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            for (var y = 0; y < Lines; y++)
                for (var x = 0; x < Width; x++)
                    if (Pixels[y * Width + x] >= 0)
                    {
                        int sx = x + HotX, sy = y + HotY;
                        drawn.Add((sx, sy));
                        minX = Math.Min(minX, sx); maxX = Math.Max(maxX, sx); minY = Math.Min(minY, sy); maxY = Math.Max(maxY, sy);
                    }
            if (drawn.Count < 8) return Box.Full;
            double a = (minX - 24) / 24.0, b = (maxX + 1 - 24) / 24.0, c = minY / 12.0, d = (maxY + 1 - 15) / 12.0;
            double k = (a + b + c + d) / 2, l = (c + d - a - b) / 2, sum = ((b - a) + (d - c)) / 2;
            var full = Likeness(drawn, Box.Full);
            var best = (Box: Box.Full, Score: full);
            for (var wu = 1 / 16.0; wu <= Math.Min(1, sum) + 1e-9; wu += 1 / 32.0)
            {
                var wv = sum - wu;
                if (wv < 1 / 16.0 || wv > 1) continue;
                var box = new Box(Math.Clamp((k - wu) / 2, 0, 1), Math.Clamp((k + wu) / 2, 0, 1), Math.Clamp((l - wv) / 2, 0, 1), Math.Clamp((l + wv) / 2, 0, 1));
                if (box.U1 - box.U0 < 1 / 16.0 || box.V1 - box.V0 < 1 / 16.0) continue;
                var score = Likeness(drawn, box);
                if (score > best.Score) best = (box, score);
            }
            LastFit = (full, best.Score, best.Box);
            // (a brick that draws little of its cell -- a piece of a post or a rail, whose picture LBA1 splits among the bricks of the cells
            // it crosses on the screen, not in the world -- is the box nearest it however unlike, a stick rather than a block)
            var sparse = full < 0.35 && best.Score > full;
            if (!sparse && (best.Score < 0.6 || full > 0.85 || best.Score < full + 0.1)) return Box.Full;
            var (u0, u1, v0, v1) = best.Box;
            return u0 <= 0.1 && u1 >= 0.9 && v0 <= 0.1 && v1 >= 0.9 ? Box.Full : best.Box;
        }

        // How alike a brick's outline and a box's are: the pixels both cover over those either does.
        private static double Likeness(HashSet<(int X, int Y)> drawn, Box box)
        {
            var hull = Hull(new[] { (box.U0, box.V0), (box.U1, box.V0), (box.U0, box.V1), (box.U1, box.V1) }
                .SelectMany(p => new[] { Project(p.Item1, p.Item2, 1), Project(p.Item1, p.Item2, 0) }).ToList());
            int both = 0, either = drawn.Count;
            for (var y = -2; y < 42; y++)
                for (var x = -2; x < 50; x++)
                {
                    if (!Inside(hull, x + 0.5, y + 0.5)) continue;
                    if (drawn.Contains((x, y))) both++; else either++;
                }
            return either == 0 ? 0 : (double)both / either;
        }

        // A point of the cell (u, v across, h up a layer, 0..1) on the brick's picture (sprite coordinates before the hot spot).
        public static (double X, double Y) Project(double u, double v, double h) => (24 + 24 * (u - v), 12 * (u + v) + 15 * (1 - h));

        private static List<(double X, double Y)> Hull(List<(double X, double Y)> points)
        {
            var p = points.Distinct().OrderBy(q => q.X).ThenBy(q => q.Y).ToList();
            if (p.Count < 3) return p;
            static double Cross((double X, double Y) o, (double X, double Y) a, (double X, double Y) b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
            var hull = new List<(double X, double Y)>();
            foreach (var pass in new[] { p, Enumerable.Reverse(p).ToList() })
            {
                var start = hull.Count;
                foreach (var q in pass)
                {
                    while (hull.Count >= start + 2 && Cross(hull[^2], hull[^1], q) <= 0) hull.RemoveAt(hull.Count - 1);
                    hull.Add(q);
                }
                hull.RemoveAt(hull.Count - 1);
            }
            return hull;
        }

        private static bool Inside(List<(double X, double Y)> hull, double x, double y)
        {
            for (var i = 0; i < hull.Count; i++)
            {
                var (ax, ay) = hull[i]; var (bx, by) = hull[(i + 1) % hull.Count];
                if ((bx - ax) * (y - ay) - (by - ay) * (x - ax) < 0) return false;
            }
            return hull.Count >= 3;
        }

        private int At((double X, double Y) p) => At(p.X, p.Y);

        // A face's tile: texel (i, j) -- u along the face, v down it -- the brick's pixel there; where the brick draws nothing, the nearest
        // pixel it draws on the face. A face the brick hardly draws (a side LBA1 never showed, a thin post's) is the tile of the face it
        // draws most of, at this face's size; a brick that draws none of them much, its pixels' most common colour.
        public const double Drawn = 0.3;
        public int[] Tile(Face face) => Tile(face, Box.Full);
        // (a face of the box the brick fills, when it doesn't fill its cell)
        public int[] Tile(Face face, Box box)
        {
            if (Coverage(face, box) >= Drawn) return Sample(face, face, box);
            var best = Enum.GetValues<Face>().MaxBy(f => Coverage(f, box));
            if (Coverage(best, box) >= Drawn) return Sample(best, face, box);
            var (w, h) = Size(face);
            var common = Pixels.Where(c => c >= 0).GroupBy(c => c).OrderByDescending(g => g.Count()).Select(g => g.Key).DefaultIfEmpty(0).First();
            var tile = Sample(best, face, box);
            var blend = Coverage(best, box) > 0.05;
            for (var i = 0; i < tile.Length; i++) if (!blend || tile[i] <= 0) tile[i] = common;
            return blend ? tile : Enumerable.Repeat(common, w * h).ToArray();
        }

        // A face's pixels as a tile the size of another's (`size`).
        private int[] Sample(Face face, Face size, Box box)
        {
            var (w, h) = Size(size);
            var tile = new int[w * h];
            var drawn = new List<(int I, int J, int C)>();
            for (var j = 0; j < h; j++)
                for (var i = 0; i < w; i++)
                {
                    var c = At(Where(face, (i + 0.5) / w, (j + 0.5) / h, box));
                    tile[j * w + i] = c;
                    if (c >= 0) drawn.Add((i, j, c));
                }
            for (var j = 0; j < h; j++)
                for (var i = 0; i < w; i++)
                    if (tile[j * w + i] < 0)
                        tile[j * w + i] = drawn.Count == 0 ? 0 : drawn.MinBy(d => (d.I - i) * (d.I - i) + (d.J - j) * (d.J - j)).C;
            return tile;
        }

        // A point of a face in sprite coordinates: the top (s along x, t along z); the +x side (s along z, t down a layer); the +z side
        // (s along x, t down) -- of the whole cell, or of a box in it.
        public static (double X, double Y) Where(Face face, double s, double t) => Where(face, s, t, Box.Full);
        public static (double X, double Y) Where(Face face, double s, double t, Box box)
        {
            double U(double f) => box.U0 + f * (box.U1 - box.U0);
            double V(double f) => box.V0 + f * (box.V1 - box.V0);
            return face switch
            {
                Face.Top => Project(U(s), V(t), 1),
                Face.SideX => Project(box.U1, V(s), 1 - t),
                _ => Project(U(s), box.V1, 1 - t),
            };
        }
    }

    // LBA1's palette to an LBA2 island palette: each LBA1 colour the nearest of the island's lit colours (16..239: not the first ramp nor
    // the unlit ones), by a weighted RGB distance.
    public sealed class Colours
    {
        public readonly int[] Nearest = new int[256];
        public readonly byte[] Lba1;

        public Colours(byte[] lba1Palette, byte[] islandPalette)
        {
            Lba1 = lba1Palette;
            for (var c = 0; c < 256; c++)
            {
                var (r, g, b) = Rgb(lba1Palette, c);
                var best = 16; var bestD = int.MaxValue;
                for (var p = 16; p <= 239; p++)
                {
                    var (pr, pg, pb) = Rgb(islandPalette, p);
                    var d = (r - pr) * (r - pr) * 3 + (g - pg) * (g - pg) * 4 + (b - pb) * (b - pb) * 2;
                    if (d < bestD) { bestD = d; best = p; }
                }
                Nearest[c] = best;
            }
        }

        // a colour of a palette as 8-bit RGB (the 6-bit palettes, 0..63, four times brighter)
        public static (int R, int G, int B) Rgb(byte[] palette, int i)
        {
            var six = palette.Take(768).Max() <= 63;
            var k = six ? 4 : 1;
            return (palette[i * 3] * k, palette[i * 3 + 1] * k, palette[i * 3 + 2] * k);
        }
    }

    // Texture pages of tiles: each page 256 x 256 in slots of 32 x 32, a slot holding one 32 x 32 tile or two 32 x 16 (a side a layer
    // tall) one over the other. Tiles go into the first page with room; a page is added when none has (up to the most pages there may
    // be). Place: each tile's page and corner.
    public sealed class Pages
    {
        public const int Slot = TileSize, SlotsPerRow = 256 / Slot, Halves = SlotsPerRow * SlotsPerRow * 2;
        public readonly List<byte[]> List = new();
        public readonly Dictionary<object, (int Page, int X, int Y)> Place = new();
        private readonly List<bool[]> used = new();
        private readonly Colours colours;
        private readonly int max;

        // `first`: the page to fill first (the island's own: its ground or object atlas); `max`: how many pages there may be in all
        public Pages(byte[] first, Colours colours, int max)
        {
            this.colours = colours; this.max = max;
            Array.Clear(first);
            List.Add(first); used.Add(new bool[Halves]);
        }

        // Keeps a row of slots of the first page empty: the ground's last row, where a race track's road tiles go (RaceTrackTextures
        // copies them into the blocks of page 0 that no triangle reads).
        public void Reserve(int row)
        {
            for (var s = row * SlotsPerRow; s < (row + 1) * SlotsPerRow; s++) used[0][2 * s] = used[0][2 * s + 1] = true;
        }

        // Half-slots free on a page.
        public int Free(int page) => page < used.Count ? used[page].Count(u => !u) : Halves;
        public int Count => List.Count;

        // How many half-slots a tile takes.
        public static int HalvesOf(int h) => h > Slot / 2 ? 2 : 1;

        // Puts a tile on a page (`page`, or the first with room). False when no page may take it.
        public bool Add(object key, int[] tile, int w, int h, int? page = null)
        {
            if (Place.ContainsKey(key)) return true;
            var need = HalvesOf(h);
            for (var p = page ?? 0; p < (page is null ? max : page.Value + 1); p++)
            {
                while (p >= List.Count) { if (List.Count >= max) return false; List.Add(new byte[256 * 256]); used.Add(new bool[Halves]); }
                var slots = used[p];
                for (var i = 0; i < Halves; i += need == 2 ? 2 : 1)
                {
                    if (slots[i] || (need == 2 && slots[i + 1])) continue;
                    slots[i] = true; if (need == 2) slots[i + 1] = true;
                    int slot = i / 2, x = slot % SlotsPerRow * Slot, y = slot / SlotsPerRow * Slot + (i % 2) * (Slot / 2);
                    var pixels = List[p];
                    for (var j = 0; j < h; j++)
                        for (var k = 0; k < w; k++)
                            pixels[(y + j) * 256 + x + k] = (byte)colours.Nearest[tile[j * w + k]];
                    Place[key] = (p, x, y);
                    return true;
                }
            }
            return false;
        }
    }
}

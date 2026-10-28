using System.Buffers.Binary;
using System.IO;
using System.Numerics;
using LbaBodyStudio;

namespace LBAAssembler.Terrain.Palace;

// The palace's pictures of the game's characters (the user, 2026-10-13: "Let's also see if we can generate some pictures to hang on the
// walls based on some of the games characters"). Each is painted here from the game folder's own body (BODY.HQR, and RESS.HQR's page of
// the bodies' textures) in the island's palette, in the colours the game draws that body in there: three-quarters from the front, lit
// from above at the left as the game lights a body (steps up its colours' ramps), on a dark backdrop, in a gold frame -- Wide x High
// pixels, four to a page of the island's object textures (IslandFile.ObjectPages). The palace finds them their places (Spots: a room's
// plain wall, Cells across and Layers up, a cell of it clear on each side, nothing standing in front) and hangs them (Hang): a body of
// one perspective-correct textured polygon (TEXTUREZ) each, where the wall's own faces are left out (Covered) -- no wall behind it for
// the depth test to mix it up with.
internal static class PalacePortraits
{
    public const int Cells = 2, Layers = 8;     // on the wall: 2 cells across and 8 layers up (4,096 x 5,120 on the island) ...
    public const int Wide = 96, High = 120;     // ... in pixels: 42.7 world units each
    public const int Over = 2;                  // its foot this many layers over the floor's top (over the skirting)
    public const int Border = 5;                // the frame (pixels)
    public const int PerPage = 4;               // two across a page and two down
    private const int BodyTextures = 6;         // RESS.HQR: the bodies' texture page
    private const int LightSteps = 9;           // the most steps up its ramp the light takes a body's polygon (LightModel.Max)
    private const double Tilt = 0.12;           // the viewer a little over the sitter (radians)
    private const int Spacing = 7;              // pictures along a long wall: one every this many cells
    private const int Clear = 5;                // the room's air this many cells out from a picture

    // A picture's texture entry: its place in the page, and a repeat mask of 128 x 128 (more than it is: its UVs stay inside it).
    public static uint Entry(int x, int y) => 0x7F7F0000u | (uint)(y * 256 + x);

    // A sitter: who, their body (BODY.HQR), how far they are turned from looking at the viewer (radians: their left towards the viewer),
    // how much of them shows (a share of their height from the top: 1 the whole of them standing), the backdrop's colour, and how much
    // bigger than the frame holds them (the Dino-Fly's wings past its sides).
    public sealed record Sitter(string Name, int Body, double Turn, double Show, (float R, float G, float B) Backdrop, double Zoom = 1);

    // (the last, the Queen of the Mosquibees, hangs only by the fireplaces that are a teleporter -- PalaceIsland; the user, 2026-10-13: "Let's
    // move the queen's picture to where dinofly is, and drop the dinofly portrait"). The people of Zeelich, whose Emperor's palace it is
    // (the user, 2026-10-16: "Replace portraits with characters from Zeelich"; they were Twinsun's: Twinsen, Zoe, the Dark Monk, FunFrock,
    // Baldino, Joe the Elf).
    public static readonly Sitter[] Sitters =
    {
        new("a Sup guard of the palace", 271, 0.45, 0.62, (44, 66, 100)),
        new("an old Sup lady", 263, -0.45, 0.62, (100, 44, 56)),
        new("a Sup priest", 292, 0.35, 0.7, (40, 40, 54)),
        new("the old Franco", 276, -0.35, 0.65, (84, 34, 32)),
        new("a Franco policeman", 86, 0.45, 0.66, (44, 76, 64)),
        new("a Wannie", 194, -0.45, 0.85, (66, 54, 88)),
        new("a Mosquibee", 192, 0.4, 0.85, (60, 80, 44)),
        new("the Queen of the Mosquibees", 196, -0.45, 0.8, (84, 66, 34)),
    };
    public static int Queen => Sitters.Length - 1;

    // ---- the pictures ------------------------------------------------------------------------------------------------------------------

    public sealed record Painted(List<byte[]> Pages, (int Page, int X, int Y)[] Places, List<string> Log);

    // Every sitter's picture, four to a page (the rest of a page left 0); a sitter whose body the game folder hasn't is left out (page -1).
    // `nearest`: a colour's nearest in the island's palette.
    public static Painted PaintAll(string gameDirectory, Func<(float R, float G, float B), int> nearest)
    {
        var log = new List<string>();
        var bodies = HqrArchive.Open(Path.Combine(gameDirectory, "BODY.HQR"));
        byte[]? texture = null;
        try { texture = HqrArchive.Open(Path.Combine(gameDirectory, "RESS.HQR")).Read(BodyTextures); }
        catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException) { log.Add($"the bodies' textures: {e.Message}"); }
        var pages = new List<byte[]>();
        var places = new (int Page, int X, int Y)[Sitters.Length];
        var painted = 0;
        for (var k = 0; k < Sitters.Length; k++)
        {
            var sitter = Sitters[k];
            Body body;
            try { body = Body.Read(bodies.Read(sitter.Body), 2, allowStatic: true); }
            catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException)
            {
                log.Add($"{sitter.Name} (body {sitter.Body}): {e.Message}");
                places[k] = (-1, 0, 0);
                continue;
            }
            var picture = Paint(body, texture, nearest, sitter);
            int page = painted / PerPage, within = painted % PerPage, x = within % 2 * Wide, y = within / 2 * High;
            while (pages.Count <= page) pages.Add(new byte[256 * 256]);
            for (var row = 0; row < High; row++) Array.Copy(picture, row * Wide, pages[page], (y + row) * 256 + x, Wide);
            places[k] = (page, x, y);
            painted++;
        }
        log.Insert(0, $"the pictures: {painted} of the game's characters ({string.Join(", ", Sitters.Where((_, k) => places[k].Page >= 0).Select(s => s.Name))}) " +
                      $"painted, {pages.Count} pages of the island's object textures");
        return new Painted(pages, places, log);
    }

    // A sitter's picture: Wide x High indices of the island's palette.
    public static byte[] Paint(Body body, byte[]? texturePage, Func<(float R, float G, float B), int> nearest, Sitter sitter)
    {
        var image = new byte[Wide * High];
        var depth = new float[Wide * High];
        Array.Fill(depth, float.PositiveInfinity);
        // ---- the backdrop: the sitter's colour, lightest behind their head, darker towards the frame -- in eighths, dithered between them
        // (the ordered dither of a 4 x 4 Bayer matrix)
        int[] bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
        for (var y = 0; y < High; y++)
            for (var x = 0; x < Wide; x++)
            {
                double dx = (x + 0.5 - Wide / 2.0) / (Wide / 2.0), dy = (y + 0.5 - High * 0.32) / (High / 2.0);
                var eighths = Math.Clamp(1.3 - 0.6 * Math.Sqrt(dx * dx + dy * dy), 0.45, 1.3) * 8;
                var glow = (Math.Floor(eighths) + (eighths - Math.Floor(eighths) > (bayer[(y & 3) * 4 + (x & 3)] + 0.5) / 16 ? 1 : 0)) / 8;
                var (r, g, b) = sitter.Backdrop;
                image[y * Wide + x] = (byte)nearest(((float)Math.Min(255, Math.Round(r * glow)), (float)Math.Min(255, Math.Round(g * glow)), (float)Math.Min(255, Math.Round(b * glow))));
            }

        // ---- the sitter: their body as modelled (the pose it rests in), turned, seen from a little above (the viewer looks along +z: the
        // nearer a point, the smaller its z)
        var world = body.World();
        double ct = Math.Cos(sitter.Turn), st = Math.Sin(sitter.Turn), cp = Math.Cos(Tilt), sp = Math.Sin(Tilt);
        Vector3 Turned(Vector3 p)
        {
            // (a body looks towards +z, the way the viewer looks: turned half round to look at the viewer, and by the sitter's turn)
            double x = -(p.X * ct + p.Z * st), z = -(-p.X * st + p.Z * ct), y = p.Y;
            return new Vector3((float)x, (float)(y * cp + z * sp), (float)(-y * sp + z * cp));
        }
        var seen = world.Select(Turned).ToArray();
        float top = seen.Max(p => p.Y), bottom = seen.Min(p => p.Y);
        var cut = top - (float)sitter.Show * (top - bottom);
        var shown = seen.Where(p => p.Y >= cut - 1).ToList();
        float left = shown.Min(p => p.X), right = shown.Max(p => p.X);
        int inW = Wide - 2 * Border, inH = High - 2 * Border;
        var whole = sitter.Show >= 1;
        var scale = Math.Min(inH * (whole ? 0.88 : 0.93) / Math.Max(1, top - cut), inW * 0.94 * sitter.Zoom / Math.Max(1, right - left));
        var sx0 = Wide / 2.0 - (left + right) / 2 * scale;
        // (the head a little under the frame's top -- the whole of them, standing, in the middle)
        var sy0 = whole ? Border + (inH - (top - bottom) * scale) / 2 + top * scale : Border + inH * 0.05 + top * scale;
        (double X, double Y) Screen(Vector3 p) => (sx0 + p.X * scale, sy0 - p.Y * scale);

        // (the light: from above, at the viewer's left, towards the viewer; normals the posed body's own, turned with it)
        var light = Vector3.Normalize(new Vector3(-0.55f, 0.65f, -0.55f));
        var normals = new Vector3[world.Length];
        foreach (var f in body.Faces)
        {
            var n = Newell(f.Points.Select(i => seen[i]).ToArray());
            foreach (var i in f.Points) normals[i] += n;
        }
        float Steps(int point, Vector3 n)
        {
            var share = body.LightScale is { } ls && point < ls.Length ? ls[point] : 1f;
            var d = n.LengthSquared() > 1e-12f ? Vector3.Dot(Vector3.Normalize(n), light) : 0;
            return Math.Clamp(d, 0, 1) * LightSteps * share;
        }
        var pointSteps = normals.Select((n, i) => Steps(i, n)).ToArray();
        static int Lit(int colour, float steps) => Math.Min((colour & ~15) + 15, colour + (int)Math.Round(steps));

        foreach (var f in body.Faces)
        {
            var type = f.Material;
            // (how the game lights a type: 0 not, 1 a step for the whole polygon, 2 corner by corner; textured from 8 up, as 0-7 again; the
            // "incrust" ones' colour 0 see-through)
            var lighting = (type & 3) switch { 0 => 0, 1 => 1, _ => type is 2 or 3 ? 0 : 2 };
            if (type is 4 or 5 or 6 or 7) lighting = 2;
            var incrust = type is >= 12 and <= 15 or >= 20 and <= 23;
            var tex = f.Texture is { } t && texturePage != null && t.Handle < body.Textures.Length ? t : null;
            var faceSteps = lighting == 1 ? f.Points.Average(i => Steps(i, Newell(f.Points.Select(p => seen[p]).ToArray()))) : 0;
            for (var k = 1; k + 1 < f.Points.Length; k++)
            {
                int ia = f.Points[0], ib = f.Points[k], ic = f.Points[k + 1];
                var (pa, pb, pc) = (Screen(seen[ia]), Screen(seen[ib]), Screen(seen[ic]));
                var area = Edge(pa, pb, pc.X, pc.Y);
                if (Math.Abs(area) < 1e-6) continue;
                int x0 = Math.Max(Border, (int)Math.Floor(Math.Min(pa.X, Math.Min(pb.X, pc.X)))), x1 = Math.Min(Wide - 1 - Border, (int)Math.Ceiling(Math.Max(pa.X, Math.Max(pb.X, pc.X))));
                int y0 = Math.Max(Border, (int)Math.Floor(Math.Min(pa.Y, Math.Min(pb.Y, pc.Y)))), y1 = Math.Min(High - 1 - Border, (int)Math.Ceiling(Math.Max(pa.Y, Math.Max(pb.Y, pc.Y))));
                for (var y = y0; y <= y1; y++)
                    for (var x = x0; x <= x1; x++)
                    {
                        double wa = Edge(pb, pc, x + 0.5, y + 0.5) / area, wb = Edge(pc, pa, x + 0.5, y + 0.5) / area, wc = 1 - wa - wb;
                        if (wa < -1e-4 || wb < -1e-4 || wc < -1e-4) continue;
                        var z = (float)(wa * seen[ia].Z + wb * seen[ib].Z + wc * seen[ic].Z);
                        var at = y * Wide + x;
                        if (z > depth[at]) continue;
                        var colour = f.Colour;
                        if (tex is not null)
                        {
                            // (U, V: 8.8 fixed point pixels of the page, from the entry's place, wrapped by its repeat mask)
                            double u = wa * tex.UV[0] + wb * tex.UV[k * 2] + wc * tex.UV[(k + 1) * 2];
                            double v = wa * tex.UV[1] + wb * tex.UV[k * 2 + 1] + wc * tex.UV[(k + 1) * 2 + 1];
                            var info = body.Textures[tex.Handle];
                            var mask = (int)(info >> 16);
                            int tx = ((int)u >> 8) & (mask & 0xFF), ty = ((int)v >> 8) & ((mask >> 8) & 0xFF);
                            colour = texturePage![((int)(info & 0xFFFF) + ty * 256 + tx) & 0xFFFF];
                            if (incrust && colour == 0) continue;
                        }
                        if (lighting == 1) colour = Lit(colour, (float)faceSteps);
                        else if (lighting == 2) colour = Lit(colour, (float)(wa * pointSteps[ia] + wb * pointSteps[ib] + wc * pointSteps[ic]));
                        depth[at] = z;
                        image[at] = (byte)Math.Clamp(colour, 1, 254);
                    }
            }
        }
        // (lines and spheres -- hair, hands, eyes -- through the same depth test)
        void Plot(int x, int y, float z, int colour)
        {
            if (x < Border || y < Border || x >= Wide - Border || y >= High - Border) return;
            var at = y * Wide + x;
            if (z > depth[at]) return;
            depth[at] = z; image[at] = (byte)Math.Clamp(colour, 1, 254);
        }
        foreach (var l in body.Lines)
        {
            var (pa, pb) = (Screen(seen[l.A]), Screen(seen[l.B]));
            var n = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(pb.X - pa.X), Math.Abs(pb.Y - pa.Y)) * 2));
            for (var k = 0; k <= n; k++)
            {
                var t = k / (double)n;
                Plot((int)Math.Floor(pa.X + (pb.X - pa.X) * t), (int)Math.Floor(pa.Y + (pb.Y - pa.Y) * t), seen[l.A].Z + (seen[l.B].Z - seen[l.A].Z) * (float)t - 2, l.Colour);
            }
        }
        foreach (var s in body.Spheres)
        {
            var c = seen[s.Point]; var p = Screen(c); var r = Math.Max(0.7, s.Radius * scale);
            for (var y = (int)Math.Floor(p.Y - r); y <= (int)Math.Ceiling(p.Y + r); y++)
                for (var x = (int)Math.Floor(p.X - r); x <= (int)Math.Ceiling(p.X + r); x++)
                {
                    double dx = x + 0.5 - p.X, dy = y + 0.5 - p.Y, d2 = dx * dx + dy * dy;
                    if (d2 > r * r) continue;
                    Plot(x, y, c.Z - (float)(Math.Sqrt(r * r - d2) / scale), s.Colour);
                }
        }

        // ---- the frame: gold, lit from the top left (the top and left sides' outer slope light and inner slope dark, the others the other
        // way round), dark lines round it and inside it
        int shadow = nearest((36, 24, 10)), dark = nearest((112, 80, 32)), gold = nearest((180, 136, 58)), light2 = nearest((236, 200, 116));
        for (var y = 0; y < High; y++)
            for (var x = 0; x < Wide; x++)
            {
                var d = Math.Min(Math.Min(x, y), Math.Min(Wide - 1 - x, High - 1 - y));
                if (d >= Border) continue;
                var lit = (y == d || x == d) && !(Wide - 1 - x == d && y != d) && !(High - 1 - y == d && x != d);
                image[y * Wide + x] = (byte)(d switch { 0 => shadow, 1 => lit ? light2 : dark, 2 => gold, 3 => lit ? dark : light2, _ => shadow });
            }
        return image;
    }

    private static double Edge((double X, double Y) a, (double X, double Y) b, double x, double y) => (x - a.X) * (b.Y - a.Y) - (y - a.Y) * (b.X - a.X);

    // A polygon's normal (Newell's), as long as twice its area: outwards, the way the games' bodies wind theirs (Body.VertexNormals).
    private static Vector3 Newell(Vector3[] p)
    {
        var n = Vector3.Zero;
        for (var i = 0; i < p.Length; i++)
        {
            var (a, b) = (p[i], p[(i + 1) % p.Length]);
            n += new Vector3((a.Y - b.Y) * (a.Z + b.Z), (a.Z - b.Z) * (a.X + b.X), (a.X - b.X) * (a.Y + b.Y));
        }
        return n;
    }

    // ---- the places ------------------------------------------------------------------------------------------------------------------

    // A picture's place: the room, the wall's plane (`Dir` the way its faces look -- BrickBuilding.Directions, a side one -- and `At` its
    // cells' coordinate across it), its first cell along the wall and its first layer; and who is in it.
    public sealed record Spot(int Tile, int Dir, int At, int U0, int Y0, int Sitter);

    // The wall's faces a picture covers: its cells, each with the way the face looks.
    public static IEnumerable<((int X, int Y, int Z) Cell, int Dir)> Covered(Spot s)
    {
        var dx = BrickBuilding.Directions[s.Dir].Dx;
        for (var u = s.U0; u < s.U0 + Cells; u++)
            for (var y = s.Y0; y < s.Y0 + Layers; y++)
                yield return (dx != 0 ? (s.At, y, u) : (u, y, s.At), s.Dir);
    }

    // The places for pictures: on the rooms' walls (`shown`: what is drawn) facing a room's `air`, the picture's whole Cells x Layers -- from
    // Over layers over the floor's top (`floorTop`: the room's air's lowest layer) -- plain wall: none of it a piece of furniture on the wall
    // (`furniture`: their cells, and the standing pieces'), the room's air Clear cells out from it with nothing standing in the first two,
    // and a cell of plain wall on each side (not hard by a door). In the middle of each such stretch of a wall (one every Spacing cells along a long one), `perRoom` a room at
    // the most -- the longest stretches -- and the sitters by turns. `room`: the room (tile) a cell of air is in, -1 none.
    // (`all`: every place a picture could hang, chosen or not -- PalaceIsland's teleporters choose from them)
    public static List<Spot> Spots(IReadOnlyDictionary<(int X, int Y, int Z), (int Brick, int Tile)> shown, HashSet<(int X, int Y, int Z)> air,
        HashSet<(int X, int Y, int Z)> furniture, int floorTop, Func<(int X, int Y, int Z), int> room, int perRoom, out List<Spot> all, Func<int, int>? morePerRoom = null)
    {
        var planes = new Dictionary<(int Dir, int At), HashSet<(int U, int Y)>>();
        var rooms = new Dictionary<(int Dir, int At, int U), int>();
        var y0 = floorTop + Over;
        foreach (var ((x, y, z), _) in shown)
        {
            if (y < y0 || y >= y0 + Layers || furniture.Contains((x, y, z))) continue;
            for (var d = 2; d < BrickBuilding.Directions.Length; d++)
            {
                var (dx, _, dz, _) = BrickBuilding.Directions[d];
                var front = (x + dx, y, z + dz);
                // (the room's air Clear cells out from it -- not a door's side: its way through the walls is four cells wide -- and no piece
                // of furniture in the first two)
                var open = true;
                for (var k = 1; k <= Clear && open; k++)
                {
                    var c = (x + k * dx, y, z + k * dz);
                    open = air.Contains(c) && (k > 2 || !furniture.Contains(c));
                }
                if (!open) continue;
                var key = (d, dx != 0 ? x : z);
                if (!planes.TryGetValue(key, out var plane)) planes[key] = plane = new();
                var u = dx != 0 ? z : x;
                plane.Add((u, y));
                rooms[(d, key.Item2, u)] = room(front);
            }
        }
        var found = new List<(int Tile, int Dir, int At, int U0, int Run)>();
        foreach (var ((d, at), plane) in planes)
        {
            // (the cells along the wall whose every layer the picture takes is plain wall, in runs)
            var columns = plane.Select(p => p.U).Distinct().Where(u => Enumerable.Range(y0, Layers).All(y => plane.Contains((u, y)))).OrderBy(u => u).ToList();
            for (var i = 0; i < columns.Count;)
            {
                var j = i;
                while (j + 1 < columns.Count && columns[j + 1] == columns[j] + 1 && rooms[(d, at, columns[j + 1])] == rooms[(d, at, columns[i])]) j++;
                int a = columns[i], b = columns[j], run = b - a + 1, tile = rooms[(d, at, a)];
                i = j + 1;
                if (run < Cells + 2 || tile < 0) continue;
                // (a cell clear on each side: the pictures in the run's inside, one every Spacing cells, centred)
                var inside = run - 2;
                var count = Math.Max(1, (inside + Spacing - Cells) / Spacing);
                var first = a + 1 + (inside - (count - 1) * Spacing - Cells) / 2;
                for (var k = 0; k < count; k++) found.Add((tile, d, at, first + k * Spacing, run));
            }
        }
        var spots = new List<Spot>();
        var turn = 0;
        foreach (var group in found.GroupBy(f => f.Tile).OrderBy(g => g.Key))
        {
            var most = perRoom + (morePerRoom?.Invoke(group.Key) ?? 0);
            foreach (var f in group.OrderByDescending(f => f.Run).ThenBy(f => f.Dir).ThenBy(f => f.At).ThenBy(f => f.U0).Take(most)
                         .OrderBy(f => f.Dir).ThenBy(f => f.At).ThenBy(f => f.U0))
                spots.Add(new Spot(f.Tile, f.Dir, f.At, f.U0, y0, turn++ % Queen));
        }
        all = found.Select(f => new Spot(f.Tile, f.Dir, f.At, f.U0, y0, -1)).ToList();
        return spots;
    }

    // A picture's middle on its wall (the palace's cells, x and z) and the way it looks.
    public static (double X, double Z, int Nx, int Nz) Middle(Spot s)
    {
        var (dx, _, dz, _) = BrickBuilding.Directions[s.Dir];
        return dx != 0 ? (s.At + (dx > 0 ? 1 : 0), s.U0 + Cells / 2.0, dx, 0) : (s.U0 + Cells / 2.0, s.At + (dz > 0 ? 1 : 0), 0, dz);
    }

    // ---- hung ---------------------------------------------------------------------------------------------------------------------------

    // The pictures hung: a body each (appended to `newBodies`, numbered from `firstBody` on) -- one textured polygon, perspective-correct,
    // on the wall's plane where its faces are left out -- a decor nothing touches in every cube it can be seen from (`reach`), its page in
    // the decor's body number (IslandFile.DecorPageShift: `firstPage` is the island's object page of the painted pages' first), drawn close
    // to the camera (BrickBuilding.CloseFlag). `corner`: a cell corner's place in the world. Returns how many were hung.
    public static int Hang(IslandFile island, List<Spot> spots, Painted painted, int firstPage, Func<int, int, int, Vector3> corner, List<byte[]> newBodies,
        int firstBody, int maxPerCube, double reach)
    {
        var hung = 0;
        foreach (var s in spots)
        {
            var (page, px, py) = painted.Places[s.Sitter];
            if (page < 0) continue;
            var (dx, _, dz, _) = BrickBuilding.Directions[s.Dir];
            // (the wall's plane: the cells' face on the side the room is)
            Vector3 P(int u, int y) => dx != 0 ? corner(s.At + (dx > 0 ? 1 : 0), y, u) : corner(u, y, s.At + (dz > 0 ? 1 : 0));
            var quad = new[] { P(s.U0, s.Y0), P(s.U0 + Cells, s.Y0), P(s.U0 + Cells, s.Y0 + Layers), P(s.U0, s.Y0 + Layers) };
            // (seen from the room, looking at the wall: the picture's left to the right of the way the wall faces, its top up)
            var right = new Vector3(dz, 0, -dx);
            float a = quad.Min(q => Vector3.Dot(q, right)), b = quad.Max(q => Vector3.Dot(q, right)), lo = quad.Min(q => q.Y), hi = quad.Max(q => q.Y);
            // (8.8 fixed point pixels from the picture's corner, half a pixel in from its edges: nothing of the next picture on the page)
            int U(Vector3 q) => (int)Math.Round((0.5 + (Vector3.Dot(q, right) - a) / (b - a) * (Wide - 1)) * 256);
            int V(Vector3 q) => (int)Math.Round((0.5 + (hi - q.Y) / (hi - lo) * (High - 1)) * 256);
            var origin = (quad[0] + quad[2]) / 2;
            var points = quad.Select(q => q - origin).ToList();
            var ids = new[] { 0, 1, 2, 3 };
            // (wound so that the plain cross product points out of the wall, as the rooms' faces are: BrickBuilding.AddBodies)
            var n = Vector3.Cross(quad[1] - quad[0], quad[2] - quad[0]);
            if (Vector3.Dot(n, new Vector3(dx, 0, dz)) < 0) Array.Reverse(ids);
            var uv = ids.SelectMany(i => new[] { U(quad[i]), V(quad[i]) }).ToArray();
            var header = new byte[96];
            BinaryPrimitives.WriteInt32LittleEndian(header, 0x10 | BrickBuilding.CloseFlag);
            var body = new Body
            {
                Game = 2, Static = true, Lit = false, Header = header, Vertices = points,
                Bones = new List<Bone> { new(0, points.Count, 0, -1, new byte[8]) },
                Faces = new List<Face> { new(ids, 0, Texture: new FaceTexture(0, uv), Lba2Type: 16) },
                Textures = new[] { Entry(px, py) },
            };
            var number = firstBody + newBodies.Count;
            newBodies.Add(body.Write());
            var lo3 = quad.Aggregate(Vector3.Min); var hi3 = quad.Aggregate(Vector3.Max);
            var placed = BrickBuilding.AddToCubes(island, number | (firstPage + page) << IslandFile.DecorPageShift, 0, origin.X, (int)Math.Round(origin.Y), origin.Z,
                lo3.X, hi3.X, (int)Math.Floor(lo3.Y), BrickBuilding.NoBoxTop, lo3.Z, hi3.Z, maxPerCube, reach).Placed;
            if (placed > 0) hung++;
        }
        return hung;
    }
}

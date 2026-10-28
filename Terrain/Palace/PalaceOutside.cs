using System.Buffers.Binary;
using System.IO;
using System.Numerics;
using LbaBodyStudio;

namespace LBAAssembler.Terrain.Palace;

// The palace's outside (the user, 2026-10-12: "We need to add in the outside of the palace as featured in Otringal.ile"). On Otringal's
// island the palace is a cluster of decor objects on the hill of cube (8, 7): four octagonal halls round four points -- three gold domes
// ringed in turquoise and a grey one -- each hall cut into the quarters of OTRINGAL.OBL's bodies 36-48 (a quarter turned about its hall's
// middle, the towers, wings and the grey stone dome to the north-west built onto them), the porch with the red front door on its west
// side (49), and the stone arch in front of it (33-35). The interior is five times its size and of another shape -- sixteen square rooms
// four by four and the long hall (80) off them -- so the two can't be the same walls; here the cluster stands on the sixteen rooms' roof,
// the way it stands on its hill.
//
// 2026-10-12, later (the user: "We need to make the outside look more like the original. Perhaps scaling up the roof section, or adding more
// towers to the roof section. It's okay for the inside to be underground"): the rooms are under the hill (PalaceCliff), their roofs the
// rock of its top. The cluster stood on the sixteen rooms' roof 5.5 times its size, two of the north hall 3.5 times on the long hall's.
//
// 2026-10-13 (the user: "let's also make the turrets taller and wider", then "Make the island small enough so that it only just fits the
// inside of the palace, then have the turret's on the roof cover the whole of our inside of the palace"): the island is the palace's L and
// a few cells, and its four halls stand apart as turrets over the whole of the roof:
//   - over the sixteen rooms two by two, each hall filling a quarter of their roof (SlotFill of it), turned to vary; along the long hall's
//     part out of them, the north hall one after another; each Tall times as tall as it is wide -- 6 to 9 times their own width, 30,000 to
//     60,000 high where they stood 26,000 (TurretBody: wider and taller apart, about their middle height);
//   - each body drawn only (its box touches nothing, as the rooms' are), in every cube it can be seen from (`Seen`: the engine draws the
//     decors of the 3 x 3 cubes round the camera's, each copy once a frame), and drawn close to the camera (BrickBuilding.CloseFlag);
//   - the halls' shape seen from above (Cell x Cell columns as high as their tops, up to MaxClimb layers over the roof -- all of them) as
//     collision boxes, so a car round the rooftops goes round the turrets, not through them.
internal static class PalaceOutside
{
    // Otringal's palace: the decors of this cube of OTRINGAL.ILE with these bodies of OTRINGAL.OBL -- its four domed halls (each a turret
    // here); the porch (49) and the arch (33-35) are its way in at the hilltop's foot, which the island no longer has
    public static readonly (int X, int Z) SourceCube = (8, 7);
    public static readonly int[] ClusterBodies = { 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48 };
    // the halls, each the decors at one place (Otringal's cube's frame): the west hall, the north hall (its two small domes, its wing, its
    // turret), the east one, the south-east one
    private static readonly (int X, int Z) WestHall = (12616, 19968), NorthHall = (13692, 10520), EastHall = (16944, 15768), SouthHall = (18944, 19968);
    // (the user, 2026-10-13: "spread them out ... let's also make the turrets taller and wider, we can add more turrets if we need to too";
    // then: "Make the island small enough so that it only just fits the inside of the palace, then have the turret's on the roof cover the
    // whole of our inside of the palace"): over the sixteen rooms two by two, a hall a quarter of their roof; along the long hall one after
    // another; each as wide as its place allows (SlotFill of it) and Tall times that up -- 30,000 to 60,000 over the roof
    public const double SlotFill = 0.80;
    // (the long hall's two smaller: the race's road goes between them -- the user's way round the roof, 2026-10-13)
    public const double HallFill = 0.70;
    public const double Tall = 1.35;
    public const double TallMost = 12.5;        // (times the hall's own height at the most: a body's points are 16-bit about its middle)
    public const int MaxClimb = 100;            // the collision's layers over the roof at the most (all of the tallest turret)
    // the rooftops (2026-10-14): their widths, widest first (cells across, a hall's longer side), as tall as their own shape between
    // RoofTall's times its width and between RoofRiseLeast and RoofRiseMost over the roof, their tops RoofOverRoad over or RoofUnderRoad
    // under the road nearest them in turn; RoofGap cells between them, RoadGap cells clear of the road's edge (unless the road is
    // RoadOverTop over the top), RoofMost of them at the most
    public static readonly double[] RoofWidths = { 34, 30, 26, 22, 19, 16, 13 };
    public static readonly (double Least, double Most) RoofTall = (0.8, 1.35);
    public const double RoofRiseLeast = 6000, RoofRiseMost = 13500, RoofOverRoad = 1500, RoofUnderRoad = 2000;
    public const double RoofGap = 2, RoadGap = 2.5, RoadOverTop = 1800;
    public const int RoofMost = 60;
    private const double IslandCell = 512;      // (the rooftops' sizes are island cells; the site's Cell is the building's, a brick)
    // (the engine draws the decors of the 3 x 3 cubes round the camera's: each piece of a turret is in every cube it can be seen from --
    // as far round it as the far plane -- or from further than a cube off a turret stood with quarters missing, the sky through it: the
    // user, 2026-10-13, "a lot of points on our new roof that isn't full and can be seen through". DECORS.CPP draws the copies once a frame)
    public const double Seen = PalaceCliff.FarClip;

    // Where the turrets go: the sixteen rooms' extent (world units), the roof's top, the building's cell and layer; the long hall's extent
    // (its turrets on its part out of the sixteen rooms) when there is one.
    public sealed record Site(double X0, double Z0, double X1, double Z1, double RoofTop, double Cell, double Layer, (double X0, double Z0, double X1, double Z1)? Hall);

    public sealed record Placed(int Decors, int Bodies, List<(double X0, double Y0, double Z0, double X1, double Y1, double Z1)> Boxes, List<string> Log);

    // Adds the turrets to `island` (their bodies, scaled, appended to `newBodies`, numbered from `firstBody` on; at most `maxPerCube` decors
    // a cube) and returns the collision boxes for the caller to add with its empty body.
    // `road`: the race's road over the roof (world units: each point's place, height and half width in cells) -- the rooftops go round it;
    // without it, the turrets as before them.
    public static Placed Add(IslandFile island, IslandFile source, HqrArchive sourceObl, List<byte[]> newBodies, int firstBody, Site site, int maxPerCube,
        IReadOnlyList<(double X, double Z, double H, double Half)>? road = null)
    {
        var log = new List<string>();
        var boxes = new List<(double, double, double, double, double, double)>();
        var cube = source.CubeAt(SourceCube.X, SourceCube.Z) ?? throw new InvalidDataException($"{PalaceIsland.SourceIle} has no cube {SourceCube}: no palace to copy.");
        var cluster = cube.Decors.Where(d => ClusterBodies.Contains(d.Body & 0xFFFF)).ToList();
        if (cluster.Count == 0) throw new InvalidDataException($"{PalaceIsland.SourceIle}'s cube {SourceCube} has no palace (bodies {ClusterBodies[0]}-{ClusterBodies[^1]}).");
        List<IslandDecor> Hall((int X, int Z) at) => cluster.Where(d => d.X == at.X && d.Z == at.Z).ToList();
        int decors = 0, bodies = 0, left = 0;
        // (each source body scaled once for each size it is placed at: its points kxz times as far across and ky times as far up, about
        // their middle -- `Shift` from its own origin)
        var scaled = new Dictionary<(int Body, double Kxz, double Ky), (int Index, Vector3 Shift)>();
        (int Index, Vector3 Shift) BodyOf(int body, double kxz, double ky)
        {
            if (scaled.TryGetValue((body, kxz, ky), out var known)) return known;
            var clipped = 0;
            var bytes = TurretBody(sourceObl.Read(body), kxz, ky, out var shift, ref clipped);
            if (clipped > 0) throw new InvalidDataException($"{PalaceIsland.SourceObl} body {body} {kxz:0.##} times as wide and {ky:0.##} times as tall is bigger than a body holds.");
            // (drawn close to the camera, as the rooms are: BrickBuilding.CloseFlag)
            BinaryPrimitives.WriteInt32LittleEndian(bytes, BinaryPrimitives.ReadInt32LittleEndian(bytes) | BrickBuilding.CloseFlag);
            newBodies.Add(bytes);
            bodies++;
            return scaled[(body, kxz, ky)] = (firstBody + newBodies.Count - 1, shift);
        }
        var triangles = new List<(Vector3 A, Vector3 B, Vector3 C)>();
        var built = new List<string>();
        // a hall's size across and along (its parts' box, world units, the way `quarter` quarter turns leave it) and its own height
        (double Wide, double Deep, double High) Size((int X, int Z) at, int quarter)
        {
            var parts = Hall(at);
            if (parts.Count == 0) return (0, 0, 0);
            double hx = parts.Max(d => (double)d.XMax) - parts.Min(d => (double)d.XMin), hz = parts.Max(d => (double)d.ZMax) - parts.Min(d => (double)d.ZMin);
            return (quarter % 2 == 0 ? hx : hz, quarter % 2 == 0 ? hz : hx, parts.Max(d => (double)d.YMax) - parts.Min(d => (double)d.YMin));
        }
        // (a hall at a place: turned `quarter` quarter turns about its middle, kxz times as wide and ky times as tall, its middle at (mx, mz),
        // its foot on the roof; each piece drawn only, in every cube its box reaches)
        void TurretAt((int X, int Z) at, int quarter, double mx, double mz, double kxz, double ky, string name)
        {
            var parts = Hall(at);
            if (parts.Count == 0) return;
            double hx0 = parts.Min(d => (double)d.XMin), hx1 = parts.Max(d => (double)d.XMax), hz0 = parts.Min(d => (double)d.ZMin), hz1 = parts.Max(d => (double)d.ZMax);
            double cx = (hx0 + hx1) / 2, cz = (hz0 + hz1) / 2;
            var turn = Matrix4x4.CreateRotationY((float)(quarter * Math.PI / 2));
            Vector3 Turned(double x, double z) => Vector3.Transform(new Vector3((float)(x - cx), 0, (float)(z - cz)), turn);
            var baseY = parts.Min(d => d.YMin);
            foreach (var d in parts)
            {
                var (body, shift) = BodyOf(d.Body & 0xFFFF, kxz, ky);
                var p = Turned(d.X, d.Z);
                var copy = d.Clone();
                copy.Beta = (d.Beta & ~0xFFFF) | ((d.Beta + quarter * 1024) & 4095);
                // (its origin moved to the middle of its points: as far as they are turned with it -- the engine's way, LongRotate)
                var moved = Vector3.Transform(new Vector3(shift.X, 0, shift.Z), Matrix4x4.CreateRotationY((float)((copy.Beta & 4095) * 2 * Math.PI / 4096)));
                double wx = mx + p.X * kxz + moved.X, wz = mz + p.Z * kxz + moved.Z;
                var corners = new[] { Turned(d.XMin, d.ZMin), Turned(d.XMax, d.ZMin), Turned(d.XMin, d.ZMax), Turned(d.XMax, d.ZMax) };
                double minX = mx + corners.Min(c => c.X) * kxz, maxX = mx + corners.Max(c => c.X) * kxz;
                double minZ = mz + corners.Min(c => c.Z) * kxz, maxZ = mz + corners.Max(c => c.Z) * kxz;
                var foot = site.RoofTop + (d.YMin - baseY) * ky;
                var y = Math.Round(site.RoofTop + (d.Y - baseY) * ky + shift.Y);
                var (placed, full) = AddCopies(island, copy, body, wx, (int)y, wz, minX, maxX, (int)Math.Floor(foot), BrickBuilding.NoBoxTop, minZ, maxZ, maxPerCube, Seen);
                if (placed == 0) left++;
                left += full; decors += placed;
                triangles.AddRange(Triangles(newBodies[body - firstBody], wx, y, wz, copy.Beta & 0xFFFF));
            }
            built.Add($"{name} {kxz:0.##} times as wide and {ky:0.##} as tall ({(hx1 - hx0) * kxz / 512:0} x {(hz1 - hz0) * kxz / 512:0} cells, {(parts.Max(d => d.YMax) - baseY) * ky:0} high)");
        }
        // (a hall filling `fill` of a place, Tall times as tall as it is wide -- the layout before the rooftops)
        void Turret((int X, int Z) at, int quarter, (double X0, double Z0, double X1, double Z1) place, string name, double fill = SlotFill)
        {
            var (wide, deep, _) = Size(at, quarter);
            if (wide <= 0) return;
            var kxz = Math.Round(Math.Min((place.X1 - place.X0) / wide, (place.Z1 - place.Z0) / deep) * fill, 2);
            TurretAt(at, quarter, (place.X0 + place.X1) / 2, (place.Z0 + place.Z1) / 2, kxz, Math.Round(Math.Min(TallMost, kxz * Tall), 2), name);
        }

        if (road is { Count: > 0 })
        {
            // ---- the rooftops: many halls, lower, round the race's road (the user, 2026-10-14: "Let's lower the roof tops. Are we able to
            // add in more roof tops?" -- after "lift the track up, so we're hugging tighter to the buildings, so we get more of a sense of
            // driving through the roof tops"; the road can't go over ~30,600, the turrets stood 22,500 to 47,500 over the roof). Halls
            // RoofWidest down to RoofNarrowest cells across, as tall as their own shape makes them (RoofTall times), their tops round the
            // height of the road nearest them, a little over it and a little under it in turn; packed over the sixteen rooms' roof and the
            // long hall's -- in an order of their own, not a grid -- each RoofGap from the next, none where the road is (its half width and
            // RoadGap either side) unless the road passes over its top by RoadOverTop; RoofMost at the most.
            var regions = new List<(double X0, double Z0, double X1, double Z1)> { (site.X0, site.Z0, site.X1, site.Z1) };
            if (site.Hall is { } longHall) regions.Add(longHall);
            var halls = new[] { (At: WestHall, Name: "the west hall"), (At: EastHall, Name: "the east hall"), (At: SouthHall, Name: "the south-east hall"), (At: NorthHall, Name: "the north hall") };
            var placedRects = new List<(double X0, double Z0, double X1, double Z1)>();
            var roofs = 0;
            double RoadNear(double x, double z)
            {
                var best = double.MaxValue; var h = site.RoofTop + RoofRiseMost;
                foreach (var (rx, rz, rh, _) in road) { var d = (rx - x) * (rx - x) + (rz - z) * (rz - z); if (d < best) { best = d; h = rh; } }
                return h;
            }
            bool RoadClear(double x0, double z0, double x1, double z1, double top)
            {
                foreach (var (rx, rz, rh, half) in road)
                {
                    var m = (half + RoadGap) * IslandCell;
                    if (rx > x0 - m && rx < x1 + m && rz > z0 - m && rz < z1 + m && rh < top + RoadOverTop) return false;
                }
                return true;
            }
            var step = 2 * IslandCell;
            foreach (var widest in RoofWidths)
            {
                var spots = new List<(double X, double Z, int Region, uint Order)>();
                for (var ri = 0; ri < regions.Count; ri++)
                {
                    var (qx0, qz0, qx1, qz1) = regions[ri];
                    for (var z = qz0; z <= qz1; z += step)
                        for (var x = qx0; x <= qx1; x += step)
                        {
                            var order = (uint)((int)(x / step) * 73856093 ^ (int)(z / step) * 19349663 ^ (int)widest * 83492791);
                            order ^= order >> 13; order *= 0x5bd1e995; order ^= order >> 15;
                            spots.Add((x, z, ri, order));
                        }
                }
                foreach (var (x, z, ri, _) in spots.OrderBy(s => s.Order))
                {
                    if (roofs >= RoofMost) break;
                    var (hallAt, hallName) = halls[roofs % halls.Length];
                    var quarter = (roofs / halls.Length + roofs) % 4;
                    var (wide, deep, high) = Size(hallAt, quarter);
                    if (wide <= 0) continue;
                    var kxz = Math.Round(widest * IslandCell / Math.Max(wide, deep), 2);
                    double hw = wide * kxz / 2, hd = deep * kxz / 2;
                    double x0 = x - hw, x1 = x + hw, z0 = z - hd, z1 = z + hd;
                    var (rx0, rz0, rx1, rz1) = regions[ri];
                    if (x0 < rx0 || x1 > rx1 || z0 < rz0 || z1 > rz1) continue;
                    var g = RoofGap * IslandCell;
                    if (placedRects.Any(p => x0 < p.X1 + g && x1 > p.X0 - g && z0 < p.Z1 + g && z1 > p.Z0 - g)) continue;
                    // its top: the road's height nearest it, over or under it in turn, its shape's own height or a little more or less
                    var want = RoadNear(x, z) + (roofs % 2 == 0 ? RoofOverRoad : -RoofUnderRoad) - site.RoofTop;
                    var ky = Math.Round(Math.Clamp(want / high, kxz * RoofTall.Least, kxz * RoofTall.Most), 2);
                    var rise = high * ky;
                    if (rise < RoofRiseLeast) ky = Math.Round(RoofRiseLeast / high, 2);
                    if (rise > RoofRiseMost) ky = Math.Round(RoofRiseMost / high, 2);
                    if (!RoadClear(x0, z0, x1, z1, site.RoofTop + high * ky)) continue;
                    TurretAt(hallAt, quarter, x, z, kxz, ky, hallName);
                    placedRects.Add((x0, z0, x1, z1));
                    roofs++;
                }
            }
            log.Add($"the outside: the rooftops -- {roofs} of Otringal's palace's halls round the race's road over the palace's roof, " +
                    $"{RoofWidths[^1]:0} to {RoofWidths[0]:0} cells across: {string.Join("; ", built)}");
        }
        else
        {
            // ---- over the sixteen rooms: two by two, a hall a quarter of their roof
            double midX = (site.X0 + site.X1) / 2, midZ = (site.Z0 + site.Z1) / 2;
            Turret(WestHall, 0, (site.X0, site.Z0, midX, midZ), "the west hall");
            Turret(EastHall, 1, (midX, site.Z0, site.X1, midZ), "the east hall");
            Turret(SouthHall, 0, (site.X0, midZ, midX, site.Z1), "the south-east hall");
            Turret(EastHall, 3, (midX, midZ, site.X1, site.Z1), "the east hall");
            // ---- along the long hall's part out of them: the north hall, one after another
            if (site.Hall is { } hall)
            {
                var alongZ = hall.Z1 - hall.Z0 >= hall.X1 - hall.X0;
                double a0 = alongZ ? hall.Z0 : hall.X0, a1 = alongZ ? Math.Min(hall.Z1, site.Z0) : Math.Min(hall.X1, site.X0);
                double across = alongZ ? hall.X1 - hall.X0 : hall.Z1 - hall.Z0;
                var count = Math.Max(1, (int)Math.Round((a1 - a0) / (across * 1.4)));
                for (var t = 0; t < count; t++)
                {
                    double s0 = a0 + (a1 - a0) * t / count, s1 = a0 + (a1 - a0) * (t + 1) / count;
                    Turret(NorthHall, alongZ ? 2 * (t % 2) : 1 + 2 * (t % 2), alongZ ? (hall.X0, s0, hall.X1, s1) : (s0, hall.Z0, s1, hall.Z1), "the north hall", HallFill);
                }
            }
            log.Add($"the outside: Otringal's palace's halls as turrets over the whole of the palace -- {string.Join("; ", built)}");
        }

        // ---- the halls' collision: their shape seen from above, columns of cells as high as their tops (MaxClimb layers at the most), merged
        var tops = new Dictionary<(int X, int Z), double>();
        const int Sub = 3;      // (samples a cell each way: thin walls and towers still count)
        var gx0 = Math.Min(site.X0, site.Hall?.X0 ?? site.X0); var gz0 = Math.Min(site.Z0, site.Hall?.Z0 ?? site.Z0);
        foreach (var (a, b, c) in triangles)
        {
            double minX = Math.Min(a.X, Math.Min(b.X, c.X)), maxX = Math.Max(a.X, Math.Max(b.X, c.X));
            double minZ = Math.Min(a.Z, Math.Min(b.Z, c.Z)), maxZ = Math.Max(a.Z, Math.Max(b.Z, c.Z));
            double det = (b.Z - c.Z) * (a.X - c.X) + (c.X - b.X) * (a.Z - c.Z);
            if (Math.Abs(det) < 1e-3) continue;     // (an upright face: the faces over it give the top)
            var step = site.Cell / Sub;
            int i0 = (int)Math.Floor((minX - gx0) / step), i1 = (int)Math.Ceiling((maxX - gx0) / step);
            int j0 = (int)Math.Floor((minZ - gz0) / step), j1 = (int)Math.Ceiling((maxZ - gz0) / step);
            for (var j = j0; j <= j1; j++)
                for (var i = i0; i <= i1; i++)
                {
                    double px = gx0 + (i + 0.5) * step, pz = gz0 + (j + 0.5) * step;
                    var w0 = ((b.Z - c.Z) * (px - c.X) + (c.X - b.X) * (pz - c.Z)) / det;
                    var w1 = ((c.Z - a.Z) * (px - c.X) + (a.X - c.X) * (pz - c.Z)) / det;
                    var w2 = 1 - w0 - w1;
                    if (w0 < -1e-6 || w1 < -1e-6 || w2 < -1e-6) continue;
                    var y = w0 * a.Y + w1 * b.Y + w2 * c.Y;
                    var key = ((int)Math.Floor((double)i / Sub), (int)Math.Floor((double)j / Sub));
                    if (!tops.TryGetValue(key, out var h) || y > h) tops[key] = y;
                }
        }
        var solidCells = new HashSet<(int X, int Y, int Z)>();
        foreach (var ((gx, gz), h) in tops)
        {
            var layers = Math.Min(MaxClimb, (int)Math.Ceiling((h - site.RoofTop) / site.Layer - 0.25));
            for (var l = 0; l < layers; l++) solidCells.Add((gx, l, gz));
        }
        var hallBoxes = BrickBuilding.Boxes(solidCells);
        foreach (var bx in hallBoxes)
            boxes.Add((gx0 + bx.X0 * site.Cell, site.RoofTop + bx.Y0 * site.Layer, gz0 + bx.Z0 * site.Cell,
                       gx0 + (bx.X1 + 1) * site.Cell, site.RoofTop + (bx.Y1 + 1) * site.Layer, gz0 + (bx.Z1 + 1) * site.Cell));
        log.Add($"  their shape from above as {hallBoxes.Count} collision boxes ({tops.Count} columns of the roof's cells, up to {MaxClimb} layers); " +
                $"{decors} objects in all, counting the copies in each cube a body reaches");
        if (left > 0) log.Add($"  WARNING: {left} of its objects left out (a cube full)");
        return new Placed(decors, bodies, boxes, log);
    }

    // The race's road over the palace's roof, for the rooftops to go round: the palace's track as built into the program (its points, their
    // heights and half widths), out of the rooms (the plan's bare stretches) and over `over` -- world units, the half width in cells.
    public static List<(double X, double Z, double H, double Half)> RaceRoad(double over)
    {
        var road = new List<(double, double, double, double)>();
        var plan = RaceTrackPlan.Built(RaceTrackIsland.Palace);
        if (plan.Heights is not { } heights) return road;
        var bare = new bool[plan.Points.Length];
        foreach (var b in plan.Bare ?? Array.Empty<int[]>())
            if (b.Length >= 2) for (var i = Math.Max(0, b[0]); i <= Math.Min(bare.Length - 1, b[1]); i++) bare[i] = true;
        for (var i = 0; i < plan.Points.Length && i < heights.Length; i++)
        {
            if (bare[i] || heights[i] <= over) continue;
            var half = plan.RaisedHalfs is { } halfs && i < halfs.Length ? halfs[i] : plan.RaisedHalf ?? 4.5;
            road.Add(((plan.Points[i][0] + plan.OriginCellX) * 512, (plan.Points[i][1] + plan.OriginCellZ) * 512, heights[i], half));
        }
        return road;
    }

    // A body kxz times as wide and ky times as tall, its points moved by `shift` -- to be about their own middle: a turret 60,000 high and
    // as wide is still 16-bit either way of it (its decor's place moves that much the other way). Its box and its spheres too.
    private static byte[] TurretBody(byte[] body, double kxz, double ky, out Vector3 shift, ref int clipped)
    {
        var b = (byte[])body.Clone();
        int I(int at) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));
        int points = I(40), p0 = I(44);
        shift = Vector3.Zero;
        if (p0 < 0 || p0 + points * 8 > b.Length) return body;
        var lo = new double[] { double.MaxValue, double.MaxValue, double.MaxValue }; var hi = new double[] { double.MinValue, double.MinValue, double.MinValue };
        var k3 = new[] { kxz, ky, kxz };
        for (var i = 0; i < points; i++)
            for (var a = 0; a < 3; a++)
            {
                var v = BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan(p0 + i * 8 + a * 2)) * k3[a];
                lo[a] = Math.Min(lo[a], v); hi[a] = Math.Max(hi[a], v);
            }
        var mid = new[] { Math.Round((lo[0] + hi[0]) / 2), Math.Round((lo[1] + hi[1]) / 2), Math.Round((lo[2] + hi[2]) / 2) };
        shift = new Vector3((float)mid[0], (float)mid[1], (float)mid[2]);
        var lost = 0;
        void Put(int at, double v)
        {
            v = Math.Round(v);
            if (v > short.MaxValue || v < short.MinValue) lost++;
            BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(at), (short)Math.Clamp(v, short.MinValue, short.MaxValue));
        }
        for (var i = 0; i < points; i++)
        {
            var at = p0 + i * 8;
            Put(at, BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan(at)) * kxz - mid[0]);
            Put(at + 2, BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan(at + 2)) * ky - mid[1]);
            Put(at + 4, BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan(at + 4)) * kxz - mid[2]);
        }
        // (the header's box: x, y, z, each its least and most)
        var scale = new[] { kxz, kxz, ky, ky, kxz, kxz };
        for (var k = 0; k < 6; k++) BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(8 + k * 4), (int)Math.Round(I(8 + k * 4) * scale[k] - mid[k / 2]));
        int spheres = I(80), q = I(84);
        if (spheres > 0 && q > 0 && q + spheres * 8 <= b.Length)
            for (var i = 0; i < spheres; i++, q += 8)
                BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(q + 6), (ushort)Math.Min(65535, Math.Round(BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(q + 6)) * Math.Max(kxz, ky))));
        clipped += lost;
        return b;
    }

    // A source decor's copy in every cube its box reaches (world units) -- and every cube `reach` round it -- each in that cube's frame, its
    // body `body`, its turn and the rest as the source's, its box its own.
    private static (int Placed, int Full) AddCopies(IslandFile island, IslandDecor source, int body, double wx, int y, double wz,
        double minX, double maxX, int yMin, int yMax, double minZ, double maxZ, int maxPerCube, double reach = 0)
    {
        var size = (double)IslandFile.CubeSize;
        int placed = 0, full = 0;
        for (var cz = (int)Math.Floor((minZ - reach) / size); cz <= (int)Math.Floor((maxZ + reach - 1e-6) / size); cz++)
        for (var cx = (int)Math.Floor((minX - reach) / size); cx <= (int)Math.Floor((maxX + reach - 1e-6) / size); cx++)
        {
            if (island.CubeAt(cx, cz) is not { } cube) continue;
            if (cube.Decors.Count >= maxPerCube) { full++; continue; }
            double ox = cx * size, oz = cz * size;
            var copy = source.Clone();
            copy.Body = (source.Body & ~0xFFFF) | body;
            copy.X = (int)Math.Round(wx - ox); copy.Y = y; copy.Z = (int)Math.Round(wz - oz);
            copy.XMin = (int)Math.Floor(minX - ox); copy.XMax = (int)Math.Ceiling(maxX - ox);
            copy.ZMin = (int)Math.Floor(minZ - oz); copy.ZMax = (int)Math.Ceiling(maxZ - oz);
            copy.YMin = yMin; copy.YMax = yMax;
            cube.Decors.Add(copy);
            placed++;
        }
        return (placed, full);
    }

    // A placed body's polygons as triangles in the world: its points turned by the decor's angle (the engine's way) about its place.
    private static IEnumerable<(Vector3, Vector3, Vector3)> Triangles(byte[] data, double x, double y, double z, int beta)
    {
        var body = Body.Read(data, 2, allowStatic: true);
        var turn = Matrix4x4.CreateRotationY((float)(beta * 2 * Math.PI / 4096)) * Matrix4x4.CreateTranslation((float)x, (float)y, (float)z);
        var world = body.World().Select(v => Vector3.Transform(v, turn)).ToArray();
        foreach (var f in body.Faces)
            for (var k = 1; k + 1 < f.Points.Length; k++)
                yield return (world[f.Points[0]], world[f.Points[k]], world[f.Points[k + 1]]);
    }
}

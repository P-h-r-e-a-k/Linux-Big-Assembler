using System.Buffers.Binary;
using System.IO;
using System.Numerics;
using System.Text.Json;
using LbaBodyStudio;
using LBAAssembler.Terrain.Palace;

namespace LBAAssembler.Terrain.Temple;

// The Temple of Bú's flag marshal (the user, 2026-10-17: "Let's make the temple track a 5 lap race, and let's use the rabbibunny wizard
// assistant from the school of magic to drop a flag to start the race, and wave a chequered flag to end the race"): the School of Magic's
// assistant -- the rabbibunny in the stage magician's assistant's black and red who greets Twinsen in the school's hall, "Hello!! Allow me
// to introduce the Grand Rector of the School of Magic!" (Desert Island's text 69, said by scene 27's actor 9: entity 77, BODY.HQR 124, "a
// female rabbibunny in the School of Magic"; the Grand Rector himself, the Dean, is entity 76, BODY.HQR 123) -- a flag in each hand, drawn
// and moved by the race-track mode (RACEMOD.CPP marshal=, written for the Temple alone):
//   - the start flag, a solid green, in the hand on her +x side: held up at her side; raised high as the count-down starts, its cloth
//     fluttering; swept down in front of her, towards the grid, at GO;
//   - the chequered flag, black and white, in her other hand: held up at her other side; waved over her head from the first car's finish
//     until Twinsen's race is over.
// Where she stands (Place; the user, 2026-10-18: "The magicians assistant who waves the flag isn't visible behind the start finish line,
// let's have her start the race from the grid between the cars, with cars going either side of her. Then have her standing close to the
// track to wave the flag at the end of the race"): for the start, on the road in its middle, ahead of the front row between the grid's two
// columns (the Temple's plan stands them apart: RaceTrackPlan.GridFirst, GridSide) -- the cars set off past her on either side, and once
// they have, she hops off the road (marshal_start=); for the race and its finish, on a stone plinth right beside the road at the line,
// where the cars coming up to it see her (marshal=, marshal_plinth=).
// The body (Make): the game's own, kept byte for byte (PalaceTorturers.Parts, as the dungeon's torturers are grafted), with bones added after
// its own: for each flag a pole -- the hand's child, its cloth's first strip with it -- and two more strips of cloth, each turning on the
// edge of the one before it (round the pole's way), so an animation sets the flag's cloth where it likes round its pole and ripples it.
// Then Scale times as big. The cloth is two-sided and unlit (a flag's colours as they are, whichever side shows); the poles wood, their
// tips brass -- lit as the game's own bodies are. Her plinth is a body of its own (MakePlinth: stone), standing where she waves the
// chequered flag all race long: she isn't on it at the start.
// The animations (Anims): ready (both flags held up, one at each side, their cloths stirring); the raise (the green flag up high, then
// held there, fluttering: looped); the drop (swept down in a quarter of a second, held, back to ready); the wave (the chequered flag over her
// head from side to side: looped). Each pose given as where her arms and the poles point and which way the cloth flies, each bone turned
// from its parent's frame as the engine turns it (PalaceTorturers.Aim's way: Lba1Pose, M(Alpha) M(Gamma) M(Beta)).
// Her body, her plinth's and her animations go in at the end of a build (Install, after every track's: RaceTrackService.Build), as the
// entity's bodies Body and Plinth and animations Ready, Raise, Drop and Wave (RESS.HQR's entity table) -- the race-track mode finds them
// there, so nothing else moves.
internal static class TempleMarshal
{
    public const int Entity = 77, Retail = 124;
    // her body, her plinth's and her animations, the entity's own numbers for them (its own: body 0; animations 0, 5, 6, 22, 28 and 288)
    public const int Body = 40, Plinth = 41, Ready = 660, Raise = 661, Drop = 662, Wave = 663;
    public const double Scale = 1.6;

    // the game's body: her arms (upper arm, forearm, hand -- the green flag's on her +x side, the chequered flag's on her -x side), the
    // torso; the bones added after its own: the green flag's pole (with its cloth's first strip) and its two more strips, the chequered
    // flag's
    private const int GreenUpper = 10, GreenFore = 11, GreenHand = 12, CheckUpper = 13, CheckFore = 14, CheckHand = 15, Torso = 4;
    private const int Own = 22, GreenPole = Own, CheckPole = Own + 3;
    public const int GreenPoleBone = GreenPole, CheckPoleBone = CheckPole;
    // (model units, before Scale) the poles: how far behind the fist and ahead of it they reach, half their thickness, the brass tip's length;
    // the cloth: its hoist (along the pole, from the tip back) and its fly (out from the pole), in columns (two a strip) and the chequered
    // one's rows
    private const float PoleBack = 90, PoleAhead = 1000, PoleHalf = 13, TipLong = 45;
    private const float Hoist = 460, Fly = 640;
    private const int Columns = 6, CheckRows = 4;
    // the plinth: its block (half across, its bottom and top under her feet) and the slab over it
    private const float BlockHalf = 235, BlockBottom = -220, SlabHalf = 270, SlabBottom = -45;
    // the colours (ramp starts of the island's palette, and how much of the light each takes): the poles' wood, their tips' brass, the plinth's
    // stone and its slab; the cloths' (unlit): the start flag's green, the chequered flag's black and white
    private const int Wood = 96, Brass = 100, Stone = 224, Slab = 48, Green = 139, Black = 48, White = 62;
    private const float WoodLight = 0.7f, BrassLight = 0.55f, StoneLight = 0.8f, SlabLight = 0.6f;
    // the pole as it stands, in its hand's frame: forward (+z) through the fist (its hand's point nearest the hand's middle); the cloth's
    // fly from it, unturned: down (-y)
    private static readonly Vector3 PoleWay = Vector3.UnitZ, FlyWay = -Vector3.UnitY;

    // ---- into the game folder -----------------------------------------------------------------------------------------------------

    // Into the game folder: her body and her plinth's (BODY.HQR's end) and her animations (ANIM.HQR's), the entity's bodies Body and Plinth
    // and animations Ready, Raise, Drop, Wave (RESS.HQR's entity table). Lines for the build's log.
    public static List<string> Install(string gameDirectory)
    {
        var bodyPath = Path.Combine(gameDirectory, "BODY.HQR");
        var animPath = Path.Combine(gameDirectory, "ANIM.HQR");
        var ressPath = Path.Combine(gameDirectory, "RESS.HQR");
        var table = HqrArchive.Open(ressPath).Read(44);
        var entity = Lba2EntityTable.Parse(table).Entities.FirstOrDefault(e => e.Id == Entity);
        if (entity is null || RaceTrackGuards.BodyOf(table, Entity, 0) != Retail || !entity.Anims.Any(a => a.Generic == 0))
            return new() { $"WARNING: the Temple of Bú's flag marshal: entity {Entity} isn't the School of Magic's assistant (BODY.HQR {Retail}) -- none made" };
        if (entity.Bodies.Any(b => b.Generic is Body or Plinth) || entity.Anims.Any(a => a.Generic is Ready or Raise or Drop or Wave))
            return new() { $"WARNING: the Temple of Bú's flag marshal: entity {Entity} already has body {Body} or {Plinth} or one of animations {Ready}-{Wave} -- none made" };
        var retail = HqrArchive.Open(bodyPath).Read(Retail);
        var bytes = Make(retail, out var standing);
        var plinth = MakePlinth(retail);
        var anims = Anims(standing, HqrArchive.Open(animPath).Read(entity.Anims.First(a => a.Generic == 0).Anim));
        var bodyIndex = HqrArchive.CountEntries(bodyPath);
        File.WriteAllBytes(bodyPath, HqrWriter.AppendEntry(HqrWriter.AppendEntry(File.ReadAllBytes(bodyPath), HqrWriter.StoredEntry(bytes)), HqrWriter.StoredEntry(plinth)));
        var animIndex = HqrArchive.CountEntries(animPath);
        var file = File.ReadAllBytes(animPath);
        foreach (var anim in anims) file = HqrWriter.AppendEntry(file, HqrWriter.StoredEntry(anim.Write()));
        File.WriteAllBytes(animPath, file);
        table = RaceTrackBaldinoCar.WithBody(table, Entity, Body, bodyIndex);
        table = RaceTrackBaldinoCar.WithBody(table, Entity, Plinth, bodyIndex + 1);
        var generics = new[] { Ready, Raise, Drop, Wave };
        for (var k = 0; k < generics.Length; k++) table = RaceTrackJumpAnim.WithAnim(table, Entity, generics[k], animIndex + k);
        File.WriteAllBytes(ressPath, HqrWriter.ReplaceEntry(File.ReadAllBytes(ressPath), 44, HqrWriter.StoredEntry(table)));
        return new()
        {
            $"the Temple of Bú's flag marshal: the School of Magic's assistant (BODY.HQR {Retail}), the start flag and the chequered flag in her hands, {Scale} " +
            $"times as big -- BODY.HQR {bodyIndex} ({standing.Vertices.Count} points, {standing.Bones.Count} bones), entity {Entity}'s body {Body}; her plinth BODY.HQR " +
            $"{bodyIndex + 1}, its body {Plinth}; ready, the raise, the drop and the wave ANIM.HQR {animIndex}-{animIndex + 3}, its animations {Ready}-{Wave}",
        };
    }

    // ---- where she stands -------------------------------------------------------------------------------------------------------

    // Where the cars see her (the camera that follows the car rides the road's middle behind it, 9 cells back and 2,100 up in the Temple:
    // RACEMOD.CPP RailCamera; its view about 27 degrees either side of where it looks, 21 over and under it at 640x480):
    //   - at the start, in the road's middle StartAhead cells ahead of pole's middle -- pole's nose (the pumpkin F1's, the longest of the
    //     Temple's cars, 1.74 cells ahead of its middle) a cell and a quarter behind her -- and at least StartLineGap cells short of the line:
    //     in the middle of every grid camera's view, over the road between the columns, in front of the start gantry (past the line, its beam
    //     hid a raised flag from the camera), facing the cars. The columns leave her room: GapLeast cells at least between her (HerHalf
    //     cells round her middle) and the car box (CarHalf) either side, else she stays on her plinth for the start too;
    //   - for the race and its finish, on her plinth right beside the road -- its edge RoadGap cells off the road's edge -- Before cells
    //     short of the start line (the start gantry's post, at the line on the road's edge, behind her as the cars come up), on the side away
    //     from the pit spots (the Temple's slip lane), else the nearer: in the view of the camera behind a car coming up the straight to the
    //     line until it is a few cells short of it. (Placed past the line, 7 cells out beside the slip lane, she was seen only at the edge of
    //     the view, the user said not at all, 2026-10-18.) No nearer than PitGap to a pit spot, and on a floor clear for her and her flags,
    //     up to FarthestBack cells short of the line. Facing the cars coming, turned TurnToRoad to the road.
    private const double StartAhead = 3.0, StartLineGap = 1.5, HerHalf = 0.65, CarHalf = 633 / 512.0, GapLeast = 0.5;
    private const double RoadGap = 0.35, Before = 1.5, FarthestBack = 8, PitGap = 3, PlinthHalf = SlabHalf * Scale / 512.0, TurnToRoad = 25 * Math.PI / 180;

    // Where the flag marshal stands (above): on her plinth beside the road by the start line, on the crypt's floor, and -- with the grid's
    // columns `gridSide` cells either side of the road's middle and pole `gridFirst` behind the line (RaceTrackOptions) -- in the road's
    // middle ahead of the front row for the start. The race file's marshal= numbers (Line), or null (no start line, no floor clear there: a
    // note in the report).
    public static int[]? Place(string gameDirectory, RaceTrackReport report, double gridFirst, double gridSide)
    {
        if (report.StartLine.Count == 0) return null;
        var line = report.StartLine[0];
        double dl = Math.Sqrt(line.DirX * line.DirX + line.DirZ * line.DirZ);
        if (dl < 1e-9) return null;
        double dx = line.DirX / dl, dz = line.DirZ / dl;
        var floors = FloorsOf(gameDirectory);
        // (the roads: the raised road's pieces, world units -- a gap of more than 3 cells between two points is a branch's start, not a piece)
        var pieces = new List<(Vector3 A, Vector3 B, double HalfA, double HalfB)>();
        for (var i = 0; i + 1 < report.Raised.Count; i++)
        {
            int[] a = report.Raised[i], b = report.Raised[i + 1];
            if (a.Length < 4 || b.Length < 4) continue;
            var pa = new Vector3(a[0] / 512f, a[2], a[1] / 512f); var pb = new Vector3(b[0] / 512f, b[2], b[1] / 512f);
            if (Math.Abs(pa.X - pb.X) + Math.Abs(pa.Z - pb.Z) > 3) continue;
            pieces.Add((pa, pb, a[3] / 512.0, b[3] / 512.0));
        }
        // (how far a place is from the nearest road's edge -- negative on it -- of the roads at about its height; a lap on the ground: from its
        // middle line, past its widest half)
        double Clear(double x, double z, double y)
        {
            var best = pieces.Count == 0 && report.DistanceToRoad(x, z) is var ground and < 1e8 ? ground - 4.5 : 1e9;
            foreach (var (a, b, ha, hb) in pieces)
            {
                if (Math.Abs((a.Y + b.Y) / 2 - y) > 2500) continue;
                double sx = b.X - a.X, sz = b.Z - a.Z, len2 = sx * sx + sz * sz;
                var t = len2 < 1e-9 ? 0 : Math.Clamp(((x - a.X) * sx + (z - a.Z) * sz) / len2, 0, 1);
                double ex = x - a.X - sx * t, ez = z - a.Z - sz * t;
                best = Math.Min(best, Math.Sqrt(ex * ex + ez * ez) - (ha + (hb - ha) * t));
            }
            return best;
        }
        // (a spot `along` cells past the line, `o` out to a side of it -- side -1 the lap's left, 1 its right: the plinth's corners and middle
        // each on the floor, clear of the roads and the pit spots; its floor's height)
        double? Spot(double along, int side, double o)
        {
            double x = line.X + dx * along - dz * side * o, z = line.Z + dz * along + dx * side * o;
            double? floor = null;
            foreach (var (cx, cz) in new[] { (0.0, 0.0), (1.0, 1.0), (1.0, -1.0), (-1.0, 1.0), (-1.0, -1.0) })
            {
                double px = x + cx * PlinthHalf, pz = z + cz * PlinthHalf;
                var f = floors(px, pz, line.Y);
                if (f is null || floor is { } other && Math.Abs(other - f.Value) > 1) return null;
                floor ??= f;
                if (Clear(px, pz, line.Y) < RoadGap) return null;
            }
            return report.Pits.Any(p => Math.Sqrt((p.X - x) * (p.X - x) + (p.Z - z) * (p.Z - z)) < PitGap + PlinthHalf) ? null : floor;
        }
        // ---- on her plinth beside the road: the road's half width at the line (from its edge, the line's middle is that far in); the side
        // away from the pit spots first (the lap's left -1, its right 1), else the right; the nearest the line, then the nearest the road
        var half = Math.Max(0, -Clear(line.X, line.Z, line.Y));
        var pits = report.Pits.Select(p => (p.X - line.X) * -dz + (p.Z - line.Z) * dx).ToList();
        var first = pits.Count > 0 && pits.Average() > 0 ? -1 : 1;
        (double Along, int Side, double Out, double Floor)? found = null;
        foreach (var side in new[] { first, -first })
        {
            for (var back = Before; back <= FarthestBack && found is null; back += 0.25)
                for (var o = half + RoadGap + PlinthHalf; o <= half + RoadGap + PlinthHalf + 3 && found is null; o += 0.25)
                    if (Spot(-back, side, o) is { } floor) found = (-back, side, o, floor);
            if (found is not null) break;
        }
        if (found is not { } spot)
        {
            report.Notes.Add("the flag marshal: no floor clear of the roads beside the start line -- left out");
            return null;
        }
        double mx = line.X + dx * spot.Along - dz * spot.Side * spot.Out, mz = line.Z + dz * spot.Along + dx * spot.Side * spot.Out;
        // (facing the cars coming -- back along the lap -- turned to the road: on its left, round to her left)
        var beta = (int)Math.Round((Math.Atan2(-dx, -dz) - spot.Side * TurnToRoad) * 4096 / (2 * Math.PI)) & 4095;
        report.Notes.Add($"the flag marshal: on her plinth {spot.Out:0.##} cells to the {(spot.Side < 0 ? "left" : "right")} of the lap (its edge {spot.Out - PlinthHalf - half:0.##} " +
                         $"off the road), {-spot.Along:0.##} short of the start line, at cell ({mx:0.##}, {mz:0.##}), the floor {spot.Floor:0}");
        // ---- for the start: in the road's middle ahead of the front row, facing the cars, on the road's deck -- when the grid's columns
        // leave her room between them
        (double X, double Z, double Y, int Beta)? start = null;
        var gap = gridSide - CarHalf - HerHalf;
        if (gap < GapLeast)
            report.Notes.Add($"the flag marshal: the grid's columns, {gridSide:0.##} cells either side of the road's middle, leave her {gap:0.##} cells between them " +
                             $"and her (at least {GapLeast}) -- she gives the start from her plinth");
        else
        {
            var startBack = Math.Max(StartLineGap, gridFirst - StartAhead);
            double sx = line.X - dx * startBack, sz = line.Z - dz * startBack;
            var sy = report.RaisedFloor?.Invoke(sx, sz, line.Y) ?? line.Y;
            start = (sx, sz, sy, (int)Math.Round(Math.Atan2(-dx, -dz) * 4096 / (2 * Math.PI)) & 4095);
            report.Notes.Add($"the flag marshal: for the start in the road's middle {startBack:0.##} cells short of the line, {gridFirst - startBack:0.##} ahead of pole's middle, " +
                             $"the grid's columns {gridSide:0.##} either side of her ({gap:0.##} cells between her and a car's box), at cell ({sx:0.##}, {sz:0.##}), the road {sy:0}");
        }
        return Line(mx, mz, spot.Floor, beta, start);
    }

    // The race file's numbers: marshal='s -- the entity, its body and animations (ready, raise, drop, wave), where she stands for the race
    // (island units: x, z, her plinth's top -- `floor` its foot) and the way she faces -- then marshal_plinth='s (her plinth's body) and, when
    // she gives the start from the road, marshal_start='s (x, z, the road's height there, the way she faces).
    public static int[] Line(double x, double z, double floor, int beta, (double X, double Z, double Y, int Beta)? start)
    {
        var line = new List<int> { Entity, Body, Ready, Raise, Drop, Wave, (int)Math.Round(x * 512), (int)Math.Round(z * 512), (int)Math.Round(floor - BlockBottom * Scale), beta & 4095, Plinth };
        if (start is { } s) line.AddRange(new[] { (int)Math.Round(s.X * 512), (int)Math.Round(s.Z * 512), (int)Math.Round(s.Y), s.Beta & 4095 });
        return line.ToArray();
    }

    // The temple's floors (TEMPLE.JSON, the island's layout: TempleIsland): the top of the highest floor at an island cell under a height --
    // with room over it for her on her plinth -- or null (no floor there, or something over it).
    private static Func<double, double, double, double?> FloorsOf(string gameDirectory)
    {
        var path = Path.Combine(gameDirectory, TempleIsland.LayoutFile);
        if (!File.Exists(path)) return (_, _, _) => null;
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        int scale = root.GetProperty("scale").GetInt32(), ox = root.GetProperty("offsetX").GetInt32(), oz = root.GetProperty("offsetZ").GetInt32();
        int baseY = root.GetProperty("baseY").GetInt32(), minY = root.GetProperty("minY").GetInt32(), layer = root.GetProperty("layer").GetInt32();
        var bits = new Dictionary<(int, int), long>();
        foreach (var c in root.GetProperty("columns").EnumerateArray()) bits[(c[0].GetInt32(), c[1].GetInt32())] = c[2].GetInt64();
        double Top(int y) => baseY + (y - minY + 1) * layer;
        // (her height on her plinth, and a little: no layer over the floor that low)
        var room = (Hoist + PoleAhead + 1700 - BlockBottom) * Scale;
        return (x, z, near) =>
        {
            if (!bits.TryGetValue(((int)Math.Floor((x - ox) / scale), (int)Math.Floor((z - oz) / scale)), out var b)) return null;
            for (var y = minY + 63; y >= minY; y--)
            {
                if (((b >> (y - minY)) & 1) == 0 || Top(y) > near + 200) continue;
                for (var k = y + 1; k < minY + 64 && Top(k - 1) < Top(y) + room; k++)
                    if (((b >> (k - minY)) & 1) != 0) return null;
                return Top(y);
            }
            return null;
        };
    }

    // ---- the body -----------------------------------------------------------------------------------------------------------------

    // A new polygon: its points (wound to face `Out`: Body.VertexNormals' way) and colour, drawn Gouraud lit or flat and unlit (its colour as
    // it is: not counted in its points' normals).
    private sealed record Poly(int[] Points, int Colour, bool Lit = true);

    // The marshal's body from the game's (`retail`: BODY.HQR Retail), its bytes, Scale times as big; `standing`: the body as it stands, for
    // the animations (not scaled).
    public static byte[] Make(byte[] retail, out Body standing)
    {
        var p = PalaceTorturers.Parts.Read(retail);
        if (p.Bones.Count != Own || p.Bones[GreenHand].Count < 1 || p.Bones[CheckHand].Count < 1 || p.Bones[0].Count < 1)
            throw new InvalidDataException($"BODY.HQR {Retail} isn't the School of Magic's assistant.");
        var world = p.World().ToList();
        var oldPoints = p.Points.Count;
        var polys = new List<Poly>();
        var light = new Dictionary<int, float>();
        var added = new List<Vector3>();
        var newBones = new List<(int Parent, int Pivot, int First, int Count)>();
        int Add(Vector3 at, float share) { added.Add(at); light[oldPoints + added.Count - 1] = share; return oldPoints + added.Count - 1; }
        Vector3 At(int i) => i < oldPoints ? world[i] : added[i - oldPoints];
        void Face(int[] points, int colour, Vector3 outward, bool lit = true)
        {
            var n = PalaceTorturers.Newell(points.Select(At).ToArray());
            polys.Add(new Poly(Vector3.Dot(n, outward) >= 0 ? points : points.Reverse().ToArray(), colour, lit));
        }
        int[] Ring(Vector3 centre, Vector3 u, Vector3 v, float half, float share) =>
            new[] { (1, 1), (-1, 1), (-1, -1), (1, -1) }.Select(k => Add(centre + (u * k.Item1 + v * k.Item2) * half, share)).ToArray();

        // each flag: its pole through the fist (the hand's point nearest its middle, the pole's way through it: forward), the brass tip, and
        // the cloth -- its first strip with the pole, each strip after it a bone of its own turning on the last column of the strip before
        void Flag(int hand, int poleBone, int rows, Func<int, int, int> colour)
        {
            int start = p.StartOf(hand), count = p.Bones[hand].Count;
            var middle = Enumerable.Range(start, count).Select(i => world[i]).Aggregate(Vector3.Zero, (a, b) => a + b) / count;
            var grip = Enumerable.Range(start, count).MinBy(i => (world[i] - middle).LengthSquared());
            var axis = world[grip];
            var across = Vector3.Cross(PoleWay, FlyWay);
            var firstPole = oldPoints + added.Count;
            var back = Ring(axis - PoleWay * PoleBack, FlyWay, across, PoleHalf, WoodLight);
            var tip = Ring(axis + PoleWay * PoleAhead, FlyWay, across, PoleHalf, WoodLight);
            var apex = Add(axis + PoleWay * (PoleAhead + TipLong), BrassLight);
            for (var s = 0; s < 4; s++)
            {
                var q = new[] { back[s], back[(s + 1) % 4], tip[(s + 1) % 4], tip[s] };
                var mid = q.Select(At).Aggregate(Vector3.Zero, (a, b) => a + b) / 4;
                Face(q, Wood, mid - (axis + PoleWay * Vector3.Dot(mid - axis, PoleWay)));
            }
            for (var s = 0; s < 4; s++)
            {
                var t = new[] { tip[s], tip[(s + 1) % 4], apex };
                var mid = t.Select(At).Aggregate(Vector3.Zero, (a, b) => a + b) / 3;
                Face(t, Brass, mid - (axis + PoleWay * (PoleAhead - 40)));
            }
            Face(new[] { back[0], back[1], back[2], back[3] }, Wood, -PoleWay);
            // the cloth: columns 0..Columns out from the pole, rows 0..rows along the hoist from the tip back; two columns a strip
            var cloth = new int[Columns + 1][];
            var strip = 0;
            var stripFirst = firstPole;
            var pivot = grip;
            var parent = hand;
            for (var c = 0; c <= Columns; c++)
            {
                cloth[c] = new int[rows + 1];
                for (var r = 0; r <= rows; r++)
                    cloth[c][r] = Add(axis + PoleWay * (PoleAhead - Hoist * r / rows) + FlyWay * (Fly * c / Columns), 0);
                // (a strip's last column: the next strip turns on its first row's point)
                if (c % 2 == 0 && c > 0 && c < Columns)
                {
                    newBones.Add((parent, pivot, stripFirst, oldPoints + added.Count - stripFirst));
                    (parent, pivot, stripFirst) = (poleBone + strip, cloth[c][0], oldPoints + added.Count);
                    strip++;
                }
            }
            newBones.Add((parent, pivot, stripFirst, oldPoints + added.Count - stripFirst));
            // (both sides of each square: the cloth seen from either side)
            var normal = across;
            for (var c = 0; c < Columns; c++)
                for (var r = 0; r < rows; r++)
                {
                    var q = new[] { cloth[c][r], cloth[c + 1][r], cloth[c + 1][r + 1], cloth[c][r + 1] };
                    Face(q, colour(c, r), normal, lit: false);
                    Face(q, colour(c, r), -normal, lit: false);
                }
        }
        Flag(GreenHand, GreenPole, 1, (_, _) => Green);
        Flag(CheckHand, CheckPole, CheckRows, (c, r) => (c + r) % 2 == 0 ? Black : White);
        if (newBones.Count + Own > 30) throw new InvalidDataException("The flag marshal has more than 30 bones.");

        // ---- written: the new points after the body's own -- each from its bone's pivot
        world.AddRange(added);
        for (var b = 0; b < newBones.Count; b++)
        {
            var (parent, piv, firstPoint, count) = newBones[b];
            p.Bones.Add((parent, piv, count, 0));
            for (var i = firstPoint; i < firstPoint + count; i++)
            {
                var q = world[i] - world[piv];
                p.Points.Add(((short)Math.Round(q.X), (short)Math.Round(q.Y), (short)Math.Round(q.Z), Own + b));
            }
        }
        // (the flat-lit polygons' lights are found past every point: on by the points added)
        foreach (var (_, records) in p.Blocks)
            foreach (var r in records)
            {
                var normal = BinaryPrimitives.ReadUInt16LittleEndian(r.AsSpan(10));
                if (normal >= oldPoints) BinaryPrimitives.WriteUInt16LittleEndian(r.AsSpan(10), (ushort)(normal + added.Count));
            }
        // (the new points' normals: from the lit polygons round them, as long as their share of the light)
        var sum = new Dictionary<int, Vector3>();
        foreach (var poly in polys.Where(q => q.Lit))
        {
            var n = PalaceTorturers.Newell(poly.Points.Select(i => world[i]).ToArray());
            foreach (var i in poly.Points) sum[i] = sum.GetValueOrDefault(i) + n;
        }
        for (var b = 0; b < newBones.Count; b++)
            for (var i = newBones[b].First; i < newBones[b].First + newBones[b].Count; i++)
            {
                var n = sum.TryGetValue(i, out var v) && v.LengthSquared() > 1e-9f ? Vector3.Normalize(v) * 10240 * light.GetValueOrDefault(i, 1f) : Vector3.Zero;
                p.Normals.Add(((short)Math.Round(n.X), (short)Math.Round(n.Y), (short)Math.Round(n.Z), Own + b));
            }
        // (the new polygons: a block each of lit and unlit triangles and quads)
        foreach (var group in polys.GroupBy(q => (Quad: q.Points.Length == 4, q.Lit)))
        {
            var type = (group.Key.Quad ? 0x8000 : 0) | (group.Key.Lit ? 4 : 0);
            p.Blocks.Add((type, group.Select(q =>
            {
                var r = new byte[12];
                for (var k = 0; k < q.Points.Length; k++) BinaryPrimitives.WriteUInt16LittleEndian(r.AsSpan(k * 2), (ushort)q.Points[k]);
                BinaryPrimitives.WriteUInt16LittleEndian(r.AsSpan(8), (ushort)q.Colour);
                return r;
            }).ToList()));
        }
        var bytes = p.Write();
        standing = LbaBodyStudio.Body.Read(bytes, 2, allowStatic: true);
        // (its box: round every pose of her animations -- the flags reach high over her, and out in front)
        var box = Boxed(standing, world);
        for (var k = 0; k < 6; k++) BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8 + k * 4), box[k]);
        standing = LbaBodyStudio.Body.Read(bytes, 2, allowStatic: true);
        return RaceTrackSmallCars.Scaled(bytes, Scale);
    }

    // Her plinth, a body of its own (one bone, its top at its origin): a block of stone, a slab on it, Scale times as big -- the game's body's
    // (`retail`) header, its own box; lit as hers is.
    public static byte[] MakePlinth(byte[] retail)
    {
        var game = PalaceTorturers.Parts.Read(retail);
        var p = new PalaceTorturers.Parts { Header = (byte[])game.Header.Clone() };
        var points = new List<Vector3>();
        var light = new List<float>();
        var polys = new List<Poly>();
        int Add(Vector3 at, float share) { points.Add(at); light.Add(share); return points.Count - 1; }
        void Face(int[] q, int colour, Vector3 outward)
        {
            var n = PalaceTorturers.Newell(q.Select(i => points[i]).ToArray());
            polys.Add(new Poly(Vector3.Dot(n, outward) >= 0 ? q : q.Reverse().ToArray(), colour));
        }
        int[] Box(float half, float bottom, float top, float share) =>
            new[] { bottom, top }.SelectMany(y => new[] { (1, 1), (-1, 1), (-1, -1), (1, -1) }.Select(k => Add(new Vector3(k.Item1 * half, y, k.Item2 * half), share))).ToArray();
        void Sides(int[] box, int colour)
        {
            for (var s = 0; s < 4; s++)
            {
                var q = new[] { box[s], box[(s + 1) % 4], box[4 + (s + 1) % 4], box[4 + s] };
                var mid = q.Select(i => points[i]).Aggregate(Vector3.Zero, (a, b) => a + b) / 4;
                Face(q, colour, new Vector3(mid.X, 0, mid.Z));
            }
        }
        var block = Box(BlockHalf, BlockBottom, SlabBottom, StoneLight);
        var slab = Box(SlabHalf, SlabBottom, 0, SlabLight);
        Sides(block, Stone);
        Sides(slab, Slab);
        Face(new[] { slab[4], slab[5], slab[6], slab[7] }, Slab, Vector3.UnitY);
        Face(new[] { slab[0], slab[1], slab[2], slab[3] }, Slab, -Vector3.UnitY);
        p.Bones.Add((game.Bones[0].Parent, 0, points.Count, 0));
        foreach (var v in points) p.Points.Add(((short)Math.Round(v.X), (short)Math.Round(v.Y), (short)Math.Round(v.Z), 0));
        // (each point's normal: from the polygons round it, as long as its share of the light)
        var sum = new Vector3[points.Count];
        foreach (var poly in polys)
        {
            var n = PalaceTorturers.Newell(poly.Points.Select(i => points[i]).ToArray());
            foreach (var i in poly.Points) sum[i] += n;
        }
        for (var i = 0; i < points.Count; i++)
        {
            var n = sum[i].LengthSquared() > 1e-9f ? Vector3.Normalize(sum[i]) * 10240 * light[i] : Vector3.Zero;
            p.Normals.Add(((short)Math.Round(n.X), (short)Math.Round(n.Y), (short)Math.Round(n.Z), 0));
        }
        p.Blocks.Add((0x8000 | 4, polys.Select(q =>
        {
            var r = new byte[12];
            for (var k = 0; k < 4; k++) BinaryPrimitives.WriteUInt16LittleEndian(r.AsSpan(k * 2), (ushort)q.Points[k]);
            BinaryPrimitives.WriteUInt16LittleEndian(r.AsSpan(8), (ushort)q.Colour);
            return r;
        }).ToList()));
        var bytes = p.Write();
        var box = new[] { -SlabHalf, SlabHalf, BlockBottom, 0, -SlabHalf, SlabHalf };
        for (var k = 0; k < 6; k++) BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8 + k * 4), (int)Math.Round(box[k]));
        return RaceTrackSmallCars.Scaled(bytes, Scale);
    }

    // (a box round her as she stands and with both flags as high and as far out as they go: her arms' reach and the poles' length round each
    // shoulder)
    private static int[] Boxed(Body body, List<Vector3> world)
    {
        var reach = 380 + PoleAhead + TipLong + Fly;
        float x0 = world.Min(v => v.X), x1 = world.Max(v => v.X), y0 = world.Min(v => v.Y), y1 = world.Max(v => v.Y), z0 = world.Min(v => v.Z), z1 = world.Max(v => v.Z);
        foreach (var shoulder in new[] { GreenUpper, CheckUpper })
        {
            var s = world[body.Bones[shoulder].Pivot];
            x0 = Math.Min(x0, s.X - reach); x1 = Math.Max(x1, s.X + reach); y1 = Math.Max(y1, s.Y + reach); z0 = Math.Min(z0, s.Z - reach); z1 = Math.Max(z1, s.Z + reach);
        }
        return new[] { (int)Math.Floor(x0), (int)Math.Ceiling(x1), (int)Math.Floor(y0), (int)Math.Ceiling(y1), (int)Math.Floor(z0), (int)Math.Ceiling(z1) };
    }

    // ---- the animations -----------------------------------------------------------------------------------------------------------

    // A flag's pose: where the arm's bones point (the upper arm, the forearm -- the body's own frame: +z her front, +y up, +x the green
    // flag's side), the pole's way, the way its cloth flies from it (kept square to the pole), and the cloth's two strips turned round their
    // edges (radians: the ripple).
    internal sealed record Arm(Vector3 Upper, Vector3 Fore, Vector3 Pole, Vector3 Cloth, float RippleA = 0, float RippleB = 0)
    {
        public Arm Rippled(float a, float b) => this with { RippleA = a, RippleB = b };
        public Arm Mirrored() => new(M(Upper), M(Fore), M(Pole), M(Cloth), RippleA, RippleB);
        private static Vector3 M(Vector3 v) => new(-v.X, v.Y, v.Z);
    }
    // A pose: each arm's -- the green flag's, the chequered flag's -- and the torso leant forward (radians, on the game's standing's).
    internal sealed record Pose(Arm Green, Arm Check, float Lean = 0);

    private static Vector3 N(float x, float y, float z) => Vector3.Normalize(new Vector3(x, y, z));
    // ready: each flag held up at her side, the forearm forward, the pole upright, the cloth flying out and a little back
    private static readonly Arm ReadyGreen = new(N(0.3f, -0.94f, 0.15f), N(0.12f, -0.3f, 0.95f), N(0.1f, 0.98f, 0.15f), N(0.85f, -0.25f, -0.45f));
    private static readonly Arm ReadyCheck = ReadyGreen.Mirrored();
    // the raise: the green flag held up high in front of her, the pole leaning out to the cars (upright, it was over the top of the grid
    // camera's picture), its cloth out to her side (broadside to them)
    private static readonly Arm Raised = new(N(0.3f, 0.75f, 0.6f), N(0.15f, 0.8f, 0.58f), N(0.12f, 0.72f, 0.68f), N(0.95f, -0.1f, 0.1f));
    // the drop: swept down in front of her, towards the grid -- she gives the start from the road between its two columns (2026-10-18):
    // swept out to her side, as it was at first, the flag (its pole three cells long) came down over a column and the cars drove through
    // its cloth -- level over the way between them, its cloth out to her side, high over the cars; down, its follow-through, the cloth
    // trailing up over it (not out to the side)
    private static readonly Arm[] Dropping =
    {
        new(N(0.3f, 0.15f, 0.94f), N(0.25f, 0.1f, 0.96f), N(0.2f, 0.05f, 0.98f), N(0.9f, 0.3f, 0)),
        new(N(0.25f, -0.35f, 0.9f), N(0.2f, -0.45f, 0.87f), N(0.18f, -0.42f, 0.89f), N(0.3f, 1, 0)),
        new(N(0.2f, -0.55f, 0.81f), N(0.15f, -0.6f, 0.79f), N(0.12f, -0.5f, 0.86f), N(0.1f, 1, -0.2f)),
    };
    // the wave: the chequered flag up over her, out to her side and in over her head, the cloth trailing across her front (broadside to the
    // cars)
    private static readonly Arm WaveOut = new(N(-0.5f, 0.82f, 0.28f), N(-0.35f, 0.88f, 0.3f), N(-0.75f, 0.55f, 0.35f), N(0.9f, -0.25f, -0.2f));
    private static readonly Arm WaveIn = new(N(-0.45f, 0.85f, 0.28f), N(-0.15f, 0.92f, 0.35f), N(0.55f, 0.68f, 0.45f), N(-0.9f, -0.25f, -0.2f));

    // (ms) the keyframes' times: ready's two (the first into it from wherever she was); the raise's (up, then the two fluttering); the
    // drop's (level, down, the follow-through, held, back to ready); the wave's (out, in, out)
    public static readonly int[] ReadyMs = { 500, 700 }, RaiseMs = { 450, 240, 240 }, DropMs = { 90, 130, 170, 450, 600 }, WaveMs = { 350, 300, 300 };

    // The animations, in the order of Ready, Raise, Drop, Wave: for the body as it stands (Make's), from the game's standing (`retailIdle`: the
    // entity's animation 0, ANIM.HQR -- its first keyframe: her back, her head, her legs and her ears as she stands in the school's hall).
    public static Anim[] Anims(Body body, byte[] retailIdle)
    {
        var basis = PalaceTorturers.ReadFrames(retailIdle, body.Bones.Count)[0].Bones;
        Anim Of(int loop, int[] ms, params Pose[] poses)
        {
            var anim = new Anim { Game = 2, LoopFrame = loop };
            for (var k = 0; k < poses.Length; k++) anim.Frames.Add(Frame(body, ms[k], basis, poses[k]));
            return anim;
        }
        var ready = new Pose(ReadyGreen.Rippled(0.25f, -0.3f), ReadyCheck.Rippled(-0.25f, 0.3f));
        var stirred = new Pose(ReadyGreen.Rippled(-0.1f, 0.25f), ReadyCheck.Rippled(0.1f, -0.25f));
        return new[]
        {
            Of(0, ReadyMs, ready, stirred),
            Of(1, RaiseMs, new Pose(Raised.Rippled(0.2f, -0.3f), ReadyCheck, -0.05f), new Pose(Raised.Rippled(0.4f, -0.5f), ReadyCheck, -0.05f),
                new Pose(Raised.Rippled(-0.3f, 0.45f), ReadyCheck, -0.05f)),
            Of(DropMs.Length - 1, DropMs, new Pose(Dropping[0].Rippled(0.2f, 0.3f), ReadyCheck, 0.08f), new Pose(Dropping[1].Rippled(0.35f, 0.45f), ReadyCheck, 0.22f),
                new Pose(Dropping[2].Rippled(-0.3f, -0.4f), ReadyCheck, 0.25f), new Pose(Dropping[2].Rippled(0.15f, 0.2f), ReadyCheck, 0.15f), ready),
            Of(1, WaveMs, new Pose(ReadyGreen, WaveOut.Rippled(0.35f, 0.4f), -0.05f), new Pose(ReadyGreen, WaveIn.Rippled(-0.35f, -0.45f), -0.08f),
                new Pose(ReadyGreen, WaveOut.Rippled(0.35f, 0.45f), -0.05f)),
        };
    }

    // A keyframe: the game's standing's values (`basis`), the torso leant, then each arm aimed -- from the shoulder to the pole -- and its
    // flag's cloth turned round the pole to fly the way the pose says, rippled.
    internal static AnimFrame Frame(Body body, int time, (int Type, double A, double B, double C)[] basis, Pose pose)
    {
        var v = Aim(body, basis, pose);
        return new AnimFrame(time, 0, 0, 0, v.Select((b, i) => i == 0 ? AnimBone.Neutral
            : b.Type == 1 ? new AnimBone(1, (float)(b.A * Scale), (float)(b.B * Scale), (float)(b.C * Scale))
            : new AnimBone(0, (float)(b.A / (2 * Math.PI)), (float)(b.B / (2 * Math.PI)), (float)(b.C / (2 * Math.PI)))).ToArray());
    }

    // The bones' values for a pose (radians; translations in the body's units, not scaled).
    internal static (int Type, double A, double B, double C)[] Aim(Body body, (int Type, double A, double B, double C)[] basis, Pose pose)
    {
        var v = new (int Type, double A, double B, double C)[body.Bones.Count];
        for (var b = 0; b < v.Length; b++) v[b] = b < basis.Length ? basis[b] : (0, 0, 0, 0);
        v[Torso] = (0, v[Torso].A + pose.Lean, v[Torso].B, v[Torso].C);
        var rest = body.World();
        Vector3 Pivot(int b) => rest[body.Bones[b].Pivot];
        void Point(int bone, Vector3 from, Vector3 to)
        {
            var (_, matrices) = PalaceTorturers.Posed(body, v);
            v[bone] = PalaceTorturers.Turn(matrices[body.Bones[bone].Parent], Vector3.Normalize(from), Vector3.Normalize(to));
        }
        foreach (var (arm, upper, fore, hand, pole, rows) in new[] { (pose.Green, GreenUpper, GreenFore, GreenHand, GreenPole, 1), (pose.Check, CheckUpper, CheckFore, CheckHand, CheckPole, CheckRows) })
        {
            Point(upper, Pivot(fore) - Pivot(upper), arm.Upper);
            Point(fore, Pivot(hand) - Pivot(fore), arm.Fore);
            Point(hand, PoleWay, arm.Pole);
            // (the cloth round the pole: its fly, unturned and a quarter turned round the pole, then the turn that has it fly the pose's way
            // -- square to the pole -- the strips after it straight; then rippled)
            Vector3 FlyAt(double turn)
            {
                v[pole] = (0, 0, 0, turn); v[pole + 1] = (0, 0, 0, 0); v[pole + 2] = (0, 0, 0, 0);
                var (points, _) = PalaceTorturers.Posed(body, v);
                var b = body.Bones[pole];
                // (the pole's nine points -- its two rings and its tip -- then the cloth's columns, a point a row: the first column's first
                // point, on the pole, to the second column's)
                return points[b.Start + 9 + rows + 1] - points[b.Start + 9];
            }
            var (_, posed) = PalaceTorturers.Posed(body, v);
            var axis = Vector3.Normalize(Apply(posed[hand], PoleWay));
            var want = arm.Cloth - axis * Vector3.Dot(arm.Cloth, axis);
            if (want.LengthSquared() < 1e-6f) want = Vector3.Cross(axis, Vector3.UnitX);
            want = Vector3.Normalize(want);
            Vector3 a = Vector3.Normalize(FlyAt(0)), q = Vector3.Normalize(FlyAt(Math.PI / 2));
            var gamma = Math.Atan2(Vector3.Dot(want, q), Vector3.Dot(want, a));
            v[pole] = (0, 0, 0, gamma);
            v[pole + 1] = (0, 0, 0, arm.RippleA);
            v[pole + 2] = (0, 0, 0, arm.RippleB);
        }
        return v;
    }

    private static Vector3 Apply(double[] r, Vector3 p) => new((float)(r[0] * p.X + r[1] * p.Y + r[2] * p.Z), (float)(r[3] * p.X + r[4] * p.Y + r[5] * p.Z), (float)(r[6] * p.X + r[7] * p.Y + r[8] * p.Z));
}

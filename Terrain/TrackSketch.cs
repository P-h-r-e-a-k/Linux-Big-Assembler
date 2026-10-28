namespace LBAAssembler.Terrain;

// A race track drawn on an island's map, made into a plan (the user, 2026-10-27: "The track plan format isn't feasible for a user to hand
// generate. ... take a screenshot of an island with a map drawn on it, then we can automatically generate the track accordingly ... the
// track in one colour, let's go with green, jumps in pink, pitlane in orange, and vertical loops in blue").
//
// The map is IslandMapRenderer's: Scale pixels to a cell, its top left the island cell (OriginX, OriginZ). What is drawn on it is a kind
// for each pixel (SketchKind): painted in the track generator (TrackGeneratorWindow's pens and track sections), or found in a picture the
// user drew on the map (FromImage: the pens' colours where the picture differs from the map). TrackSketchTracer follows the road -- green,
// pink and blue together -- along its middle, through the places it crosses itself, round to where it began (a lap) or from one end to the
// other (a sprint), and makes the plan: the points a cell apart, a gap jump over each pink stretch, a vertical loop in the middle of each
// blue one (the road straight and level round it), the pit lane between the orange stroke's ends, heights from the ground when there is a
// jump or a loop (the plan's jumps and loops need them).
internal enum SketchKind : byte { None, Road, Jump, Pit, Loop }

internal sealed class TrackSketch
{
    public int Width { get; }
    public int Height { get; }
    public int Scale { get; }
    public int OriginX { get; }
    public int OriginZ { get; }
    public SketchKind[] Kinds { get; }
    // How high what is drawn at each pixel is (world units), NaN on the ground: drawn in the air -- the generator's height, its 3D view's
    // height plane (the user, 2026-10-27: "enhance map generation to allow us to easily draw track in the air")
    public float[] Heights { get; }

    public TrackSketch(int width, int height, int scale, int originX, int originZ)
    {
        Width = width; Height = height; Scale = Math.Max(1, scale); OriginX = originX; OriginZ = originZ;
        Kinds = new SketchKind[width * height];
        Heights = new float[width * height];
        Array.Fill(Heights, float.NaN);
    }

    // The pens' colours, as the generator draws them and as a picture is read
    public static (byte R, byte G, byte B) Colour(SketchKind kind) => kind switch
    {
        SketchKind.Road => (40, 200, 60),
        SketchKind.Jump => (255, 80, 200),
        SketchKind.Pit => (255, 150, 20),
        SketchKind.Loop => (40, 110, 255),
        _ => (0, 0, 0),
    };

    // What a pen colour is: strong colours only (the map's own sand, grass and sea are pale or dark beside a pen's), by hue -- green
    // 80-170 degrees, blue 185-255, pink 280-350 (magenta to pink), orange 12-48
    public static SketchKind Classify(byte r, byte g, byte b)
    {
        double rf = r / 255.0, gf = g / 255.0, bf = b / 255.0;
        double max = Math.Max(rf, Math.Max(gf, bf)), min = Math.Min(rf, Math.Min(gf, bf)), d = max - min;
        if (max < 0.35 || d < 0.35 || d / max < 0.5) return SketchKind.None;
        double h = max == rf ? 60 * (((gf - bf) / d) % 6) : max == gf ? 60 * ((bf - rf) / d + 2) : 60 * ((rf - gf) / d + 4);
        if (h < 0) h += 360;
        if (h >= 80 && h <= 170) return SketchKind.Road;
        if (h >= 185 && h <= 255) return SketchKind.Loop;
        if (h >= 280 && h <= 350) return SketchKind.Jump;
        if (h >= 12 && h <= 48) return SketchKind.Pit;
        return SketchKind.None;
    }

    // The kinds drawn in a picture of the map (BGRA, any size: taken to the map's), where it differs from the map itself (`map`, BGRA,
    // Width x Height) -- a pen's colour over the map's own
    public static TrackSketch FromImage(byte[] picture, int pw, int ph, byte[] map, int width, int height, int scale, int originX, int originZ)
    {
        var sketch = new TrackSketch(width, height, scale, originX, originZ);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                // (the picture's pixel there, the nearest)
                int sx = Math.Min(pw - 1, (int)((x + 0.5) * pw / width)), sy = Math.Min(ph - 1, (int)((y + 0.5) * ph / height));
                int o = (sy * pw + sx) * 4, m = (y * width + x) * 4;
                byte b = picture[o], g = picture[o + 1], r = picture[o + 2];
                var kind = Classify(r, g, b);
                if (kind == SketchKind.None) continue;
                int dr = r - map[m + 2], dg = g - map[m + 1], db = b - map[m];
                if (dr * dr + dg * dg + db * db < 70 * 70) continue;
                sketch.Kinds[y * width + x] = kind;
            }
        return sketch;
    }
}

internal sealed record SketchResult(RaceTrackPlan? Plan, List<(double X, double Z)> Route, List<SketchKind> RouteKinds, bool Closed, int Start,
    (double X, double Z)? PitA, (double X, double Z)? PitB, List<string> Notes, List<string> Problems)
{
    // each point's height as the plan has it (a raised road's), or null: on the ground; and the ground under each point (the height profile)
    public double[]? Heights { get; init; }
    public double[] Ground { get; init; } = Array.Empty<double>();
}

internal static class TrackSketchTracer
{
    // the plan's origin (the island cells its points are counted from): as the islands' own plans
    public const int PlanOrigin = 448;
    // a loop as the old moon's are: its ring's radius, how far across the car comes down, the road straight and level this many cells either side
    private const double LoopRadius = 4, LoopAcross = 3, LoopStraight = 14;
    private const double ShortestLap = 120, ShortestJump = 4, ShortestPit = 25;
    // a jump's flight (cells) at the most, and the ramps' points before and after it (RaceTrackOptions.JumpRampLength, JumpLandingLength)
    private const int LongestJump = 22, JumpRamp = 9, JumpLanding = 13;
    // a raised road at least this high over the ground under it, and two of its decks this far apart where it passes over itself
    private const double RaisedClear = 900, DeckRoom = 1600;
    // a stretch drawn in the air: its ramps up and down, points (cells) at either end
    private const int RaiseRamp = 40;
    // the steepest a raised road climbs (a share: 0.25 is a quarter of a cell's length up for each cell along)
    private const double MostGrade = 0.25;

    // `given`: the road's middle already known (track sections placed in a chain, map pixels, in order) -- followed as it is, not from
    // the picture; `givenClosed` whether it comes back round
    // `givenRise`: with `given`, how high each of its points is over its first (world units) -- climbs and spirals: the track a raised road
    // `profile`: each point's height, edited in the height profile (as many as the route the drawing gives: else not used)
    public static SketchResult Trace(TrackSketch sketch, IslandFile? island, bool reverse = false, List<(double X, double Y)>? given = null, bool givenClosed = false,
        List<double>? givenRise = null, double[]? profile = null, List<(double X, double Z)>? profileAt = null)
    {
        var notes = new List<string>(); var problems = new List<string>();
        int w = sketch.Width, h = sketch.Height;
        var empty = new SketchResult(null, new(), new(), false, 0, null, null, notes, problems);
        List<(double X, double Y)> line;
        bool closed;
        double half;
        if (given is { Count: >= 2 })
        {
            line = given.ToList(); closed = givenClosed; half = 3 * sketch.Scale;
            var rise = givenRise is { } gr && gr.Count == line.Count ? gr.ToList() : null;
            if (reverse) { line.Reverse(); rise?.Reverse(); }
            return Plan(sketch, island, line, closed, half, notes, problems, empty, rise, profile, profileAt);
        }

        // the road: green, pink and blue together, small gaps closed, its biggest piece
        var road = new bool[w * h];
        for (var i = 0; i < road.Length; i++) road[i] = sketch.Kinds[i] is SketchKind.Road or SketchKind.Jump or SketchKind.Loop;
        road = Erode(Dilate(road, w, h, 2), w, h, 2);
        road = Biggest(road, w, h, out var pieces);
        if (pieces == 0) { problems.Add("No track drawn: draw it in green (jumps pink, loops blue)."); return empty; }
        if (pieces > 1) notes.Add($"{pieces - 1} other green piece{(pieces > 2 ? "s" : "")} apart from the track left out (the track is the biggest drawn)");
        var dt = Distance(road, w, h);
        half = Percentile(road, dt, 0.9);
        var step = Math.Max(3.0, half * 1.2);

        // where to begin: on a straight stretch, in the middle of the stroke
        var (sx, sy, sdx, sdy) = BeginAt(road, dt, w, h, half);
        if (sx < 0) { problems.Add("The track couldn't be followed: draw it as a line."); return empty; }
        var forward = March(road, dt, w, h, sx, sy, sdx, sdy, step, half, out closed);
        if (closed) line = forward;
        else
        {
            var back = March(road, dt, w, h, sx, sy, -sdx, -sdy, step, half, out _);
            back.Reverse();
            line = back.Concat(forward.Skip(1)).ToList();
            // (its two ends met after all -- the march stopped a step short of them: a lap)
            var (ax, ay) = line[0]; var (bx, by) = line[^1];
            if ((ax - bx) * (ax - bx) + (ay - by) * (ay - by) < Math.Pow(Math.Max(step * 2, half * 3), 2) && line.Count > 20) closed = true;
        }
        if (reverse) line.Reverse();
        return Plan(sketch, island, line, closed, half, notes, problems, empty, null, profile, profileAt);
    }

    // The plan from the road's middle (map pixels, in order)
    private static SketchResult Plan(TrackSketch sketch, IslandFile? island, List<(double X, double Y)> line, bool closed, double half, List<string> notes, List<string> problems, SketchResult empty,
        List<double>? lineRise = null, double[]? profile = null, List<(double X, double Z)>? profileAt = null)
    {
        // into cells, a cell apart, smoothed
        var cells = line.Select(p => (X: sketch.OriginX + p.X / sketch.Scale, Z: sketch.OriginZ + p.Y / sketch.Scale)).ToList();
        var route = Respace(cells, 1.0, closed);
        for (var pass = 0; pass < 3; pass++) route = Smooth(route, 2, closed);
        route = Respace(route, 1.0, closed);
        var length = Length(route, closed);
        if (route.Count < 10) { problems.Add("The track drawn is too short to follow."); return empty; }
        if (length < ShortestLap) problems.Add($"The track is {length:0} cells long: draw it at least {ShortestLap:0} cells long (a cell is {sketch.Scale} pixels of the map).");
        notes.Add(closed ? $"a lap {length:0} cells long" : $"a sprint {length:0} cells long, from one end of the line to the other (no lap: its ends don't meet)");

        // how high each point is over the first, from the sections' climbs: by its share of the way along (the route a cell apart along the
        // same line); any climb at all and the whole track is a raised road
        var rises = new double[route.Count];
        if (lineRise is { Count: > 1 })
        {
            var along = new double[line.Count];
            for (var i = 1; i < line.Count; i++) along[i] = along[i - 1] + Math.Sqrt(Math.Pow(line[i].X - line[i - 1].X, 2) + Math.Pow(line[i].Y - line[i - 1].Y, 2));
            var total = along[^1];
            for (var k = 0; k < route.Count; k++)
            {
                var want = total * k / Math.Max(1, route.Count - (closed ? 0 : 1));
                var j = Array.BinarySearch(along, want); if (j < 0) j = ~j;
                j = Math.Clamp(j, 1, line.Count - 1);
                var f = (want - along[j - 1]) / Math.Max(1e-9, along[j] - along[j - 1]);
                rises[k] = lineRise[j - 1] + (lineRise[j] - lineRise[j - 1]) * Math.Clamp(f, 0, 1);
            }
        }
        // (and how high the drawing says each point is: the highest drawn in the air round it, NaN where it's all on the ground)
        var wanted = route.Select(p =>
        {
            double cx = (p.X - sketch.OriginX) * sketch.Scale, cy = (p.Z - sketch.OriginZ) * sketch.Scale;
            var best = double.NaN; var r = (int)Math.Ceiling(half + 1);
            for (var yy = (int)cy - r; yy <= (int)cy + r; yy++)
                for (var xx = (int)cx - r; xx <= (int)cx + r; xx++)
                {
                    if (xx < 0 || yy < 0 || xx >= sketch.Width || yy >= sketch.Height) continue;
                    var v = sketch.Heights[yy * sketch.Width + xx];
                    if (!float.IsNaN(v) && (double.IsNaN(best) || v > best)) best = v;
                }
            return best;
        }).ToArray();
        // (the profile's points where they were when it was edited: put to this route's by where they are -- the route may begin elsewhere
        // now, its start following its pit lane)
        var usable = profile is not null && profile.Length == route.Count && profileAt is not null && profileAt.Count == route.Count;
        var raised = rises.Any(r => Math.Abs(r) > 100) || wanted.Any(v => !double.IsNaN(v)) || usable;
        if (raised && closed && lineRise is { Count: > 1 } && Math.Abs(lineRise[^1] - lineRise[0]) > 200)
            problems.Add($"The lap ends {lineRise[^1] - lineRise[0]:+0;-0} higher than it starts: its climbs and drops must come back to where it began (add a ramp or a spiral the other way).");

        // what each point of the road is: a jump's, a loop's, or road
        var kinds = route.Select(p => KindNear(sketch, (p.X - sketch.OriginX) * sketch.Scale, (p.Z - sketch.OriginZ) * sketch.Scale, half)).ToList();
        var jumps = Runs(kinds, SketchKind.Jump, closed).Where(r => r.Length >= ShortestJump).ToList();
        var loops = Runs(kinds, SketchKind.Loop, closed).Where(r => r.Length >= 2).ToList();
        if (jumps.Count > RaceTrackJumpAnim.MaxJumps) { notes.Add($"{jumps.Count} jumps drawn: the first {RaceTrackJumpAnim.MaxJumps} built (the most a track has)"); jumps = jumps.Take(RaceTrackJumpAnim.MaxJumps).ToList(); }

        // the start: on the pit lane's middle when there is one (the builder's), else on the longest straight
        (double X, double Z)? pitA = null, pitB = null;
        if (PitEnds(sketch, out var a, out var b) is { } pitLength)
        {
            if (pitLength < ShortestPit) notes.Add($"the orange pit lane is only {pitLength:0} cells long: left out (draw it {ShortestPit:0} cells or more beside the track)");
            else if (raised) notes.Add("the pit lane is left out: the track climbs, so it is a raised road, and a pit lane is built on the ground beside a ground road");
            else { pitA = a; pitB = b; }
        }
        var start = closed ? Straightest(route, closed) : Math.Min(6, route.Count / 4);

        // a lap begins at its start (indexes the builder counts from there are the plan's own)
        if (closed && pitA is null && start > 0)
        {
            route = route.Skip(start).Concat(route.Take(start)).ToList();
            kinds = kinds.Skip(start).Concat(kinds.Take(start)).ToList();
            rises = rises.Skip(start).Concat(rises.Take(start)).ToArray();
            wanted = wanted.Skip(start).Concat(wanted.Take(start)).ToArray();
            jumps = Runs(kinds, SketchKind.Jump, closed).Where(r => r.Length >= ShortestJump).Take(RaceTrackJumpAnim.MaxJumps).ToList();
            loops = Runs(kinds, SketchKind.Loop, closed).Where(r => r.Length >= 2).ToList();
            start = 0;
        }

        var plan = new RaceTrackPlan
        {
            OriginCellX = PlanOrigin, OriginCellZ = PlanOrigin,
            Open = !closed,
        };
        // (loops: the road straight through them -- the ring stands in the plane of the way -- a straight line between the points either side)
        var loopFeet = new List<int>();
        foreach (var run in loops)
        {
            var foot = (run.First + run.Length / 2) % route.Count;
            int from = foot - (int)LoopStraight, to = foot + (int)LoopStraight;
            if (!closed && (from < 0 || to >= route.Count)) { notes.Add("a loop too near an end of the sprint: left out"); continue; }
            var p0 = route[Wrap(from, route.Count)]; var p1 = route[Wrap(to, route.Count)];
            for (var k = from + 1; k < to; k++) { var t = (double)(k - from) / (to - from); route[Wrap(k, route.Count)] = (p0.X + (p1.X - p0.X) * t, p0.Z + (p1.Z - p0.Z) * t); }
            loopFeet.Add(Wrap(foot, route.Count));
        }
        // (jumps: as long as a jump flies at the most, along its pink stretch -- or a little either side -- where it starts and lands in the
        // one cube (the game changes scene at a cube's edge: a flight over one breaks), the road straight from its ramp to its landing)
        var jumpPairs = new List<int[]>();
        foreach (var run in jumps)
        {
            var len = Math.Min(run.Length, LongestJump);
            var centre = run.First + run.Length / 2;
            int? found = null;
            for (var shift = 0; shift <= 40 && found is null; shift++)
                foreach (var sign in new[] { 1, -1 })
                {
                    var lip = centre - len / 2 + sign * shift; var land = lip + len - 1;
                    if (!closed && (lip < JumpRamp + 4 || land > route.Count - JumpLanding - 4)) continue;
                    var cubes = Enumerable.Range(lip - JumpRamp, len + JumpRamp + JumpLanding).Select(k => route[Wrap(k, route.Count)])
                        .Select(p => ((int)Math.Floor(p.X / 64), (int)Math.Floor(p.Z / 64))).Distinct().Count();
                    if (cubes != 1) continue;
                    if (loopFeet.Any(f => Math.Abs(Math.IEEERemainder(f - (lip + land) / 2.0, route.Count)) < len / 2.0 + JumpLanding + LoopStraight)) continue;
                    found = lip; break;
                }
            if (found is not { } at) { notes.Add("a jump left out: nowhere on or near its pink stretch does it start and land in the one cube of the island (the game changes scene at a cube's edge)"); continue; }
            int from = at - JumpRamp, to = at + len - 1 + JumpLanding;
            var q0 = route[Wrap(from, route.Count)]; var q1 = route[Wrap(to, route.Count)];
            for (var k = from + 1; k < to; k++) { var t = (double)(k - from) / (to - from); route[Wrap(k, route.Count)] = (q0.X + (q1.X - q0.X) * t, q0.Z + (q1.Z - q0.Z) * t); }
            jumpPairs.Add(new[] { Wrap(at, route.Count), Wrap(at + len - 1, route.Count) });
            if (len < run.Length) notes.Add($"a pink stretch {run.Length} cells long: its jump {len} cells (the longest a jump flies well)");
        }
        plan.Points = route.Select(p => new[] { Math.Round(p.X - PlanOrigin, 2), Math.Round(p.Z - PlanOrigin, 2) }).ToArray();
        if (pitA is { } pa && pitB is { } pb) { plan.PitA = new[] { Math.Round(pa.X - PlanOrigin, 2), Math.Round(pa.Z - PlanOrigin, 2) }; plan.PitB = new[] { Math.Round(pb.X - PlanOrigin, 2), Math.Round(pb.Z - PlanOrigin, 2) }; }
        else plan.Start = start;
        if (!closed) plan.Finish = Math.Max(start + 10, route.Count - 4);

        // heights, for jumps and loops: the ground's, smoothed; level where a jump flies and round a loop
        if ((jumpPairs.Count > 0 || loopFeet.Count > 0) && island is not null)
        {
            var hs = route.Select(p => Math.Max(300.0, IslandOps.Altitude(island, p.X * 512, p.Z * 512) ?? 0)).ToArray();
            hs = SmoothHeights(hs, 6, closed);
            foreach (var pair in jumpPairs)
            {
                int from = pair[0] - JumpRamp, to = pair[0] + Wrap(pair[1] - pair[0], route.Count) + JumpLanding;
                var level = Enumerable.Range(from, to - from + 1).Max(k => hs[Wrap(k, hs.Length)]);
                for (var k = from; k <= to; k++) hs[Wrap(k, hs.Length)] = level;
            }
            foreach (var foot in loopFeet)
            {
                var level = hs[foot];
                for (var k = foot - (int)LoopStraight; k <= foot + (int)LoopStraight; k++) hs[Wrap(k, hs.Length)] = level;
            }
            plan.Heights = hs.Select(v => Math.Round(v)).ToArray();
            plan.MaxGrade = 0.15;
            if (jumpPairs.Count > 0) plan.GapJumps = jumpPairs.ToArray();
            if (loopFeet.Count > 0) plan.Loops = loopFeet.Select(f => new[] { (double)f, LoopRadius, LoopAcross, 0 }).ToArray();
        }
        // (the profile matched to the route as it now runs: the turn of the one against the other that puts their points nearest)
        if (usable)
        {
            int n3 = route.Count, bestOffset = 0; var bestSum = double.MaxValue;
            for (var o = 0; o < n3; o++)
            {
                double sum = 0;
                for (var q = 0; q < 8 && sum < bestSum; q++)
                {
                    var k = q * n3 / 8; var a2 = route[k]; var b2 = profileAt![(k + o) % n3];
                    sum += (a2.X - b2.X) * (a2.X - b2.X) + (a2.Z - b2.Z) * (a2.Z - b2.Z);
                }
                if (sum < bestSum) { bestSum = sum; bestOffset = o; }
            }
            if (bestSum / 8 > 9) usable = false;
            else profile = Enumerable.Range(0, n3).Select(k => profile![(k + bestOffset) % n3]).ToArray();
        }
        double[]? heightsOut = null;
        var groundOut = island is null ? new double[route.Count] : route.Select(p => Math.Max(0.0, IslandOps.Altitude(island, p.X * 512, p.Z * 512) ?? 0)).ToArray();
        // a raised road: the whole track on piers (as the Elevator Platform's, Volcano Island's), each point the sections' height over the
        // start, the whole lifted so it is clear of the ground everywhere -- RaisedClear over the highest ground under it, the ground cut
        // down where it would come up into the deck (RaisedCut); where it passes over itself, room for a car between the decks
        if (raised && island is not null)
        {
            var ground = route.Select(p => Math.Max(0.0, IslandOps.Altitude(island, p.X * 512, p.Z * 512) ?? 0)).ToArray();
            // (the heights the track has on the ground: its jumps' and loops' level stretches, the ground's elsewhere)
            var onGround = plan.Heights is { } gh && gh.Length == route.Count ? gh.ToArray() : SmoothHeights(ground, 6, closed);
            // (each point the sections' climb over the start's height, or the ground's height and a little where the ground is higher --
            // the road follows the ground where the sections are level, and climbs over it as they climb -- smoothed, never under the ground)
            // (the ground averaged round each point, 20 cells every way -- the turns of a spiral, one over the other, are over the same ground
            // and so on the same base, their climb between them whole -- and the climb on it; never into the ground)
            double Around(double x, double z)
            {
                double sum = 0; var n = 0;
                for (var dz = -20; dz <= 20; dz += 4)
                    for (var dx = -20; dx <= 20; dx += 4)
                    {
                        if (dx * dx + dz * dz > 400) continue;
                        sum += Math.Max(0.0, IslandOps.Altitude(island, (x + dx) * 512, (z + dz) * 512) ?? 0); n++;
                    }
                return sum / n;
            }
            var floor = route.Select(p => Around(p.X, p.Z) + RaisedClear).ToArray();
            var hs = Enumerable.Range(0, route.Count).Select(k => floor[k] + rises[k] - rises[0]).ToArray();
            // (drawn in the air: there, the height it was drawn at)
            var drawnAny = wanted.Any(v => !double.IsNaN(v));
            for (var k = 0; k < hs.Length; k++) if (!double.IsNaN(wanted[k])) hs[k] = wanted[k];
            // (and smoothed along the way: the ground's own ups and downs under a climb don't make it dip; a drawn height eased into)
            for (var pass = 0; pass < 3; pass++) hs = SmoothHeights(hs, 8, closed);
            // (the height profile, edited: as it says)
            if (usable) hs = profile!.ToArray();
            if (!drawnAny && !usable)
            {
                var under = Enumerable.Range(0, route.Count).Max(k => ground[k] + RaisedClear * 0.6 - hs[k]);
                if (under > 0) for (var k = 0; k < hs.Length; k++) hs[k] += under;
            }
            for (var k = 0; k < hs.Length; k++) if (hs[k] < ground[k] + RaisedClear * 0.5) hs[k] = ground[k] + RaisedClear * 0.5;
            // (only some of it in the air -- drawn there, or lifted in the profile -- and none of the sections climbing: that stretch raised,
            // ramps RaiseRamp points long either side, the rest a road on the ground as it would be: a bridge over the road, not the whole lap
            // on piers. A lap has one raised stretch at the most, and not through its start)
            var span = (From: 0, To: route.Count - 1);
            var whole = true;
            if (!rises.Any(r => Math.Abs(r) > 100))
            {
                var lifted = Enumerable.Range(0, route.Count).Where(k => !double.IsNaN(wanted[k]) || usable && profile![k] > onGround[k] + 500).ToList();
                var n2 = route.Count;
                (int From, int To)? found = null;
                if (lifted.Count == 0) found = (-1, -1);
                else if (!closed) found = (Math.Max(0, lifted[0] - RaiseRamp), Math.Min(n2 - 1, lifted[^1] + RaiseRamp));
                else
                {
                    // (the stretch round the lap that holds them all: the lap less its longest stretch with none)
                    var gapAt = 0; var gapLen = -1;
                    for (var i = 0; i < lifted.Count; i++)
                    {
                        var next = lifted[(i + 1) % lifted.Count] + (i + 1 == lifted.Count ? n2 : 0);
                        if (next - lifted[i] > gapLen) { gapLen = next - lifted[i]; gapAt = i; }
                    }
                    int from = lifted[(gapAt + 1) % lifted.Count] - RaiseRamp, to = lifted[gapAt] + RaiseRamp;
                    if (from >= 0 && to < n2 && from < to && to - from < n2 - 20) found = (from, to);
                    else notes.Add("the stretch in the air runs through the start: the whole lap is a raised road (draw it away from the start to keep the rest on the ground)");
                }
                if (found is { } f2)
                {
                    whole = false;
                    span = f2;
                    var groundRoad = onGround;
                    var air = hs;
                    hs = new double[n2];
                    for (var k = 0; k < n2; k++)
                    {
                        if (k < span.From || k > span.To) { hs[k] = groundRoad[k]; continue; }
                        // (up and down its ramps: from the ground at its ends to the height in the air RaiseRamp points in)
                        var t = Math.Min(1.0, Math.Min(k - span.From, span.To - k) / (double)RaiseRamp);
                        t = t * t * (3 - 2 * t);
                        hs[k] = groundRoad[k] + (air[k] - groundRoad[k]) * t;
                    }
                }
            }
            // (no steeper than MostGrade: a height drawn or dragged up too steeply is kept, the slopes either side of it made longer -- a hill
            // widened, not cut down; the profile shows it so)
            hs = GradeLimited(hs, closed);
            // (and the raised stretch out over the slopes that made: as far as the road is over the road on the ground)
            if (!whole && span.From >= 0)
            {
                while (span.From > 0 && hs[span.From - 1] > onGround[span.From - 1] + 100) span.From--;
                while (span.To < hs.Length - 1 && hs[span.To + 1] > onGround[span.To + 1] + 100) span.To++;
                if (closed && (span.From == 0 || span.To == hs.Length - 1))
                {
                    whole = true; span = (0, hs.Length - 1);
                    notes.Add("its slopes run through the start: the whole lap is a raised road");
                }
            }
            // (the raised stretch never into the ground: a point drawn or dragged under it is put just over it)
            var raisedTo = 0;
            if (span.From >= 0)
                for (var k = span.From; k <= span.To; k++)
                {
                    var t = Math.Min(1.0, Math.Min(k - span.From, span.To - k) / (double)RaiseRamp);
                    if (!whole && t < 1) continue;
                    if (hs[k] < ground[k] + RaisedClear * 0.5) { hs[k] = ground[k] + RaisedClear * 0.5; raisedTo++; }
                }
            if (raisedTo > 0 && (drawnAny || usable)) notes.Add($"{raisedTo} points were drawn under the ground and are put just over it");
            foreach (var pair in plan.GapJumps ?? Array.Empty<int[]>())
            {
                int from = pair[0] - JumpRamp, to = pair[0] + Wrap(pair[1] - pair[0], route.Count) + JumpLanding;
                var level = Enumerable.Range(from, to - from + 1).Max(k => hs[Wrap(k, hs.Length)]);
                for (var k = from; k <= to; k++) hs[Wrap(k, hs.Length)] = level;
            }
            foreach (var foot in loopFeet)
            {
                var level = hs[foot];
                for (var k = foot - (int)LoopStraight; k <= foot + (int)LoopStraight; k++) hs[Wrap(k, hs.Length)] = level;
            }
            heightsOut = hs;
            if (Environment.GetEnvironmentVariable("LBA2_TRACKGEN_DUMP") is { Length: > 0 } dump)
                System.IO.File.WriteAllLines(dump, Enumerable.Range(0, route.Count).Select(k => FormattableString.Invariant($"{k},{route[k].X:0.00},{route[k].Z:0.00},{rises[k]:0},{hs[k]:0},{ground[k]:0},{floor[k]:0}")));
            plan.Heights = hs.Select(v => Math.Round(v)).ToArray();
            plan.MaxGrade = 0.25;
            plan.Raised = span.From < 0 ? null : new[] { span.From, span.To };
            plan.RaisedHalf = 4.75; plan.AsphaltHalf = 3.75; plan.CurbHalf = 4.5;
            plan.RaisedCut = true;
            plan.PitA = plan.PitB = null;
            plan.Start ??= start;
            if (span.From < 0) notes.Add("on the ground, at the heights the profile gives it");
            else notes.Add((whole ? "a raised road on piers" : $"points {span.From} to {span.To} a raised road on piers (ramps up and down at its ends), the rest on the ground") +
                           $", from {hs.Min():0} to {hs.Max():0} high, up to {Enumerable.Range(0, hs.Length).Max(k => hs[k] - ground[k]):0} over the ground");
            // (passing over itself: how much room between the decks)
            var tight = 0; var crossOver = 0; (int I, int J)? where = null;
            for (var i = 0; i < route.Count; i += 2)
                for (var j = i + 30; j < route.Count; j += 2)
                {
                    if (closed && route.Count - (j - i) < 30) continue;
                    // (two stretches on the ground: a crossroads, the build's to deal with)
                    bool Up(int q) => span.From >= 0 && q >= span.From && q <= span.To;
                    if (!Up(i) && !Up(j)) continue;
                    double dx = route[i].X - route[j].X, dz = route[i].Z - route[j].Z;
                    // (decks over each other by half a deck's width and more -- side by side, rails touching, is no passing over)
                    if (dx * dx + dz * dz > 49) continue;
                    var gap = Math.Abs(hs[i] - hs[j]);
                    if (gap < DeckRoom) { tight++; where ??= (i, j); } else crossOver++;
                }
            if (where is { } w2)
                problems.Add($"Where the track passes over itself, at cell ({route[w2.I].X:0}, {route[w2.I].Z:0}), there isn't room for a car between the two roads ({hs[w2.I]:0} and {hs[w2.J]:0} high, " +
                             $"less than {DeckRoom:0} apart): climb more before it passes over, or keep it further from the road below.");
            else if (crossOver > 0) notes.Add("it passes over itself with room for a car between the roads");
        }
        else if (plan.Heights is not null && Crossings(route, closed) is var crossings and > 0)
            notes.Add($"the track crosses itself {crossings} time{(crossings == 1 ? "" : "s")}: the roads meet there, a crossroads (with no jump or loop the build makes a jump there itself)");
        notes.Add($"{plan.Points.Length} points; {(plan.GapJumps?.Length ?? 0)} jump{((plan.GapJumps?.Length ?? 0) == 1 ? "" : "s")}, {(plan.Loops?.Length ?? 0)} loop{((plan.Loops?.Length ?? 0) == 1 ? "" : "s")}, " +
                  (plan.PitA is not null ? "a pit lane (the start line in its middle)" : $"no pit lane (the start line at point {start})"));
        return new SketchResult(problems.Count == 0 ? plan : null, route, kinds, closed, plan.Start ?? -1, pitA, pitB, notes, problems)
            { Heights = heightsOut ?? plan.Heights, Ground = groundOut };
    }

    private static int Wrap(int k, int n) => ((k % n) + n) % n;

    // Heights a cell apart no steeper than MostGrade: each point at least as high as its neighbours less a grade's worth (both ways, twice
    // round a lap)
    private static double[] GradeLimited(double[] hs, bool closed)
    {
        var h = hs.ToArray(); var n = h.Length; var most = MostGrade * 512;
        for (var round = 0; round < (closed ? 2 : 1); round++)
        {
            for (var k = closed ? 0 : 1; k < n; k++) h[k] = Math.Max(h[k], h[Wrap(k - 1, n)] - most);
            for (var k = closed ? n - 1 : n - 2; k >= 0; k--) h[k] = Math.Max(h[k], h[Wrap(k + 1, n)] - most);
        }
        return h;
    }

    // How many times the route crosses itself: places where two parts of it far apart along it come within two cells
    private static int Crossings(List<(double X, double Z)> route, bool closed)
    {
        var n = route.Count; var count = 0; var last = -100;
        for (var i = 0; i < n; i++)
        {
            var near = false;
            for (var j = i + 40; j < n && !near; j++)
            {
                if (closed && n - (j - i) < 40) continue;
                double dx = route[i].X - route[j].X, dz = route[i].Z - route[j].Z;
                near = dx * dx + dz * dz < 4;
            }
            if (near && i - last > 20) count++;
            if (near) last = i;
        }
        return count;
    }

    // ---- the road's middle, followed

    // A point on a straight stretch, in the middle of the stroke, and the way along it
    private static (int X, int Y, double Dx, double Dy) BeginAt(bool[] m, double[] dt, int w, int h, double half)
    {
        var best = (-1, -1, 0.0, 0.0); var bestScore = -1.0;
        var grid = Math.Max(2, (int)half);
        var reach = (int)Math.Max(4, half * 3);
        for (var y = 0; y < h; y += grid)
            for (var x = 0; x < w; x += grid)
            {
                var i = y * w + x;
                if (!m[i] || dt[i] < half * 0.6 || dt[i] > half * 1.3) continue;
                // the stroke's way here: the long axis of the drawn pixels round it
                double sxx = 0, syy = 0, sxy = 0; var n = 0;
                for (var yy = Math.Max(0, y - reach); yy <= Math.Min(h - 1, y + reach); yy++)
                    for (var xx = Math.Max(0, x - reach); xx <= Math.Min(w - 1, x + reach); xx++)
                    {
                        if (!m[yy * w + xx]) continue;
                        double dx = xx - x, dy = yy - y;
                        if (dx * dx + dy * dy > reach * reach) continue;
                        sxx += dx * dx; syy += dy * dy; sxy += dx * dy; n++;
                    }
                if (n < 4) continue;
                var tr = sxx + syy; var det = sxx * syy - sxy * sxy;
                var l1 = tr / 2 + Math.Sqrt(Math.Max(0, tr * tr / 4 - det)); var l2 = tr - l1;
                var ratio = l1 / Math.Max(1e-6, l2);
                if (ratio <= bestScore) continue;
                bestScore = ratio;
                var angle = 0.5 * Math.Atan2(2 * sxy, sxx - syy);
                best = (x, y, Math.Cos(angle), Math.Sin(angle));
            }
        return best;
    }

    // From (x, y) along (dx, dy): steps the length of `step`, each the way that stays on the road best and keeps nearest straight on, set
    // back to the stroke's middle across the way; until it comes back round to where it began (a lap) or the road ends
    private static List<(double X, double Y)> March(bool[] m, double[] dt, int w, int h, int x, int y, double dx, double dy, double step, double half, out bool closed)
    {
        var pts = new List<(double X, double Y)> { (x, y) };
        closed = false;
        double px = x, py = y, hx = dx, hy = dy, travelled = 0;
        var most = (int)(m.Count(v => v) / Math.Max(1.0, step) * 3) + 50;
        // (the road it has been along: a sharp corner is followed round onto road not yet gone along, a dead end isn't turned back from)
        var visited = new bool[m.Length];
        void Visit(double cx0, double cy0)
        {
            // (the whole stroke's width and a little: its own edges aren't road not yet gone along -- marked narrower, a turn back
            // along them read as fresh road and the march went back round the lap the wrong way)
            var r = half * 1.4;
            for (var yy = (int)(cy0 - r); yy <= (int)(cy0 + r); yy++)
                for (var xx = (int)(cx0 - r); xx <= (int)(cx0 + r); xx++)
                    if (xx >= 0 && yy >= 0 && xx < w && yy < h && (xx - cx0) * (xx - cx0) + (yy - cy0) * (yy - cy0) <= r * r) visited[yy * w + xx] = true;
        }
        Visit(px, py);
        for (var n = 0; n < most; n++)
        {
            // (where the road crosses itself the stroke is wider across than a road: there it goes straight on, as near as the road lets
            // it, and isn't set back to a middle -- the crossing's middle would turn it onto the other way)
            var crossing = Across(m, w, h, px, py, -hy, hx, half * 4) > half * 2.8;
            double bestScore = double.MinValue, bx = 0, by = 0;
            var reach = crossing ? step * 3 : step * 1.5;
            for (var a = -50.0; a <= 50.0; a += 2.5)
            {
                if (crossing && Math.Abs(a) > 20) continue;
                var r = a * Math.PI / 180;
                double cx = hx * Math.Cos(r) - hy * Math.Sin(r), cy = hx * Math.Sin(r) + hy * Math.Cos(r);
                var inside = 0;
                for (var s = 1; s <= 12; s++) if (On(m, w, h, px + cx * reach * s / 12, py + cy * reach * s / 12)) inside++;
                if (inside < 11) continue;
                var score = crossing ? -Math.Abs(a) : inside + Dt(dt, w, h, px + cx * step, py + cy * step) / Math.Max(1, half) * 0.5 - Math.Abs(a) / 50.0 * 2;
                if (score > bestScore) { bestScore = score; bx = cx; by = cy; }
            }
            // (nothing ahead: a sharp corner -- a hairpin, the stroke's own tip where it turns back -- looked for further round, up to
            // nearly back the way it came, on road not gone along yet; none: the road's end. The user, 2026-10-27: a lap drawn on Wannies
            // Island with two hairpins was followed as a sprint between them)
            // (at a crossing: straight on, a little less far and a little more round -- never round the corner onto the crossing road, which
            // is road not gone along yet too: the Desert lap's crossing sent it round its lower half three times)
            if (bestScore == double.MinValue && crossing)
                for (var a = -35.0; a <= 35.0; a += 2.5)
                {
                    var r = a * Math.PI / 180;
                    double cx = hx * Math.Cos(r) - hy * Math.Sin(r), cy = hx * Math.Sin(r) + hy * Math.Cos(r);
                    var inside = 0;
                    for (var s = 1; s <= 12; s++) if (On(m, w, h, px + cx * step * 2 * s / 12, py + cy * step * 2 * s / 12)) inside++;
                    if (inside < 10) continue;
                    var score = -Math.Abs(a);
                    if (score > bestScore) { bestScore = score; bx = cx; by = cy; }
                }
            if (bestScore == double.MinValue && !crossing)
                for (var a = 55.0; a <= 170.0 && bestScore == double.MinValue; a += 5)
                    foreach (var sign in new[] { 1, -1 })
                    {
                        var r = sign * a * Math.PI / 180;
                        double cx = hx * Math.Cos(r) - hy * Math.Sin(r), cy = hx * Math.Sin(r) + hy * Math.Cos(r);
                        var fresh = 0; var on = 0;
                        for (var s = 1; s <= 12; s++)
                        {
                            double sx = px + cx * step * 2.5 * s / 12, sy = py + cy * step * 2.5 * s / 12;
                            if (!On(m, w, h, sx, sy)) continue;
                            on++;
                            int xi = (int)Math.Round(sx), yi = (int)Math.Round(sy);
                            if (!visited[yi * w + xi]) fresh++;
                        }
                        if (on < 10 || fresh < 7) continue;
                        var score = Dt(dt, w, h, px + cx * step, py + cy * step) + fresh * 0.2 - a / 30.0;
                        if (score > bestScore) { bestScore = score; bx = cx; by = cy; }
                    }
            if (bestScore == double.MinValue) break;
            double qx = px + bx * step, qy = py + by * step;
            if (!crossing)
            {
                // (to the middle of the stroke across the way)
                double nx = -by, ny = bx, bestDt = Dt(dt, w, h, qx, qy), off = 0;
                for (var o = -half; o <= half; o += 0.5)
                {
                    var v = Dt(dt, w, h, qx + nx * o, qy + ny * o);
                    if (v > bestDt + 0.01 || (Math.Abs(v - bestDt) <= 0.01 && Math.Abs(o) < Math.Abs(off))) { bestDt = v; off = o; }
                }
                qx += nx * off * 0.5; qy += ny * off * 0.5;
            }
            double mx = qx - px, my = qy - py, ml = Math.Sqrt(mx * mx + my * my);
            if (ml < 1e-6) break;
            // (the way on: this step's, a little of the last's kept -- a wobble of the middle doesn't turn it)
            hx = hx * 0.35 + mx / ml * 0.65; hy = hy * 0.35 + my / ml * 0.65;
            var hl = Math.Sqrt(hx * hx + hy * hy); hx /= hl; hy /= hl;
            travelled += ml;
            px = qx; py = qy;
            pts.Add((px, py));
            Visit(px, py);
            // (back round to the start, going the way it set off)
            if (travelled > step * 12)
            {
                double ex = px - x, ey = py - y;
                if (ex * ex + ey * ey < Math.Max(step * step * 2.5, half * half * 4) && hx * dx + hy * dy > 0) { closed = true; break; }
            }
        }
        return pts;
    }

    // How wide the stroke is across (nx, ny) at (x, y): the run of road through it, out to `most` either side
    private static double Across(bool[] m, int w, int h, double x, double y, double nx, double ny, double most)
    {
        double a = 0, b = 0;
        while (a < most && On(m, w, h, x + nx * (a + 0.5), y + ny * (a + 0.5))) a += 0.5;
        while (b < most && On(m, w, h, x - nx * (b + 0.5), y - ny * (b + 0.5))) b += 0.5;
        return a + b;
    }

    private static bool On(bool[] m, int w, int h, double x, double y)
    {
        int xi = (int)Math.Round(x), yi = (int)Math.Round(y);
        return xi >= 0 && yi >= 0 && xi < w && yi < h && m[yi * w + xi];
    }

    private static double Dt(double[] dt, int w, int h, double x, double y)
    {
        int xi = (int)Math.Round(x), yi = (int)Math.Round(y);
        return xi >= 0 && yi >= 0 && xi < w && yi < h ? dt[yi * w + xi] : 0;
    }

    // ---- masks

    private static bool[] Dilate(bool[] m, int w, int h, int r)
    {
        var o = new bool[m.Length];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                if (!m[y * w + x]) continue;
                for (var yy = Math.Max(0, y - r); yy <= Math.Min(h - 1, y + r); yy++)
                    for (var xx = Math.Max(0, x - r); xx <= Math.Min(w - 1, x + r); xx++) o[yy * w + xx] = true;
            }
        return o;
    }

    private static bool[] Erode(bool[] m, int w, int h, int r)
    {
        var inv = m.Select(v => !v).ToArray();
        return Dilate(inv, w, h, r).Select(v => !v).ToArray();
    }

    private static bool[] Biggest(bool[] m, int w, int h, out int pieces)
    {
        var label = new int[m.Length]; var sizes = new List<int> { 0 };
        for (var i = 0; i < m.Length; i++)
        {
            if (!m[i] || label[i] != 0) continue;
            var id = sizes.Count; var size = 0;
            var stack = new Stack<int>(); stack.Push(i); label[i] = id;
            while (stack.Count > 0)
            {
                var c = stack.Pop(); size++;
                int cx = c % w, cy = c / w;
                for (var dy = -1; dy <= 1; dy++)
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        int nx = cx + dx, ny = cy + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        var n = ny * w + nx;
                        if (m[n] && label[n] == 0) { label[n] = id; stack.Push(n); }
                    }
            }
            sizes.Add(size);
        }
        // (pieces of a few pixels -- a speck of a pen's colour in the map -- aren't counted)
        pieces = sizes.Skip(1).Count(s => s >= 30);
        if (sizes.Count == 1) { pieces = 0; return m; }
        var keep = sizes.IndexOf(sizes.Max());
        return label.Select(l => l == keep).ToArray();
    }

    // Each pixel's distance to the nearest pixel off the road (chamfer 3-4, in pixels)
    private static double[] Distance(bool[] m, int w, int h)
    {
        var d = new double[m.Length];
        const double big = 1e9;
        for (var i = 0; i < m.Length; i++) d[i] = m[i] ? big : 0;
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var i = y * w + x; if (d[i] == 0) continue;
                var v = d[i];
                if (x > 0) v = Math.Min(v, d[i - 1] + 1); else v = Math.Min(v, 1);
                if (y > 0) v = Math.Min(v, d[i - w] + 1); else v = Math.Min(v, 1);
                if (x > 0 && y > 0) v = Math.Min(v, d[i - w - 1] + 1.414);
                if (x < w - 1 && y > 0) v = Math.Min(v, d[i - w + 1] + 1.414);
                d[i] = v;
            }
        for (var y = h - 1; y >= 0; y--)
            for (var x = w - 1; x >= 0; x--)
            {
                var i = y * w + x; if (d[i] == 0) continue;
                var v = d[i];
                if (x < w - 1) v = Math.Min(v, d[i + 1] + 1); else v = Math.Min(v, 1);
                if (y < h - 1) v = Math.Min(v, d[i + w] + 1); else v = Math.Min(v, 1);
                if (x < w - 1 && y < h - 1) v = Math.Min(v, d[i + w + 1] + 1.414);
                if (x > 0 && y < h - 1) v = Math.Min(v, d[i + w - 1] + 1.414);
                d[i] = v;
            }
        return d;
    }

    private static double Percentile(bool[] m, double[] dt, double p)
    {
        var v = dt.Where((_, i) => m[i]).OrderBy(x => x).ToArray();
        return v.Length == 0 ? 1 : Math.Max(1, v[(int)Math.Min(v.Length - 1, v.Length * p)]);
    }

    // ---- the route

    private static List<(double X, double Z)> Respace(List<(double X, double Z)> pts, double spacing, bool closed)
    {
        var src = closed ? pts.Append(pts[0]).ToList() : pts;
        var o = new List<(double X, double Z)> { src[0] };
        double carry = 0;
        for (var i = 1; i < src.Count; i++)
        {
            var (ax, az) = src[i - 1]; var (bx, bz) = src[i];
            var seg = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
            var t = spacing - carry;
            while (t <= seg) { o.Add((ax + (bx - ax) * t / seg, az + (bz - az) * t / seg)); t += spacing; }
            carry = seg - (t - spacing);
        }
        if (closed && o.Count > 2)
        {
            var (lx, lz) = o[^1];
            if (Math.Sqrt((lx - o[0].X) * (lx - o[0].X) + (lz - o[0].Z) * (lz - o[0].Z)) < spacing * 0.5) o.RemoveAt(o.Count - 1);
        }
        return o;
    }

    private static List<(double X, double Z)> Smooth(List<(double X, double Z)> pts, int r, bool closed)
    {
        var n = pts.Count; var o = new List<(double X, double Z)>(n);
        for (var i = 0; i < n; i++)
        {
            if (!closed && (i < r || i >= n - r)) { o.Add(pts[i]); continue; }
            double sx = 0, sz = 0;
            for (var k = -r; k <= r; k++) { var p = pts[Wrap(i + k, n)]; sx += p.X; sz += p.Z; }
            o.Add((sx / (2 * r + 1), sz / (2 * r + 1)));
        }
        return o;
    }

    private static double[] SmoothHeights(double[] hs, int r, bool closed)
    {
        var n = hs.Length; var o = new double[n];
        for (var i = 0; i < n; i++)
        {
            double s = 0; var c = 0;
            for (var k = -r; k <= r; k++)
            {
                var j = i + k;
                if (!closed && (j < 0 || j >= n)) continue;
                s += hs[Wrap(j, n)]; c++;
            }
            o[i] = s / c;
        }
        return o;
    }

    private static double Length(List<(double X, double Z)> pts, bool closed)
    {
        double l = 0;
        for (var i = 1; i < pts.Count + (closed ? 1 : 0); i++)
        {
            var a = pts[i - 1]; var b = pts[i % pts.Count];
            l += Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z));
        }
        return l;
    }

    // The kind most drawn round a point of the road (a jump's or a loop's when it is a good share of it)
    private static SketchKind KindNear(TrackSketch s, double x, double y, double half)
    {
        int road = 0, jump = 0, loop = 0; var r = (int)Math.Ceiling(half + 1);
        for (var yy = (int)y - r; yy <= (int)y + r; yy++)
            for (var xx = (int)x - r; xx <= (int)x + r; xx++)
            {
                if (xx < 0 || yy < 0 || xx >= s.Width || yy >= s.Height) continue;
                switch (s.Kinds[yy * s.Width + xx]) { case SketchKind.Road: road++; break; case SketchKind.Jump: jump++; break; case SketchKind.Loop: loop++; break; }
            }
        var all = road + jump + loop;
        if (all == 0) return SketchKind.Road;
        if (jump >= all * 0.3 && jump >= loop) return SketchKind.Jump;
        if (loop >= all * 0.3) return SketchKind.Loop;
        return SketchKind.Road;
    }

    // The stretches of one kind, each its first point and how many (a lap's last one may run on round its start)
    private static List<(int First, int Length)> Runs(List<SketchKind> kinds, SketchKind kind, bool closed)
    {
        var n = kinds.Count; var runs = new List<(int First, int Length)>();
        if (kinds.All(k => k == kind)) return runs;
        // (a lap: begin where the kind isn't, so no run is cut in two at the start)
        var begin = closed ? kinds.FindIndex(k => k != kind) : 0;
        var i = 0;
        while (i < n)
        {
            var k = Wrap(begin + i, n);
            if (kinds[k] != kind) { i++; continue; }
            var len = 0;
            while (i < n && kinds[Wrap(begin + i, n)] == kind) { len++; i++; }
            runs.Add((k, len));
        }
        return runs;
    }

    // The middle of the longest stretch that bends least
    private static int Straightest(List<(double X, double Z)> pts, bool closed)
    {
        var n = pts.Count; var turn = new double[n];
        for (var i = 0; i < n; i++)
        {
            var a = pts[Wrap(i - 3, n)]; var b = pts[i]; var c = pts[Wrap(i + 3, n)];
            double h1 = Math.Atan2(b.Z - a.Z, b.X - a.X), h2 = Math.Atan2(c.Z - b.Z, c.X - b.X);
            var d = Math.Abs(Math.IEEERemainder(h2 - h1, 2 * Math.PI));
            turn[i] = d;
        }
        int bestStart = 0, bestLen = 0;
        for (var i = 0; i < n; i++)
        {
            if (turn[i] > 0.06) continue;
            var len = 0;
            while (len < n && turn[Wrap(i + len, n)] <= 0.06) len++;
            if (len > bestLen) { bestLen = len; bestStart = i; }
        }
        return Wrap(bestStart + bestLen / 2, n);
    }

    // The orange stroke's two ends (cells) -- the two farthest apart along it -- and its length, or null
    private static double? PitEnds(TrackSketch s, out (double X, double Z) a, out (double X, double Z) b)
    {
        a = b = default;
        int w = s.Width, h = s.Height;
        var m = new bool[w * h];
        for (var i = 0; i < m.Length; i++) m[i] = s.Kinds[i] == SketchKind.Pit;
        m = Biggest(Erode(Dilate(m, w, h, 2), w, h, 2), w, h, out var pieces);
        if (pieces == 0) return null;
        var first = Array.IndexOf(m, true);
        var (far1, _) = Farthest(m, w, h, first);
        var (far2, dist) = Farthest(m, w, h, far1);
        a = (s.OriginX + (far1 % w + 0.5) / s.Scale, s.OriginZ + (far1 / w + 0.5) / s.Scale);
        b = (s.OriginX + (far2 % w + 0.5) / s.Scale, s.OriginZ + (far2 / w + 0.5) / s.Scale);
        return dist / s.Scale;
    }

    // The pixel of the stroke farthest from `from` along it, and how far
    private static (int At, double Distance) Farthest(bool[] m, int w, int h, int from)
    {
        var d = new double[m.Length]; Array.Fill(d, double.MaxValue); d[from] = 0;
        var queue = new PriorityQueue<int, double>(); queue.Enqueue(from, 0);
        var far = from;
        while (queue.TryDequeue(out var c, out var dc))
        {
            if (dc > d[c]) continue;
            if (dc > d[far]) far = c;
            int cx = c % w, cy = c / w;
            for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    int nx = cx + dx, ny = cy + dy;
                    if ((dx == 0 && dy == 0) || nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    var n = ny * w + nx;
                    if (!m[n]) continue;
                    var nd = dc + (dx != 0 && dy != 0 ? 1.414 : 1);
                    if (nd < d[n]) { d[n] = nd; queue.Enqueue(n, nd); }
                }
        }
        return (far, d[far]);
    }
}

using System.Buffers.Binary;
using System.IO;
using System.Numerics;
using LBAAssembler.Lba1;
using LBAAssembler.Lba1.Runtime;
using LBAAssembler.Terrain.Palace;
using LBAAssembler.Terrain.Polar;
using LbaBodyStudio;

namespace LBAAssembler.Terrain.Temple;

// The Temple of Bú's LBA1 actors, as the island's decor (2026-10-18, the port's audit: "everything has been ported 100% correctly into
// 3D" -- the grid was, its actors weren't): what LBA1 shows in the temple's three scenes as they start, drawn where it shows them, in its
// colours, three times its size as everything of the port is, and touching nothing (each a decor whose box nothing bumps into).
//   - Its sprites -- the spiked wooden doors and the stone ones, the lifts' stone-grate platforms, the floor plates, the statue of Bù, the
//     wall switch scene 41 starts pulled: each a box of the size LBA1's own volume for it gives (a door: the panel the picture fits, in its
//     plane), each face of it textured with LBA1's picture unskewed onto it -- the faces LBA1's camera sees with what it draws there (a
//     door's only inside its clip window: what the doorway shows of it), the others with the opposite face's, see-through where the
//     picture has nothing; so that from LBA1's angle the box draws LBA1's picture, as TempleObjects' boxes draw its bricks'.
//   - Its 3D actors -- the electro-plants, the skeletons (asleep on their backs, and standing), the eye creatures, the bird, the spike
//     traps, the Book of Bù: LBA1's own bodies (BODY.HQR) in the pose their animation starts them in (ANIM.HQR), turned the way they face,
//     each face in the colour LBA1's light gives it (the scene's light, LBA1's own lighting: the ramp steps its normals give; Gouraud faces
//     cut in four, each its share of the shading), lines as thin bars.
// Which actors, and how: LBA1's own simulation (Lba1Runtime) runs each scene a few frames, as the game does before its first picture --
// what its life scripts hide at once is left out (the wall switches but one, which show only once pulled; the invisible fire-ball
// turrets, the hidden falling rocks), as are the actors it starts without a body (the spike cylinders, a skeleton, the mechanical penguins);
// its doors are where they are once settled (scene 41's first door slides open as it starts); the pickups (scene 41's) are left out.
// In the race track's build (TempleIsland.Build with the road's cuts) whatever would stand on or over the road's deck, or between it and
// the chase camera's height over it, is left out -- the doors the road goes through stand open, as LBA1's do when Twinsen passes.
internal static class TempleActors
{
    // the frames LBA1's simulation runs before the scene's first picture (its scripts' first passes), and before its doors have settled
    public const int StartFrames = 3, SettleFrames = 150;
    // the pickups (sprites of scene 41's: a red magic ball, a key), left out
    public static readonly int[] PickupSprites = { 5, 92 };
    // the doors' sprites: panels, in the plane their pictures fit
    private static readonly int[] DoorSprites = { 50, 51, 52 };
    // LBA1's engine draws an actor 1 pixel left of and 2 above where the bricks' pictures put its place (OBJECT.C's projection against
    // GRILLE_A's Map2Screen: a cell's corner 23 and 13 pixels from its brick's anchor, the pictures' 24 and 15): the actors moved that much
    // (LBA1's units: along x and z, on the floor)
    public static readonly Vector3 PictureShift = new(-53.3f, 0, -32f);
    // a texel: 16 of LBA1's units (32 a cell, as the bricks' tiles); a tile at most 32 x 32 texels
    private const double Texel = 16;
    private const int MaxTexels = 32;
    // a floor plate's face over the floor (LBA1's units), so that it isn't drawn into it
    private const float PlateLift = 6;
    // a line of a body: a bar this thick (LBA1's units; LBA1 draws it a pixel wide)
    private const float LineThick = 20;
    // a Gouraud face with a side this long (LBA1's units) and more is cut in four to show its shading (smaller ones in one shade)
    private const float GouraudCut = 100;
    // a door's panel: its thickness (LBA1's units) is the thinnest of these that holds its picture
    private static readonly float[] PanelThickness = { 32, 64, 96, 128 };
    private const int MaxPoints = 500, MaxFaces = 500;
    // the engine's polygon types: a textured face drawn as its texture is, and one whose colour 0 isn't drawn (TempleObjects')
    private const int TextureSolid = 8, TextureSeeThrough = 12;
    // the race track: an actor is left out when any of it is over the road's deck -- from its surface to DeckOver over it (a car's height
    // and the chase camera's) -- or, standing higher than its rails (RailTop over it), within RoadMargin cells of its edge. What lies lower
    // than the deck's surface (a floor plate it covers, a lift's platform in the pit beside it) stays: the deck hides it or it is under it.
    public const double RoadMargin = 0.5, DeckOver = 2600, RailTop = 220, DeckSurface = 4;

    // ---- what LBA1 shows as a scene starts

    // An actor as the scene starts: its sprite (or -1) or its body (BODY.HQR) and its pose (its animation's key frame), its place (LBA1's
    // world units, its scene's), the way it faces (1024 a turn), a door's clip window, the scene's light.
    public sealed record Actor(int Scene, int Index, string Kind, int Sprite, int Body, int Entity, IReadOnlyList<Lba1Animation.BoneFrame>? Pose,
        int X, int Y, int Z, int Beta, int[]? Clip, int AlphaLight, int BetaLight)
    {
        public bool IsSprite => Sprite >= 0;
        public string Name => $"{Scene}:{Index}";
    }

    private static readonly Dictionary<int, string> SpriteKinds = new()
    {
        [14] = "lift platform", [48] = "floor plate", [56] = "floor plate", [49] = "statue of Bù", [50] = "spiked door", [51] = "spiked door",
        [52] = "stone door", [53] = "wall switch", [55] = "wall switch", [25] = "wall switch",
    };
    private static readonly Dictionary<int, string> EntityKinds = new()
    {
        [28] = "skeleton", [29] = "spike trap", [30] = "spike cylinder", [31] = "electro-plant", [50] = "bird", [51] = "eye creature",
        [52] = "falling rock", [53] = "Book of Bù", [18] = "turret", [9] = "mechanical penguin",
    };

    // The temple's actors LBA1 shows as its scenes start, and a line for each kind left out.
    public static List<Actor> Read(string lba1Directory, Lba1Game game, TempleLayout layout, List<string> log)
    {
        var data = new Lba1RuntimeData(lba1Directory);
        var area = game.Areas.First(a => a.Tiles.Any(t => t.Scene == TempleLayout.FirstScene));
        var actors = new List<Actor>();
        var left = new SortedDictionary<string, List<string>>();
        void Leave(string why, string who) { if (!left.TryGetValue(why, out var l)) left[why] = l = new(); l.Add(who); }
        foreach (var placed in layout.Placements)
        {
            var s = placed.Scene;
            var tile = area.Tiles.First(t => t.Scene == s);
            var scene = game.LoadScene(s);
            var r = new Lba1Runtime(data);
            r.ChangeCube(s);
            r.Run(StartFrames);
            var start = r.Objects.Take(r.NbObjets).Select(o => (Visible: (o.Flags & Lba1Const.Invisible) == 0 && o.Body >= 0 && !o.IsDead, o.Body, o.Flags,
                Pose: o.Animation is { } an && an.Frames.Count > 0 ? an.Frames[Math.Min(o.Frame, an.Frames.Count - 1)].Bones : null)).ToArray();
            r.Run(SettleFrames - StartFrames);
            for (var n = 1; n < scene.Actors.Count && n < r.NbObjets; n++)
            {
                var src = scene.Actors[n];
                var o = r.Objects[n];
                var sprite = src.IsSprite;
                var kind = sprite ? SpriteKinds.GetValueOrDefault(src.Sprite, $"sprite {src.Sprite}") : EntityKinds.GetValueOrDefault(src.Entity, $"entity {src.Entity}");
                var who = $"{s}:{n}";
                var door = sprite && (src.StaticFlags & Lba1Const.SpriteClip) != 0;
                // (a door where it has settled; everything else where the scene places it)
                var visible = door ? (o.Flags & Lba1Const.Invisible) == 0 && o.Body >= 0 && !o.IsDead : start[n].Visible;
                var kinds = kind.EndsWith("ch") ? kind + "es" : kind + "s";
                if (!visible) { Leave(!sprite && start[n].Body < 0 ? $"{kinds} without a body as the scene starts" : $"{kinds} hidden as the scene starts", who); continue; }
                if (sprite && PickupSprites.Contains(src.Sprite)) { Leave("pickups", who); continue; }
                int x = door ? o.PosX : src.X, y = door ? o.PosY : src.Y, z = door ? o.PosZ : src.Z;
                if (!tile.HoldsPoint(x, z)) { Leave("outside the joined map's part of their scene", who); continue; }
                actors.Add(new Actor(s, n, kind, sprite ? start[n].Body : -1, sprite ? -1 : start[n].Body, src.Entity, sprite ? null : start[n].Pose,
                    x, y, z, sprite ? 0 : src.Angle & 1023, door ? new[] { o.Info, o.Info1, o.Info2, o.Info3 } : null, scene.AlphaLight, scene.BetaLight));
            }
        }
        log.Add($"LBA1's actors as the temple's scenes start: {actors.Count} shown -- " + string.Join(", ", actors.GroupBy(a => a.Kind).Select(g => $"{g.Count()} {g.Key}")) +
                "; left out: " + string.Join("; ", left.Select(l => $"{l.Value.Count} {l.Key} ({string.Join(" ", l.Value)})")));
        return actors;
    }

    // ---- the models

    // A model: points (LBA1's units from the actor's place, turned the way it faces), and faces -- a colour of the island's palette, or a
    // tile (its texels the island's colours, 0 see-through) and each corner's place in it.
    public sealed class Model
    {
        public readonly List<Vector3> Points = new();
        public readonly List<MFace> Faces = new();
        private readonly Dictionary<(int, int, int), int> index = new();
        public int P(Vector3 v)
        {
            var key = ((int)MathF.Round(v.X * 8), (int)MathF.Round(v.Y * 8), (int)MathF.Round(v.Z * 8));
            if (index.TryGetValue(key, out var i)) return i;
            Points.Add(v); return index[key] = Points.Count - 1;
        }
        // a face: its corners turned so that it faces `outward` (the plain cross product of its first corners that way: the engine's front)
        public void Add(Vector3[] corners, Vector3 outward, int colour, Tile? tile = null, (double U, double V)[]? uv = null)
        {
            var ids = corners.Select(P).ToArray();
            var keep = Enumerable.Range(0, ids.Length).Where(i => !ids.Take(i).Contains(ids[i])).ToArray();
            if (keep.Length < 3) return;
            ids = keep.Select(i => ids[i]).ToArray(); corners = keep.Select(i => corners[i]).ToArray(); uv = uv is null ? null : keep.Select(i => uv[i]).ToArray();
            var n = Vector3.Cross(corners[1] - corners[0], corners[2] - corners[0]);
            if (Vector3.Dot(n, outward) < 0) { Array.Reverse(ids); if (uv is not null) Array.Reverse(uv); }
            Faces.Add(new MFace(ids, colour, tile, uv));
        }
        public (Vector3 Lo, Vector3 Hi) Box()
        {
            var lo = new Vector3(float.MaxValue); var hi = new Vector3(float.MinValue);
            foreach (var p in Points) { lo = Vector3.Min(lo, p); hi = Vector3.Max(hi, p); }
            return (lo, hi);
        }
    }
    public sealed record Tile(int W, int H, int[] Pixels, bool Solid, string Key);
    public readonly record struct MFace(int[] Ids, int Colour, Tile? Tile, (double U, double V)[]? Uv);

    // ---- sprites

    public sealed record SpriteInfo(PolarTextures.Sprite Picture, int OffX, int OffY, (int X0, int X1, int Y0, int Y1, int Z0, int Z1) Volume);

    public static SpriteInfo? ReadSprite(string lba1Directory, Lba1RuntimeData data, int sprite)
    {
        var path = Path.Combine(lba1Directory, "SPRITES.HQR");
        if (!File.Exists(path)) return null;
        var archive = HqrArchive.Open(path);
        if (!archive.IsValid(sprite)) return null;
        var entry = archive.Read(sprite);
        var start = entry.Length >= 8 ? (int)BinaryPrimitives.ReadUInt32LittleEndian(entry) : entry.Length;
        var picture = PolarTextures.Sprite.Decode(start + 4 <= entry.Length ? entry[start..] : null);
        var (ox, oy) = data.SpriteOffset(sprite);
        if (picture.Width == 0 || data.SpriteVolume(sprite) is not { } v) return null;
        return new SpriteInfo(picture, ox, oy, (v.XMin, v.XMax, v.YMin, v.YMax, v.ZMin, v.ZMax));
    }

    // Where LBA1 draws a point (LBA1's units from the actor's place) relative to the actor's own projection: its picture's pixel there is
    // the sprite's at this minus its offset.
    private static (double X, double Y) Screen(Vector3 q) => (24 * (q.X - q.Z) / 512.0, 12 * (q.X + q.Z) / 512.0 - 15 * q.Y / 256.0);

    // The colour LBA1 draws at a point of the actor's box -- its sprite's pixel there, -1 where it draws nothing (or a door's clip window
    // hides it).
    private static int Sample(Actor a, SpriteInfo s, Vector3 q)
    {
        var (u, v) = Screen(q);
        int i = (int)Math.Floor(u - s.OffX), j = (int)Math.Floor(v - s.OffY);
        var c = s.Picture.At(i, j);
        if (c < 0 || a.Clip is not { } clip) return c;
        // (the clip window: screen coordinates from the projection of the scene's origin, OBJECT.C SetClip(Info + XpOrgw, ...))
        int px = (int)Math.Floor((a.X - a.Z) * 24 / 512.0) + s.OffX + i, py = (int)Math.Floor(((a.X + a.Z) * 12 - a.Y * 30) / 512.0) + s.OffY + j;
        return px >= clip[0] && px <= clip[2] && py >= clip[1] && py <= clip[3] ? c : -1;
    }

    // Whether LBA1's view ray through a pixel of the sprite (its picture's) hits a box (LBA1's units from the actor's place).
    private static bool Covers((float X0, float X1, float Y0, float Y1, float Z0, float Z1) b, double u, double v)
    {
        // (the ray: the points of the screen point (u, v), along LBA1's view (1, 0.8, 1) -- from the point of it at y 0)
        double sum = 512 * v / 12, diff = 512 * u / 24;
        double qx = (sum + diff) / 2, qz = (sum - diff) / 2;
        double lo = double.MinValue, hi = double.MaxValue;
        void Slab(double p, double d, double a0, double a1)
        {
            var t0 = (a0 - p) / d; var t1 = (a1 - p) / d;
            lo = Math.Max(lo, Math.Min(t0, t1)); hi = Math.Min(hi, Math.Max(t0, t1));
        }
        Slab(qx, 1, b.X0, b.X1); Slab(0, 0.8, b.Y0, b.Y1); Slab(qz, 1, b.Z0, b.Z1);
        return lo <= hi;
    }

    // The share of the pixels a sprite draws (inside its clip window) that a box holds.
    private static double Coverage(Actor a, SpriteInfo s, (float X0, float X1, float Y0, float Y1, float Z0, float Z1) b)
    {
        int drawn = 0, held = 0;
        var p = s.Picture;
        for (var j = 0; j < p.Lines; j++)
            for (var i = 0; i < p.Width; i++)
            {
                if (p.Pixels[j * p.Width + i] < 0) continue;
                double u = i + 0.5 + s.OffX, v = j + 0.5 + s.OffY;
                if (a.Clip is { } clip)
                {
                    int px = (int)Math.Floor((a.X - a.Z) * 24 / 512.0) + s.OffX + i, py = (int)Math.Floor(((a.X + a.Z) * 12 - a.Y * 30) / 512.0) + s.OffY + j;
                    if (px < clip[0] || px > clip[2] || py < clip[1] || py > clip[3]) continue;
                }
                drawn++;
                if (Covers(b, u, v)) held++;
            }
        return drawn == 0 ? 0 : (double)held / drawn;
    }

    // A sprite's box (LBA1's units from the actor's place): its volume -- but a floor plate's, a face just over the floor; and a door's, the
    // panel in it its picture fits: as long and as tall as the volume, as thin as will hold its picture, where it holds the most of it.
    public static ((float X0, float X1, float Y0, float Y1, float Z0, float Z1) Box, bool Plate, double Held) BoxOf(Actor a, SpriteInfo s)
    {
        var v = s.Volume;
        (float, float, float, float, float, float) whole = (v.X0, v.X1, v.Y0, v.Y1, v.Z0, v.Z1);
        if (v.Y1 - v.Y0 <= 48 && v.Y1 <= 0) return ((v.X0, v.X1, PlateLift, PlateLift, v.Z0, v.Z1), true, 1);
        int dx = v.X1 - v.X0, dz = v.Z1 - v.Z0;
        if (!DoorSprites.Contains(a.Sprite) || Math.Max(dx, dz) < 1.5 * Math.Min(dx, dz)) { var (g, gh) = Grow(a, s, whole, Coverage(a, s, whole)); return (g, false, gh); }
        var alongX = dx > dz;
        var best = (Box: whole, Held: -1.0);
        foreach (var t in PanelThickness)
        {
            var (a0, a1) = alongX ? (v.Z0, v.Z1) : (v.X0, v.X1);
            for (var at = (float)a0; at + t <= a1 + 0.01f; at += 8)
            {
                (float, float, float, float, float, float) box = alongX ? (v.X0, v.X1, v.Y0, v.Y1, at, at + t) : (at, at + t, v.Y0, v.Y1, v.Z0, v.Z1);
                var held = Coverage(a, s, box);
                if (held > best.Held + 1e-9) best = (box, held);
            }
            if (best.Held >= 0.995) break;
        }
        var (grown, grownHeld) = Grow(a, s, best.Box, best.Held);
        return (grown, false, grownHeld);
    }

    // A box grown a step (16 of LBA1's units) at a time on the side that holds the most more of the picture, while it holds less than all
    // but a few pixels of it (a statue's ear over its volume, a platform's corner pixels).
    private static ((float X0, float X1, float Y0, float Y1, float Z0, float Z1) Box, double Held) Grow(Actor a, SpriteInfo s,
        (float X0, float X1, float Y0, float Y1, float Z0, float Z1) box, double held)
    {
        var b = new[] { box.X0, box.X1, box.Y0, box.Y1, box.Z0, box.Z1 };
        (float, float, float, float, float, float) T(float[] c) => (c[0], c[1], c[2], c[3], c[4], c[5]);
        for (var step = 0; step < 16 && held < 0.995; step++)
        {
            var (bestSide, bestHeld) = (-1, held);
            for (var side = 0; side < 6; side++)
            {
                var g = (float[])b.Clone();
                g[side] += side % 2 == 0 ? -16 : 16;
                var h = Coverage(a, s, T(g));
                if (h > bestHeld + 1e-4) (bestSide, bestHeld) = (side, h);
            }
            if (bestSide < 0) break;
            b[bestSide] += bestSide % 2 == 0 ? -16 : 16;
            held = bestHeld;
        }
        return (T(b), held);
    }

    // A sprite actor's model: its box, each face cut in tiles of at most a cell (32 texels) each way, the faces LBA1's camera sees (the
    // top, +x, +z) textured with what it draws there, the others with the opposite face's (as seen through the box), a floor plate's only
    // its top. Faces whose tiles draw nothing are left out.
    public static Model SpriteModel(Actor a, SpriteInfo s, int[] map)
    {
        var (b, plate, _) = BoxOf(a, s);
        var m = new Model();
        Vector3 V(float x, float y, float z) => new(x, y, z);
        // each face: its corners (u 0 v 0, u 1 v 0, u 0 v 1), the way out, and the point whose picture a point of it shows
        var faces = new List<(Vector3 C0, Vector3 C1, Vector3 C3, Vector3 Out, Func<Vector3, Vector3> Look)>
        {
            (V(b.X0, b.Y1, b.Z0), V(b.X1, b.Y1, b.Z0), V(b.X0, b.Y1, b.Z1), Vector3.UnitY, q => q),
        };
        if (!plate)
        {
            faces.Add((V(b.X1, b.Y1, b.Z0), V(b.X1, b.Y1, b.Z1), V(b.X1, b.Y0, b.Z0), Vector3.UnitX, q => q));
            faces.Add((V(b.X0, b.Y1, b.Z1), V(b.X1, b.Y1, b.Z1), V(b.X0, b.Y0, b.Z1), Vector3.UnitZ, q => q));
            faces.Add((V(b.X0, b.Y1, b.Z0), V(b.X0, b.Y1, b.Z1), V(b.X0, b.Y0, b.Z0), -Vector3.UnitX, q => q with { X = b.X1 }));
            faces.Add((V(b.X0, b.Y1, b.Z0), V(b.X1, b.Y1, b.Z0), V(b.X0, b.Y0, b.Z0), -Vector3.UnitZ, q => q with { Z = b.Z1 }));
            faces.Add((V(b.X0, b.Y0, b.Z0), V(b.X1, b.Y0, b.Z0), V(b.X0, b.Y0, b.Z1), -Vector3.UnitY, q => q with { Y = b.Y1 }));
        }
        foreach (var (c0, c1, c3, outward, look) in faces)
        {
            var U = c1 - c0; var W = c3 - c0;
            int nu = Math.Max(1, (int)Math.Ceiling(U.Length() / (Texel * MaxTexels) - 1e-6)), nv = Math.Max(1, (int)Math.Ceiling(W.Length() / (Texel * MaxTexels) - 1e-6));
            for (var tv = 0; tv < nv; tv++)
                for (var tu = 0; tu < nu; tu++)
                {
                    var a0 = c0 + U * (tu / (float)nu) + W * (tv / (float)nv);
                    var du = U / nu; var dv = W / nv;
                    int w = Math.Clamp((int)Math.Ceiling(du.Length() / Texel - 1e-6), 1, MaxTexels), h = Math.Clamp((int)Math.Ceiling(dv.Length() / Texel - 1e-6), 1, MaxTexels);
                    var pixels = new int[w * h];
                    var drawn = 0;
                    for (var j = 0; j < h; j++)
                        for (var i = 0; i < w; i++)
                        {
                            var q = look(a0 + du * (float)((i + 0.5) / w) + dv * (float)((j + 0.5) / h));
                            var c = Sample(a, s, q);
                            pixels[j * w + i] = c < 0 ? 0 : map[c];
                            if (c >= 0) drawn++;
                        }
                    if (drawn == 0) continue;
                    var key = $"a{w}x{h}:{Convert.ToBase64String(System.Security.Cryptography.SHA1.HashData(pixels.Select(p => (byte)p).ToArray()))}";
                    var tile = new Tile(w, h, pixels, drawn == pixels.Length, key);
                    m.Add(new[] { a0, a0 + du, a0 + du + dv, a0 + dv }, outward, -1, tile, new[] { (0.0, 0.0), (1.0, 0.0), (1.0, 1.0), (0.0, 1.0) });
                }
        }
        return m;
    }

    // ---- the statue

    // the statue of Bù's sprite: a figure, not a box (a box with its picture on each side is a crate with a statue painted on it)
    private const int StatueSprite = 49;
    // its rings: every FigureStep of LBA1's units up, FigureSides points round; its voxels FigureVoxel across
    private const int FigureStep = 64, FigureSides = 16, FigureVoxel = 24;
    private const float FigureTolerance = 16;

    // The statue (TempleModels' figures' way, its picture a sprite's): a figure is the same either side of the way it faces, so its other
    // side's view is LBA1's picture mirrored -- what both views leave of its box (a voxel whose point falls on the picture's figure from
    // LBA1's angle, and whose mirror image across the box's middle, along x or along z -- the one leaving more -- does too) is its shape:
    // ring by ring up it, FigureSides points round each ring's middle, each as far out that way as the shape reaches; each side a strip of
    // tiles up it, its texels the picture's where the view that sees that side best (LBA1's, or the mirror image's) sees them.
    public static Model FigureModel(Actor a, SpriteInfo s, int[] map)
    {
        var (b, _, _) = BoxOf(a, s);
        var p = s.Picture;
        bool R(int x, int y) => p.At(x, y) >= 0;
        // (the picture's figure, its dithering's gaps of two pixels closed)
        bool Seen(Vector3 q)
        {
            var (u, v) = Screen(q);
            int x = (int)Math.Floor(u - s.OffX), y = (int)Math.Floor(v - s.OffY);
            return R(x, y) || (R(x - 1, y) || R(x - 2, y)) && (R(x + 1, y) || R(x + 2, y)) || (R(x, y - 1) || R(x, y - 2)) && (R(x, y + 1) || R(x, y + 2));
        }
        float mx = (b.X0 + b.X1) / 2, mz = (b.Z0 + b.Z1) / 2;
        Vector3 FlipX(Vector3 q) => q with { X = 2 * mx - q.X };
        Vector3 FlipZ(Vector3 q) => q with { Z = 2 * mz - q.Z };
        int nx = (int)Math.Ceiling((b.X1 - b.X0) / FigureVoxel), nz = (int)Math.Ceiling((b.Z1 - b.Z0) / FigureVoxel);
        Vector3 Voxel(int i, float y, int k) => new(b.X0 + (i + 0.5f) * FigureVoxel, y, b.Z0 + (k + 0.5f) * FigureVoxel);
        int Count(Func<Vector3, Vector3> flip)
        {
            var n = 0;
            for (var y = b.Y0 + FigureVoxel / 2f; y < b.Y1; y += FigureVoxel)
                for (var i = 0; i < nx; i++) for (var k = 0; k < nz; k++) { var q = Voxel(i, y, k); if (Seen(q) && Seen(flip(q))) n++; }
            return n;
        }
        var mirrorX = Count(FlipX) >= Count(FlipZ);
        Func<Vector3, Vector3> flip = mirrorX ? FlipX : FlipZ;
        var view1 = Vector3.Normalize(new Vector3(1, 0, 1));
        var view2 = mirrorX ? Vector3.Normalize(new Vector3(-1, 0, 1)) : Vector3.Normalize(new Vector3(1, 0, -1));

        // ---- the rings: at each height, the middle of the shape's voxels near it, and its reach each way round (the slice's outline's
        // farthest point that way); a height the shape has nothing at, a point in the middle of its neighbours'
        var heights = new List<float>();
        for (var y = b.Y0; y < b.Y1 - 1; y += FigureStep) heights.Add(y);
        heights.Add(b.Y1);
        var rings = new List<Vector3[]>();
        var middles = new List<Vector3?>();
        foreach (var y in heights)
        {
            var pts = new List<Vector3>();
            for (var dy = -FigureStep / 2f + FigureVoxel / 2f; dy < FigureStep / 2f; dy += FigureVoxel)
                for (var i = 0; i < nx; i++) for (var k = 0; k < nz; k++)
                {
                    var q = Voxel(i, Math.Clamp(y + dy, b.Y0 + 1, b.Y1 - 1), k);
                    if (Seen(q) && Seen(flip(q))) pts.Add(q with { Y = y });
                }
            middles.Add(pts.Count == 0 ? null : new Vector3(pts.Average(q => q.X), y, pts.Average(q => q.Z)));
            if (pts.Count == 0) { rings.Add(Array.Empty<Vector3>()); continue; }
            var c = middles[^1]!.Value;
            // (each way's reach: the farthest of the voxels in its sector of the ring -- the outline's dents kept, an ear or an arm sticking
            // out; a sector with none, the nearest way's)
            var reaches = Enumerable.Range(0, FigureSides).Select(k =>
            {
                var a0 = k * MathF.Tau / FigureSides;
                var inSector = pts.Where(q =>
                {
                    var d = q - c;
                    if (d.X * d.X + d.Z * d.Z < 1) return true;
                    var diff = MathF.Atan2(d.Z, d.X) - a0;
                    diff = (diff % MathF.Tau + MathF.Tau * 1.5f) % MathF.Tau - MathF.PI;
                    return MathF.Abs(diff) <= MathF.PI / FigureSides + 1e-4f;
                }).ToList();
                var way = new Vector3(MathF.Cos(a0), 0, MathF.Sin(a0));
                return inSector.Count == 0 ? -1f : inSector.Max(q => Vector3.Dot(q - c, way)) + FigureVoxel / 2f;
            }).ToArray();
            for (var k = 0; k < FigureSides; k++)
                if (reaches[k] < 0)
                    reaches[k] = Enumerable.Range(1, FigureSides / 2).SelectMany(o => new[] { reaches[(k + o) % FigureSides], reaches[(k - o + FigureSides) % FigureSides] }).FirstOrDefault(r => r >= 0, FigureVoxel / 2f);
            rings.Add(Enumerable.Range(0, FigureSides).Select(k =>
                c + new Vector3(MathF.Cos(k * MathF.Tau / FigureSides), 0, MathF.Sin(k * MathF.Tau / FigureSides)) * Math.Max(4, reaches[k])).ToArray());
        }
        // (an empty ring: a point at its neighbours' middle, or the box's)
        for (var r = 0; r < rings.Count; r++)
            if (rings[r].Length == 0)
            {
                var near = middles.Select((m, i) => (m, i)).Where(t => t.m is not null).OrderBy(t => Math.Abs(t.i - r)).Select(t => t.m!.Value).FirstOrDefault(new Vector3(mx, 0, mz));
                rings[r] = Enumerable.Repeat(near with { Y = heights[r] }, FigureSides).ToArray();
            }
        // (the rings kept where the shape turns: one is left out where those from the last kept one to the next lie within FigureTolerance
        // of the straight way between them -- but at a tile's edge, where the sides' strips of tiles are cut)
        var tileHeight = (float)(Texel * MaxTexels);
        var kept = new List<int> { 0 };
        for (var r = 1; r < rings.Count - 1; r++)
        {
            if (Math.Abs((b.Y1 - heights[r]) % tileHeight) < 0.5f) { kept.Add(r); continue; }
            int k0 = kept[^1], k1 = r + 1;
            var straight = true;
            for (var q = k0 + 1; q < k1 && straight; q++)
            {
                var t = (heights[q] - heights[k0]) / (heights[k1] - heights[k0]);
                for (var k = 0; k < FigureSides && straight; k++)
                    straight = Vector3.Distance(Vector3.Lerp(rings[k0][k], rings[k1][k], t), rings[q][k]) <= FigureTolerance;
            }
            if (!straight) kept.Add(r);
        }
        kept.Add(rings.Count - 1);
        rings = kept.Select(r => rings[r]).ToList();
        heights = kept.Select(r => heights[r]).ToList();

        // ---- each side a strip of tiles up it (a tile at most MaxTexels texels tall: the rings meet the tiles' edges)
        var m = new Model();
        for (var k = 0; k < FigureSides; k++)
        {
            int k2 = (k + 1) % FigureSides;
            var outward = new Vector3(MathF.Cos((k + 0.5f) * MathF.Tau / FigureSides), 0, MathF.Sin((k + 0.5f) * MathF.Tau / FigureSides));
            var mirrored = Vector3.Dot(outward, view2) > Vector3.Dot(outward, view1);
            // (a point of the side at height y, u across it)
            Vector3 At(float y, float u)
            {
                var r = Math.Clamp(heights.FindLastIndex(hy => hy <= y + 0.01f), 0, heights.Count - 2);
                var t = Math.Clamp((y - heights[r]) / (heights[r + 1] - heights[r]), 0, 1);
                var left = Vector3.Lerp(rings[r][k], rings[r + 1][k], t); var right = Vector3.Lerp(rings[r][k2], rings[r + 1][k2], t);
                return Vector3.Lerp(left, right, u);
            }
            for (var top = b.Y1; top > b.Y0 + 0.5f; top -= tileHeight)
            {
                var bottom = Math.Max(b.Y0, top - tileHeight);
                // (its width: the side's widest across the tile's rings)
                var width = heights.Select((hy, r) => (hy, r)).Where(t => t.hy >= bottom - 0.5f && t.hy <= top + 0.5f).Max(t => Vector3.Distance(rings[t.r][k], rings[t.r][k2]));
                int w = Math.Clamp((int)Math.Ceiling(width / Texel), 2, MaxTexels), h = Math.Clamp((int)Math.Ceiling((top - bottom) / Texel - 1e-6), 1, MaxTexels);
                var pixels = new int[w * h];
                var drawn = new List<(int I, int J, int C)>();
                for (var j = 0; j < h; j++)
                    for (var i = 0; i < w; i++)
                    {
                        var q = At(top - (j + 0.5f) / h * (top - bottom), (i + 0.5f) / w);
                        var c = Sample(a, s, mirrored ? flip(q) : q);
                        pixels[j * w + i] = c;
                        if (c >= 0) drawn.Add((i, j, c));
                    }
                if (drawn.Count == 0) continue;
                // (solid: a gap the nearest drawn texel's colour)
                for (var t = 0; t < pixels.Length; t++)
                {
                    if (pixels[t] >= 0) { pixels[t] = map[pixels[t]]; continue; }
                    int i = t % w, j = t / w;
                    pixels[t] = map[drawn.MinBy(d => (d.I - i) * (d.I - i) + (d.J - j) * (d.J - j)).C];
                }
                var key = $"f{w}x{h}:{Convert.ToBase64String(System.Security.Cryptography.SHA1.HashData(pixels.Select(v => (byte)v).ToArray()))}";
                var tile = new Tile(w, h, pixels, true, key);
                // (its quads: between the rings in it)
                for (var r = 0; r + 1 < heights.Count; r++)
                {
                    float y0 = heights[r], y1 = heights[r + 1];
                    if (y0 < bottom - 0.5f || y1 > top + 0.5f) continue;
                    double v0 = (top - y1) / (top - bottom), v1 = (top - y0) / (top - bottom);
                    m.Add(new[] { rings[r + 1][k], rings[r + 1][k2], rings[r][k2], rings[r][k] }, outward, -1, tile, new[] { (0.0, v0), (1.0, v0), (1.0, v1), (0.0, v1) });
                }
            }
        }
        // the top and the bottom: fans in the colour the picture has at the top's middle
        var topMiddle = rings[^1].Aggregate(Vector3.Zero, (acc, q) => acc + q) / FigureSides;
        var capColour = Sample(a, s, topMiddle) is >= 0 and var cc ? map[cc] : map[Math.Max(0, p.Pixels.FirstOrDefault(v => v >= 0))];
        foreach (var (r, up) in new[] { (rings.Count - 1, 1f), (0, -1f) })
        {
            var ring = rings[r];
            var c = ring.Aggregate(Vector3.Zero, (acc, q) => acc + q) / ring.Length;
            for (var k = 0; k < FigureSides; k++) m.Add(new[] { c, ring[k], ring[(k + 1) % FigureSides] }, new Vector3(0, up, 0), capColour);
        }
        return m;
    }

    // ---- bodies

    // A body actor's model: LBA1's body in its pose, turned the way it faces, each face in the colour LBA1's light gives it.
    public static Model? BodyModel(Actor a, Body body, int[] map)
    {
        var bones = (a.Pose ?? Array.Empty<Lba1Animation.BoneFrame>()).Select(f => (f.Type, (double)f.X, (double)f.Y, (double)f.Z)).ToList();
        var posed = Lba1Pose.World(body, bones, out var matrices);
        double beta = a.Beta * Math.PI * 2 / 1024, sy = Math.Sin(beta), cy = Math.Cos(beta);
        Vector3 Turn(Vector3 p) => new((float)(p.X * cy + p.Z * sy), p.Y, (float)(-p.X * sy + p.Z * cy));
        var pts = posed.Select(Turn).ToArray();
        var light = Intensities(body, matrices, a.Beta, a.AlphaLight, a.BetaLight);
        int Colour(int lba1) => map[Math.Clamp(lba1, 0, 255)];
        var m = new Model();
        foreach (var f in body.Faces)
        {
            if (f.Points.Length < 3) continue;
            var corners = f.Points.Select(i => pts[i]).ToArray();
            // (out of the surface: the corners' Newell normal, LBA1's polygons wound so -- TempleHalloween's skeleton's Outward)
            var n = Vector3.Zero;
            for (var i = 0; i < corners.Length; i++)
            {
                var p = corners[i]; var q = corners[(i + 1) % corners.Length];
                n += new Vector3((p.Y - q.Y) * (p.Z + q.Z), (p.Z - q.Z) * (p.X + q.X), (p.X - q.X) * (p.Y + q.Y));
            }
            if (n.LengthSquared() < 1e-6f) continue;
            if (f.Material >= 9 && f.PointNormals is { } pn && light.Length > 0)
            {
                // Gouraud: each corner its normal's light; a face whose corners' shades differ by a step or more and that is big enough to
                // show it (a side GouraudCut long) cut in four (a fan of triangles first, past four corners), each piece the shade at its
                // middle; else the face in its corners' mean shade
                var shade = pn.Select(k => k >= 0 && k < light.Length ? light[k] : 0f).ToArray();
                var longest = corners.Select((c, i) => Vector3.Distance(c, corners[(i + 1) % corners.Length])).Max();
                if (shade.Max() - shade.Min() >= 1 && longest >= GouraudCut)
                    foreach (var (c, s) in Pieces(corners, shade))
                        m.Add(c, n, Colour(f.Colour + (int)Math.Round(s)));
                else
                {
                    var mean = Colour(f.Colour + (int)Math.Round(shade.Average()));
                    if (corners.Length > 4)
                        for (var k = 1; k + 1 < corners.Length; k++) m.Add(new[] { corners[0], corners[k], corners[k + 1] }, n, mean);
                    else m.Add(corners, n, mean);
                }
                continue;
            }
            var lit = f.Material is 7 or 8 && f.FaceNormal >= 0 && f.FaceNormal < light.Length ? (int)light[f.FaceNormal] : 0;
            if (corners.Length > 4)
                for (var k = 1; k + 1 < corners.Length; k++) m.Add(new[] { corners[0], corners[k], corners[k + 1] }, n, Colour(f.Colour + lit));
            else m.Add(corners, n, Colour(f.Colour + lit));
        }
        foreach (var l in body.Lines) Bar(m, pts[l.A], pts[l.B], LineThick, Colour(l.Colour));
        foreach (var s in body.Spheres) Ball(m, pts[s.Point], Math.Max(LineThick / 2, s.Radius), Colour(s.Colour));
        return m.Faces.Count == 0 ? null : m;
    }

    // A face's pieces and each one's shade: a triangle in four (its corners and the middles of its sides), a quad in four (and its middle),
    // more corners a fan of triangles each in four.
    private static IEnumerable<(Vector3[] Corners, float Shade)> Pieces(Vector3[] c, float[] s)
    {
        if (c.Length == 3)
        {
            Vector3 ab = (c[0] + c[1]) / 2, bc = (c[1] + c[2]) / 2, ca = (c[2] + c[0]) / 2;
            float sab = (s[0] + s[1]) / 2, sbc = (s[1] + s[2]) / 2, sca = (s[2] + s[0]) / 2;
            yield return (new[] { c[0], ab, ca }, (s[0] + sab + sca) / 3);
            yield return (new[] { ab, c[1], bc }, (sab + s[1] + sbc) / 3);
            yield return (new[] { ca, bc, c[2] }, (sca + sbc + s[2]) / 3);
            yield return (new[] { ab, bc, ca }, (sab + sbc + sca) / 3);
        }
        else if (c.Length == 4)
        {
            Vector3 ab = (c[0] + c[1]) / 2, bc = (c[1] + c[2]) / 2, cd = (c[2] + c[3]) / 2, da = (c[3] + c[0]) / 2, mid = (c[0] + c[1] + c[2] + c[3]) / 4;
            float sab = (s[0] + s[1]) / 2, sbc = (s[1] + s[2]) / 2, scd = (s[2] + s[3]) / 2, sda = (s[3] + s[0]) / 2, smid = (s[0] + s[1] + s[2] + s[3]) / 4;
            yield return (new[] { c[0], ab, mid, da }, (s[0] + sab + smid + sda) / 4);
            yield return (new[] { ab, c[1], bc, mid }, (sab + s[1] + sbc + smid) / 4);
            yield return (new[] { mid, bc, c[2], cd }, (smid + sbc + s[2] + scd) / 4);
            yield return (new[] { da, mid, cd, c[3] }, (sda + smid + scd + s[3]) / 4);
        }
        else
            for (var k = 1; k + 1 < c.Length; k++)
                foreach (var piece in Pieces(new[] { c[0], c[k], c[k + 1] }, new[] { s[0], s[k], s[k + 1] })) yield return piece;
    }

    // LBA1's light on each of a body's normals (LIB_3D P_OB_ISO.ASM ComputeAnimNormal, as BodyStudio's Lba1Shading has it): turned by its
    // bone's matrix and by the actor's facing, dotted with the scene's light, in steps of its ramp (0 .. its range).
    private static float[] Intensities(Body body, double[][] matrices, int actorBeta, int alphaLight, int betaLight)
    {
        static double Rad(double a) => a * Math.PI * 2 / 1024;
        double sa = Math.Sin(Rad(alphaLight)), ca = Math.Cos(Rad(alphaLight)), sb = Math.Sin(Rad(betaLight)), cb = Math.Cos(Rad(betaLight));
        double lx = sb, ly = -sa * cb, lz = ca * cb;
        double sy = Math.Sin(Rad(actorBeta)), cy = Math.Cos(Rad(actorBeta));
        var result = new float[body.Normals.Count];
        for (var i = 0; i < result.Length; i++)
        {
            var n = body.Normals[i];
            var m = matrices.Length == 0 ? new double[] { 1, 0, 0, 0, 1, 0, 0, 0, 1 } : matrices[Math.Clamp(i < body.NormalBone.Length ? body.NormalBone[i] : 0, 0, matrices.Length - 1)];
            double x = m[0] * n.X + m[1] * n.Y + m[2] * n.Z, y = m[3] * n.X + m[4] * n.Y + m[5] * n.Z, z = m[6] * n.X + m[7] * n.Y + m[8] * n.Z;
            double wx = cy * x + sy * z, wz = -sy * x + cy * z;
            var dot = wx * lx + y * ly + wz * lz;
            result[i] = dot <= 0 || n.Range == 0 ? 0 : (float)Math.Floor(dot * 59 / n.Range);
        }
        return result;
    }

    // a bar from a to b, t thick (its four sides: its ends, a line's width across, are never seen)
    private static void Bar(Model m, Vector3 a, Vector3 b, float t, int colour)
    {
        var d = b - a;
        if (d.LengthSquared() < 1) return;
        d = Vector3.Normalize(d);
        var u = Vector3.Normalize(Vector3.Cross(d, Math.Abs(d.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY)) * t / 2;
        var v = Vector3.Normalize(Vector3.Cross(d, u)) * t / 2;
        var cs = new[] { u + v, u - v, -u - v, -u + v };
        var mid = (a + b) / 2;
        for (var k = 0; k < 4; k++)
        {
            var q = new[] { a + cs[k], a + cs[(k + 1) % 4], b + cs[(k + 1) % 4], b + cs[k] };
            m.Add(q, (q[0] + q[2]) / 2 - mid, colour);
        }
    }

    // a ball: an octahedron r across
    private static void Ball(Model m, Vector3 c, float r, int colour)
    {
        var axes = new[] { Vector3.UnitX, Vector3.UnitZ, -Vector3.UnitX, -Vector3.UnitZ };
        foreach (var pole in new[] { Vector3.UnitY, -Vector3.UnitY })
            for (var k = 0; k < 4; k++)
            {
                var q = new[] { c + pole * r, c + axes[k] * r, c + axes[(k + 1) % 4] * r };
                m.Add(q, (q[0] + q[1] + q[2]) / 3 - c, colour);
            }
    }

    // ---- into the island

    public sealed record Result(List<byte[]> Bodies, int Decors, int Faces, int Pages, List<string> Log);

    // The actors' decors into the island (its cubes, its object pages after the temple's: `island.ObjectPages` as TempleObjects left them),
    // their bodies numbered from `firstBody`. `race`: the race track's build -- the actors on or over its road (RaceTrackPlan.Built's) left out,
    // and those in the cells it fills (`walls`, the plan's TempleWalls: the hidden passage's stone door, walled up).
    public static Result Place(string lba1Directory, Lba1Game game, TempleLayout layout, IslandFile island, TempleFrame frame, int[] map, int firstBody, bool race,
        IEnumerable<int[]>? walls = null)
    {
        var log = new List<string>();
        List<Actor> actors;
        // (LBA1's own simulation runs its scenes: whatever goes wrong there, the temple is built without its actors, and says so)
        try { actors = Read(lba1Directory, game, layout, log); }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            log.Add($"WARNING: LBA1's actors not read ({e.GetType().Name}: {e.Message}): the temple without them");
            return new Result(new List<byte[]>(), 0, 0, 0, log);
        }
        var data = new Lba1RuntimeData(lba1Directory);
        var sprites = new Dictionary<int, SpriteInfo?>();
        var bodies = new Dictionary<int, Body?>();
        var made = new List<(Actor Actor, Model Model, Vector3 Place)>();
        foreach (var a in actors)
        {
            Model? m;
            try
            {
                if (a.IsSprite)
                {
                    if (!sprites.TryGetValue(a.Sprite, out var s)) sprites[a.Sprite] = s = ReadSprite(lba1Directory, data, a.Sprite);
                    m = s is null ? null : a.Sprite == StatueSprite ? FigureModel(a, s, map) : SpriteModel(a, s, map);
                }
                else
                {
                    if (!bodies.TryGetValue(a.Body, out var b)) bodies[a.Body] = b = game.ReadBody(a.Body) is { } bytes ? Body.Read(bytes, 1) : null;
                    m = b is null ? null : BodyModel(a, b, map);
                }
                if (m is not null && (m.Points.Count > MaxPoints || m.Faces.Count > MaxFaces))
                {
                    log.Add($"WARNING: {a.Name} ({a.Kind}): {m.Points.Count} points and {m.Faces.Count} faces, more than a body holds: left out");
                    continue;
                }
            }
            catch (Exception e) when (e is InvalidDataException or IndexOutOfRangeException or ArgumentException or InvalidOperationException)
            {
                log.Add($"WARNING: {a.Name} ({a.Kind}) not made ({e.Message})");
                continue;
            }
            if (m is null || m.Faces.Count == 0) { log.Add($"{a.Name} ({a.Kind}): nothing of it shows{(a.Clip is not null ? " (open, slid into the wall)" : "")}"); continue; }
            var placed = layout.Placements.First(p => p.Scene == a.Scene);
            var (wx, wy, wz) = frame.FromLba1(placed, a.X, a.Y, a.Z);
            made.Add((a, m, new Vector3((float)wx, (float)wy, (float)wz) + PictureShift * TempleFrame.Scale));
        }

        // the race track: what comes near its road's deck left out
        if (race)
        {
            var plan = RaceTrackPlan.Built(RaceTrackIsland.Temple);
            var kept = new List<(Actor, Model, Vector3)>();
            var gone = new List<string>();
            // (the cells the race fills: island world units)
            var filled = (walls ?? Array.Empty<int[]>()).Where(w => w.Length >= 4)
                .Select(w => (Lo: new Vector3((float)frame.WorldX(w[0]), (float)frame.WorldY(w[2]), (float)((TempleFrame.OffsetZ + w[1] * TempleFrame.Scale) * 512.0)),
                              Hi: new Vector3((float)frame.WorldX(w[0] + 1), (float)frame.WorldY(w[3] + 1), (float)((TempleFrame.OffsetZ + (w[1] + 1) * TempleFrame.Scale) * 512.0))))
                .ToList();
            foreach (var (a, m, at) in made)
            {
                var (lo, hi) = m.Box();
                Vector3 alo = at + lo * TempleFrame.Scale, ahi = at + hi * TempleFrame.Scale;
                if (filled.Any(f => alo.X < f.Hi.X && ahi.X > f.Lo.X && alo.Y < f.Hi.Y && ahi.Y > f.Lo.Y && alo.Z < f.Hi.Z && ahi.Z > f.Lo.Z))
                    gone.Add($"{a.Name} {a.Kind} (in the wall the race fills)");
                else if (NearRoad(plan, alo, ahi) is { } near) gone.Add($"{a.Name} {a.Kind} ({near})");
                else kept.Add((a, m, at));
            }
            log.Add($"the race track: {gone.Count} of LBA1's actors left out on or over the road -- {string.Join("; ", gone)}");
            made = kept;
        }

        // the bodies (the same model once), their tiles on pages after the temple's, the decors
        var firstPage = island.ObjectPages.Count + 1;
        var pages = new PolarTextures.Pages(new byte[256 * 256], PolarTextures.Colours.AsTheyAre(Array.Empty<byte>()), IslandFile.MaxObjectPages - firstPage);
        var written = new List<byte[]>();
        var known = new Dictionary<string, int>();
        int decors = 0, faces = 0, full = 0;
        foreach (var (a, m, at) in made)
        {
            var tiles = m.Faces.Where(f => f.Tile is not null).Select(f => f.Tile!).DistinctBy(t => t.Key).ToList();
            var onPage = -1;
            if (tiles.Count > 0)
            {
                // (all of a body's tiles on one page: the first that has them already, or room for those it hasn't -- a top needs a slot's
                // two halves together: a half more)
                for (var p = 0; onPage < 0; p++)
                {
                    if (p >= IslandFile.MaxObjectPages - firstPage) throw new InvalidOperationException($"The temple's actors' textures take more than the {IslandFile.MaxObjectPages} object pages.");
                    var missing = tiles.Where(t => !pages.Place.ContainsKey((t.Key, p))).ToList();
                    if (missing.Count == 0 || missing.Sum(t => PolarTextures.Pages.HalvesOf(t.H)) + 1 <= pages.Free(p)) onPage = p;
                }
                foreach (var t in tiles)
                    if (!pages.Add((t.Key, onPage), t.Pixels, t.W, t.H, onPage)) throw new InvalidOperationException("A page took fewer tiles than it had room for.");
            }
            var (lo, hi) = m.Box();
            var origin = new Vector3(MathF.Round((lo.X + hi.X) / 2 * TempleFrame.Scale), MathF.Round((lo.Y + hi.Y) / 2 * TempleFrame.Scale), MathF.Round((lo.Z + hi.Z) / 2 * TempleFrame.Scale));
            var bytes = Write(m, origin, tiles.Count > 0 ? key => { var p = pages.Place[(key, onPage)]; return (p.X, p.Y); } : null);
            var hash = Convert.ToBase64String(System.Security.Cryptography.SHA1.HashData(bytes)) + (tiles.Count > 0 ? $"/{onPage}" : "");
            if (!known.TryGetValue(hash, out var body)) { known[hash] = body = firstBody + written.Count; written.Add(bytes); }
            var w = at + origin;
            var (placedHere, fullHere) = BrickBuilding.AddToCubes(island, body | (tiles.Count > 0 ? (firstPage + onPage) << IslandFile.DecorPageShift : 0), 0,
                w.X, (int)Math.Round(w.Y), w.Z, at.X + lo.X * TempleFrame.Scale, at.X + hi.X * TempleFrame.Scale, (int)Math.Floor(at.Y + lo.Y * TempleFrame.Scale),
                BrickBuilding.NoBoxTop, at.Z + lo.Z * TempleFrame.Scale, at.Z + hi.Z * TempleFrame.Scale, IslandDecors.MaxPerCubeOwnEngine);
            decors += placedHere; full += fullHere;
            faces += m.Faces.Count;
        }
        var pagesUsed = pages.Place.Count == 0 ? 0 : pages.Count;
        if (pagesUsed > 0) island.ObjectPages.AddRange(pages.List.Take(pagesUsed));
        log.Add($"LBA1's actors made: {made.Count} ({string.Join(", ", made.GroupBy(m => m.Actor.Kind).Select(g => $"{g.Count()} {g.Key}"))}), {faces} faces, " +
                $"{written.Count} bodies, {decors} decors, {pages.Place.Count} tiles on {pagesUsed} page(s) after the temple's {firstPage}" + (full > 0 ? $"; {full} copies left out (cubes full)" : ""));
        return new Result(written, decors, faces, pagesUsed, log);
    }

    // Whether a box (island world units) comes near the race's road: the reason, or null.
    public static string? NearRoad(RaceTrackPlan plan, Vector3 lo, Vector3 hi)
    {
        var pts = plan.Points; var hs = plan.Heights;
        if (hs is null || hs.Length != pts.Length) return null;
        double best = double.MaxValue; string? why = null;
        for (var i = 0; i < pts.Length; i++)
        {
            int j = Math.Min(i + 1, pts.Length - 1), k = Math.Max(0, j - 1);
            double px = plan.OriginCellX + pts[i][0], pz = plan.OriginCellZ + pts[i][1];
            double dx = pts[j][0] - pts[k][0], dz = pts[j][1] - pts[k][1], len = Math.Sqrt(dx * dx + dz * dz);
            if (len < 1e-9) continue;
            dx /= len; dz /= len;
            var half = plan.RaisedHalfs is { } halfs && i < halfs.Length ? halfs[i] : plan.RaisedHalf ?? 4.5;
            double from = -half, to = half;
            if (plan.RaisedStrips is { } strips && i < strips.Length && strips[i] is { Length: 2 } st && st[1] > st[0]) { from = Math.Min(from, st[0]); to = Math.Max(to, st[1]); }
            // (the box's cells: its corners' place along the road at the point, and across it)
            double x0 = lo.X / 512.0 - px, x1 = hi.X / 512.0 - px, z0 = lo.Z / 512.0 - pz, z1 = hi.Z / 512.0 - pz;
            double aMin = double.MaxValue, aMax = double.MinValue, cMin = double.MaxValue, cMax = double.MinValue;
            foreach (var (cx, cz) in new[] { (x0, z0), (x1, z0), (x0, z1), (x1, z1) })
            {
                var along = cx * dx + cz * dz; var across = -cx * dz + cz * dx;
                aMin = Math.Min(aMin, along); aMax = Math.Max(aMax, along); cMin = Math.Min(cMin, across); cMax = Math.Max(cMax, across);
            }
            if (aMin > 0.3 || aMax < -0.3) continue;
            var h = hs[i];
            if (hi.Y <= h + DeckSurface || lo.Y > h + DeckOver) continue;
            var margin = hi.Y > h + RailTop ? RoadMargin : 0;
            if (cMin > to + margin || cMax < from - margin) continue;
            var gap = Math.Max(0, Math.Max(cMin - to, from - cMax));
            if (gap < best) { best = gap; why = $"road point {i}, {(gap > 0 ? $"{gap:0.0} cells beside its deck" : "over its deck")}, {lo.Y - h:0} to {hi.Y - h:0} over it"; }
        }
        // (and the branches' roads -- the hidden passage's shortcut: its spur and its way out; not their gaps)
        foreach (var b in plan.Branches ?? Array.Empty<BranchRun>())
            for (var i = 0; i < b.Points.Length && i < b.Heights.Length; i++)
            {
                var half = b.Halfs is { } halfs && i < halfs.Length ? halfs[i] : plan.RaisedHalf ?? 4.5;
                if (half <= 0) continue;
                int j = Math.Min(i + 1, b.Points.Length - 1), k = Math.Max(0, j - 1);
                double px = plan.OriginCellX + b.Points[i][0], pz = plan.OriginCellZ + b.Points[i][1];
                double dx = b.Points[j][0] - b.Points[k][0], dz = b.Points[j][1] - b.Points[k][1], len = Math.Sqrt(dx * dx + dz * dz);
                if (len < 1e-9 || len > 2) continue;
                dx /= len; dz /= len;
                double x0 = lo.X / 512.0 - px, x1 = hi.X / 512.0 - px, z0 = lo.Z / 512.0 - pz, z1 = hi.Z / 512.0 - pz;
                double aMin = double.MaxValue, aMax = double.MinValue, cMin = double.MaxValue, cMax = double.MinValue;
                foreach (var (cx, cz) in new[] { (x0, z0), (x1, z0), (x0, z1), (x1, z1) })
                {
                    var along = cx * dx + cz * dz; var across = -cx * dz + cz * dx;
                    aMin = Math.Min(aMin, along); aMax = Math.Max(aMax, along); cMin = Math.Min(cMin, across); cMax = Math.Max(cMax, across);
                }
                if (aMin > 0.3 || aMax < -0.3) continue;
                var h = b.Heights[i];
                if (hi.Y <= h + DeckSurface || lo.Y > h + DeckOver) continue;
                var margin = hi.Y > h + RailTop ? RoadMargin : 0;
                if (cMin > half + margin || cMax < -half - margin) continue;
                var gap = Math.Max(0, Math.Max(cMin - half, -half - cMax));
                if (gap < best) { best = gap; why = $"branch road point {i}, {(gap > 0 ? $"{gap:0.0} cells beside its deck" : "over its deck")}, {lo.Y - h:0} to {hi.Y - h:0} over it"; }
            }
        return why;
    }

    // A model as a body (LBA2's, of one bone, unlit): its points in island units from `origin` (the model's own units times the temple's
    // scale), its flat faces in their colours, its textured ones on their page (`place`: a tile's corner on it).
    private static byte[] Write(Model m, Vector3 origin, Func<string, (int X, int Y)>? place)
    {
        var points = m.Points.Select(p => new Vector3(MathF.Round(p.X * TempleFrame.Scale - origin.X), MathF.Round(p.Y * TempleFrame.Scale - origin.Y), MathF.Round(p.Z * TempleFrame.Scale - origin.Z))).ToList();
        if (points.Count > MaxPoints || m.Faces.Count > MaxFaces) throw new InvalidOperationException($"An actor's model of {points.Count} points and {m.Faces.Count} faces is more than a body holds.");
        var faces = new List<Face>();
        var handles = new Dictionary<(int, int), int>();
        var textures = new List<uint>();
        foreach (var f in m.Faces)
        {
            if (f.Tile is not { } t || place is null) { faces.Add(new Face(f.Ids, f.Colour, Material: 0)); continue; }
            var (tx, ty) = place(t.Key);
            if (!handles.TryGetValue((tx, ty), out var handle)) { handle = handles[(tx, ty)] = textures.Count; textures.Add(0x1F1F0000u | (uint)(ty * 256 + tx)); }
            var uv = f.Uv!.SelectMany(k => new[] { (int)Math.Round((0.4 + k.U * (t.W - 0.8)) * 256), (int)Math.Round((0.4 + k.V * (t.H - 0.8)) * 256) }).ToArray();
            faces.Add(new Face(f.Ids, 0, Texture: new FaceTexture(handle, uv), Lba2Type: t.Solid ? TextureSolid : TextureSeeThrough));
        }
        // (the header's first word: the body format's version, 0x10 as every retail body has it, and the close flag: cut at the near plane
        // close in front of the camera, BrickBuilding.CloseFlag)
        var header = new byte[96];
        BinaryPrimitives.WriteInt32LittleEndian(header, 0x10 | BrickBuilding.CloseFlag);
        var body = new Body
        {
            Game = 2, Static = true, Lit = false, Header = header,
            Vertices = points,
            Bones = new List<Bone> { new(0, points.Count, 0, -1, new byte[8]) },
            Faces = faces,
            Textures = textures.ToArray(),
        };
        return body.Write();
    }
}

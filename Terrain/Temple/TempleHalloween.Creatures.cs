using System.IO;
using System.Numerics;
using LbaBodyStudio;
using LBAAssembler.Terrain.Palace;

namespace LBAAssembler.Terrain.Temple;

// The Temple of Bú's creatures out of LBA2 itself (the user, 2026-10-18: "The game contains an actor called '36: The flying rat', and '35:
// Rabbibunny skeleton' let's replace the current bats we have with the ones from the game and add more of the games in-built skeletons to
// our temple track too"):
//   - the flying rat of the sewers (BODY.HQR 36, its entity's flapping, generic animation 0): the spooks that were bats ("rat"), flown by the
//     race-track mode as the bats were and flapping its own wings (RACEMOD.CPP spook= flags 4: an animation of ANIM.HQR); its body in
//     BODY.HQR where the bat's was (no other body moves), RatScale times as big, and shaded -- the game's is flat near-black (colour 224,
//     not lit), a shape lost in the temple's dark: each face a shade of the same ramp by the way it faces (225 dark to 233, RatShades), so
//     it is a dark brown-grey rat whose wings catch the light as they flap;
//   - the rabbibunny skeleton of the Tralu's cave (BODY.HQR 35): decor in four poses of its own animations, made a body of one bone in the
//     island's palette as LBA1's skeletons are (TempleHalloween.Skeletons) -- lying on its back (its lying, generic 146), sitting against a
//     wall, its legs out (half way through its getting up, 147), standing guard (its standing, 0) and crouching, an arm out (7); and
//     ("riser") the game's own body by the road (made lighter: InstallRisers), drawn by the race-track mode (spook= flags 8: standing):
//     fallen in a heap of bones (its falling apart, 151, held at its end) until Twinsen's car comes near, then getting up and swiping at
//     the road (its attack, 150) and falling back down as the car goes by.
// Bone: its greys (its colour 49 lit: 49 and up), 52 left out -- a teal in the temple's palette (TemplePalette) -- for 212, the grey
// between 51 and 53 there (as Stone has it).
internal static partial class TempleHalloween
{
    public const int RabbiSkeletonBody = 35, FlyingRatBody = 36;
    // (the entities' generic animation numbers)
    public const int RatFly = 0, RabbiStand = 0, RabbiCrouch = 7, RabbiLie = 146, RabbiGetUp = 147, RabbiSwipe = 150, RabbiCollapse = 151;
    public const float RatScale = 2.0f, RabbiScale = 1.0f;
    private static readonly Paint GreyBone = new(new[] { 51, 212, 53, 54, 55, 56, 57, 58, 59, 60 });
    // (the rat's: its ramp, 224 on, dark to light -- 229 and 232 left out, mauve in the temple's palette)
    private static readonly int[] RatShades = { 225, 226, 227, 228, 230, 231, 233 };

    public enum RabbiPose { Lying, Sitting, Standing, Crouching }

    private static readonly Dictionary<RabbiPose, (int Anim, int Frame)> RabbiPoses = new()
    {
        [RabbiPose.Lying] = (RabbiLie, 0),
        [RabbiPose.Sitting] = (RabbiGetUp, 5),
        [RabbiPose.Standing] = (RabbiStand, 0),
        [RabbiPose.Crouching] = (RabbiCrouch, 1),
    };

    // The game folder whose BODY.HQR, ANIM.HQR and entity table (RESS.HQR 44) the creatures come from: the build's (PlaceDecor sets it).
    private static string? creaturesFrom;

    // An entity's body: the entity that has BODY.HQR `body` (its own), and its animations by their generic numbers (ANIM.HQR indices).
    private static (int Entity, Dictionary<int, int> Anims)? Creature(string game, int body)
    {
        if (Lba2EntityTable.Load(game) is not { } table || table.KindForBody(body) is not { } kind) return null;
        return (kind.Entity, table.Entities[kind.Entity].Anims.GroupBy(a => a.Generic).ToDictionary(g => g.Key, g => g.First().Anim));
    }

    // The ANIM.HQR index of a creature's animation (generic number), or -1.
    public static int CreatureAnim(string game, int body, int generic) => Creature(game, body) is { } c && c.Anims.TryGetValue(generic, out var a) ? a : -1;

    // ---- the flying rat: its body, RatScale times as big, lit in its own ramp (null: no such body)
    public static byte[]? RatBody(string game)
    {
        try
        {
            var body = Body.Read(HqrArchive.Open(Path.Combine(game, "BODY.HQR")).Read(FlyingRatBody), 2);
            body.Vertices = body.Vertices.Select(v => v * RatScale).ToList();
            // (each face shaded by the way it faces as it is modelled, as the Halloween models are, in its own colour's ramp -- the game's
            // light can't do it: its wings' two sides share their points, whose normals come out nothing, the faces near black, a point here
            // and there past the ramp's end)
            var world = body.World();
            body.Faces = body.Faces.Select(f =>
            {
                var n = Vector3.Zero;
                for (var i = 0; i < f.Points.Length; i++)
                {
                    var a = world[f.Points[i]]; var b = world[f.Points[(i + 1) % f.Points.Length]];
                    n += new Vector3((a.Y - b.Y) * (a.Z + b.Z), (a.Z - b.Z) * (a.X + b.X), (a.X - b.X) * (a.Y + b.Y));
                }
                var lit = n.LengthSquared() > 1e-6f ? 0.5f + 0.5f * Vector3.Dot(Vector3.Normalize(n), Light) : 0.3f;
                var shades = (f.Colour & ~15) == 224 ? RatShades : new[] { f.Colour };
                return f with { Colour = shades[Math.Clamp((int)Math.Round(lit * (shades.Length - 1)), 0, shades.Length - 1)], Material = 0 };
            }).ToList();
            body.Lit = false;
            var bytes = body.Write();
            // (drawn close to the camera: its polygons cut at the near plane only a little in front of it)
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes, System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes) | BrickBuilding.CloseFlag);
            return bytes;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException) { return null; }
    }

    // ---- the rabbibunny skeleton posed, a model of one bone (null: no such body or animation in the game folder)
    private static Model? RabbiModel(RabbiPose pose)
    {
        if (creaturesFrom is not { } game || Creature(game, RabbiSkeletonBody) is not { } c) return null;
        var (generic, frame) = RabbiPoses[pose];
        if (!c.Anims.TryGetValue(generic, out var animIndex)) return null;
        Body body;
        byte[] anim;
        try
        {
            body = Body.Read(HqrArchive.Open(Path.Combine(game, "BODY.HQR")).Read(RabbiSkeletonBody), 2);
            anim = HqrArchive.Open(Path.Combine(game, "ANIM.HQR")).Read(animIndex);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException) { return null; }
        var frames = PalaceTorturers.ReadFrames(anim, body.Bones.Count);
        if (frame >= frames.Count) return null;
        var values = frames[frame].Bones;
        // (bone 1 moves the body up and down as it walks: as modelled)
        if (values.Length > 1) values[1] = (1, 0, 0, 0);
        var pts = PalaceTorturers.Posed(body, values).Points.Select(p => p * RabbiScale).ToArray();
        // (sitting: its back to the wall behind it, its legs out in front, +z -- turned round when its feet are behind its back)
        Vector3 Mean(IEnumerable<int> bones) => bones.SelectMany(b => Enumerable.Range(body.Bones[b].Start, body.Bones[b].Count)).Aggregate(Vector3.Zero, (s, i) => s + pts[i])
                                                / bones.Sum(b => body.Bones[b].Count);
        if (pose == RabbiPose.Sitting && body.Bones.Count > 16 && Mean(new[] { 13, 16 }).Z < Mean(new[] { 3 }).Z)
            pts = pts.Select(p => new Vector3(-p.X, p.Y, -p.Z)).ToArray();
        // (its box: of the points drawn -- lying and sitting, its middle bones turned about its hip, they are well over its root's)
        var drawn = body.Faces.SelectMany(f => f.Points).Concat(body.Lines.SelectMany(l => new[] { l.A, l.B })).Distinct().Select(i => pts[i]).ToArray();
        var lo = new Vector3(drawn.Min(v => v.X), drawn.Min(v => v.Y), drawn.Min(v => v.Z));
        var hi = new Vector3(drawn.Max(v => v.X), drawn.Max(v => v.Y), drawn.Max(v => v.Z));
        var shift = new Vector3(-(lo.X + hi.X) / 2, -lo.Y, pose == RabbiPose.Sitting ? -lo.Z + 30 : -(lo.Z + hi.Z) / 2);
        var at = pts.Select(v => v + shift).ToArray();
        var m = new Model();
        foreach (var f in body.Faces) m.Outward(f.Points.Select(i => at[i]).ToArray(), GreyBone);
        foreach (var l in body.Lines) m.Bar(at[l.A], at[l.B], 22 * RabbiScale, GreyBone);
        return m;
    }

    private static double RabbiReach(RabbiPose pose) => pose switch { RabbiPose.Lying => 900, RabbiPose.Sitting => 1000, RabbiPose.Crouching => 800, _ => 600 } * RabbiScale;

    // ---- the risers' body: the game's skeleton a little lighter -- the game's greys from 49 lit, drawn by the race-track mode, were near
    // black by the road in the temple's dark, and 52, a teal in its palette, showed in its shading: from RiserColour (53) on, past it, the
    // greys up to 63 (its lines, unlit, a light grey) -- the entity's body RiserBody (RESS.HQR 44), at the end of the build's BODY.HQR as
    // the flag marshal's is, so no other body moves (RaceTrackService: after everything else's). The race file names it by the entity and
    // that number (RACEMOD.CPP spook= flags 16).
    public const int RiserBody = 40, RiserColour = 53, RiserLineColour = 58;

    public static List<string> InstallRisers(string gameDirectory)
    {
        var bodyPath = Path.Combine(gameDirectory, "BODY.HQR");
        var ressPath = Path.Combine(gameDirectory, "RESS.HQR");
        var table = HqrArchive.Open(ressPath).Read(44);
        var parsed = Lba2EntityTable.Parse(table);
        if (parsed.KindForBody(RabbiSkeletonBody) is not { } kind)
            return new() { $"WARNING: the Temple of Bú's risers: no entity has the rabbibunny skeleton (BODY.HQR {RabbiSkeletonBody}) -- none made" };
        if (parsed.Entities[kind.Entity].Bodies.Any(b => b.Generic == RiserBody))
            return new() { $"WARNING: the Temple of Bú's risers: entity {kind.Entity} already has body {RiserBody} -- none made" };
        var body = Body.Read(HqrArchive.Open(bodyPath).Read(RabbiSkeletonBody), 2);
        body.Faces = body.Faces.Select(f => f.Colour is >= 48 and < 64 ? f with { Colour = Math.Max(f.Colour, RiserColour) } : f).ToList();
        body.Lines = body.Lines.Select(l => l.Colour is >= 48 and < 64 ? l with { Colour = RiserLineColour } : l).ToList();
        var index = HqrArchive.CountEntries(bodyPath);
        File.WriteAllBytes(bodyPath, HqrWriter.AppendEntry(File.ReadAllBytes(bodyPath), HqrWriter.StoredEntry(body.Write())));
        table = RaceTrackBaldinoCar.WithBody(table, kind.Entity, RiserBody, index);
        File.WriteAllBytes(ressPath, HqrWriter.ReplaceEntry(File.ReadAllBytes(ressPath), 44, HqrWriter.StoredEntry(table)));
        return new() { $"the Temple of Bú's risers: the rabbibunny skeleton (BODY.HQR {RabbiSkeletonBody}) its greys from {RiserColour} -- BODY.HQR {index}, entity {kind.Entity}'s body {RiserBody}" };
    }

    // The entity whose own body the rabbibunny skeleton is (-1 none).
    public static int RabbiEntity(string game) => Creature(game, RabbiSkeletonBody)?.Entity ?? -1;

    // (for a look at them: the decor bodies, as the island has them -- null without the creature in `game`)
    public static byte[]? RabbiDecorBody(string game, RabbiPose pose)
    {
        creaturesFrom = game;
        return RabbiModel(pose)?.Write();
    }
}

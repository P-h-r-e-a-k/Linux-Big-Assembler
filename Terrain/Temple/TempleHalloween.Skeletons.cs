using System.IO;
using System.Numerics;
using LBAAssembler.Lba1;
using LbaBodyStudio;

namespace LBAAssembler.Terrain.Temple;

// The Temple of Bú's skeletons (the user, 2026-10-17: "Let's make the temple track creepier with some skeletons"): LBA1's own -- the
// skeleton that wakes up in LBA1's temple and comes at Twinsen with its knife (BODY.HQR 55; its knife, bone 17 and what is on it, left
// out) -- posed by its own animations and by hand, and made a body of one bone in the island's palette as the rest of Halloween is, each
// face shaded by the way it faces (Bone):
//   - lying on its back beside the road (its sleeping, ANIM.HQR 244);
//   - slumped against a wall, its legs out, one knee up, its head fallen to one side (its waking up, 245, a third of the way);
//   - hanging by its wrists on a wall, in chains to two plates, its legs dangling and its head hung;
//   - hanging the same in the dark beside the road, its chains going up out of sight;
//   - reaching out with both arms, hunched (its standing, 246): the one that jumps up out of the floor beside the road (scare=), a body the
//     race-track mode draws itself, as it does the spooks.
// Its points as the posed body has them (Lba1Pose: LBA1's renderer's way), SkeletonScale times as big -- a little taller than Twinsen, the
// temple being three times LBA1's size and the cars LBA2's.
internal static partial class TempleHalloween
{
    public const int Lba1SkeletonBody = 55;
    private const int KnifeBone = 17, KnifeColour = 144, KnifeLineColour = 155;
    private const float SkeletonScale = 1.4f;
    private static readonly Paint Bone = new(new[] { 231, 233, 235, 236, 237, 238, 239 });

    public enum SkeletonPose { Lying, Slumped, Hanging, Chained, Reaching }

    // Each pose: the animation and key frame it starts from (-1: the body as it is modelled), then bones turned by hand (degrees about x, y
    // and z, LBA1's order: x, then z, then y).
    private static readonly Dictionary<SkeletonPose, (int Anim, int Frame, (int Bone, double X, double Y, double Z)[] Turns)> Poses = new()
    {
        [SkeletonPose.Lying] = (244, 0, Array.Empty<(int, double, double, double)>()),
        [SkeletonPose.Slumped] = (245, 6, new[] { (4, 35.0, 0.0, 20.0), (5, -10.0, 0.0, 8.0), (6, 0.0, 0.0, 0.0), (8, -10.0, 0.0, -8.0), (9, 0.0, 0.0, 0.0) }),
        [SkeletonPose.Hanging] = (-1, 0, HangingTurns()),
        [SkeletonPose.Chained] = (-1, 0, HangingTurns()),
        [SkeletonPose.Reaching] = (246, 0, new[] { (4, -20.0, 0.0, 0.0), (5, -100.0, 0.0, 10.0), (6, -10.0, 0.0, 0.0), (8, -100.0, 0.0, -10.0), (9, -10.0, 0.0, 0.0) }),
    };

    // (its arms straight up over its head, the wrists a little apart; its legs hanging, the knees a little bent, the feet pointing down;
    // its head hung forward and to the side)
    private static (int, double, double, double)[] HangingTurns() => new[]
    {
        (2, 0.0, 0.0, 0.0), (4, 40.0, 0.0, 15.0), (5, 0.0, 0.0, 160.0), (6, 0.0, 0.0, 15.0), (8, 0.0, 0.0, -160.0), (9, 0.0, 0.0, -15.0),
        (11, 8.0, 0.0, -6.0), (12, -20.0, 0.0, 0.0), (13, 40.0, 0.0, 0.0), (14, 12.0, 0.0, 6.0), (15, -25.0, 0.0, 0.0), (16, 40.0, 0.0, 0.0),
    };

    // ---- LBA1's skeleton and its animations, read once from the LBA1 folder (null without one)
    private static (Body Body, Dictionary<int, Lba1Animation> Anims)? skeleton;
    private static string? skeletonFrom;

    private static (Body Body, Dictionary<int, Lba1Animation> Anims)? Skeleton()
    {
        var lba1 = RaceTrackService.Lba1Folder();
        if (skeleton is not null && skeletonFrom == lba1) return skeleton;
        skeletonFrom = lba1;
        skeleton = null;
        if (string.IsNullOrWhiteSpace(lba1) || !File.Exists(Path.Combine(lba1, "BODY.HQR")) || !File.Exists(Path.Combine(lba1, "ANIM.HQR"))) return null;
        var body = Body.Read(HqrArchive.Open(Path.Combine(lba1, "BODY.HQR")).Read(Lba1SkeletonBody), 1);
        var anims = HqrArchive.Open(Path.Combine(lba1, "ANIM.HQR"));
        var read = new Dictionary<int, Lba1Animation>();
        foreach (var a in Poses.Values.Select(p => p.Anim).Where(a => a >= 0).Distinct())
            if (Lba1Animation.Parse(anims.Read(a)) is { } anim) read[a] = anim;
        return skeleton = (body, read);
    }

    // The skeleton posed, its points (world units, SkeletonScale times LBA1's), and the faces and lines that aren't the knife's -- or null with
    // no LBA1 folder.
    private static (Vector3[] Points, List<Face> Faces, List<BodyLine> Lines)? Posed(SkeletonPose pose)
    {
        if (Skeleton() is not { } s) return null;
        var (anim, frame, turns) = Poses[pose];
        var bones = new (int Type, double X, double Y, double Z)[s.Body.Bones.Count];
        if (anim >= 0 && s.Anims.TryGetValue(anim, out var a) && frame < a.Frames.Count)
            for (var b = 0; b < bones.Length && b < a.Frames[frame].Bones.Count; b++)
            {
                var f = a.Frames[frame].Bones[b];
                bones[b] = (f.Type, f.X, f.Y, f.Z);
            }
        // (bone 1 moves the body about, not turns it: as its animations have it, at no offset; the knife's bone as modelled)
        if (bones.Length > 1) bones[1] = (1, 0, 0, 0);
        if (bones.Length > KnifeBone) bones[KnifeBone] = (0, 0, 0, 0);
        foreach (var (b, x, y, z) in turns)
            if (b < bones.Length) bones[b] = (0, x * 1024 / 360, y * 1024 / 360, z * 1024 / 360);
        var world = Lba1Pose.World(s.Body, bones).Select(p => p * SkeletonScale).ToArray();
        var knife = s.Body.Bones.Count > KnifeBone ? Enumerable.Range(s.Body.Bones[KnifeBone].Start, s.Body.Bones[KnifeBone].Count).ToHashSet() : new HashSet<int>();
        var faces = s.Body.Faces.Where(f => f.Colour != KnifeColour && !f.Points.Any(knife.Contains)).ToList();
        var lines = s.Body.Lines.Where(l => l.Colour != KnifeLineColour && !knife.Contains(l.A) && !knife.Contains(l.B)).ToList();
        return (world, faces, lines);
    }

    // A skeleton's model in a pose: its feet (or its back, against a wall) at the model's origin as the decor's place wants it -- lying, its
    // middle over the origin; slumped and hung on a wall, its back at z 0 (the wall behind it, its front +z), standing on y 0 (hung: its feet
    // there, the decor lifted); in the dark, its chains up from its wrists; reaching, its middle over its feet. Null with no LBA1 folder.
    private static Model? SkeletonModel(SkeletonPose pose)
    {
        if (Posed(pose) is not { } p) return null;
        var pts = p.Points;
        var lo = new Vector3(pts.Min(v => v.X), pts.Min(v => v.Y), pts.Min(v => v.Z));
        var hi = new Vector3(pts.Max(v => v.X), pts.Max(v => v.Y), pts.Max(v => v.Z));
        var shift = new Vector3(-(lo.X + hi.X) / 2, -lo.Y, pose is SkeletonPose.Slumped or SkeletonPose.Hanging ? -lo.Z + 30 : -(lo.Z + hi.Z) / 2);
        var at = pts.Select(v => v + shift).ToArray();
        var m = new Model();
        foreach (var f in p.Faces)
        {
            var corners = f.Points.Select(i => at[i]).ToArray();
            m.Outward(corners, Bone);
        }
        foreach (var l in p.Lines) m.Bar(at[l.A], at[l.B], 26 * SkeletonScale, Bone);
        if (pose is SkeletonPose.Hanging or SkeletonPose.Chained)
        {
            // (its wrists: the highest point of each hand)
            var s = Skeleton()!.Value.Body;
            foreach (var hand in new[] { 7, 10 })
            {
                var wrist = Enumerable.Range(s.Bones[hand].Start, s.Bones[hand].Count).Select(i => at[i]).OrderByDescending(v => v.Y).First();
                m.Box(wrist - new Vector3(45, 30, 45), wrist + new Vector3(45, 60, 45), Iron);
                if (pose == SkeletonPose.Hanging)
                {
                    // (a short chain up to a plate on the wall behind it)
                    var top = new Vector3(wrist.X * 1.15f, wrist.Y + 420, 30);
                    m.Chain(wrist + new Vector3(0, 60, 0), top, 110, 34);
                    m.Box(top + new Vector3(-95, -95, -40), top + new Vector3(95, 95, 10), Iron);
                }
                else
                    // (a long one up into the dark, out of sight)
                    m.Chain(wrist + new Vector3(0, 60, 0), new Vector3(wrist.X * 0.6f, wrist.Y + 2600, wrist.Z), 200, 50);
            }
        }
        return m;
    }

    // How far a skeleton's decor reaches from its place (its box: the engine draws a decor whose box's corners are in sight), and how tall.
    private static double SkeletonReach(SkeletonPose pose) => pose switch { SkeletonPose.Lying => 1100, SkeletonPose.Slumped => 1000, _ => 700 };
}

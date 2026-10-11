using System.IO;
using System.Numerics;
using LbaBodyStudio;
using LBAAssembler.Lba1;

namespace LBAAssembler.Terrain;

// The gas monster's neck bent over a raised road's rail (RaceTrackGasMonster.InstallPoses): the user, 2026-10-09, of the monsters leaning
// stiffly from the gas onto the road: "Some of the gas monsters strike too low and appear through the track, let's have them permanently
// visible with their necks above the track and randomly striking down"; then, of their heads coming down flat across the road: "The gas
// monsters aren't really biting players they're just slamming their heads onto the track ... Let's make the gas monsters necks longer,
// their body size doesn't really matter, so they're above the track then when they're ready they strike down onto the track into their
// strike zone".
//
// The body (the game's shut one, 320) is a chain from its foot (bone 0, the hole) up to its head: a stub (bone 1, one point), the neck
// (bones 2-10) and the head (11: its top 12-13 with the eyes, its jaw 14-16 hinged at 14's pivot, the mouth's red inside and its teeth --
// colours 47 and 64 -- between them). The game opens its mouth by turning the jaw (its rise, anim 1480, turns bone 14 by 56 degrees), so one
// body does for every move. A Shape bends the neck in the plane of the body's y (up) and z (where its mouth faces): each neck bone leans
// Lean + (Top - Lean) * along^Gather from upright towards the mouth's side (along: the bone's middle up the neck, 0 to 1), the head Nod
// past the neck's top, the jaw open Jaw; the stub moves the whole neck down Drop (a translation: the neck sinking into its hole as it
// strikes); Sway turns the neck sideways, shared among its bones. The engine turns a bone about x by M(Alpha) (+y towards +z) in its
// parent's frame (M(Alpha)M(Gamma)M(Beta), LIB386/3D/IMATSTDF.CPP), as Lba1Pose does, so a bone's own value is the difference from its
// parent's lean.
internal sealed class GasMonsterNeck
{
    internal readonly record struct Shape(double Lean, double Top, double Gather, double Nod, double Jaw, double Drop = 0);

    // the colours of the mouth's red inside and its teeth (the game's palette)
    private static readonly HashSet<int> MouthColours = new() { 47, 64 };

    private readonly Body body;
    private readonly int stub, head, jaw;
    private readonly int[] neck;
    private readonly double[] along;
    public readonly int[] HeadPoints, MouthPoints;
    // how long the neck is (from the stub's top to the head's pivot, at rest), and from the head's pivot to the mouth
    public readonly double Length, HeadReach;

    public GasMonsterNeck(Body body)
    {
        this.body = body;
        var rest = body.World();
        // (the chain: from the bone of the highest point down to the foot)
        var top = Enumerable.Range(0, rest.Length).MaxBy(p => rest[p].Y);
        var bone = body.Bones.FindIndex(b => top >= b.Start && top < b.Start + b.Count);
        var chain = new List<int>();
        for (; bone >= 0; bone = body.Bones[bone].Parent) chain.Insert(0, bone);
        var headAt = chain.FindIndex(b => b > 0 && body.Bones[b].Count >= 30);
        if (headAt < 3 || body.Bones[chain[1]].Count > 2) throw new InvalidDataException("The gas monster's body has no stub and neck below a head.");
        stub = chain[1];
        head = chain[headAt];
        neck = chain.Skip(2).Take(headAt - 2).ToArray();
        double Pivot(int b) => rest[body.Bones[b].Pivot].Y;
        double foot = Pivot(neck[0]);
        Length = Pivot(head) - foot;
        along = neck.Select((b, i) => ((Pivot(b) + (i + 1 < neck.Length ? Pivot(neck[i + 1]) : Pivot(head))) / 2 - foot) / Length).ToArray();
        HashSet<int> Under(int root)
        {
            var under = new HashSet<int> { root };
            for (var changed = true; changed;)
            {
                changed = false;
                for (var b = 0; b < body.Bones.Count; b++)
                    if (!under.Contains(b) && under.Contains(body.Bones[b].Parent)) { under.Add(b); changed = true; }
            }
            return under;
        }
        int[] PointsOf(HashSet<int> bones) => bones.SelectMany(b => Enumerable.Range(body.Bones[b].Start, body.Bones[b].Count)).ToArray();
        HeadPoints = PointsOf(Under(head));
        // (the jaw: the head's child reaching furthest towards the mouth's side)
        jaw = Enumerable.Range(0, body.Bones.Count).Where(b => body.Bones[b].Parent == head).MaxBy(b => PointsOf(Under(b)).Average(p => rest[p].Z));
        var inHead = HeadPoints.ToHashSet();
        MouthPoints = body.Faces.Where(f => MouthColours.Contains(f.Colour) && f.Points.All(inHead.Contains)).SelectMany(f => f.Points).Distinct().ToArray();
        if (MouthPoints.Length == 0) MouthPoints = HeadPoints;
        HeadReach = Vector3.Distance(rest[body.Bones[head].Pivot], Mouth(rest));
    }

    // Each bone's move for a shape: its type (0 a turn, 1 a translation) and three values (a turn's about x, y, z in radians; a translation).
    private (int Type, double A, double B, double C)[] Moves(Shape s, double sway)
    {
        var moves = new (int, double, double, double)[body.Bones.Count];
        moves[stub] = (1, 0, s.Drop, 0);
        var lean = 0.0;
        for (var i = 0; i < neck.Length; i++)
        {
            var at = s.Lean + (s.Top - s.Lean) * Math.Pow(along[i], s.Gather);
            moves[neck[i]] = (0, at - lean, 0, sway / neck.Length);
            lean = at;
        }
        moves[head] = (0, s.Top + s.Nod - lean, 0, 0);
        moves[jaw] = (0, s.Jaw, 0, 0);
        return moves;
    }

    // The body's points posed, from its foot.
    public Vector3[] Pose(Shape s, double sway = 0) => Posed(Moves(s, sway));

    // ... and part way (t) from one shape to another as the engine moves between keyframes: each bone's move by itself.
    public Vector3[] Blend(Shape a, Shape b, double t)
    {
        var (ma, mb) = (Moves(a, 0), Moves(b, 0));
        return Posed(ma.Select((x, i) => (x.Type, x.A + (mb[i].A - x.A) * t, x.B + (mb[i].B - x.B) * t, x.C + (mb[i].C - x.C) * t)).ToArray());
    }

    private Vector3[] Posed((int Type, double A, double B, double C)[] moves)
    {
        const double Units = 1024 / (2 * Math.PI);
        return Lba1Pose.World(body, moves.Select(m => m.Type == 0 ? (0, m.A * Units, m.B * Units, m.C * Units) : (1, m.A, m.B, m.C)).ToArray());
    }

    // The middle of the mouth, posed.
    public Vector3 Mouth(Vector3[] posed) => MouthPoints.Aggregate(Vector3.Zero, (sum, p) => sum + posed[p]) / MouthPoints.Length;

    // An animation keyframe of the shape: slot 0 the root's (nothing), each bone's move (turns in turns).
    public AnimFrame Frame(Shape s, int time, double sway = 0)
    {
        var moves = Moves(s, sway);
        return new AnimFrame(time, 0, 0, 0, moves.Select((m, i) => i == 0 ? AnimBone.Neutral
            : m.Type == 0 ? new AnimBone(0, (float)(m.A / (2 * Math.PI)), (float)(m.B / (2 * Math.PI)), (float)(m.C / (2 * Math.PI)))
            : new AnimBone(1, (float)m.A, (float)m.B, (float)m.C)).ToArray());
    }

    // The game's body with its neck stretched `k` times as long (its neck bones' points that much further up from their pivots, every bone
    // above with them; its height in its header), its bytes.
    public static byte[] LongNeck(byte[] bytes, double k)
    {
        var body = Body.Read(bytes, 2, allowStatic: true);
        var chain = new GasMonsterNeck(body);
        var b = (byte[])bytes.Clone();
        int points = BitConverter.ToInt32(b, 40), at = BitConverter.ToInt32(b, 44);
        foreach (var bone in chain.neck)
            for (var p = body.Bones[bone].Start; p < body.Bones[bone].Start + body.Bones[bone].Count && p < points; p++)
            {
                var y = at + p * 8 + 2;
                BitConverter.GetBytes((short)Math.Clamp(Math.Round(BitConverter.ToInt16(b, y) * k), short.MinValue, short.MaxValue)).CopyTo(b, y);
            }
        var height = (int)Math.Ceiling(Body.Read(b, 2, allowStatic: true).World().Max(v => v.Y));
        BitConverter.GetBytes(height).CopyTo(b, 20);
        return b;
    }
}

// The monster's poses at one place, its foot at the gas: poised (its head high over its strike zone, mouth open, facing down), reared (back
// and higher, mouth wide) and struck (its neck sunk and bent, its mouth down on the strike zone; then shut), found by search against the
// road beside it. In the monster's own frame: its foot at the place it rises from, z towards its bite D away, the rail E away, the road's
// deck Road over the gas.
internal static class GasMonsterPoser
{
    internal sealed record Poses(GasMonsterNeck.Shape Poised, GasMonsterNeck.Shape Rear, GasMonsterNeck.Shape Struck, GasMonsterNeck.Shape Bit,
        double StruckMiss, double PoisedOver, double LeastOverRail, double LeastOverDeck);

    // how far over the rail (220 over the deck) and over the deck the body keeps, struck and when not; where its mouth is over the road,
    // poised and reared, and its lowest point struck (its open jaw on the road, the mouth's middle over the strike zone); which way it faces
    // (radians from forward, down), poised, reared and struck; how open its jaw is (radians)
    private const double RailHeight = RaceTrackRaisedBody.Rail, RailOver = 120, DeckOver = 30, PoisedClear = 450;
    public const double PoisedHigh = 2600, RearHigh = 3300, StruckHigh = 50;
    private const double PoisedFacing = 1.3, RearFacing = 0.8, StruckFacing = 1.75;
    private const double PoisedJaw = 0.7, RearJaw = 1.0, StruckJaw = 1.0, BitJaw = -0.05;

    public static Poses Solve(GasMonsterNeck neck, double d, double e, double road)
    {
        // (how a pose sits against the road: the least height over the rail and over the deck of the points out over them -- nothing on the
        // road's side of the rail lower than the deck, not even under it; its hole in the gas, beside the road, is clear)
        (double Rail, double Deck) Clear(Vector3[] p)
        {
            double rail = double.MaxValue, deck = double.MaxValue;
            foreach (var q in p)
            {
                if (q.Z < e - 100) continue;
                if (q.Z <= e + 150) rail = Math.Min(rail, q.Y - road - RailHeight);
                deck = Math.Min(deck, q.Y - road);
            }
            return (rail, deck);
        }
        double Cost(GasMonsterNeck.Shape s, Vector3 want, double facing, double over, bool struck)
        {
            var p = neck.Pose(s);
            var mouth = neck.Mouth(p);
            var y = struck ? neck.HeadPoints.Min(i => p[i].Y) : mouth.Y;
            double cost = (y - want.Y) * (y - want.Y) + (mouth.Z - want.Z) * (mouth.Z - want.Z);
            var turn = (s.Top + s.Nod - facing) * 1500;
            cost += turn * turn;
            var (rail, deck) = Clear(p);
            var railNeed = RailOver + (struck ? 0 : over);
            var deckNeed = struck ? DeckOver : over;
            if (rail < railNeed) cost += 400 * (railNeed - rail) * (railNeed - rail) + 1e6;
            if (deck < deckNeed) cost += 400 * (deckNeed - deck) * (deckNeed - deck) + 1e6;
            // (a gentle bend: no bone turning back on itself)
            if (s.Top < s.Lean - 0.3) cost += 1e5 * (s.Lean - 0.3 - s.Top);
            return cost;
        }
        GasMonsterNeck.Shape Search(Vector3 want, double facing, double jaw, double over, bool struck, out double best)
        {
            var bestShape = new GasMonsterNeck.Shape(0, 0, 1, 0, jaw);
            best = double.MaxValue;
            foreach (var drop in struck ? Steps(0, -0.45 * neck.Length, 6) : new[] { 0.0 })
            foreach (var lean in Steps(-0.4, 1.2, 9))
            foreach (var top in Steps(0, 3.0, 13))
            foreach (var gather in new[] { 0.6, 1.0, 1.6, 2.5, 4.0 })
            foreach (var nod in Steps(-0.8, 1.6, 9))
            {
                var s = new GasMonsterNeck.Shape(lean, top, gather, nod, jaw, drop);
                var c = Cost(s, want, facing, over, struck);
                if (c < best) { best = c; bestShape = s; }
            }
            // (then nearer, each value in turn, the steps halving)
            for (var step = 0.1; step > 0.004; step /= 2)
                for (var improved = true; improved;)
                {
                    improved = false;
                    for (var axis = 0; axis < (struck ? 5 : 4); axis++)
                        foreach (var sign in new[] { -1, 1 })
                        {
                            var s = bestShape;
                            s = axis switch
                            {
                                0 => s with { Lean = s.Lean + sign * step },
                                1 => s with { Top = s.Top + sign * step },
                                2 => s with { Gather = Math.Max(0.3, s.Gather + sign * step * 4) },
                                3 => s with { Nod = s.Nod + sign * step },
                                _ => s with { Drop = Math.Min(0, s.Drop + sign * step * neck.Length) },
                            };
                            var c = Cost(s, want, facing, over, struck);
                            if (c < best - 1e-6) { best = c; bestShape = s; improved = true; }
                        }
                }
            return bestShape;
        }

        var struckAt = new Vector3(0, (float)(road + StruckHigh), (float)d);
        var struck = Search(struckAt, StruckFacing, StruckJaw, 0, true, out _);
        var bit = struck with { Jaw = BitJaw };
        var poised = Search(new Vector3(0, (float)(road + PoisedHigh), (float)(d - 0.15 * (d - e))), PoisedFacing, PoisedJaw, PoisedClear, false, out _);
        var rear = Search(new Vector3(0, (float)(road + RearHigh), (float)(e - 300)), RearFacing, RearJaw, PoisedClear, false, out _);
        // (how well it came out: the struck mouth's miss, the poised mouth's height over the deck, the least clearances, the moves between
        // the poses too, as the engine blends them)
        var sp = neck.Pose(struck);
        var miss = Math.Sqrt(Math.Pow(neck.Mouth(sp).Z - struckAt.Z, 2) + Math.Pow(neck.HeadPoints.Min(i => sp[i].Y) - struckAt.Y, 2));
        var poisedOver = neck.Mouth(neck.Pose(poised)).Y - road;
        double leastRail = double.MaxValue, leastDeck = double.MaxValue;
        void Take(Vector3[] p) { var (rail, deck) = Clear(p); leastRail = Math.Min(leastRail, rail); leastDeck = Math.Min(leastDeck, deck); }
        foreach (var s in new[] { poised, rear, struck, bit }) Take(neck.Pose(s));
        foreach (var (a, b) in new[] { (poised, rear), (rear, struck), (struck, bit), (bit, poised) })
            for (var t = 0.25; t < 1; t += 0.25) Take(neck.Blend(a, b, t));
        return new Poses(poised, rear, struck, bit, miss, poisedOver, leastRail, leastDeck);
    }

    private static IEnumerable<double> Steps(double from, double to, int n) => Enumerable.Range(0, n).Select(i => from + (to - from) * i / (n - 1));
}

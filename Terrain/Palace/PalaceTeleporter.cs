using System.Numerics;

namespace LBAAssembler.Terrain.Palace;

// The Dark Monk's teleporter on the harbour's pillar (the race's finish, PalaceTrack: "finishing with massive jump to harbor teleport"): one of
// the gold rings he comes and goes by (the Dark Monk's statue's teleporters on Celebration Island; his car carries one too,
// RaceTrackCharacterCars' FunFrock), standing on the pillar across the jump's way, big enough for a car to fly through it -- its inner edge's
// foot a little under the road that crosses the pillar, a pale glowing rim round its inside. Drawn only: nothing touches it.
internal static class PalaceTeleporter
{
    public const double Radius = 3200;          // the ring's middle line, from its middle
    public const double Tube = 420;             // its thickness, from that line
    public const double Glow = 140;             // the pale rim round its inside
    private const int Around = 32;              // (its sections round the ring, and round each tube: within the engine's 550 points a body)
    // Otringal's palette: its gold ramp (dark to light) and a pale cream
    private static readonly int[] Gold = { 100, 101, 102, 103, 104, 105, 106 };
    private const int Cream = 239;

    // The ring's body, its middle the origin, its axis along x (it stands across a way along x).
    public static byte[] Body()
    {
        var m = new RaceTrackRaisedBody.Mesh();
        Torus(m, Radius, Tube, 6, shaded: true);
        Torus(m, Radius - Tube - Glow * 0.6, Glow, 4, shaded: false);
        return m.Write();
    }

    private static void Torus(RaceTrackRaisedBody.Mesh m, double radius, double tube, int AroundTube, bool shaded)
    {
        var ids = new int[Around, AroundTube];
        for (var i = 0; i < Around; i++)
        {
            var a = 2 * Math.PI * i / Around;
            var c = new Vector3(0, (float)(radius * Math.Sin(a)), (float)(radius * Math.Cos(a)));
            var outward = new Vector3(0, (float)Math.Sin(a), (float)Math.Cos(a));
            for (var j = 0; j < AroundTube; j++)
            {
                var b = 2 * Math.PI * j / AroundTube;
                ids[i, j] = m.P(c + outward * (float)(tube * Math.Cos(b)) + Vector3.UnitX * (float)(tube * Math.Sin(b)));
            }
        }
        for (var i = 0; i < Around; i++)
            for (var j = 0; j < AroundTube; j++)
            {
                int i1 = (i + 1) % Around, j1 = (j + 1) % AroundTube;
                // (its face's way out: from the tube's middle line; the gold by how it faces the sky, as the light falls)
                double a = 2 * Math.PI * (i + 0.5) / Around, b = 2 * Math.PI * (j + 0.5) / AroundTube;
                var radial = new Vector3(0, (float)Math.Sin(a), (float)Math.Cos(a));
                var normal = Vector3.Normalize(radial * (float)Math.Cos(b) + Vector3.UnitX * (float)Math.Sin(b));
                var colour = shaded ? Gold[(int)Math.Round((normal.Y * 0.5 + 0.5) * (Gold.Length - 1))] : Cream;
                m.Quad(ids[i, j], ids[i1, j], ids[i1, j1], ids[i, j1], colour, normal);
            }
    }

    // Where it stands: its middle over (x, z) world units, its inner edge's foot `under` under the road's height `road` there.
    public static (double X, double Y, double Z) Place(double x, double z, double road, double under = 100) => (x, road - under + Radius - Tube, z);
}

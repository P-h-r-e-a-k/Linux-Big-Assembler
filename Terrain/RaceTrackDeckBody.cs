using System.IO;
using LbaBodyStudio;

namespace LBAAssembler.Terrain;

// A simple flat rectangular deck (a box: top, bottom, four sides), for the race track's road-over-road bridge -- the same
// technique the retail game itself uses for a walkable span (Citadel Island's rope bridge at "the Cliffs of the Woodbridge":
// a flat plank decor with a ZV box whose top is the walking surface; ReajustPosDecors, EXTFUNC.CPP, lifts anything over the
// decor's footprint to that top). One body, reused for every tile of every deck: only its ZV box differs per placement.
internal static class RaceTrackDeckBody
{
    // Palette indices of the Desert island's own palette (RESS.HQR entry 29) nearest the ground's own asphalt grey, a darker
    // concrete grey for the underside and ends, the red curb colour for a rail down each long edge, and white -- so the deck
    // reads as the same road continuing, red and white curb included, not a foreign object. The white is 63, the colour of the
    // ground curb's own white texel: 15 is white in the file too, but the engine draws the first bank of colours differently
    // on objects (it came out lilac-grey).
    // The concrete is the same neutral grey ramp as the road (bank 3), lighter for the strip outside the curb, darker for the sides
    // and underside.
    public const int TopColour = 53, SideColour = 51, ConcreteColour = 57, RailColour = 75, WhiteColour = 63;

    // How many deck tiles across the road: PlaceDeck lays the deck VergeHalfWidth each side of the centre line.
    public static int Columns(RaceTrackOptions o) => Math.Max(1, (int)Math.Round(o.VergeHalfWidth * 2 / o.RoadBridgeTileLength));

    // width: across the road (the deck's local X). length: along the road (the deck's local Z). thickness: how deep the slab
    // reaches below its own top surface (local Y 0 = the walking surface, -thickness = the underside). curb: for the tiles of
    // the edge columns, the local X band (from, to) of the curb strip -- the top is then road grey inside it, red and white
    // blocks along it (`segments` of them, even, so the pattern runs on unbroken from tile to tile) and concrete outside it,
    // and the +X side is the red rail.
    public static byte[] Build(double width, double length, double thickness, (double From, double To)? curb = null, int segments = 2)
    {
        var hw = (float)(width / 2); var hl = (float)(length / 2); var t = (float)thickness;
        var v = new List<System.Numerics.Vector3>
        {
            new(-hw, 0, -hl), new(hw, 0, -hl), new(hw, 0, hl), new(-hw, 0, hl),           // 0-3: top corners
            new(-hw, -t, -hl), new(hw, -t, -hl), new(hw, -t, hl), new(-hw, -t, hl),        // 4-7: bottom
        };
        // every face's points go round so that its normal (right-hand rule) points OUT of the body, as in the retail bodies: the
        // engine culls faces seen from behind, and the first deck had them the other way round -- from above only its underside
        // was drawn, a dark slab without the curb
        var faces = new List<Face>
        {
            new(new[] { 4, 5, 6, 7 }, SideColour),
            new(new[] { 0, 1, 5, 4 }, SideColour),
            new(new[] { 7, 6, 2, 3 }, SideColour),
            new(new[] { 4, 7, 3, 0 }, SideColour),
            new(new[] { 6, 5, 1, 2 }, curb is null ? SideColour : RailColour),
        };
        if (curb is not { } c) faces.Insert(0, new(new[] { 3, 2, 1, 0 }, TopColour));
        else
        {
            // the top as a grid (bands across x rows along), every quad sharing its corners with its neighbours
            var xs = new[] { -hw, (float)Math.Clamp(c.From, -hw, hw), (float)Math.Clamp(c.To, -hw, hw), hw };
            var rows = Math.Max(2, segments);
            var zs = Enumerable.Range(0, rows + 1).Select(j => -hl + 2 * hl * j / rows).ToArray();
            var at = new int[4, rows + 1];
            for (var i = 0; i < 4; i++)
            for (var j = 0; j <= rows; j++) { at[i, j] = v.Count; v.Add(new(xs[i], 0, zs[j])); }
            for (var i = 0; i < 3; i++)
            {
                if (xs[i + 1] - xs[i] < 1) continue;
                for (var j = 0; j < rows; j++)
                {
                    var colour = i == 0 ? TopColour : i == 2 ? ConcreteColour : j % 2 == 0 ? RailColour : WhiteColour;
                    faces.Insert(0, new(new[] { at[i, j + 1], at[i + 1, j + 1], at[i + 1, j], at[i, j] }, colour));
                }
            }
        }
        var body = new Body
        {
            Game = 2, Static = true, Lit = false, Header = new byte[96],
            Vertices = v,
            Bones = new List<Bone> { new(0, v.Count, 0, -1, new byte[8]) },
            Faces = faces,
        };
        return body.Write();
    }

    // A railing piece for the deck's edges: a low wall `length` long (local Z), `thickness` across (local X), standing on local
    // Y 0 up to `height`, in red and white blocks one per `block` units along it like a crash barrier.
    public const double RailHeight = 300, RailThickness = 120, RailLength = 1024;
    public static byte[] BuildRail(double length = RailLength, double height = RailHeight, double thickness = RailThickness, double block = 512)
    {
        var hl = (float)(length / 2); var ht = (float)(thickness / 2); var h = (float)height;
        var blocks = Math.Max(1, (int)Math.Round(length / block));
        var v = new List<System.Numerics.Vector3>();
        // per cross-section j along Z: 0 bottom -X, 1 top -X, 2 top +X, 3 bottom +X
        for (var j = 0; j <= blocks; j++)
        {
            var z = -hl + 2 * hl * j / blocks;
            v.Add(new(-ht, 0, z)); v.Add(new(-ht, h, z)); v.Add(new(ht, h, z)); v.Add(new(ht, 0, z));
        }
        int P(int j, int k) => j * 4 + k;
        // the points of each face go round the way Build's do (the right-hand normal pointing out of the body)
        var faces = new List<Face>();
        for (var j = 0; j < blocks; j++)
        {
            var colour = j % 2 == 0 ? RailColour : WhiteColour;
            faces.Add(new(new[] { P(j, 1), P(j + 1, 1), P(j + 1, 2), P(j, 2) }, colour));   // top
            faces.Add(new(new[] { P(j, 0), P(j + 1, 0), P(j + 1, 1), P(j, 1) }, colour));   // -X side
            faces.Add(new(new[] { P(j, 2), P(j + 1, 2), P(j + 1, 3), P(j, 3) }, colour));   // +X side
        }
        faces.Add(new(new[] { P(0, 0), P(0, 1), P(0, 2), P(0, 3) }, SideColour));                              // ends
        faces.Add(new(new[] { P(blocks, 3), P(blocks, 2), P(blocks, 1), P(blocks, 0) }, SideColour));
        var body = new Body
        {
            Game = 2, Static = true, Lit = false, Header = new byte[96],
            Vertices = v,
            Bones = new List<Bone> { new(0, v.Count, 0, -1, new byte[8]) },
            Faces = faces,
        };
        return body.Write();
    }

    // Appends the two square deck tiles (RoadBridgeTileLength cells a side) and the railing piece to an island OBL file on disk and
    // returns the index of the first: index = the plain tile, index + 1 = the edge tile (the road's red and white curb along its
    // top, the red rail on its +X side; turned 180 degrees for the other edge), index + 2 = the railing. The deck is a grid of these small squares rather than a few long
    // slabs because the engine's collision box of a decor is axis-aligned: a long slab laid diagonally gets a box far bigger than
    // itself (an invisible floor reaching cells past the visible deck); a small square's box overhangs by well under a cell.
    // Always appended fresh: RaceTrackService rebuilds the OBL from its own pristine backup every time, so this never piles up
    // unused bodies from an earlier build.
    public static int AppendTo(string oblPath, RaceTrackOptions o, double thickness = 150)
    {
        var tile = o.RoadBridgeTileLength * 512;
        // the edge column's centre, across the road; the curb sits AsphaltHalfWidth..CurbHalfWidth from the centre line, as on the ground
        var edgeCentre = (Columns(o) - 1) * tile / 2;
        var curb = (o.AsphaltHalfWidth * 512 - edgeCentre, o.CurbHalfWidth * 512 - edgeCentre);
        // the ground's curb changes colour every 1.6 cells; the nearest even number of blocks per tile
        var segments = Math.Max(2, 2 * (int)Math.Round(o.RoadBridgeTileLength / 3.2));
        var hqr = File.ReadAllBytes(oblPath);
        var index = HqrArchive.CountEntries(oblPath);   // NOT HqrArchive.Open(...).Count -- that's the table's raw byte length, ~4x too high
        hqr = HqrWriter.AppendEntry(hqr, HqrWriter.StoredEntry(Build(tile, tile, thickness)));
        hqr = HqrWriter.AppendEntry(hqr, HqrWriter.StoredEntry(Build(tile, tile, thickness, curb, segments)));
        hqr = HqrWriter.AppendEntry(hqr, HqrWriter.StoredEntry(BuildRail()));
        File.WriteAllBytes(oblPath, hqr);
        return index;
    }
}

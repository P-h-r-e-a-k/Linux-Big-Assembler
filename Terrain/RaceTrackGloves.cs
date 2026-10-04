using System.IO;
using System.Numerics;
using LbaBodyStudio;

namespace LBAAssembler.Terrain;

// The race track mod's racing gloves: a pair of driving gloves -- racing red, a white band round each cuff and a white stripe down the back
// of the hand, dark palms and fingertips -- built from boxes, a single bone, lit like the game's own objects. The same model is written
// twice: at the inventory's scale into OBJFIX.HQR (the model the inventory and the "you have found" screen turn round), and at the room's
// scale into BODY.HQR as a body of the darts' display entity, so the pair lies where the darts did in Twinsen's attic.
internal static class RaceTrackGloves
{
    // The red is lit (a ramp of the game's palette, RESS.HQR entry 0: its light is added along the ramp); the white stripes and the dark
    // palms and tips are flat colours (a lit grey ramp came out mid-grey, neither white nor dark).
    private const int Red = 64, White = 62, Dark = 49;

    // The inventory's scale (the game's own glove stands about 4200 units tall, so these about as tall) and the room's (the darts on display
    // were about 490 across).
    public const float InventoryScale = 1.6f, RoomScale = 0.14f;

    // `upright`: the fingers up, as the inventory shows an item (turning it round the vertical, like the game's own glove, OBJFIX 11);
    // else lying flat, back of the hand up, as the darts lay on their shelf.
    public static Body Build(float scale, bool upright)
    {
        var mesh = new Mesh(upright);
        foreach (var side in new[] { 1, -1 })
            Glove(mesh, side, new Vector3(side * 640, 0, 0), side * -0.21f, scale);
        return mesh.ToBody();
    }

    // One glove, back of the hand up and the fingers pointing +z; `side` 1 is the right hand (its thumb to -x), -1 the left.
    private static void Glove(Mesh m, int side, Vector3 at, float yaw, float scale)
    {
        Vector3 Place(Vector3 p)
        {
            p.X *= side;
            var c = MathF.Cos(yaw); var s = MathF.Sin(yaw);
            return (new Vector3(p.X * c + p.Z * s, p.Y, -p.X * s + p.Z * c) + at) * scale;
        }
        void Box(Vector3 centre, Vector3 size, int top, int sides, int bottom, float localYaw = 0, bool darkBottom = false)
        {
            bool Flat(int colour) => colour != Red;
            var h = size / 2;
            var corners = new Vector3[8];
            for (var i = 0; i < 8; i++)
            {
                var c = new Vector3((i & 1) == 0 ? -h.X : h.X, (i & 2) == 0 ? -h.Y : h.Y, (i & 4) == 0 ? -h.Z : h.Z);
                var cy = MathF.Cos(localYaw); var sy = MathF.Sin(localYaw);
                corners[i] = Place(centre + new Vector3(c.X * cy + c.Z * sy, c.Y, -c.X * sy + c.Z * cy));
            }
            var ids = corners.Select(m.P).ToArray();
            var inside = corners.Aggregate(Vector3.Zero, (a, b) => a + b) / 8;
            m.Out(new[] { ids[2], ids[3], ids[7], ids[6] }, top, inside, Flat(top));         // top (+y)
            m.Out(new[] { ids[0], ids[1], ids[5], ids[4] }, bottom, inside, Flat(bottom));   // bottom
            m.Out(new[] { ids[0], ids[2], ids[6], ids[4] }, sides, inside, Flat(sides));     // -x
            m.Out(new[] { ids[1], ids[3], ids[7], ids[5] }, sides, inside, Flat(sides));     // +x
            m.Out(new[] { ids[0], ids[1], ids[3], ids[2] }, sides, inside, Flat(sides));     // -z (the wrist end)
            m.Out(new[] { ids[4], ids[5], ids[7], ids[6] }, sides, inside, Flat(sides));     // +z
        }

        // the cuff, and its white band
        Box(new Vector3(0, 0, -700), new Vector3(1000, 380, 500), Red, Red, Dark, darkBottom: true);
        Box(new Vector3(0, 0, -560), new Vector3(1050, 430, 150), White, White, White);
        // the hand: red back, dark palm, and the white stripe down its back
        Box(new Vector3(0, 0, 0), new Vector3(900, 300, 900), Red, Red, Dark, darkBottom: true);
        Box(new Vector3(0, 165, 20), new Vector3(210, 30, 860), White, White, White);
        // four fingers, curling down a little at their dark tips
        var fingers = new (float X, float Length)[] { (330, 470), (110, 580), (-110, 620), (-330, 540) };
        foreach (var (x, length) in fingers)
        {
            Box(new Vector3(x, -10, 450 + length / 2 - 60), new Vector3(190, 210, length - 120), Red, Red, Dark, darkBottom: true);
            Box(new Vector3(x, -45, 450 + length - 60), new Vector3(195, 180, 120), Dark, Dark, Dark, darkBottom: true);
        }
        // the thumb, out to the side at an angle, and its tip
        Box(new Vector3(-560, -40, -80), new Vector3(200, 190, 440), Red, Red, Dark, 0.62f, darkBottom: true);
        Box(new Vector3(-700, -60, 110), new Vector3(205, 170, 120), Dark, Dark, Dark, 0.62f, darkBottom: true);
    }

    // The inventory's model of `slot`'s item (OBJFIX.HQR entry) becomes the gloves. Returns a line for the log.
    public static string InstallInventory(string gameDirectory, int entry)
    {
        var path = Path.Combine(gameDirectory, "OBJFIX.HQR");
        var own = Body.Read(HqrArchive.Open(path).Read(entry), 2, allowStatic: true);
        var gloves = Build(InventoryScale, upright: true);
        gloves.Static = true; gloves.Header = own.Header;
        File.WriteAllBytes(path, HqrWriter.ReplaceEntry(File.ReadAllBytes(path), entry, HqrWriter.StoredEntry(gloves.Write())));
        return $"OBJFIX.HQR entry {entry}: the racing gloves ({gloves.Faces.Count} polygons)";
    }

    // The gloves as one more body of an entity (the darts' display), at the room's scale. Returns the body's generic number on the entity.
    public static (int Generic, string Log) InstallInRoom(string gameDirectory, int entity, int likeBody)
    {
        var bodyPath = Path.Combine(gameDirectory, "BODY.HQR");
        var bodies = HqrArchive.Open(bodyPath);
        var gloves = Build(RoomScale, upright: false);
        gloves.Header = Body.Read(bodies.Read(likeBody), 2, allowStatic: true).Header;
        var index = HqrArchive.CountEntries(bodyPath);
        File.WriteAllBytes(bodyPath, HqrWriter.AppendEntry(File.ReadAllBytes(bodyPath), HqrWriter.StoredEntry(gloves.Write())));
        const int generic = 1;
        var ressPath = Path.Combine(gameDirectory, "RESS.HQR");
        var table = RaceTrackBaldinoCar.WithBody(HqrArchive.Open(ressPath).Read(44), entity, generic, index);
        File.WriteAllBytes(ressPath, HqrWriter.ReplaceEntry(File.ReadAllBytes(ressPath), 44, HqrWriter.StoredEntry(table)));
        return (generic, $"BODY.HQR entry {index}: the racing gloves at the room's scale, entity {entity}'s body {generic}");
    }

    // A single-bone body under construction: points, and faces turned to face out.
    private sealed class Mesh(bool upright)
    {
        private readonly List<Vector3> points = new();
        private readonly List<(int[] Points, int Colour, bool Unlit)> faces = new();
        // (upright: the hand's +z, the way the fingers point, becomes +y, and its back, +y, faces +z -- the side the inventory shows an
        // item from before it turns it, so the red backs and their white stripes are what it shows first)
        public int P(Vector3 v) { points.Add(upright ? new Vector3(-v.X, v.Z, v.Y) : v); return points.Count - 1; }

        public void Out(int[] hs, int colour, Vector3 inside, bool unlit = false)
        {
            var n = Vector3.Zero; var centre = Vector3.Zero;
            for (var i = 0; i < hs.Length; i++)
            {
                var a = points[hs[i]]; var b = points[hs[(i + 1) % hs.Length]];
                n += new Vector3((a.Y - b.Y) * (a.Z + b.Z), (a.Z - b.Z) * (a.X + b.X), (a.X - b.X) * (a.Y + b.Y));
                centre += a;
            }
            centre /= hs.Length;
            if (Vector3.Dot(n, centre - inside) < 0) hs = hs.Reverse().ToArray();
            faces.Add((hs, colour, unlit));
        }

        public Body ToBody()
        {
            var body = new Body { Game = 2, Lit = true };
            body.Bones.Add(new Bone(0, points.Count, 0, -1, new byte[8]));
            body.Vertices.AddRange(points);
            body.SetWorld(points.ToArray());
            foreach (var (hs, colour, unlit) in faces) body.Faces.Add(new Face(hs, colour, Material: unlit ? 0 : -1));
            body.Validate();
            return body;
        }
    }
}

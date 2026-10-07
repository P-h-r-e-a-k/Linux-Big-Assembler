using System.Buffers.Binary;
using System.IO;
using System.Numerics;
using LbaBodyStudio;

namespace LBAAssembler.Demo96;

// The 1996 demo's bodies (BODY.HQR, OBJFIX.HQR and the islands' .OBL decors): the format between LBA1's and the retail LBA2 one. The
// art is the retail game's own (CITADEL.OBL's first decor has the retail one's bounding box to the unit), the layout:
//
//   header     U16 flags (2: animated), S16 x/y/z min and max, U16 size of what follows the header before the points
//   points     U16 count, then x, y, z (S16), each relative to its bone's pivot
//   bones      animated bodies only: U16 count, then 8 bytes a bone -- U16 point count (a bone's points follow the one before's),
//              U16 pivot point x 6, S16 parent x 36 (-1: the root), U16 its normals' count (LBA1's bone record is 38 bytes)
//   normals    U16 count, then x, y, z and a range (LBA1's)
//   polygons   LBA1's: U8 type, U8 points, U16 colour, U16 face normal for types 7-8, per point a U16 normal for types 9-10 and U16
//              point x 6
//   lines, spheres   LBA1's (8 bytes each)
//
// (Found by lining the sections up against the counts: Twinsen's 19 bones count 193 points, the body's points.)
//
//   textured polygons (types 11-36, the islands' decors' mostly): a light value or a normal and the point at each corner (a face normal
//              first for the flat ones: 12, 16, 20, 24), the texture as the retail table entry (U32), U16 u and v at each corner (8.8, the
//              retail values: the decors that are in both are drawn from the same places of the same pages)
//
// Written as retail LBA2 bodies (BodyStudio.Body): polygons of more than four points cut into a fan of quads and triangles (LBA2 draws
// triangles and quads only), each with its retail type (Untextured; the textured ones are the retail 8-23 less 3, found by matching the
// decors' polygons to the retail decors' by their texture coordinates), the textures a table of their own; the normals worked out from
// the geometry; the header 256 | 16 for an animated body, 16 for a fixed one (as the retail bodies).
internal static class Demo96Bodies
{
    // (`note`: what of the body was left out -- the rest of its polygons from one the reader can't follow, or those past the retail
    // engine's 550 polygons, lines and spheres -- so that every body is there, with its points and bones, for the animations and the
    // scenes that use it; null when nothing was)
    public static byte[] Convert(byte[] b) => Convert(b, out _);

    public static byte[] Convert(byte[] b, out string? note)
    {
        note = null;
        int U(int at) => BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(at));
        int S(int at) => BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan(at));
        void Need(int at, int n) { if (at < 0 || at + n > b.Length) throw new InvalidDataException("The demo body is cut short."); }

        Need(0, 16);
        var animated = (U(0) & 2) != 0;
        var p = 16 + U(14);
        Need(p, 2);
        var count = U(p); p += 2;
        Need(p, count * 6);
        var body = new Body { Game = 2, Lit = true, Static = !animated, Header = new byte[96] };
        BinaryPrimitives.WriteInt32LittleEndian(body.Header, animated ? 256 | 16 : 16);
        for (var i = 0; i < count; i++, p += 6) body.Vertices.Add(new Vector3(S(p), S(p + 2), S(p + 4)));

        if (animated)
        {
            Need(p, 2);
            var bones = U(p); p += 2;
            Need(p, bones * 8);
            var start = 0;
            for (var i = 0; i < bones; i++, p += 8)
            {
                int n = U(p), pivot = U(p + 2), parent = S(p + 4);
                if (pivot % 6 != 0 || (parent != -1 && parent % 36 != 0)) throw new InvalidDataException("A demo bone record is misaligned.");
                body.Bones.Add(new Bone(start, n, i == 0 ? 0 : pivot / 6, parent < 0 ? -1 : parent / 36, new byte[8]));
                start += n;
            }
            if (start != count) throw new InvalidDataException($"The demo body's bones count {start} points of its {count}.");
        }
        else body.Bones.Add(new Bone(0, count, 0, -1, new byte[8]));

        var textures = new List<uint>();
        var polygon = 0;
        try
        {
        Need(p, 2);
        var normals = U(p); p += 2 + normals * 8;
        Need(p, 2);
        var polygons = U(p); p += 2;
        for (polygon = 0; polygon < polygons; polygon++)
        {
            Need(p, 4);
            int type = b[p], n = b[p + 1], colour = U(p + 2) & 255;
            p += 4;
            if (n < 3 || n > 16 || type > 36) throw new InvalidDataException($"A demo polygon of type {type} with {n} points.");
            var ids = new int[n];
            int[]? uv = null;
            int lba2;
            if (type <= 10)
            {
                if (type is 7 or 8) p += 2;
                for (var k = 0; k < n; k++)
                {
                    if (type >= 9) p += 2;
                    ids[k] = Point(p); p += 2;
                }
                lba2 = Untextured[type];
            }
            else
            {
                // textured: a face normal first for the flat ones, a normal (or a light value) and the point at each corner, the texture
                // (the retail texture table's entry: low word the page offset, high the repeat mask), the corners' coordinates
                if (FlatTextured.Contains(type)) p += 2;
                for (var k = 0; k < n; k++) { ids[k] = Point(p + 2); p += 4; }
                Need(p, 4 + 4 * n);
                var word = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(p)); p += 4;
                uv = new int[2 * n];
                for (var k = 0; k < 2 * n; k++) { uv[k] = U(p); p += 2; }
                var handle = textures.IndexOf(word);
                if (handle < 0) { handle = textures.Count; textures.Add(word); }
                uv = uv.Prepend(handle).ToArray();
                // (the retail types 8-23 are the demo's 11-26; 36 has every corner on one texel: Gouraud textured)
                lba2 = type == 36 ? 10 : Math.Clamp(type - 3, 8, 23);
            }
            AddFan(body, ids, uv, colour, lba2);
        }
        Need(p, 2);
        var lines = U(p); p += 2;
        Need(p, lines * 8);
        for (var i = 0; i < lines; i++, p += 8) body.Lines.Add(new BodyLine(U(p + 4) / 6, U(p + 6) / 6, b[p + 1]));
        Need(p, 2);
        var spheres = U(p); p += 2;
        Need(p, spheres * 8);
        for (var i = 0; i < spheres; i++, p += 8) body.Spheres.Add(new BodySphere(U(p + 6) / 6, U(p + 4), b[p + 1]));
        }
        catch (InvalidDataException error) { note = $"polygons from {polygon} on left out ({error.Message})"; }
        body.Textures = textures.ToArray();
        var over = body.Faces.Count + body.Lines.Count + body.Spheres.Count - body.Limit;
        if (over > 0)
        {
            body.Faces.RemoveRange(body.Faces.Count - Math.Min(over, body.Faces.Count), Math.Min(over, body.Faces.Count));
            note = (note is null ? "" : note + "; ") + $"{over} polygons past the retail engine's {body.Limit} left out";
        }
        return body.Write();

        int Point(int at)
        {
            Need(at, 2);
            var reference = U(at);
            if (reference % 6 != 0 || reference / 6 >= count) throw new InvalidDataException("A demo polygon's point is outside the body.");
            return reference / 6;
        }
    }

    // The demo's untextured types (LBA1's: 0 flat, 1-3 copper / bopper / marble, 4 tele, 5 transparent, 6 trame, 7-8 flat lit, 9-10
    // Gouraud and dithered) as the retail ones (0 solid, 1 flat, 2 transparent, 3 trame, 4 Gouraud, 5 dithered), as the retail versions
    // of the same decors have them
    private static readonly int[] Untextured = { 0, 4, 4, 4, 0, 2, 3, 1, 1, 4, 5 };
    // the demo's textured types with a face normal: the flat ones (retail 9, 13, 17, 21)
    private static readonly HashSet<int> FlatTextured = new() { 12, 16, 20, 24 };

    // A polygon of up to 16 points as triangles and quads (LBA2 draws no others): a fan from its first point (the polygons are flat and
    // convex). `uv`: the texture handle, then the corners' coordinates (null: untextured).
    private static void AddFan(Body body, int[] ids, int[]? uv, int colour, int type)
    {
        void Add(int[] corners)
        {
            FaceTexture? texture = uv is null ? null : new FaceTexture(uv[0], corners.SelectMany(c => new[] { uv[1 + 2 * c], uv[2 + 2 * c] }).ToArray());
            body.Faces.Add(new Face(corners.Select(c => ids[c]).ToArray(), colour, Material: type is 0 or 2 or 3 ? 0 : -1, Texture: texture, Lba2Type: type));
        }
        var n = ids.Length;
        if (n <= 4) { Add(Enumerable.Range(0, n).ToArray()); return; }
        for (var k = 1; k + 1 < n; k += 2) Add(k + 2 < n ? new[] { 0, k, k + 1, k + 2 } : new[] { 0, k, k + 1 });
    }
}

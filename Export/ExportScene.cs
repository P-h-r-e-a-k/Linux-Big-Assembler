using System.Numerics;

namespace LBAAssembler.Export;

// A picture a material is painted with (RGBA, top row first).
internal sealed class TextureImage
{
    public required string Name { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required byte[] Rgba { get; init; }
    // Tiles of a body repeat (the engine wraps them); the ground atlas of an island is used once.
    public bool Repeat { get; init; } = true;
}

internal sealed class ExportMaterial
{
    public required string Name { get; init; }
    public byte R { get; init; } = 200;
    public byte G { get; init; } = 200;
    public byte B { get; init; } = 200;
    public TextureImage? Texture { get; init; }
}

// The triangles of one mesh that share a material (indices into the mesh's vertex lists).
internal sealed class ExportPrimitive
{
    public required int Material { get; init; }
    public List<int> Indices { get; } = new();
}

// Geometry in the game's own coordinates (X right, Y up, Z forward: a left-handed system); the writers turn it into the right-handed
// Y-up one every 3D tool uses. Triangles wind so that the plain cross product (b - a) x (c - a) points out of the surface.
internal sealed class ExportMesh
{
    public string Name { get; set; } = "mesh";
    public List<Vector3> Positions { get; } = new();
    // (u, v) in picture space: v counts down from the top row, as glTF and image files do.
    public List<Vector2> Uvs { get; } = new();
    // A per-vertex brightness (the island's baked light); null when the mesh has none.
    public List<Vector3>? Colors { get; }
    public List<ExportPrimitive> Primitives { get; } = new();

    public ExportMesh(string name, bool vertexColours = false)
    {
        Name = name;
        if (vertexColours) Colors = new List<Vector3>();
    }

    public int AddVertex(Vector3 position, Vector2 uv = default, Vector3? colour = null)
    {
        Positions.Add(position); Uvs.Add(uv);
        Colors?.Add(colour ?? Vector3.One);
        return Positions.Count - 1;
    }

    public void AddTriangle(int material, int a, int b, int c)
    {
        var primitive = Primitives.FirstOrDefault(p => p.Material == material);
        if (primitive is null) Primitives.Add(primitive = new ExportPrimitive { Material = material });
        primitive.Indices.Add(a); primitive.Indices.Add(b); primitive.Indices.Add(c);
    }

    public int TriangleCount => Primitives.Sum(p => p.Indices.Count / 3);
}

internal sealed class ExportNode
{
    public required string Name { get; init; }
    public required ExportMesh Mesh { get; init; }
    // Row-vector convention (System.Numerics): a point moves as p * Transform.
    public Matrix4x4 Transform { get; set; } = Matrix4x4.Identity;
}

internal sealed class ExportScene
{
    public string Name { get; set; } = "scene";
    public List<ExportMaterial> Materials { get; } = new();
    public List<ExportNode> Nodes { get; } = new();
    private readonly Dictionary<string, int> materialIndex = new();

    public int TriangleCount => Nodes.Sum(n => n.Mesh.TriangleCount);
    // What the source should have produced (set for bodies: every polygon, sphere and line accounted for); null when unknown.
    public int? ExpectedTriangles { get; set; }

    // A problem with the geometry (a NaN, an index outside its vertex list, a missing material), or null.
    public string? Problem()
    {
        foreach (var node in Nodes)
        {
            var mesh = node.Mesh;
            if (mesh.Uvs.Count != mesh.Positions.Count) return $"{node.Name}: uv count differs from the vertex count";
            foreach (var p in mesh.Positions) if (!float.IsFinite(p.X + p.Y + p.Z)) return $"{node.Name}: a vertex is not a finite number";
            foreach (var primitive in mesh.Primitives)
            {
                if (primitive.Material < 0 || primitive.Material >= Materials.Count) return $"{node.Name}: a triangle has no material";
                if (primitive.Indices.Count % 3 != 0) return $"{node.Name}: the index list isn't whole triangles";
                foreach (var i in primitive.Indices) if (i < 0 || i >= mesh.Positions.Count) return $"{node.Name}: an index is outside the vertex list";
            }
        }
        if (ExpectedTriangles is { } expected && expected != TriangleCount) return $"expected {expected} triangles but built {TriangleCount}";
        return null;
    }
    public bool HasTextures => Materials.Any(m => m.Texture is not null);

    public ExportNode Add(ExportMesh mesh, Matrix4x4? transform = null, string? name = null)
    {
        var node = new ExportNode { Name = name ?? mesh.Name, Mesh = mesh, Transform = transform ?? Matrix4x4.Identity };
        Nodes.Add(node);
        return node;
    }

    // The material of that key, made the first time it is asked for.
    public int Material(string key, Func<ExportMaterial> make)
    {
        if (materialIndex.TryGetValue(key, out var index)) return index;
        Materials.Add(make());
        return materialIndex[key] = Materials.Count - 1;
    }

    // The box round everything (game coordinates, node transforms applied), or null for an empty scene.
    public (Vector3 Min, Vector3 Max)? Bounds()
    {
        var min = new Vector3(float.MaxValue); var max = new Vector3(float.MinValue);
        var any = false;
        foreach (var node in Nodes)
        {
            // (a mesh's own box is transformed corner by corner)
            var (lo, hi) = MeshBounds(node.Mesh);
            if (lo.X > hi.X) continue;
            for (var i = 0; i < 8; i++)
            {
                var corner = Vector3.Transform(new Vector3((i & 1) == 0 ? lo.X : hi.X, (i & 2) == 0 ? lo.Y : hi.Y, (i & 4) == 0 ? lo.Z : hi.Z), node.Transform);
                min = Vector3.Min(min, corner); max = Vector3.Max(max, corner); any = true;
            }
        }
        return any ? (min, max) : null;
    }

    private readonly Dictionary<ExportMesh, (Vector3, Vector3)> meshBounds = new();
    private (Vector3, Vector3) MeshBounds(ExportMesh mesh)
    {
        if (meshBounds.TryGetValue(mesh, out var known)) return known;
        var min = new Vector3(float.MaxValue); var max = new Vector3(float.MinValue);
        foreach (var p in mesh.Positions) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
        return meshBounds[mesh] = (min, max);
    }

    // Slides the whole scene so it stands on the ground (lowest point at height 0) with its middle over the origin.
    public void RecentreOnOrigin()
    {
        if (Bounds() is not { } b) return;
        var shift = Matrix4x4.CreateTranslation(-(b.Min.X + b.Max.X) / 2, -b.Min.Y, -(b.Min.Z + b.Max.Z) / 2);
        foreach (var node in Nodes) node.Transform *= shift;
    }

    // Several scenes as one: `keepPlaces` leaves each where it is (islands, objects of one island); otherwise they stand in a row along X,
    // each on the ground with a gap between.
    public static ExportScene Merge(string name, IReadOnlyList<ExportScene> parts, bool keepPlaces)
    {
        var result = new ExportScene { Name = name };
        var cursor = 0f;
        foreach (var part in parts)
        {
            var place = Matrix4x4.Identity;
            if (!keepPlaces && part.Bounds() is { } b)
            {
                var width = b.Max.X - b.Min.X;
                place = Matrix4x4.CreateTranslation(cursor - b.Min.X, -b.Min.Y, -(b.Min.Z + b.Max.Z) / 2);
                cursor += width + Math.Max(200f, width * 0.15f);
            }
            var offset = result.Materials.Count;
            result.Materials.AddRange(part.Materials);
            var copies = new Dictionary<ExportMesh, ExportMesh>();
            foreach (var node in part.Nodes)
            {
                if (!copies.TryGetValue(node.Mesh, out var mesh))
                {
                    mesh = new ExportMesh(node.Mesh.Name, node.Mesh.Colors is not null);
                    for (var i = 0; i < node.Mesh.Positions.Count; i++) mesh.AddVertex(node.Mesh.Positions[i], node.Mesh.Uvs[i], node.Mesh.Colors?[i]);
                    foreach (var primitive in node.Mesh.Primitives)
                        for (var i = 0; i + 2 < primitive.Indices.Count; i += 3) mesh.AddTriangle(primitive.Material + offset, primitive.Indices[i], primitive.Indices[i + 1], primitive.Indices[i + 2]);
                    copies[node.Mesh] = mesh;
                }
                result.Nodes.Add(new ExportNode { Name = node.Name, Mesh = mesh, Transform = node.Transform * place });
            }
        }
        return result;
    }

    // The scene in the right-handed, Y-up coordinates of glTF / OBJ / PLY / STL, scaled (game units to metres by default): the
    // Z axis is mirrored, so every triangle changes its winding, and each node's matrix is conjugated by the mirror.
    public ExportScene ToRightHanded(float scale)
    {
        var result = new ExportScene { Name = Name };
        result.Materials.AddRange(Materials);
        var converted = new Dictionary<ExportMesh, ExportMesh>();
        foreach (var node in Nodes)
        {
            if (!converted.TryGetValue(node.Mesh, out var mesh))
            {
                mesh = new ExportMesh(node.Mesh.Name, node.Mesh.Colors is not null);
                for (var i = 0; i < node.Mesh.Positions.Count; i++)
                    mesh.AddVertex(new Vector3(node.Mesh.Positions[i].X * scale, node.Mesh.Positions[i].Y * scale, -node.Mesh.Positions[i].Z * scale), node.Mesh.Uvs[i], node.Mesh.Colors?[i]);
                foreach (var primitive in node.Mesh.Primitives)
                    for (var i = 0; i + 2 < primitive.Indices.Count; i += 3)
                        mesh.AddTriangle(primitive.Material, primitive.Indices[i], primitive.Indices[i + 2], primitive.Indices[i + 1]);
                converted[node.Mesh] = mesh;
            }
            var m = node.Transform;
            (m.M13, m.M23, m.M31, m.M32, m.M43) = (-m.M13, -m.M23, -m.M31, -m.M32, -m.M43);
            (m.M41, m.M42, m.M43) = (m.M41 * scale, m.M42 * scale, m.M43 * scale);
            result.Nodes.Add(new ExportNode { Name = node.Name, Mesh = mesh, Transform = m });
        }
        return result;
    }

    // Every node's triangles with its transform applied, one mesh per node (the formats without instancing use these).
    public IEnumerable<(string Name, ExportMesh Mesh)> Baked()
    {
        foreach (var node in Nodes)
        {
            if (node.Transform == Matrix4x4.Identity) { yield return (node.Name, node.Mesh); continue; }
            var mesh = new ExportMesh(node.Mesh.Name, node.Mesh.Colors is not null);
            for (var i = 0; i < node.Mesh.Positions.Count; i++) mesh.AddVertex(Vector3.Transform(node.Mesh.Positions[i], node.Transform), node.Mesh.Uvs[i], node.Mesh.Colors?[i]);
            foreach (var primitive in node.Mesh.Primitives)
                for (var i = 0; i + 2 < primitive.Indices.Count; i += 3) mesh.AddTriangle(primitive.Material, primitive.Indices[i], primitive.Indices[i + 1], primitive.Indices[i + 2]);
            yield return (node.Name, mesh);
        }
    }
}

internal static class Palettes
{
    // A colour of a 256-entry RGB palette; the island palettes are 6-bit (0..63), the others 8-bit.
    [ThreadStatic] private static byte[]? lastPalette;
    [ThreadStatic] private static bool lastSix;

    public static (byte R, byte G, byte B) Colour(byte[] palette, int index)
    {
        var i = Math.Clamp(index, 0, Math.Max(0, palette.Length / 3 - 1)) * 3;
        if (i + 2 >= palette.Length) return (200, 200, 200);
        if (!ReferenceEquals(palette, lastPalette)) { lastPalette = palette; lastSix = SixBit(palette); }
        var six = lastSix;
        byte S(byte v) => (byte)Math.Min(255, six ? v * 4 : v);
        return (S(palette[i]), S(palette[i + 1]), S(palette[i + 2]));
    }

    public static bool SixBit(byte[] palette)
    {
        var max = 0;
        for (var i = 0; i < Math.Min(768, palette.Length); i++) max = Math.Max(max, palette[i]);
        return max <= 63;
    }
}

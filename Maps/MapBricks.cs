using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using LBAAssembler.Scenes;

namespace LBAAssembler.Maps;

// An LBA2 interior map's own block library, bricks and grid fragments -- its "brick package" (the user, 2026-10-15: "I don't know if when
// loading a map raw it can also reference a brick package but that's what i'd like to do"): the map's BL2, BRK and GRM files.
//   - Package: out of a game folder's LBA_BKG.HQR, the library the map's grid names, every brick it names (its entries as stored, numbered
//     from 0 in the game's order; the library's brick numbers renumbered to them) and the grid fragments its scene's fragment zones use.
//   - Install: into a game folder's LBA_BKG.HQR, for the editors and Play to work on (MapInstall). A brick the file has already is used, the
//     others go after its bricks; the library, renumbered to them, is the file's own it equals, else goes after its libraries; the fragments
//     likewise after its fragments. Read back by Package.
// The engine reads the package straight from the map file (MAPPACK.CPP).
internal static class MapBricks
{
    // LBA_BKG.HQR's entry 0.
    internal sealed class BkgHeader
    {
        public readonly byte[] Bytes;
        public BkgHeader(byte[] bytes) => Bytes = bytes;
        public int Gri { get => U16(0); set => Put16(0, value); }
        public int Grm { get => U16(2); set => Put16(2, value); }
        public int Bll { get => U16(4); set => Put16(4, value); }
        public int Brk { get => U16(6); set => Put16(6, value); }
        public int MaxBrk { get => U16(8); set => Put16(8, value); }
        public int Forbidden => U16(10);
        public uint MaxGri { get => U32(12); set => Put32(12, value); }
        public uint MaxBll { get => U32(16); set => Put32(16, value); }
        public uint MaxBrickCube { get => U32(20); set => Put32(20, value); }
        public uint MaxMaskCube { get => U32(24); set => Put32(24, value); }
        private int U16(int at) => BinaryPrimitives.ReadUInt16LittleEndian(Bytes.AsSpan(at));
        private void Put16(int at, int v) => BinaryPrimitives.WriteUInt16LittleEndian(Bytes.AsSpan(at), (ushort)v);
        private uint U32(int at) => Bytes.Length >= at + 4 ? BinaryPrimitives.ReadUInt32LittleEndian(Bytes.AsSpan(at)) : 0;
        private void Put32(int at, uint v) { if (Bytes.Length >= at + 4) BinaryPrimitives.WriteUInt32LittleEndian(Bytes.AsSpan(at), v); }
    }

    // ---- out of a game folder
    public static void Package(LbaMap map, string directory) => Package(map, HqrFile.Parse(File.ReadAllBytes(Path.Combine(directory, "LBA_BKG.HQR"))));

    public static void Package(LbaMap map, HqrFile bkg)
    {
        if (map.Kind != MapKind.Lba2Interior || map.Grid is null) return;
        var header = new BkgHeader(bkg.Read(0));
        var library = bkg.Read(header.Bll + map.Grid[0]);
        var positions = BrickPositions(library);
        var used = positions.Select(p => BinaryPrimitives.ReadUInt16LittleEndian(library.AsSpan(p)) - 1).Where(b => b >= 0 && b < header.MaxBrk).Distinct().Order().ToList();
        var local = used.Select((game, k) => (game, k)).ToDictionary(x => x.game, x => x.k);
        var own = (byte[])library.Clone();
        foreach (var p in positions)
        {
            var b = BinaryPrimitives.ReadUInt16LittleEndian(library.AsSpan(p)) - 1;
            BinaryPrimitives.WriteUInt16LittleEndian(own.AsSpan(p), (ushort)(local.TryGetValue(b, out var k) ? k + 1 : 0));
        }
        var bricks = new HqrFile();
        foreach (var game in used)
            bricks.Slots.Add(new HqrFile.Slot { Extent = bkg.IsEmpty(header.Brk + game) ? HqrWriter.StoredEntry(Array.Empty<byte>()) : bkg.ExtentOf(header.Brk + game) });
        map.Library = own;
        map.Bricks = bricks.Count > 0 ? bricks.ToBytes() : HqrWriter.AppendEntry(EmptyHqr(), HqrWriter.StoredEntry(Array.Empty<byte>()));
        map.ForbiddenBrick = local.TryGetValue(header.Forbidden, out var forbidden) ? forbidden : null;
        map.Fragments = Fragments(map, bkg, header);
    }

    // The grid fragments the map's scene's fragment zones (type 3, Info0 the fragment) use: entries from the grid's header's first on.
    private static byte[]? Fragments(LbaMap map, HqrFile bkg, BkgHeader header)
    {
        var zones = SceneSerializer.Parse(SceneGame.Lba2, map.Scenes[0].Record).Zones.Where(z => z.Type == 3 && z.Info[0] >= 0).ToList();
        if (zones.Count == 0) return null;
        var count = zones.Max(z => z.Info[0]) + 1;
        var first = header.Grm + map.Grid![1];
        var file = new HqrFile();
        for (var i = 0; i < count; i++)
            file.Slots.Add(new HqrFile.Slot { Extent = first + i < header.Bll && !bkg.IsEmpty(first + i) ? bkg.ExtentOf(first + i) : HqrWriter.StoredEntry(Array.Empty<byte>()) });
        return file.ToBytes();
    }

    // ---- into a game folder's LBA_BKG.HQR (`bkg`, its header written back): the library's and the fragments' numbers the grid's header gets
    public static (int Library, int Fragments) Install(LbaMap map, HqrFile bkg)
    {
        var header = new BkgHeader(bkg.Read(0));
        int library = map.Grid![0], fragments = map.Grid[1];
        if (!map.OwnBricks) return (library, fragments);
        var package = HqrFile.Parse(map.Bricks!);

        // the bricks: each the file's own it equals (as stored, else as decoded), else added after its bricks
        var byExtent = new Dictionary<string, int>();
        for (var b = 0; b < header.MaxBrk; b++)
            if (!bkg.IsEmpty(header.Brk + b)) byExtent.TryAdd(Hash(bkg.ExtentOf(header.Brk + b)), b);
        Dictionary<string, int>? byPayload = null;
        var number = new int[package.Count];
        var added = new List<byte[]>();
        for (var k = 0; k < package.Count; k++)
        {
            var extent = package.IsEmpty(k) ? HqrWriter.StoredEntry(Array.Empty<byte>()) : package.ExtentOf(k);
            if (byExtent.TryGetValue(Hash(extent), out var g)) { number[k] = g; continue; }
            byPayload ??= Enumerable.Range(0, header.MaxBrk).Where(b => !bkg.IsEmpty(header.Brk + b))
                .Select(b => (b, Hash(bkg.Read(header.Brk + b)))).GroupBy(x => x.Item2).ToDictionary(x => x.Key, x => x.First().b);
            if (byPayload.TryGetValue(Hash(package.IsEmpty(k) ? Array.Empty<byte>() : package.Read(k)), out g)) { number[k] = g; continue; }
            number[k] = header.MaxBrk + added.Count;
            added.Add(extent);
        }
        if (header.MaxBrk + added.Count > 20000) throw new SceneEditException($"The map's bricks would make {header.MaxBrk + added.Count} in LBA_BKG.HQR: the engine holds 20000.");

        // the library, its brick numbers the file's
        var own = (byte[])map.Library!.Clone();
        foreach (var p in BrickPositions(own))
        {
            var k = BinaryPrimitives.ReadUInt16LittleEndian(own.AsSpan(p)) - 1;
            BinaryPrimitives.WriteUInt16LittleEndian(own.AsSpan(p), (ushort)(k >= 0 && k < number.Length ? number[k] + 1 : 0));
        }
        library = -1;
        for (var l = 0; l < header.Brk - header.Bll; l++)
            if (!bkg.IsEmpty(header.Bll + l) && bkg.Read(header.Bll + l).AsSpan().SequenceEqual(own)) { library = l; break; }

        // the fragments: the file's from the grid's header's first on if they are the same, else added after its fragments
        if (map.Fragments is { } frag)
        {
            var mine = HqrFile.Parse(frag);
            bool Same(int first) => Enumerable.Range(0, mine.Count).All(i => first + i < header.Bll && !bkg.IsEmpty(first + i) && !mine.IsEmpty(i)
                && bkg.Read(first + i).AsSpan().SequenceEqual(mine.Read(i)));
            if (!Same(header.Grm + fragments)) fragments = -1;
        }

        // (inserted from the last section to the first: an entry inserted doesn't move the ones before it)
        var at = header.Brk + header.MaxBrk;
        foreach (var extent in added) { bkg.InsertAt(at, Array.Empty<byte>()); bkg.SetEntry(at, extent); at++; }
        header.MaxBrk += added.Count;
        if (library < 0)
        {
            library = header.Brk - header.Bll;
            if (library > 255) throw new SceneEditException("LBA_BKG.HQR has 256 block libraries already: a grid can't name another.");
            bkg.InsertAt(header.Brk, own);
            header.Brk += 1;
            if (header.MaxBll < own.Length) header.MaxBll = (uint)own.Length;
        }
        if (fragments < 0)
        {
            var mine = HqrFile.Parse(map.Fragments!);
            fragments = header.Bll - header.Grm;
            if (fragments + mine.Count > 256) throw new SceneEditException("LBA_BKG.HQR's grid fragments are too many for a grid to name the map's.");
            for (var i = 0; i < mine.Count; i++) { bkg.InsertAt(header.Bll + i, Array.Empty<byte>()); bkg.SetEntry(header.Bll + i, mine.IsEmpty(i) ? HqrWriter.StoredEntry(Array.Empty<byte>()) : mine.ExtentOf(i)); }
            header.Bll += mine.Count;
            header.Brk += mine.Count;
        }

        // the engine's buffers for a cube's bricks and their masks (LoadUsedBrick, CreateMaskGph): this grid's too
        var (bytes, n) = UsedBrickBytes(map.Grid, own, b => b < header.MaxBrk && !bkg.IsEmpty(header.Brk + b) ? BinaryPrimitives.ReadUInt32LittleEndian(bkg.ExtentOf(header.Brk + b)) : 0);
        if (header.MaxBrickCube < (uint)((n + 1) * 4 + bytes)) header.MaxBrickCube = (uint)((n + 1) * 4 + bytes);
        if (header.MaxMaskCube < (uint)((n + 1) * 4 + bytes + n * 64)) header.MaxMaskCube = (uint)((n + 1) * 4 + bytes + n * 64);
        bkg.SetStored(0, header.Bytes);
        return (library, fragments);
    }

    // ---- read back: the map's package as the folder now has it (Package, its grid's header as the map's: the numbers the folder gave the
    // library and the fragments are the folder's, not the map's)
    public static void Capture(LbaMap captured, LbaMap map, HqrFile bkg)
    {
        if (!map.OwnBricks) return;
        Package(captured, bkg);
        if (map.Fragments is null) captured.Fragments = null;
        captured.Grid![0] = map.Grid![0];
        captured.Grid[1] = map.Grid[1];
    }

    // ---- the library's brick numbers: where they are (each once: blocks can share their bytes)
    public static List<int> BrickPositions(byte[] library)
    {
        var positions = new SortedSet<int>();
        if (library.Length < 4) return positions.ToList();
        var count = BinaryPrimitives.ReadUInt32LittleEndian(library) / 4;
        for (var b = 0; b < count && b * 4 + 4 <= library.Length; b++)
        {
            var at = (int)BinaryPrimitives.ReadUInt32LittleEndian(library.AsSpan(b * 4));
            if (at < 0 || at + 3 > library.Length) continue;
            var cells = library[at] * library[at + 1] * library[at + 2];
            for (var c = 0; c < cells && at + 3 + c * 4 + 4 <= library.Length; c++) positions.Add(at + 3 + c * 4 + 2);
        }
        return positions.ToList();
    }

    // The bricks a grid's used blocks name (its header's bitmap), their decoded sizes added up, and how many.
    public static (long Bytes, int Count) UsedBrickBytes(byte[] grid, byte[] library, Func<int, uint> size)
    {
        var bricks = new HashSet<int>();
        var count = BinaryPrimitives.ReadUInt32LittleEndian(library) / 4;
        for (var i = 1; i < 256; i++)
        {
            if ((grid[2 + (i >> 3)] & (1 << (7 - (i & 7)))) == 0 || i - 1 >= count) continue;
            var at = (int)BinaryPrimitives.ReadUInt32LittleEndian(library.AsSpan((i - 1) * 4));
            if (at + 3 > library.Length) continue;
            var cells = library[at] * library[at + 1] * library[at + 2];
            for (var c = 0; c < cells && at + 5 + c * 4 + 2 <= library.Length; c++)
            {
                var b = BinaryPrimitives.ReadUInt16LittleEndian(library.AsSpan(at + 5 + c * 4));
                if (b > 0) bricks.Add(b - 1);
            }
        }
        return (bricks.Sum(b => (long)size(b)), bricks.Count);
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA1.HashData(bytes));

    private static byte[] EmptyHqr()
    {
        var empty = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(empty, 8);
        BinaryPrimitives.WriteUInt32LittleEndian(empty.AsSpan(4), 8);
        return empty;
    }
}

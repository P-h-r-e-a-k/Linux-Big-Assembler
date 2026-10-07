using System.Buffers.Binary;
using System.IO;
using LBAAssembler.Scenes;

namespace LBAAssembler.Demo96;

// The 1996 LBA2 demo (LBA2DEMO.EXE: Citadel Island before and after the storm, Desert Island, Otringal and 46 interiors, French) turned
// into a folder laid out as the retail game's, so that the editor -- the C# side and the engine's renderer, both of which read the retail
// layout -- shows its islands, interiors and scenes. What differs, and what is done about it:
//
//   SCENE.HQR    the scene records (Demo96Scenes); no entry 0 (the retail one holds the size of the largest record), so demo scene N is
//                retail entry N + 1
//   BODY.HQR, OBJFIX.HQR, *.OBL   bodies in the older format (Demo96Bodies)
//   RESS.HQR     a smaller resource file of another layout: entry 0 is the palette with its shading table and shading levels after it
//                (the retail island palettes, XPL, have a header, the palette, the same table and a transparency table); the entities are
//                FILE3D.HQR's entries, one an entity (retail: one table, RESS 44); there are skies for Citadel Island (11, and 26 after
//                the storm) and Desert Island (13), none for Otringal
//   LBA_BKG.HQR  a 14-byte header without the retail one's four buffer sizes (the grids, block libraries and bricks are the retail
//                format: a demo brick is byte for byte retail brick 198)
//   ANIM.HQR, the islands (.ILE), SPRITES.HQR, SPRIRAW.HQR   the retail format, copied
//
// What the demo has none of and the engine reads when it starts -- the memory sizes (RESS 2), the inventory pictures (5, 8), the 3DS
// animations (RESS 43 and ANIM3DS.HQR), the particle flows (45), POF, impacts and the ACF list (46-48), the main archive (LBA2.HQR, which
// the engine looks for to know a game folder) and the holomap -- comes from the retail game's folder: they draw nothing of the demo's own.
internal static class Demo96Converter
{
    // The file a converted folder carries (MarkerFile): what it is, the converter's version and the demo folder it was made from, then each
    // demo scene's retail counterpart ("scene <demo> <retail>", -1 for none): the editor names the demo's scenes by them.
    public const string MarkerFile = "LBA2-1996-DEMO.TXT";
    public const int Version = 2;

    // Whether `dir` is a converted folder of this version made from `demoDir`.
    public static bool IsConverted(string dir, string demoDir)
    {
        var marker = Path.Combine(dir, MarkerFile);
        if (!File.Exists(marker)) return false;
        var lines = File.ReadAllLines(marker);
        return lines.Length > 2 && lines[1] == $"version {Version}" && string.Equals(lines[2], $"source {Path.GetFullPath(demoDir)}", StringComparison.OrdinalIgnoreCase);
    }

    // A converted folder's demo scene -> retail scene map; null for a folder that is not one.
    public static Dictionary<int, int>? SceneMap(string dir)
    {
        var marker = Path.Combine(dir, MarkerFile);
        if (!File.Exists(marker)) return null;
        var map = new Dictionary<int, int>();
        foreach (var line in File.ReadAllLines(marker))
        {
            var parts = line.Split(' ');
            if (parts.Length == 3 && parts[0] == "scene" && int.TryParse(parts[1], out var demo) && int.TryParse(parts[2], out var retail)) map[demo] = retail;
        }
        return map;
    }

    // A folder holding the 1996 demo: its executable, or the entity file the retail game doesn't have, beside its scenes.
    public static bool IsDemoFolder(string dir) =>
        File.Exists(Path.Combine(dir, "SCENE.HQR")) && File.Exists(Path.Combine(dir, "RESS.HQR")) &&
        (File.Exists(Path.Combine(dir, "LBA2DEMO.EXE")) || File.Exists(Path.Combine(dir, "FILE3D.HQR")));

    // The demo's island files, in the retail order of their palettes and skies (the island numbers the scenes use)
    private static readonly (string File, int Island)[] Islands = { ("CITADEL", 0), ("DESERT", 2), ("OTRINGAL", 4), ("CITABAU", 0) };

    // Converts `demoDir` into `outDir`, with what the demo lacks from the retail game's `retailDir`. Returns lines for a log.
    public static List<string> Convert(string demoDir, string outDir, string retailDir)
    {
        var log = new List<string>();
        Directory.CreateDirectory(outDir);
        string Demo(string name) => Path.Combine(demoDir, name);
        string Out(string name) => Path.Combine(outDir, name);

        // copied as they are
        foreach (var name in new[] { "ANIM.HQR", "SPRITES.HQR", "SPRIRAW.HQR", "TEXT.HQR", "SAMPLES.HQR", "SCREEN.HQR", "SCRSHOT.HQR", "FLOW.HQR", "FILE3D.HQR" })
            if (File.Exists(Demo(name))) File.Copy(Demo(name), Out(name), true);
        foreach (var (island, _) in Islands)
        {
            if (!File.Exists(Demo(island + ".ILE"))) continue;
            var (ile, textures) = ConvertIsland(File.ReadAllBytes(Demo(island + ".ILE")));
            File.WriteAllBytes(Out(island + ".ILE"), ile);
            log.Add($"{island}.ILE: {textures} ground texture definitions in the retail format");
        }
        // the retail game's, for what the engine loads at its start and the demo has none of
        foreach (var name in new[] { "ANIM3DS.HQR", "LBA2.HQR", "HOLOMAP.HQR" })
            if (File.Exists(Path.Combine(retailDir, name))) File.Copy(Path.Combine(retailDir, name), Out(name), true);
        // (the engine wants the cutscene videos' file, in video/, to start: an empty one -- viewing the demo plays none)
        Directory.CreateDirectory(Out("video"));
        if (!File.Exists(Out(Path.Combine("video", "VIDEO.HQR")))) File.WriteAllBytes(Out(Path.Combine("video", "VIDEO.HQR")), Build(new List<byte[]?>()));
        log.Add("copied: the animations, the islands' ground, the sprites, the texts and the samples (the retail format)");

        // the bodies
        foreach (var name in new[] { "BODY.HQR", "OBJFIX.HQR" }.Concat(Islands.Select(i => i.File + ".OBL")))
        {
            if (!File.Exists(Demo(name))) continue;
            var (bytes, done, failed) = ConvertBodies(File.ReadAllBytes(Demo(name)));
            File.WriteAllBytes(Out(name), bytes);
            log.Add($"{name}: {done} bodies in the retail format" + (failed.Count > 0 ? $" ({string.Join("; ", failed.Take(8))})" : ""));
        }

        // the scenes
        {
            var demo = Entries(File.ReadAllBytes(Demo("SCENE.HQR")));
            var scenes = new List<byte[]?> { null };
            var largest = 0; var unpatched = 0; var failed = new List<string>();
            for (var i = 0; i < demo.Count; i++)
            {
                if (demo[i] is null) { scenes.Add(null); continue; }
                try
                {
                    var model = Demo96Scenes.Parse(Unpack(demo[i]!));
                    byte[] record;
                    try { record = SceneSerializer.Write(model); }
                    catch (Exception)
                    {
                        // (scripts the patch table can't be rebuilt from: an empty one)
                        model.RebuildPatches = false; model.Tail = new byte[4];
                        record = SceneSerializer.Write(model);
                        unpatched++;
                    }
                    largest = Math.Max(largest, record.Length);
                    scenes.Add(HqrWriter.StoredEntry(record));
                }
                catch (Exception e) { failed.Add($"{i}: {e.Message}"); scenes.Add(null); }
            }
            var size = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(size, largest);
            scenes[0] = HqrWriter.StoredEntry(size);
            File.WriteAllBytes(Out("SCENE.HQR"), Build(scenes));
            log.Add($"SCENE.HQR: {scenes.Count(s => s is not null) - 1} scenes in the retail format (demo scene N is retail entry N + 1)" +
                    (unpatched > 0 ? $", {unpatched} with an empty patch table" : "") + (failed.Count > 0 ? $"; not converted: {string.Join("; ", failed)}" : ""));
        }

        // the interiors' header
        {
            var bkg = Entries(File.ReadAllBytes(Demo("LBA_BKG.HQR")));
            var h = Unpack(bkg[0]!);
            int gri = U16(h, 0), grm = U16(h, 2), bll = U16(h, 4), brk = U16(h, 6), maxBrk = U16(h, 8), forbidden = U16(h, 10);
            long bricks = 0;
            for (var i = brk; i < brk + maxBrk && i < bkg.Count; i++) if (bkg[i] is { } e) bricks += BinaryPrimitives.ReadUInt32LittleEndian(e);
            var header = new byte[28];
            foreach (var (at, v) in new[] { (0, gri), (2, grm), (4, bll), (6, brk), (8, maxBrk), (10, forbidden) }) BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(at), (ushort)v);
            // (the largest grid and library the engine measures itself; the bricks a cube loads, and their masks, are all of them at most)
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(20), (uint)bricks);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(24), (uint)bricks);
            bkg[0] = HqrWriter.StoredEntry(header);
            File.WriteAllBytes(Out("LBA_BKG.HQR"), Build(bkg));
            log.Add($"LBA_BKG.HQR: the retail header (grids from {gri}, libraries from {bll}, {maxBrk} bricks from {brk}; brick buffers {bricks} bytes)");
        }

        // the resources
        {
            var demo = Entries(File.ReadAllBytes(Demo("RESS.HQR")));
            var retail = Entries(File.ReadAllBytes(Path.Combine(retailDir, "RESS.HQR")));
            var ress = new List<byte[]?>(Enumerable.Repeat<byte[]?>(null, Math.Max(50, retail.Count)));
            byte[]? D(int i) => i < demo.Count ? demo[i] : null;
            byte[]? R(int i) => i < retail.Count ? retail[i] : null;
            var main = Unpack(D(0)!);
            var palette = main[..768];
            ress[0] = HqrWriter.StoredEntry(palette);
            ress[1] = D(1);                                            // the font
            ress[2] = R(2);                                            // memory sizes
            foreach (var i in new[] { 5, 8, 10, 43, 45, 46, 47, 48 }) ress[i] = R(i);
            ress[6] = D(6) ?? R(6);                                    // the bodies' texture page
            ress[7] = D(7) ?? R(7);                                    // game over
            ress[9] = HqrWriter.StoredEntry(new byte[768]);            // the black palette
            // the skies: the demo's own, and Citadel Island's where an island has none
            var citadelSky = D(11);
            for (var i = 11; i <= 21; i++) ress[i] = D(i) ?? citadelSky;
            ress[26] = D(26) ?? citadelSky;
            // the island palettes: the demo has two palettes with their shading tables -- entry 9 fading to black (fog colour 0: Citadel
            // Island in the storm, as the retail one), entry 0 to a pale blue (205: the fine-weather islands) -- each made a retail one
            // with a transparency table worked out from its palette; the interiors take the fine-weather one, as the retail ones take
            // the fine-weather Citadel Island's
            var fine = HqrWriter.CompressedEntry(IslandPalette(main));
            var storm = D(9) is { } stormEntry ? HqrWriter.CompressedEntry(IslandPalette(Unpack(stormEntry))) : fine;
            for (var i = 27; i <= 37; i++) ress[i] = fine;
            ress[27] = storm;
            ress[42] = fine;
            // the entities: FILE3D.HQR's, one table
            ress[44] = HqrWriter.StoredEntry(EntityTable(Entries(File.ReadAllBytes(Demo("FILE3D.HQR"))).Select(e => e is null ? Array.Empty<byte>() : Unpack(e)).ToList()));
            File.WriteAllBytes(Out("RESS.HQR"), Build(ress));
            log.Add($"RESS.HQR: the retail layout -- the demo's palette, font, skies and {Entries(File.ReadAllBytes(Demo("FILE3D.HQR"))).Count(e => e is not null)} entities, " +
                    "island palettes built from its palette and shading table; memory sizes, inventory pictures, 3DS animations, flows and impacts from the retail game");
        }
        // which retail scene each demo scene is: an outdoor one the retail scene of its island's cube; an interior the retail scene drawn
        // with the retail grid most like its own (most of the demo's interiors are early versions of the retail ones, under the same
        // numbers: its Citadel Island houses are the retail scenes 0-22)
        var counterparts = Counterparts(demoDir, retailDir);
        var marker = new List<string> { "LBA Assembler: the 1996 LBA2 demo in the retail game's layout", $"version {Version}", $"source {Path.GetFullPath(demoDir)}" };
        marker.AddRange(counterparts.OrderBy(c => c.Key).Select(c => $"scene {c.Key} {c.Value}"));
        File.WriteAllLines(Out(MarkerFile), marker);
        log.Add($"{MarkerFile}: {counterparts.Count(c => c.Value >= 0)} of the {counterparts.Count} scenes matched to a retail scene");
        return log;
    }

    // Each demo scene's retail counterpart (-1: none).
    private static Dictionary<int, int> Counterparts(string demoDir, string retailDir)
    {
        var result = new Dictionary<int, int>();
        var demoScenes = Entries(File.ReadAllBytes(Path.Combine(demoDir, "SCENE.HQR")));
        var retailScenes = Entries(File.ReadAllBytes(Path.Combine(retailDir, "SCENE.HQR")));
        // the retail outdoor scenes by island and cube; the interiors' islands
        var outdoors = new Dictionary<(int, int, int), int>();
        var interiorIsland = new Dictionary<int, int>();
        for (var e = 1; e < retailScenes.Count; e++)
        {
            if (retailScenes[e] is not { } entry) continue;
            var h = Unpack(entry);
            if (h.Length > 5 && h[5] != 0) outdoors.TryAdd((h[0], h[1], h[2]), e - 1);
            else if (h.Length > 5) interiorIsland[e - 1] = h[0];
        }
        // the grids: the demo's and the retail game's, and which scenes use them (the scene table: two bytes a scene, the second the grid)
        var demoBkg = Entries(File.ReadAllBytes(Path.Combine(demoDir, "LBA_BKG.HQR")));
        var retailBkg = Entries(File.ReadAllBytes(Path.Combine(retailDir, "LBA_BKG.HQR")));
        var dh = Unpack(demoBkg[0]!); var rh = Unpack(retailBkg[0]!);
        int dGri = U16(dh, 0), dGrm = U16(dh, 2), rGri = U16(rh, 0), rGrm = U16(rh, 2);
        var dTable = Unpack(demoBkg[U16(dh, 6) + U16(dh, 8)]!); var rTable = Unpack(retailBkg[U16(rh, 6) + U16(rh, 8)]!);
        var retailGrids = Enumerable.Range(rGri, rGrm - rGri).Where(i => retailBkg[i] is not null).ToDictionary(i => i - rGri, i => Unpack(retailBkg[i]!));
        double Like(byte[] a, byte[] b)
        {
            var n = Math.Min(a.Length, b.Length); var same = 0;
            for (var i = 34; i < n; i++) if (a[i] == b[i]) same++;
            return (double)same / Math.Max(a.Length, b.Length);
        }
        for (var scene = 0; scene < demoScenes.Count; scene++)
        {
            if (demoScenes[scene] is not { } entry) continue;
            var h = Unpack(entry);
            var match = -1;
            if (h.Length > 5 && h[5] != 0) match = outdoors.TryGetValue((h[0], h[1], h[2]), out var o) ? o : -1;
            else if (scene * 2 + 1 < dTable.Length && dTable[scene * 2 + 1] + dGri < dGrm && demoBkg[dTable[scene * 2 + 1] + dGri] is { } grid)
            {
                var own = Unpack(grid);
                var (best, like) = retailGrids.Select(g => (g.Key, Like(own, g.Value))).MaxBy(g => g.Item2);
                // (the retail interiors drawn with that grid: the one of the same number if it is one of them -- the demo's own numbering,
                // its likeness 0.4 or more -- else, from a likeness of 0.6, one on the same island; the outdoor scenes' grid numbers in
                // the table mean nothing)
                var users = Enumerable.Range(0, rTable.Length / 2).Where(r => rTable[r * 2 + 1] == best && interiorIsland.ContainsKey(r)).ToList();
                if (like >= 0.4 && users.Contains(scene)) match = scene;
                else if (like >= 0.6 && users.FirstOrDefault(r => interiorIsland[r] == h[0], -1) is var same and >= 0) match = same;
            }
            result[scene] = match;
        }
        return result;
    }

    // ---------------------------------------------------------------------------------------------------- the islands

    // An island (.ILE) with its ground's texture definitions in the retail format: the demo's are 8 bytes -- three corners, each a word with
    // the pixel's u in its low byte and v in its high one, and a spare word -- where the retail ones are 12, each corner's u and v in 8.8
    // fixed point (the same corners: the demo's Citadel Island's sixth cube's definitions are the retail one's to the pixel, from its
    // second on). Each corner goes to its pixel's middle. The rest of the island (its map, pictures, heights, light, decors and ground
    // polygons) is the retail format.
    private static (byte[] Ile, int Textures) ConvertIsland(byte[] ile)
    {
        var entries = Entries(ile);
        var map = Unpack(entries[0]!);
        var done = 0;
        for (var i = 0; i < 256 && i < map.Length; i++)
        {
            var cube = map[i] & 127;
            var at = 3 + 6 * (cube - 1) + 3;
            if (cube == 0 || at >= entries.Count || entries[at] is null) continue;
            var demo = Unpack(entries[at]!);
            var retail = new byte[demo.Length / 8 * 12];
            for (var k = 0; k < demo.Length / 8; k++)
                for (var c = 0; c < 3; c++)
                {
                    var corner = BinaryPrimitives.ReadUInt16LittleEndian(demo.AsSpan(k * 8 + c * 2));
                    BinaryPrimitives.WriteUInt16LittleEndian(retail.AsSpan(k * 12 + c * 4), (ushort)((corner & 255) * 256 + 128));
                    BinaryPrimitives.WriteUInt16LittleEndian(retail.AsSpan(k * 12 + c * 4 + 2), (ushort)((corner >> 8) * 256 + 128));
                }
            entries[at] = HqrWriter.StoredEntry(retail);
            done += demo.Length / 8;
        }
        return (Build(entries), done);
    }

    // ---------------------------------------------------------------------------------------------------- the bodies

    private static (byte[] Hqr, int Done, List<string> Failed) ConvertBodies(byte[] hqr)
    {
        var entries = Entries(hqr);
        var done = 0; var failed = new List<string>();
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i] is null) continue;
            try
            {
                entries[i] = HqrWriter.StoredEntry(Demo96Bodies.Convert(Unpack(entries[i]!), out var note));
                done++;
                if (note is not null) failed.Add($"{i} partly: {note}");
            }
            catch (Exception e) { failed.Add($"{i}: {e.Message}{(e.InnerException is { } inner ? " (" + inner.Message + ")" : "")}"); entries[i] = null; }
        }
        return (Build(entries), done, failed);
    }

    // ---------------------------------------------------------------------------------------------------- the resources

    // An island palette (XPL_HEADER, COMMON.H) from one of the demo's palette entries: its palette (768 bytes), its shading table (256
    // lines of 256: the line a fog level x 16 + a shade level, PtrNuances) and its levels (36 bytes: 1, the fog colour, start %, normal
    // level, end %, then nothing),
    // with a transparency table (PtrTransPal: what a see-through polygon makes of each colour over each other) of the two colours'
    // halfway point, the nearest colour of the palette.
    private static byte[] IslandPalette(byte[] main)
    {
        const int Header = 44, Pal = 768, Table = 65536;
        var x = new byte[Header + Pal + Table + Table];
        var levels = main.Length >= Pal + Table + 36 ? main[(Pal + Table)..(Pal + Table + 36)] : null;
        int Level(int i, int fallback) => levels is not null ? BinaryPrimitives.ReadInt32LittleEndian(levels.AsSpan(i * 4)) : fallback;
        int[] h = { 0, Header, 0, Header + Pal, Header + Pal + Table, Level(2, 20), Level(3, 12), Level(4, 170), Level(1, 0), 3683, 9633 };
        for (var i = 0; i < h.Length; i++) BinaryPrimitives.WriteInt32LittleEndian(x.AsSpan(i * 4), h[i]);
        main.AsSpan(0, Pal).CopyTo(x.AsSpan(Header));
        main.AsSpan(Pal, Table).CopyTo(x.AsSpan(Header + Pal));
        var trans = x.AsSpan(Header + Pal + Table, Table);
        for (var a = 0; a < 256; a++)
        for (var b = a; b < 256; b++)
        {
            int r = (main[a * 3] + main[b * 3]) / 2, g = (main[a * 3 + 1] + main[b * 3 + 1]) / 2, bl = (main[a * 3 + 2] + main[b * 3 + 2]) / 2;
            var best = 0; var bd = int.MaxValue;
            for (var c = 1; c < 256; c++)
            {
                int dr = main[c * 3] - r, dg = main[c * 3 + 1] - g, db = main[c * 3 + 2] - bl, d = dr * dr + dg * dg + db * db;
                if (d < bd) { bd = d; best = c; }
            }
            trans[a * 256 + b] = trans[b * 256 + a] = (byte)best;
        }
        return x;
    }

    // The retail entity table (RESS 44): an offset table -- one word an entity, then the end -- and the entities' records one after another.
    private static byte[] EntityTable(List<byte[]> entities)
    {
        var table = new List<byte>();
        var offset = (entities.Count + 1) * 4;
        var offsets = new byte[(entities.Count + 1) * 4];
        for (var i = 0; i <= entities.Count; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(offsets.AsSpan(i * 4), offset);
            if (i < entities.Count) offset += entities[i].Length;
        }
        table.AddRange(offsets);
        foreach (var e in entities) table.AddRange(e);
        return table.ToArray();
    }

    // ---------------------------------------------------------------------------------------------------- HQR files

    // An HQR file's entries as they are stored (their 10-byte header and payload), null for an empty slot.
    private static List<byte[]?> Entries(byte[] hqr)
    {
        var tableBytes = (int)BinaryPrimitives.ReadUInt32LittleEndian(hqr);
        var list = new List<byte[]?>();
        for (var i = 0; i < tableBytes / 4 - 1; i++)
        {
            var o = (int)BinaryPrimitives.ReadUInt32LittleEndian(hqr.AsSpan(i * 4));
            if (o == 0 || o + 10 > hqr.Length) { list.Add(null); continue; }
            var stored = (int)BinaryPrimitives.ReadUInt32LittleEndian(hqr.AsSpan(o + 4));
            list.Add(hqr[o..Math.Min(hqr.Length, o + 10 + stored)]);
        }
        return list;
    }

    // An HQR file from stored entries (null: an empty slot).
    private static byte[] Build(List<byte[]?> entries)
    {
        var tableBytes = (entries.Count + 1) * 4;
        var total = tableBytes + entries.Sum(e => e?.Length ?? 0);
        var result = new byte[total];
        var at = tableBytes;
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i] is not { } e) continue;
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(i * 4), (uint)at);
            e.CopyTo(result.AsSpan(at));
            at += e.Length;
        }
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(entries.Count * 4), (uint)total);
        return result;
    }

    // A stored entry's data, uncompressed.
    private static byte[] Unpack(byte[] entry) => HqrArchive.DecodeEntry(entry);

    private static int U16(byte[] b, int at) => BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(at));
}

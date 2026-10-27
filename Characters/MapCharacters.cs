using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace LBAAssembler.Characters;

// A map's own characters (the Character maker; 2026-10-23): each an entity -- a kind of actor -- added to the game's (RESS.HQR entry 44's
// table) with its bodies at the end of BODY.HQR and its animations at the end of ANIM.HQR, in the map's workspace; which of those are the
// map's is written beside its files in <stem>_CHARACTERS.TXT:
//   character <entity> <name>
//   body <entity> <generic> <BODY.HQR entry> <name>
//   anim <entity> <generic> <ANIM.HQR entry> <name>
// Scripts name a character's animations by their generic numbers (anim(190)), its bodies the same (body(0)); a scene's actor is of the
// entity. Save map carries them into the map file; installing it puts them back (MapInstall), numbered after what that game folder has.
internal static class MapCharacters
{
    public const string Suffix = "_CHARACTERS.TXT";

    public sealed record Part(int Generic, int Entry, string Name);

    public sealed class Character
    {
        public int Entity;
        public string Name = "";
        public List<Part> Bodies = new();
        public List<Part> Anims = new();
    }

    public static string SidecarPath(string directory, string stem) => Path.Combine(directory, stem + Suffix);

    public static List<Character> Load(string path) => File.Exists(path) ? Parse(File.ReadAllText(path, Encoding.UTF8)) : new List<Character>();

    public static List<Character> Parse(string text)
    {
        var list = new List<Character>();
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            var f = raw.Trim().Split(' ', 5, StringSplitOptions.RemoveEmptyEntries);
            if (f.Length < 2 || f[0].StartsWith('#')) continue;
            if (f[0] == "character" && int.TryParse(f[1], out var entity))
                list.Add(new Character { Entity = entity, Name = string.Join(' ', f.Skip(2)) });
            else if ((f[0] == "body" || f[0] == "anim") && f.Length >= 4 && int.TryParse(f[1], out var e) && int.TryParse(f[2], out var g) && int.TryParse(f[3], out var entry)
                     && list.FirstOrDefault(c => c.Entity == e) is { } owner)
                (f[0] == "body" ? owner.Bodies : owner.Anims).Add(new Part(g, entry, f.Length > 4 ? f[4] : ""));
        }
        return list;
    }

    public static string Text(IEnumerable<Character> characters)
    {
        var t = new StringBuilder("# The map's own characters (LBA Assembler's Character maker): \"character <entity> <name>\", then its bodies\r\n" +
                                  "# \"body <entity> <generic> <BODY.HQR entry> <name>\" and animations \"anim <entity> <generic> <ANIM.HQR entry> <name>\"\r\n");
        foreach (var c in characters)
        {
            t.Append($"character {c.Entity} {OneLine(c.Name)}\r\n");
            foreach (var b in c.Bodies) t.Append($"body {c.Entity} {b.Generic} {b.Entry} {OneLine(b.Name)}\r\n");
            foreach (var a in c.Anims) t.Append($"anim {c.Entity} {a.Generic} {a.Entry} {OneLine(a.Name)}\r\n");
        }
        return t.ToString();
    }

    private static string OneLine(string s) => s.Replace('\r', ' ').Replace('\n', ' ').Trim();

    // ---- the entity table (RESS.HQR 44): U32 offsets, one an entity and the last the table's end, then each entity's records ended by 255

    public static int EntityCount(byte[] table) => BinaryPrimitives.ReadInt32LittleEndian(table) / 4 - 1;

    // An entity's records: each body (1, generic, 17, BODY.HQR entry, 1, 14, its box: x, y, z least then most) and each animation (3,
    // generic (U16), 4, ANIM.HQR entry, no actions), ended by 255.
    public static byte[] EntityRecord(IEnumerable<(int Generic, int Entry, (short X0, short Y0, short Z0, short X1, short Y1, short Z1) Box)> bodies, IEnumerable<(int Generic, int Entry)> anims)
    {
        var r = new List<byte>();
        void S16(int v) { r.Add((byte)v); r.Add((byte)(v >> 8)); }
        foreach (var (g, entry, box) in bodies)
        {
            r.Add(1); r.Add((byte)g); r.Add(17); S16(entry); r.Add(1); r.Add(14);
            S16(box.X0); S16(box.Y0); S16(box.Z0); S16(box.X1); S16(box.Y1); S16(box.Z1);
        }
        foreach (var (g, entry) in anims) { r.Add(3); S16(g); r.Add(4); S16(entry); r.Add(0); }
        r.Add(255);
        return r.ToArray();
    }

    // The table with `record` as entity `entity` (its records replaced), or appended as a new one (entity -1). Returns the entity's number.
    public static (byte[] Table, int Entity) WithEntity(byte[] table, int entity, byte[] record)
    {
        var count = EntityCount(table);
        var offsets = new int[count + 1];
        for (var i = 0; i <= count; i++) offsets[i] = BinaryPrimitives.ReadInt32LittleEndian(table.AsSpan(i * 4));
        var records = new List<byte[]>();
        for (var i = 0; i < count; i++) records.Add(table.AsSpan(offsets[i], (i + 1 < count ? offsets[i + 1] : offsets[count]) - offsets[i]).ToArray());
        if (entity < 0 || entity >= count) { records.Add(record); entity = count; }
        else records[entity] = record;
        var head = (records.Count + 1) * 4;
        var result = new byte[head + records.Sum(r => r.Length)];
        var at = head;
        for (var i = 0; i < records.Count; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(i * 4), at);
            records[i].CopyTo(result, at);
            at += records[i].Length;
        }
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(records.Count * 4), at);
        return (result, entity);
    }

    // An entity's records as the table has them (to its 255).
    public static byte[] EntityOf(byte[] table, int entity)
    {
        var count = EntityCount(table);
        if (entity < 0 || entity >= count) return new byte[] { 255 };
        int start = BinaryPrimitives.ReadInt32LittleEndian(table.AsSpan(entity * 4)), end = BinaryPrimitives.ReadInt32LittleEndian(table.AsSpan((entity + 1) * 4));
        return table.AsSpan(start, end - start).ToArray();
    }

    // An entity record with its bodies' and animations' entries renumbered (`body`, `anim`: an entry's old number to its new) -- the map's
    // own entries in its file, the game folder's when installed.
    public static byte[] Renumber(byte[] record, Func<int, int> body, Func<int, int> anim)
    {
        var r = (byte[])record.Clone();
        for (var p = 0; p + 2 < r.Length && r[p] != 255;)
        {
            var command = r[p];
            if (command == 1 && p + 5 <= r.Length) BinaryPrimitives.WriteInt16LittleEndian(r.AsSpan(p + 3), (short)body(BinaryPrimitives.ReadInt16LittleEndian(r.AsSpan(p + 3))));
            else if (command == 3 && p + 6 <= r.Length) BinaryPrimitives.WriteInt16LittleEndian(r.AsSpan(p + 4), (short)anim(BinaryPrimitives.ReadInt16LittleEndian(r.AsSpan(p + 4))));
            p += command == 3 ? 3 + r[p + 3] : 2 + r[p + 2];
        }
        return r;
    }

    // The collision boxes of an entity record's bodies, by generic number.
    public static Dictionary<int, (short, short, short, short, short, short)> BodyBoxes(byte[] record)
    {
        var boxes = new Dictionary<int, (short, short, short, short, short, short)>();
        short S(int at) => BinaryPrimitives.ReadInt16LittleEndian(record.AsSpan(at));
        for (var p = 0; p + 2 < record.Length && record[p] != 255;)
        {
            var command = record[p];
            if (command == 1 && p + 19 <= record.Length && record[p + 5] == 1) boxes[record[p + 1]] = (S(p + 7), S(p + 9), S(p + 11), S(p + 13), S(p + 15), S(p + 17));
            p += command == 3 ? 3 + record[p + 3] : 2 + record[p + 2];
        }
        return boxes;
    }

    // ---- into a game folder (the map's workspace): a character made or changed

    public sealed record MadeBody(int Generic, string Name, byte[] Data, (short X0, short Y0, short Z0, short X1, short Y1, short Z1) Box);
    public sealed record MadeAnim(int Generic, string Name, byte[] Data);

    // Puts a character into the folder's BODY.HQR, ANIM.HQR and entity table -- the entries it had before reused for the same generic
    // numbers, new ones at the ends -- and its lines into the sidecar. `entity` -1: a new character. Returns it as installed.
    public static Character Put(string directory, string sidecar, int entity, string name, IReadOnlyList<MadeBody> bodies, IReadOnlyList<MadeAnim> anims)
    {
        var characters = Load(sidecar);
        var before = characters.FirstOrDefault(c => c.Entity == entity);
        var bodyPath = Path.Combine(directory, "BODY.HQR");
        var animPath = Path.Combine(directory, "ANIM.HQR");
        var ressPath = Path.Combine(directory, "RESS.HQR");
        var bodyFile = File.ReadAllBytes(bodyPath);
        var animFile = File.ReadAllBytes(animPath);
        var ress = File.ReadAllBytes(ressPath);
        var table = HqrArchive.FromBytes(ress).Read(44);
        var bodyCount = HqrArchive.FromBytes(bodyFile).Slots - 1;   // (Slots: Count is four times the entries)
        var animCount = HqrArchive.FromBytes(animFile).Slots - 1;
        var made = new Character { Name = name };
        var bodyRecords = new List<(int, int, (short, short, short, short, short, short))>();
        foreach (var b in bodies)
        {
            int entry;
            if (before?.Bodies.FirstOrDefault(p => p.Generic == b.Generic) is { } old && old.Entry < bodyCount) { entry = old.Entry; bodyFile = HqrWriter.ReplaceEntry(bodyFile, entry, HqrWriter.StoredEntry(b.Data)); }
            else { entry = bodyCount++; bodyFile = HqrWriter.AppendEntry(bodyFile, HqrWriter.StoredEntry(b.Data)); }
            made.Bodies.Add(new Part(b.Generic, entry, b.Name));
            bodyRecords.Add((b.Generic, entry, b.Box));
        }
        foreach (var a in anims)
        {
            int entry;
            if (before?.Anims.FirstOrDefault(p => p.Generic == a.Generic) is { } old && old.Entry < animCount) { entry = old.Entry; animFile = HqrWriter.ReplaceEntry(animFile, entry, HqrWriter.StoredEntry(a.Data)); }
            else { entry = animCount++; animFile = HqrWriter.AppendEntry(animFile, HqrWriter.StoredEntry(a.Data)); }
            made.Anims.Add(new Part(a.Generic, entry, a.Name));
        }
        (table, made.Entity) = WithEntity(table, before is null ? -1 : entity, EntityRecord(bodyRecords, made.Anims.Select(p => (p.Generic, p.Entry))));
        if (before is not null) characters.Remove(before);
        characters.Add(made);
        characters.Sort((x, y) => x.Entity.CompareTo(y.Entity));
        new FileTransaction()
            .Write(bodyPath, bodyFile)
            .Write(animPath, animFile)
            .Write(ressPath, HqrWriter.ReplaceEntry(ress, 44, HqrWriter.StoredEntry(table)))
            .Write(sidecar, Encoding.UTF8.GetBytes(Text(characters)))
            .Commit();
        return made;
    }
}

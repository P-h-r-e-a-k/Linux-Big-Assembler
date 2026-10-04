using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace LBAAssembler;

// One LBA2 text file of one language in TEXT.HQR (MESSAGE.CPP InitDial): entry (language * 15 + file) * 2 is the list of text ids (a
// U16 each), the next the texts -- a U16 offset per text plus one past the last, then each text as its attribute byte (FlagDial: how it
// is shown), its characters and a 0. The CD version's speech is looked up by a text's place in the list, so a text added at the end has
// no voice of its own (it is shown, not spoken) and the texts before it keep theirs.
internal sealed class Lba2TextBank
{
    public const int FilesPerLanguage = 15;
    public const byte NormalAttribute = 1;   // the attribute of an ordinary line of dialogue

    public sealed class Text
    {
        public int Id;
        public byte Attribute;
        public byte[] Bytes = Array.Empty<byte>();   // the characters, without the attribute or the closing 0
        public string Value => Encoding.Latin1.GetString(Bytes);
    }

    public int Language { get; }
    public int File { get; }
    public List<Text> Texts { get; } = new();

    private Lba2TextBank(int language, int file) { Language = language; File = file; }

    public static int IdsEntry(int language, int file) => (language * FilesPerLanguage + file) * 2;

    public static Lba2TextBank Load(HqrArchive text, int language, int file)
    {
        var bank = new Lba2TextBank(language, file);
        var order = IdsEntry(language, file);
        if (!text.IsValid(order) || !text.IsValid(order + 1)) return bank;
        var ids = text.Read(order);
        var data = text.Read(order + 1);
        var count = ids.Length / 2;
        for (var i = 0; i < count; i++)
        {
            int off0 = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(i * 2));
            int off1 = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(i * 2 + 2));
            var t = new Text { Id = BinaryPrimitives.ReadUInt16LittleEndian(ids.AsSpan(i * 2)) };
            if (off1 > off0 && off1 <= data.Length)
            {
                t.Attribute = data[off0];
                var end = off1;
                while (end > off0 + 1 && data[end - 1] == 0) end--;
                t.Bytes = data[(off0 + 1)..end];
            }
            bank.Texts.Add(t);
        }
        return bank;
    }

    public Text? Find(int id) => Texts.FirstOrDefault(t => t.Id == id);

    // The two entries, as the engine reads them.
    public (byte[] Ids, byte[] Data) Encode()
    {
        var ids = new byte[Texts.Count * 2];
        for (var i = 0; i < Texts.Count; i++) BinaryPrimitives.WriteUInt16LittleEndian(ids.AsSpan(i * 2), (ushort)Texts[i].Id);
        var table = (Texts.Count + 1) * 2;
        var body = new MemoryStream();
        var offsets = new List<int>();
        foreach (var t in Texts)
        {
            offsets.Add(table + (int)body.Length);
            body.WriteByte(t.Attribute);
            body.Write(t.Bytes);
            body.WriteByte(0);
        }
        offsets.Add(table + (int)body.Length);
        if (offsets[^1] > ushort.MaxValue) throw new InvalidDataException($"Text file {File} would be {offsets[^1]} bytes, past what a U16 offset reaches.");
        var data = new byte[offsets[^1]];
        for (var i = 0; i < offsets.Count; i++) BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(i * 2), (ushort)offsets[i]);
        body.ToArray().CopyTo(data, table);
        return (ids, data);
    }

    // TEXT.HQR's bytes with this bank written in place of its own two entries.
    public byte[] WriteInto(byte[] hqr)
    {
        var (ids, data) = Encode();
        var order = IdsEntry(Language, File);
        hqr = HqrWriter.ReplaceEntry(hqr, order, HqrWriter.StoredEntry(ids));
        return HqrWriter.ReplaceEntry(hqr, order + 1, HqrWriter.StoredEntry(data));
    }

    // Which language index is English: the one whose Citadel texts read most like English.
    public static int English(HqrArchive text)
    {
        var best = 0; var bestScore = -1;
        for (var lang = 0; lang < 8; lang++)
        {
            var bank = Load(text, lang, 3);
            var score = bank.Texts.Count(t => t.Value.Contains(" the ") || t.Value.Contains(" you ") || t.Value.Contains(" and "));
            if (score > bestScore) { bestScore = score; best = lang; }
        }
        return best;
    }

    // How many languages the file holds (whole sets of 15 files).
    public static int Languages(string textHqrPath) => HqrArchive.CountEntries(textHqrPath) / (FilesPerLanguage * 2);
}

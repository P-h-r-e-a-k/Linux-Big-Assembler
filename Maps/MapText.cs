using System.IO;
using System.Text;

namespace LBAAssembler.Maps;

// A map's own lines of dialogue (2026-10-23, recreating LBA1's Hamalayi by hand: its ski instructor's lines): TEXT.TXT in the map, UTF-8,
// a line "<number> = <words>" each -- "<number>:<attribute> = <words>" for one shown other than as an ordinary line (MESSAGE.CPP FlagDial:
// 1 an ordinary line, 2 a big message, 4 full screen, 8 said in a bubble) -- '#' a comment. In the map's scenes a script's message of one
// of its numbers says its words instead of the island's line of that number (MAPPACK.CPP MapPack_Text), shown and not spoken (no voice).
internal sealed class MapText
{
    public const string Suffix = "_TEXT.TXT";
    public const int Ordinary = 1;
    // (where a map's numbers start when it has none: well past the islands' own -- a map's line overrides the island's of its number)
    public const int FirstNumber = 1000;

    public sealed record Line(int Number, int Attribute, string Words);

    public SortedDictionary<int, Line> Lines { get; } = new();

    public int NextNumber => Lines.Count == 0 ? FirstNumber : Lines.Keys.Max() + 1;

    public static MapText Parse(string text)
    {
        var result = new MapText();
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            var eq = line.IndexOf('=');
            if (line.Length == 0 || line[0] == '#' || eq < 0 || !char.IsDigit(line[0])) continue;
            var key = line[..eq].Trim();
            var colon = key.IndexOf(':');
            if (!int.TryParse(colon < 0 ? key : key[..colon], out var number)) continue;
            var attribute = colon >= 0 && int.TryParse(key[(colon + 1)..], out var a) ? a : Ordinary;
            result.Lines[number] = new Line(number, attribute, line[(eq + 1)..].Trim());
        }
        return result;
    }

    public static MapText Load(string path) => File.Exists(path) ? Parse(File.ReadAllText(path, Encoding.UTF8)) : new MapText();

    public string Format()
    {
        var t = new StringBuilder("# The map's own lines of dialogue: \"<number> = <words>\" (\"<number>:<attribute> = <words>\": 2 a big message, 4 full screen,\r\n" +
                                  "# 8 said in a bubble). A script's message of one of these numbers says these words in the map's scenes.\r\n");
        foreach (var line in Lines.Values)
            t.Append(line.Attribute == Ordinary ? $"{line.Number} = {OneLine(line.Words)}\r\n" : $"{line.Number}:{line.Attribute} = {OneLine(line.Words)}\r\n");
        return t.ToString();
    }

    // (a line of the file is one line of dialogue: the engine starts a new one where the words have '@')
    public static string OneLine(string words) => words.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Trim();
}

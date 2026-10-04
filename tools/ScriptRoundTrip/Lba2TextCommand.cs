using LBAAssembler;

namespace ScriptRoundTrip;

// lba2text find <game folder> <words>: the English texts (every file) containing the words, with file and id.
// lba2text show <game folder> <file> <id>: one text in every language.
internal static class Lba2TextCommand
{
    public static int Run(string[] args)
    {
        var path = Path.Combine(args[2], "TEXT.HQR");
        var hqr = HqrArchive.Open(path);
        var english = Lba2TextBank.English(hqr);
        if (args[1] == "find")
        {
            var needle = string.Join(' ', args.Skip(3));
            Console.WriteLine($"English is language {english} of {Lba2TextBank.Languages(path)}");
            for (var file = 0; file < Lba2TextBank.FilesPerLanguage; file++)
                foreach (var t in Lba2TextBank.Load(hqr, english, file).Texts.Where(t => t.Value.Contains(needle, StringComparison.OrdinalIgnoreCase)))
                    Console.WriteLine($"file {file} id {t.Id} attr {t.Attribute}: {t.Value}");
            return 0;
        }
        if (args[1] == "en")
        {
            // lba2text en <game folder> <file> <id>...: several texts, English only, with their place in the file
            var bank = Lba2TextBank.Load(hqr, english, int.Parse(args[3]));
            foreach (var id in args.Skip(4).Select(int.Parse))
            {
                var t = bank.Find(id);
                Console.WriteLine($"{id} (#{(t is null ? -1 : bank.Texts.IndexOf(t))}): {t?.Value}");
            }
            return 0;
        }
        if (args[1] == "show")
        {
            int file = int.Parse(args[3]), id = int.Parse(args[4]);
            for (var lang = 0; lang < Lba2TextBank.Languages(path); lang++)
            {
                var bank = Lba2TextBank.Load(hqr, lang, file);
                var t = bank.Find(id);
                Console.WriteLine($"language {lang}{(lang == english ? " (English)" : "")}: {bank.Texts.Count} texts; id {id} at {(t is null ? -1 : bank.Texts.IndexOf(t))}: {t?.Value}");
            }
            return 0;
        }
        return 1;
    }
}

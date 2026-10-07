using LBAAssembler.Demo96;

namespace ScriptRoundTrip;

// demo96 <demo folder> <out folder> [retail folder]: the 1996 LBA2 demo converted into the retail layout (Demo96Converter), the log printed.
internal static class Demo96Command
{
    public static int Run(string[] args)
    {
        var retail = args.Length > 3 ? args[3] : @"E:\GOG Games\Little Big Adventure 2";
        foreach (var line in Demo96Converter.Convert(args[1], args[2], retail)) Console.WriteLine(line);
        return 0;
    }
}

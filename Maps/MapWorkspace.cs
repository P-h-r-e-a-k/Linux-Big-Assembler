using System.IO;

namespace LBAAssembler.Maps;

// The map open in the editor (File > New map, Open map): its name, its file (null until saved), the workspace that stands in for the game
// folder while it is open (LiveDataRoot's copy, the map installed in it), and the game folder that copies. The editors ask it where a save
// of theirs goes, to say so -- "into the map" rather than "SCENE.HQR" (a tester, 2026-10-17: "if I was to give an npc a script it would not
// save it to the custom maps files but instead to the main game directorys scene.hqr" -- it went into the workspace, and File > Save map put
// it into the map's file, but every message named SCENE.HQR). While a map is open its game folder is guarded: FileTransaction refuses a
// write into it outside the workspace (File > Install map into game excepted).
internal static class MapWorkspace
{
    internal sealed record Info(string Name, string? File, string Directory, string RealDirectory, bool Lba1);

    private static IDisposable? guard;

    public static Info? Current { get; private set; }

    public static void Opened(string name, string? file, string directory, string realDirectory, bool lba1)
    {
        Current = new Info(name, file, directory, realDirectory, lba1);
        guard?.Dispose();
        guard = FileTransaction.Protect(realDirectory, directory, $"the map \"{name}\" is open: its own copy of the game folder takes every save");
    }

    // File > Save map (as): the map's file from now on
    public static void Saved(string name, string file)
    {
        if (Current is { } c) Current = c with { Name = name, File = file };
    }

    public static void Closed()
    {
        Current = null;
        guard?.Dispose();
        guard = null;
    }

    // The open map whose workspace `directory` is (a scene store's, a script session's folder), or null: the game's own folder.
    public static Info? For(string? directory)
        => Current is { } c && directory is { Length: > 0 } && Same(c.Directory, directory) ? c : null;

    // Where a save into `directory` goes, for a message: "the map "Name"" or the file's name ("SCENE.HQR").
    public static string Target(string? directory, string file = "SCENE.HQR") => For(directory) is { } map ? $"the map \"{map.Name}\"" : file;

    // What happens to it next, for a message: File > Save map writes it into the map's file.
    public static string AfterSave(string? directory)
        => For(directory) is { } map
            ? map.File is { } f ? $"File > Save map (Ctrl+S) writes it into {Path.GetFileName(f)}." : "File > Save map (Ctrl+S) writes it into a map file."
            : "";

    private static bool Same(string a, string b)
    {
        static string Norm(string p) => Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(Norm(a), Norm(b), StringComparison.OrdinalIgnoreCase);
    }
}

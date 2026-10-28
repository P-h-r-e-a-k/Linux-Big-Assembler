using System.IO;
using System.Text.RegularExpressions;

namespace LBAAssembler.Terrain.Temple;

// The LBA1s installed on this PC by GOG and Steam -- where the race's temple music comes from when the LBA1 folder a build is given is a
// copy of LBA1's data files alone (the CD's music is its audio tracks, not files), and where LBRA's installer looks for LBA1 (its window's
// guess, its music's fallback).
internal static partial class TempleMusic
{
    // The folders a store installs games to, on every fixed drive (GOG's own default, GOG Galaxy's, Steam's and a Steam library's)
    private static readonly string[] LibraryFolders =
    {
        "GOG Games",
        Path.Combine("GOG Galaxy", "Games"),
        Path.Combine("Program Files (x86)", "GOG Galaxy", "Games"),
        Path.Combine("Program Files", "GOG Galaxy", "Games"),
        Path.Combine("Program Files (x86)", "GOG.com"),
        "Games",
        Path.Combine("Program Files (x86)", "Steam", "steamapps", "common"),
        Path.Combine("Program Files", "Steam", "steamapps", "common"),
        Path.Combine("Steam", "steamapps", "common"),
        Path.Combine("SteamLibrary", "steamapps", "common"),
        Path.Combine("Games", "Steam", "steamapps", "common"),
        Path.Combine("Games", "SteamLibrary", "steamapps", "common"),
    };

    // The port: the same, under the user's home folder. A Linux box has no drive letters, and nothing is installed at the root of a disk:
    // Steam puts its library in ~/.local/share/Steam (older installs keep the ~/.steam/steam symlink to it), and GOG's games arrive through
    // Wine, Heroic or Lutris into "GOG Games" or "Games". Looked in with the home folder as their root (InstalledLba1Folders).
    private static readonly string[] HomeLibraryFolders =
    {
        "GOG Games",
        "Games",
        Path.Combine(".local", "share", "Steam", "steamapps", "common"),
        Path.Combine(".steam", "steam", "steamapps", "common"),
    };

    // The names the stores give LBA1's folder: GOG's DOS release ("Little Big Adventure"; "Little Big AdventureLBAWin", its Windows-launcher
    // variant), Steam's and GOG's 2022 Classic ("Twinsen's Little Big Adventure Classic"); any other folder named like LBA1 is looked in too
    // (Twinsen's Quest, the 2024 remake, and LBA2 have no LBA1 data: left out by that)
    public static readonly string[] Lba1FolderNames = { "Little Big Adventure", "Little Big AdventureLBAWin", "Twinsen's Little Big Adventure Classic" };
    private static readonly Regex Lba1Like = new(@"little\s*big\s*adventure|twinsen|^lba(?!\s*2)|^tlba1|relentless", RegexOptions.IgnoreCase);

    // Every LBA1 installed on this PC, as the folder with its data (SCENE.HQR, LBA_GRI.HQR ...: the install folder for GOG's DOS release,
    // its Common folder for the Classic): GOG's (the registry's GOG.com\Games paths, its usual folders), Steam's (its libraries, from
    // libraryfolders.vdf, and the usual folders). Each once, in that order.
    public static List<string> InstalledLba1Folders()
    {
        var found = new List<string>();
        void Install(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) return;
                foreach (var sub in new[] { "", "Common", Path.Combine("Speedrun", "Windows"), "LBA", "GAME" })
                {
                    var data = Path.GetFullPath(Path.Combine(dir, sub)).TrimEnd('\\', '/');
                    if (!File.Exists(Path.Combine(data, "SCENE.HQR")) || !File.Exists(Path.Combine(data, "LBA_GRI.HQR")) || !File.Exists(Path.Combine(data, "LBA_BRK.HQR"))) continue;
                    if (!found.Contains(data, StringComparer.OrdinalIgnoreCase)) found.Add(data);
                    return;
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { }
        }
        void Library(string library)
        {
            try
            {
                if (!Directory.Exists(library)) return;
                foreach (var name in Lba1FolderNames) Install(Path.Combine(library, name));
                foreach (var dir in Directory.EnumerateDirectories(library).Where(d => Lba1Like.IsMatch(Path.GetFileName(d))).OrderBy(d => d, StringComparer.OrdinalIgnoreCase)) Install(dir);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { }
        }
        foreach (var dir in GogInstalls()) Install(dir);
        // The port: on Linux the stores install under the user's home folder, so that is looked in first -- the fixed drives below are
        // Windows' own places and find nothing here. (Windows is untouched: it has GogInstalls' registry paths and its drives.)
        if (!OperatingSystem.IsWindows() && Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) is { Length: > 0 } home)
            foreach (var where in HomeLibraryFolders) Library(Path.Combine(home, where));
        var roots = new List<string>();
        try { roots.AddRange(DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady).Select(d => d.RootDirectory.FullName)); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        foreach (var root in roots)
            foreach (var where in LibraryFolders.Where(w => !w.Contains("steamapps")))
                Library(Path.Combine(root, where));
        foreach (var library in SteamLibraries()) Library(Path.Combine(library, "steamapps", "common"));
        foreach (var root in roots)
            foreach (var where in LibraryFolders.Where(w => w.Contains("steamapps")))
                Library(Path.Combine(root, where));
        return found;
    }

    // GOG's installed games named like LBA1, from the registry (GOG.com\Games\<id>: path; LBA1's id is 1207658971, the DOS release's and the
    // Classic's alike)
    private static IEnumerable<string> GogInstalls()
    {
        var dirs = new List<string>();
        if (!OperatingSystem.IsWindows()) return dirs;
        try
        {
            foreach (var key in new[] { @"SOFTWARE\WOW6432Node\GOG.com\Games", @"SOFTWARE\GOG.com\Games" })
            {
                using var games = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(key);
                if (games is null) continue;
                foreach (var id in games.GetSubKeyNames())
                {
                    using var game = games.OpenSubKey(id);
                    if (game?.GetValue("path") is not string path || string.IsNullOrWhiteSpace(path)) continue;
                    var name = game.GetValue("gameName") as string ?? "";
                    if (id == "1207658971" || Lba1Like.IsMatch(name)) dirs.Add(path);
                }
            }
        }
        catch (Exception error) when (error is System.Security.SecurityException or UnauthorizedAccessException or IOException) { }
        return dirs;
    }

    // Steam's libraries (its own folder, and each "path" in steamapps\libraryfolders.vdf), Steam's folder from the registry
    private static IEnumerable<string> SteamLibraries()
    {
        var steam = new List<string>();
        if (OperatingSystem.IsWindows())
        {
            try
            {
                if (Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")?.GetValue("SteamPath") is string user) steam.Add(user.Replace('/', '\\'));
                if (Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")?.GetValue("InstallPath") is string machine) steam.Add(machine);
            }
            catch (Exception error) when (error is System.Security.SecurityException or UnauthorizedAccessException or IOException) { }
        }
        // The port: Linux has no registry, so Steam's own folder is taken from the two places it lives in under the home folder; its
        // steamapps/libraryfolders.vdf then names the libraries on the box's other disks exactly as it does on Windows.
        else if (Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) is { Length: > 0 } home)
        {
            steam.Add(Path.Combine(home, ".local", "share", "Steam"));
            steam.Add(Path.Combine(home, ".steam", "steam"));
        }
        // (the libraries' own spelling first: the registry's SteamPath is lower case)
        var libraries = new List<string>();
        foreach (var dir in steam.Where(Directory.Exists))
        {
            try
            {
                var vdf = Path.Combine(dir, "steamapps", "libraryfolders.vdf");
                if (!File.Exists(vdf)) continue;
                foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                    libraries.Add(m.Groups[1].Value.Replace("\\\\", "\\"));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
        libraries.AddRange(steam.Where(Directory.Exists));
        return libraries.Select(l => l.TrimEnd('\\', '/')).Distinct(StringComparer.OrdinalIgnoreCase);
    }
}

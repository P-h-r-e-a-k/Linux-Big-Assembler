using System.Runtime.InteropServices;

namespace ScriptRoundTrip;

// What the engine-backed tests need from the machine, the same on Windows and Linux: a scratch folder for a sandbox copy
// of the game (hard links where possible, so a copy of a 600 MB game costs nothing) and the renderer library built from
// native/lba2-classic-community for this platform.
internal static class Portable
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateHardLinkW(string newFile, string existingFile, IntPtr reserved);

    [DllImport("libc", EntryPoint = "link", SetLastError = true)]
    private static extern int LinkUnix(string oldPath, string newPath);

    // A hard link to `existing` at `target`; a copy when a link can't be made (another volume, a file system without them).
    public static void HardLink(string target, string existing)
    {
        try
        {
            if (OperatingSystem.IsWindows() ? CreateHardLinkW(target, existing, IntPtr.Zero) : LinkUnix(existing, target) == 0) return;
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException) { }
        File.Copy(existing, target, overwrite: true);
    }

    // The game's subfolders (VIDEO, MUSIC, VOX, the languages ...) mirrored into a sandbox copy as hard links: installs differ
    // in what sits at the top level (a retail install keeps VIDEO.HQR in VIDEO/, which the engine requires) and the tests
    // only copy the top-level files they change.
    public static void LinkSubfolders(string gameDir, string sandbox)
    {
        foreach (var dir in Directory.GetDirectories(gameDir, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(sandbox, Path.GetRelativePath(gameDir, dir)));
        foreach (var file in Directory.GetFiles(gameDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(gameDir, file);
            if (Path.GetDirectoryName(relative) is not { Length: > 0 }) continue;      // top-level files are the test's own business
            var target = Path.Combine(sandbox, relative);
            if (!File.Exists(target)) HardLink(target, file);
        }
    }

    // A named scratch folder: under LBA_TEST_SANDBOX when that is set (the author's machine used E:\dump), else the temp folder.
    public static string Sandbox(string name)
        => Path.Combine(Environment.GetEnvironmentVariable("LBA_TEST_SANDBOX") ?? Path.GetTempPath(), name);

    // The renderer library: LBA2_RENDERER_DLL when set, else this checkout's own native build for the platform.
    public static string RendererLibrary
    {
        get
        {
            if (Environment.GetEnvironmentVariable("LBA2_RENDERER_DLL") is { Length: > 0 } configured) return configured;
            var build = OperatingSystem.IsWindows()
                ? Path.Combine("out", "build", "windows_ucrt64_static", "SOURCES", "3DEXT", "liblba2_renderer.dll")
                : Path.Combine("out", "build", "linux", "SOURCES", "3DEXT", "liblba2_renderer.so");
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "native", "lba2-classic-community", build);
                if (File.Exists(candidate)) return candidate;
            }
            return build;
        }
    }
}

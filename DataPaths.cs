using System.IO;

namespace LBAAssembler;

// Where the editor keeps what would otherwise go in Windows' per-user folders on the system drive (the user, 2026-10-22: "Move them all to
// E:\ Anything that's already in Appdata, let's move it to E:\AppData\ anything that's in Temp to be in E:\dump\TEMP"): settings.json's
// AppDataDirectory -- laid out as AppData is, Roaming and Local under it -- and TempDirectory, when set; else Windows' own %AppData%,
// %LocalAppData% and %TEMP%.
internal static class DataPaths
{
    public static string RoamingAppData => Under("Roaming") ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    public static string LocalAppData => Under("Local") ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    private static string? Under(string part) => EditorSettings.Current.AppDataDirectory is { Length: > 0 } root ? Path.Combine(root, part) : null;

    // At start: the temporary folder the process's own (TMP, TEMP: Path.GetTempPath here, and the engine it starts, take it from them), and
    // the engine's own user folder (LBA2_USER_DIR: the renderer's copy of the engine in this process, which otherwise makes
    // %AppData%\Twinsen\LBA2) under the AppData folder.
    public static void Apply()
    {
        var settings = EditorSettings.Current;
        if (settings.TempDirectory is { Length: > 0 } temp)
        {
            try
            {
                Directory.CreateDirectory(temp);
                Environment.SetEnvironmentVariable("TMP", temp);
                Environment.SetEnvironmentVariable("TEMP", temp);
                // .NET reads TMPDIR on Unix, not TMP/TEMP, so Path.GetTempPath here would ignore the setting without this
                // (the two above are still set: the engine this process starts is the Windows build's own code, and reads them).
                Environment.SetEnvironmentVariable("TMPDIR", temp);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { DebugLog.Log($"DataPaths: {temp} can't be the temporary folder: {e.Message}"); }
        }
        if (settings.AppDataDirectory is { Length: > 0 } && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LBA2_USER_DIR")))
            Environment.SetEnvironmentVariable("LBA2_USER_DIR", Path.Combine(RoamingAppData, "Twinsen", "LBA2"));
    }
}

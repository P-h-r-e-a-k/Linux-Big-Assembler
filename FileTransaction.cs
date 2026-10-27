using System.IO;

namespace LBAAssembler;

// Writes several files as one unit: each gets a one-time `.bak` copy of what it held before, its new content is
// written beside it and checked, and only then are the files swapped in. If a swap fails part-way the files
// already swapped are put back, so a save never leaves the game folder half-edited.
internal sealed class FileTransaction
{
    private sealed record Change(string Path, byte[] Content, Func<byte[], string?>? Verify);

    private readonly List<Change> changes = new();

    // `verify` receives the file's bytes as read back from the temporary file and returns an error message or null.
    // Two writes to the same path in one transaction is always a caller bug (their two temp files, both named
    // `path + ".tmp"`, would collide during Commit -- one Move would silently consume the other's file, and the
    // second Move would then fail with a confusing "file not found" deep inside Commit): callers that touch the
    // same file more than once (HqrEntryStore.Compose, several entries of one archive) merge into one Write
    // instead, so this throws early and clearly rather than committing half a transaction.
    public FileTransaction Write(string path, byte[] content, Func<byte[], string?>? verify = null)
    {
        var full = System.IO.Path.GetFullPath(path);
        if (changes.Any(c => string.Equals(c.Path, full, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"{path} is already queued in this transaction; merge the two writes into one instead.");
        changes.Add(new Change(full, content, verify));
        return this;
    }

    // ---- folders no transaction writes into
    // While a map is open (or test edits run) the game folder's own files must stay as they are: everything goes into the working copy
    // inside it. Protect(folder, except) refuses any write into `folder` or below it, but for `except` (the working copy) and below; a
    // deliberate write into it (File > Install map into game) runs inside AllowWrites(). Every scene, script, zone, actor and HQR entry
    // save goes through here, so a window still pointing at the game folder can't write it behind the map's back.
    private sealed record Guard(string Folder, string Except, string Why);
    private static readonly List<Guard> guards = new();
    [ThreadStatic] private static int allowed;

    public static IDisposable Protect(string folder, string except, string why)
    {
        var guard = new Guard(Normal(folder), Normal(except), why);
        lock (guards) guards.Add(guard);
        return new Scope(() => { lock (guards) guards.Remove(guard); });
    }

    public static IDisposable AllowWrites()
    {
        allowed++;
        return new Scope(() => allowed--);
    }

    // The reason a write to `path` is refused now, or null.
    public static string? Refusal(string path)
    {
        if (allowed > 0) return null;
        var full = Normal(path);
        lock (guards)
            foreach (var g in guards)
                if (Under(full, g.Folder) && !Under(full, g.Except))
                    return $"{System.IO.Path.GetFileName(full)} wasn't written: it is the game folder's own ({System.IO.Path.GetDirectoryName(full)}), and {g.Why}.";
        return null;
    }

    private static string Normal(string path) => System.IO.Path.GetFullPath(path).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
    private static bool Under(string path, string folder)
        => path.Equals(folder, StringComparison.OrdinalIgnoreCase) || path.StartsWith(folder + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private sealed class Scope(Action end) : IDisposable
    {
        private Action? end = end;
        public void Dispose() { end?.Invoke(); end = null; }
    }

    public void Commit()
    {
        // (an IOException: every save's "Not saved: ..." already reports those)
        foreach (var change in changes)
            if (Refusal(change.Path) is { } why) throw new IOException(why + " Nothing was modified.");
        var before = new Dictionary<string, byte[]?>();
        var temps = new List<(string Temp, string Path)>();
        try
        {
            foreach (var change in changes)
            {
                before[change.Path] = File.Exists(change.Path) ? File.ReadAllBytes(change.Path) : null;
                var temp = change.Path + ".tmp";
                File.WriteAllBytes(temp, change.Content);
                temps.Add((temp, change.Path));
                var readBack = File.ReadAllBytes(temp);
                if (!readBack.AsSpan().SequenceEqual(change.Content)) throw new InvalidDataException($"Verification failed after writing {System.IO.Path.GetFileName(change.Path)}; nothing was modified.");
                var problem = change.Verify?.Invoke(readBack);
                if (problem is not null) throw new InvalidDataException($"{System.IO.Path.GetFileName(change.Path)}: {problem} Nothing was modified.");
            }

            foreach (var change in changes)
            {
                var backup = change.Path + ".bak";
                if (before[change.Path] is not null && !File.Exists(backup)) File.WriteAllBytes(backup, before[change.Path]!);
            }

            var swapped = new List<string>();
            try
            {
                foreach (var (temp, path) in temps)
                {
                    File.Move(temp, path, overwrite: true);
                    swapped.Add(path);
                }
            }
            catch
            {
                foreach (var path in swapped)
                {
                    var original = before[path];
                    try { if (original is not null) File.WriteAllBytes(path, original); else File.Delete(path); } catch (IOException) { }
                }
                throw;
            }
        }
        finally
        {
            foreach (var (temp, _) in temps)
                if (File.Exists(temp)) { try { File.Delete(temp); } catch (IOException) { } }
        }
    }
}

using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace LBAAssembler;

// WPF's (Microsoft.Win32) file dialogs over Avalonia's storage provider, used in the blocking style the editor's code
// expects: dialog.ShowDialog(owner) == true, then dialog.FileName. Filter takes WPF's "Name|*.ext;*.ext2|..." form.
public abstract class FileDialogBase
{
    public string Filter { get; set; } = "";
    public string FileName { get; set; } = "";
    public string? InitialDirectory { get; set; }
    public string? Title { get; set; }
    public string? DefaultExt { get; set; }

    protected static List<FilePickerFileType> ParseFilter(string filter)
    {
        var types = new List<FilePickerFileType>();
        var parts = filter.Split('|');
        for (var i = 0; i + 1 < parts.Length; i += 2)
        {
            var patterns = parts[i + 1].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            // A pattern list that also accepts the lower-case spelling: Linux file names are case-sensitive, and the
            // game files come in either case.
            var all = patterns.SelectMany(p => new[] { p, p.ToLowerInvariant(), p.ToUpperInvariant() }).Distinct().ToList();
            types.Add(new FilePickerFileType(parts[i]) { Patterns = all });
        }
        return types;
    }

    protected static async Task<IStorageFolder?> Folder(TopLevel top, string? path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return null;
        try { return await top.StorageProvider.TryGetFolderFromPathAsync(path); } catch { return null; }
    }
}

public sealed class OpenFileDialog : FileDialogBase
{
    public bool Multiselect { get; set; }
    public string[] FileNames { get; private set; } = Array.Empty<string>();

    public bool? ShowDialog(Window? owner = null)
    {
        owner ??= DialogPump.ActiveWindow;
        if (owner is null) return false;
        var files = DialogPump.Wait(Run(owner));
        if (files.Count == 0) return false;
        FileNames = files.Select(f => f.TryGetLocalPath() ?? "").Where(p => p.Length > 0).ToArray();
        FileName = FileNames.FirstOrDefault() ?? "";
        return FileNames.Length > 0;
    }

    private async Task<IReadOnlyList<IStorageFile>> Run(Window owner)
    {
        var options = new FilePickerOpenOptions
        {
            Title = Title ?? "Open",
            AllowMultiple = Multiselect,
            FileTypeFilter = ParseFilter(Filter),
            SuggestedStartLocation = await Folder(owner, InitialDirectory),
        };
        return await owner.StorageProvider.OpenFilePickerAsync(options);
    }
}

public sealed class SaveFileDialog : FileDialogBase
{
    public bool? ShowDialog(Window? owner = null)
    {
        owner ??= DialogPump.ActiveWindow;
        if (owner is null) return false;
        var file = DialogPump.Wait(Run(owner));
        var path = file?.TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return false;
        FileName = path;
        return true;
    }

    private async Task<IStorageFile?> Run(Window owner)
    {
        var types = ParseFilter(Filter);
        var options = new FilePickerSaveOptions
        {
            Title = Title ?? "Save",
            SuggestedFileName = string.IsNullOrEmpty(FileName) ? null : Path.GetFileName(FileName),
            FileTypeChoices = types,
            DefaultExtension = DefaultExt ?? types.FirstOrDefault()?.Patterns?.FirstOrDefault()?.TrimStart('*', '.'),
            SuggestedStartLocation = await Folder(owner, InitialDirectory ?? (string.IsNullOrEmpty(FileName) ? null : Path.GetDirectoryName(FileName))),
            ShowOverwritePrompt = true,
        };
        return await owner.StorageProvider.SaveFilePickerAsync(options);
    }
}

public sealed class OpenFolderDialog : FileDialogBase
{
    public string FolderName { get; private set; } = "";

    public bool? ShowDialog(Window? owner = null)
    {
        owner ??= DialogPump.ActiveWindow;
        if (owner is null) return false;
        var folders = DialogPump.Wait(Run(owner));
        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return false;
        FolderName = path;
        return true;
    }

    private async Task<IReadOnlyList<IStorageFolder>> Run(Window owner)
    {
        var options = new FolderPickerOpenOptions
        {
            Title = Title ?? "Select a folder",
            AllowMultiple = false,
            SuggestedStartLocation = await Folder(owner, InitialDirectory),
        };
        return await owner.StorageProvider.OpenFolderPickerAsync(options);
    }
}

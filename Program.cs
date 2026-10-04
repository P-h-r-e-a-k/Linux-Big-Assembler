using Avalonia;

namespace LBAAssembler;

// Avalonia's entry point (WPF generated one from App.xaml's StartupUri): the app is built here and its main window
// opened from App.OnFrameworkInitializationCompleted.
internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // Batch export (--export, see Export/ExportCli.cs) writes model files and never opens a window. It is handled
        // here, before the toolkit is started at all rather than from inside the app as the WPF build does: the export
        // code touches no UI type, so this way it also runs on a machine with no display -- a build server, or a Linux
        // box over ssh -- where bringing up a windowing backend first would fail before reaching it.
        if (Export.ExportCli.Wants(args)) return Export.ExportCli.Run(args);
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}

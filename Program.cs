using Avalonia;

namespace LBAAssembler;

// Avalonia's entry point (WPF generated one from App.xaml's StartupUri): the app is built here and its main window
// opened from App.OnFrameworkInitializationCompleted.
internal static class Program
{
    [STAThread]
    public static int Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}

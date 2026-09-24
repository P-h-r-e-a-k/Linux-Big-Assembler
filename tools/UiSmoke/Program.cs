using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

// usage: UiSmoke <window class name> [name-of-element-to-require ...]
//   e.g. UiSmoke ActorScriptWindow ScriptTextBox StatusText
// Opens the window on the headless platform (no display needed), lays it out, checks the named elements exist, and
// with UISMOKE_PNG=<file> renders it to that PNG (the headless platform draws with Skia).
internal static class Program
{
    private static string[] args = Array.Empty<string>();
    private static int result = 1;

    [STAThread]
    private static int Main(string[] arguments)
    {
        args = arguments;
        if (args.Length == 0) { Console.WriteLine("usage: UiSmoke <window class name> [required element names ...]"); return 2; }
        AppBuilder.Configure<SmokeApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).UseSkia().WithInterFont()
            .SetupWithoutStarting();
        Dispatcher.UIThread.Invoke(Run);
        return result;
    }

    private static void Run()
    {
        try
        {
            var type = typeof(LBAAssembler.App).Assembly.GetTypes().FirstOrDefault(t => t.Name == args[0] && typeof(Window).IsAssignableFrom(t));
            if (type is null) { Console.WriteLine($"no window class named {args[0]}"); return; }
            var ctor = type.GetConstructors(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .FirstOrDefault(c => c.GetParameters().Length == 0);
            if (ctor is null) { Console.WriteLine($"{args[0]} has no parameterless constructor; it needs its data to open"); return; }
            var window = (Window)ctor.Invoke(null);
            window.Width = 1180; window.Height = 760;
            window.Show();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var missing = args.Skip(1).Where(n => window.FindControl<Control>(n) is null).ToList();
            if (missing.Count > 0) { Console.WriteLine("missing named elements: " + string.Join(", ", missing)); return; }
            foreach (var n in args.Skip(1)) { var c = window.FindControl<Control>(n)!; Console.WriteLine($"  {n}: {c.Bounds.Width:0}x{c.Bounds.Height:0}"); }
            var png = Environment.GetEnvironmentVariable("UISMOKE_PNG");
            if (png is not null)
            {
                var bitmap = new RenderTargetBitmap(new PixelSize((int)window.Width, (int)window.Height));
                bitmap.Render(window);
                bitmap.Save(png);
                Console.WriteLine("wrote " + png);
            }
            window.Close();
            Console.WriteLine("XAML loaded and laid out OK");
            result = 0;
        }
        catch (Exception e)
        {
            Console.WriteLine("XAML FAILED: " + e.GetType().Name + ": " + e.Message);
            if (e.InnerException is not null) Console.WriteLine("  inner: " + e.InnerException.Message);
        }
    }
}

// The app's own resources and theme (App.axaml) are what the windows' StaticResource lookups need.
internal sealed class SmokeApp : LBAAssembler.App
{
    public override void OnFrameworkInitializationCompleted() { }
}

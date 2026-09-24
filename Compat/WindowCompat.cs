using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.VisualTree;

namespace LBAAssembler;

public enum ResizeMode { NoResize, CanMinimize, CanResize, CanResizeWithGrip }

// WPF window members the editor uses that Avalonia spells differently: Owner is set before Show() (Avalonia takes it
// as Show(owner)'s argument), DialogResult closes the window with its value, ShowDialog() blocks.
public static class WindowCompat
{
    private static readonly ConditionalWeakTable<Window, StrongBox<Window?>> owners = new();
    private static readonly ConditionalWeakTable<Window, StrongBox<object?>> results = new();

    extension(Window window)
    {
        // The window this one belongs to; passed to Show/ShowDialog when it is shown through ShowOwned/ShowDialog().
        public Window? OwnerWindow
        {
            get => owners.TryGetValue(window, out var box) ? box.Value : window.Owner as Window;
            set => owners.AddOrUpdate(window, new StrongBox<Window?>(value));
        }

        public object? DialogResultValue => results.TryGetValue(window, out var box) ? box.Value : null;

        // Setting the result closes the window, as in WPF.
        public bool? DialogResult
        {
            get => window.DialogResultValue as bool?;
            set { results.AddOrUpdate(window, new StrongBox<object?>(value)); window.Close(value); }
        }

        public ResizeMode ResizeMode
        {
            get => window.CanResize ? ResizeMode.CanResize : ResizeMode.NoResize;
            set => window.CanResize = value is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip;
        }

        // Non-modal, owned by OwnerWindow when one was set (so it stays above it and centres on it).
        public void ShowOwned()
        {
            var owner = window.OwnerWindow;
            if (owner is not null && !ReferenceEquals(owner, window) && owner.IsVisible) window.Show(owner); else window.Show();
        }

        // Modal and blocking, like WPF's; the result is what DialogResult was set to (null when closed another way).
        public bool? ShowDialog()
        {
            var owner = window.OwnerWindow;
            var result = DialogPump.ShowModal(window, owner);
            return result as bool? ?? window.DialogResultValue as bool?;
        }

        public bool? ShowDialog(Window? owner)
        {
            if (owner is not null) window.OwnerWindow = owner;
            return window.ShowDialog();
        }

        // WPF's Left/Top in screen pixels.
        public double Left { get => window.Position.X; set => window.Position = new PixelPoint((int)value, window.Position.Y); }
        public double Top { get => window.Position.Y; set => window.Position = new PixelPoint(window.Position.X, (int)value); }

        public static Window? GetWindow(Visual? visual) => visual is null ? null : TopLevel.GetTopLevel(visual) as Window;
    }

    extension(Application app)
    {
        public void Shutdown() => (app.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
        public IReadOnlyList<Window> Windows => (app.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Windows ?? Array.Empty<Window>();
        public Window? MainWindowOrNull => (app.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    }
}

public static class WindowOwnerCompat
{
    // `new SomeWindow(...).WithOwner(this)`: what WPF's `{ Owner = this }` initializer did (an extension property can't be
    // set in an object initializer).
    public static T WithOwner<T>(this T window, Window? owner) where T : Window { window.OwnerWindow = owner; return window; }
}

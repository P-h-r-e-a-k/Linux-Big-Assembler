using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace LBAAssembler;

// The scheme's text colours for the windows that are built in code (see Theme.xaml for the whole scheme).
internal static class UiBrushes
{
    public static readonly IBrush Text = Frozen(0x10, 0x24, 0x3E);
    public static readonly IBrush Muted = Frozen(0x4E, 0x6B, 0x8A);
    public static readonly IBrush Accent = Frozen(0x1B, 0x6E, 0xC2);

    private static IBrush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}

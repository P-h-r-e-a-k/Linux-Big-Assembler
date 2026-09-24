namespace LBAAssembler;

// WPF's Freeze() on brushes, pens, bitmaps and geometries: Avalonia's are immutable enough already (a SolidColorBrush or a
// bitmap shared between controls is fine), so this is a no-op kept for the call sites.
public static class FreezableCompat
{
    extension(object _) { public void Freeze() { } }
}

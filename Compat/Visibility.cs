using Avalonia;

namespace LBAAssembler;

// WPF's three-state Visibility over Avalonia's IsVisible. Hidden (keep the space, draw nothing) has no Avalonia
// equivalent on a plain control, so it is treated as Collapsed; the editor only ever used Visible and Collapsed.
public enum Visibility { Visible, Hidden, Collapsed }

public static class VisibilityExtensions
{
    extension(Visual visual)
    {
        public Visibility Visibility
        {
            get => visual.IsVisible ? Visibility.Visible : Visibility.Collapsed;
            set => visual.IsVisible = value == Visibility.Visible;
        }
    }
}

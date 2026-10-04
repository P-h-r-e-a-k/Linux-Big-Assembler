using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;

namespace LBAAssembler;

// The window a DockItem lives in while floated (DockGroup.Float). Closing it -- the titlebar X or the
// Dock button, there is no other way out -- always docks the content back where it came from; a float
// is a temporary "out of the way," never a second way to close/hide a panel (that's the tab's own ×,
// gated separately by DockItem.CanClose).
internal sealed class DockFloatWindow : Window
{
    private readonly DockItem item;
    private readonly DockGroup owner;
    private bool docking;

    public DockFloatWindow(DockItem item, DockGroup owner)
    {
        this.item = item;
        this.owner = owner;
        Title = $"{item.Title} – LBA Assembler";
        Width = 420; Height = 480; MinWidth = 240; MinHeight = 160;
        this.SetResourceReference(TemplatedControl.BackgroundProperty, "ThemeWindowBrush");
        var root = new DockPanel { LastChildFill = true };
        var bar = new Border { BorderThickness = new Thickness(0, 0, 0, 1) };
        bar.SetResourceReference(Border.BackgroundProperty, "ThemeHeaderBrush");
        bar.SetResourceReference(Border.BorderBrushProperty, "ThemeBorderBrush");
        var dockBack = new Button { Content = "⇱ Dock", Margin = new Thickness(6), Padding = new Thickness(8, 3, 8, 3), HorizontalAlignment = HorizontalAlignment.Left };
        dockBack.Click += (_, _) => Close();
        bar.Child = dockBack;
        DockPanel.SetDock(bar, Dock.Top);
        root.Children.Add(bar);
        root.Children.Add(item.Content);
        Content = root;
        Closing += (_, _) => Redock();
        // Avalonia's Owner is read-only and set by Show(owner) instead; the port keeps the intent (and the
        // "stays above the editor" behaviour) in Compat/WindowCompat.cs -- DockGroup.Float calls ShowOwned().
        this.WithOwner(Application.Current?.MainWindowOrNull);
    }

    private void Redock()
    {
        if (docking) return;
        docking = true;
        (Content as DockPanel)?.Children.Remove(item.Content);
        owner.Unfloat(item);
        owner.Activate(item.Key);
    }
}

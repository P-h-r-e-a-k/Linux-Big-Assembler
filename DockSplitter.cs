using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;

namespace LBAAssembler;

// A drag handle that resizes one neighbouring DockGroup by adjusting its explicit Height/Width, for use
// inside a DockPanel (GridSplitter -- Avalonia has one too -- needs a Grid parent, which the DockPanel-based
// layout in MainWindow.axaml doesn't have -- see DockGroup's own comment for why). The DockPanel's remaining
// ("fill") child absorbs whatever size change this makes automatically; there is nothing on the other side of
// the splitter to adjust in turn.
internal sealed class DockSplitter : Thumb
{
    private readonly Control target;
    private readonly bool resizesWidth;   // true: a vertical bar the user drags left/right, resizing Width. false: a horizontal bar, resizing Height.
    private readonly int sizeSign;        // +1 if dragging down/right grows the target, -1 if it shrinks it (depends which side of the target the splitter sits on)
    private const double Min = 40, Max = 900;

    public DockSplitter(Control target, bool resizesWidth, int sizeSign)
    {
        this.target = target; this.resizesWidth = resizesWidth; this.sizeSign = sizeSign;
        // A bare Thumb paints nothing of its own, and a control with no geometry isn't hit-testable either, so the
        // handle brings its own one-Border template rather than relying on whatever the active theme gives a Thumb.
        Template = new FuncControlTemplate<DockSplitter>((splitter, _) => new Border { [!Border.BackgroundProperty] = splitter[!BackgroundProperty] });
        this.SetResourceReference(BackgroundProperty, "ThemeBorderBrush");
        // (named for UI Automation: an unnamed thumb is all it saw)
        Avalonia.Automation.AutomationProperties.SetName(this, resizesWidth ? "Resize width" : "Resize height");
        if (resizesWidth) { Width = 4; Cursor = Cursors.SizeWE; HorizontalAlignment = HorizontalAlignment.Stretch; }
        else { Height = 4; Cursor = Cursors.SizeNS; VerticalAlignment = VerticalAlignment.Stretch; }
        DragDelta += OnDragDelta;
        // Follows the target's own collapse (DockGroup.RebuildHeader, when its last item floats or closes):
        // nothing left to resize against once the target takes no space. Avalonia has no IsVisibleChanged event;
        // the property-changed notification it does raise carries the property, so the new value is read back off
        // the target (the port's own FilterableComboBox watches ComboBox.Text the same way).
        IsVisible = target.IsVisible;
        target.PropertyChanged += (_, e) => { if (e.Property == IsVisibleProperty) IsVisible = target.IsVisible; };
    }

    // Avalonia's Thumb reports the drag as one Vector: where the pointer is now in the thumb's own coordinates,
    // less where it grabbed it -- the same quantity WPF split across DragDeltaEventArgs.Horizontal/VerticalChange.
    private void OnDragDelta(object? sender, VectorEventArgs e)
    {
        var delta = (resizesWidth ? e.Vector.X : e.Vector.Y) * sizeSign;
        if (resizesWidth) target.Width = Math.Clamp((double.IsNaN(target.Width) ? target.ActualWidth : target.Width) + delta, Min, Max);
        else target.Height = Math.Clamp((double.IsNaN(target.Height) ? target.ActualHeight : target.Height) + delta, Min, Max);
    }
}

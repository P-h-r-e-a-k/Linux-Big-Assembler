using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using LBAAssembler;

namespace LbaBodyStudio;

// A small stand-in for WinForms' NumericUpDown (a bounded integer field with a spinner): nothing like it
// existed in this app before -- every other numeric input here (GridEditorWindow's own "layer" control) is
// a genuinely different shape, a live pick-a-value-along-a-range slider, not "type or nudge a precise
// number" -- so this is new, not a duplicate of something already available. Used by BodyStudioWindow and
// AnimationStudioWindow (the Avalonia port of Body Studio / Animation Studio), matching WinForms' own
// NumericUpDown behaviour closely enough that porting each field over was a direct swap.
internal sealed class NumberBox : Grid
{
    public readonly int Minimum, Maximum;
    private readonly TextBox box = new() { VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(6, 2, 4, 2), BorderThickness = new Thickness(1) };
    private int currentValue;

    public event Action? ValueChanged;

    public int Value
    {
        get => currentValue;
        set
        {
            var clamped = System.Math.Clamp(value, Minimum, Maximum);
            currentValue = clamped;
            var text = clamped.ToString();
            if (box.Text != text) box.Text = text;
            ValueChanged?.Invoke();
        }
    }

    public NumberBox(int min, int max, int initial)
    {
        Minimum = min; Maximum = max;
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        SetColumn(box, 0); SetColumnSpan(box, 2);

        var up = new RepeatButton { Content = "▲", Width = 18, Height = 11, Padding = new Thickness(0), FontSize = 7, Focusable = false };
        var down = new RepeatButton { Content = "▼", Width = 18, Height = 11, Padding = new Thickness(0), FontSize = 7, Focusable = false };
        var spin = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 2, 0) };
        spin.Children.Add(up); spin.Children.Add(down);
        SetColumn(spin, 1);

        Children.Add(box); Children.Add(spin);
        currentValue = System.Math.Clamp(initial, min, max);
        box.Text = currentValue.ToString();

        up.Click += (_, _) => Value = currentValue + 1;
        down.Click += (_, _) => Value = currentValue - 1;
        box.LostFocus += (_, _) => { if (int.TryParse(box.Text, out var v)) Value = v; else box.Text = currentValue.ToString(); };
        // WPF's PreviewMouseWheel: Avalonia has no separate preview events, a tunnelling handler on the same
        // event is the equivalent, and it still gets to mark the notch handled before the text box's own
        // scroll viewer sees it.
        box.AddHandler(InputElement.PointerWheelChangedEvent, (object? _, PointerWheelEventArgs e) => { Value = currentValue + (e.WheelDelta > 0 ? 1 : -1); e.Handled = true; }, RoutingStrategies.Tunnel);
    }

    // Resource key names, not resolved Brush values, so this keeps tracking the active theme -- a Brush
    // captured once at construction time would freeze at whatever theme was active then, the same
    // StaticResource vs DynamicResource distinction the theme dictionaries' own styles rely on.
    // Named ApplyTheme, not Theme as in the WPF original: Avalonia's StyledElement already has a Theme
    // property (the control's ControlTheme), which a method of that name here would hide.
    public void ApplyTheme(string fieldKey, string textKey, string borderKey)
    {
        box.SetResourceReference(TextBox.BackgroundProperty, fieldKey);
        box.SetResourceReference(TextBox.ForegroundProperty, textKey);
        box.SetResourceReference(TextBox.BorderBrushProperty, borderKey);
    }
}

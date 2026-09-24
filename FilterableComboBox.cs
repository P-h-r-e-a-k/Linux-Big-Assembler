using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace LBAAssembler;

// Wires an editable ComboBox up with type-to-filter narrowing and safe
// mouse-click selection, for the main window's Island/Scene pickers.
// Shares its approach (not its code -- ActorAttributesWindow's own Body/
// Animation combos predate this and work, so they're left alone rather
// than risked mid-cleanup) with the fix already proven there: an editable
// ComboBox syncs its own Text to the newly-picked item as part of the same
// selection operation, which raises the same text change typing does,
// but the toolkit doesn't guarantee that sync happens before SelectionChanged runs.
// A real mouse click on a dropdown popup item can land that sync *after*,
// so the immediate post-selection reset-to-full-list gets clobbered right
// back down to one entry a moment later. Re-running the reset a second
// time at Background dispatcher priority -- after any such late sync has
// already run -- fixes it regardless of ordering.
//
// Avalonia: the editable combo box's text is its Text property (a styled property, watched here through
// PropertyChanged); its text box is the template's own, found through ComboBox.EditableTextBox (Compat).
internal sealed class FilterableComboBox
{
    public sealed record Option(int Index, string Display)
    {
        public override string ToString() => Display;
    }

    private readonly ComboBox combo;
    private readonly Func<IReadOnlyList<Option>> allOptions;
    private bool suppress;

    public event Action? Committed;

    public FilterableComboBox(ComboBox combo, Func<IReadOnlyList<Option>> allOptions)
    {
        this.combo = combo;
        this.allOptions = allOptions;
        combo.PropertyChanged += (_, e) => { if (e.Property == ComboBox.TextProperty) OnTextChanged(); };
        combo.GotFocus += (_, _) => ResetFilter();
        combo.SelectionChanged += OnSelectionChanged;
    }

    public void Refresh() => combo.ItemsSource = allOptions();

    private void OnTextChanged()
    {
        if (suppress) return;
        var options = allOptions();
        var text = combo.Text ?? "";
        var editBox = combo.EditableTextBox;
        var caret = editBox?.CaretIndex ?? text.Length;
        var filtered = string.IsNullOrWhiteSpace(text)
            ? options
            : options.Where(o => o.Display.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();

        suppress = true;
        combo.ItemsSource = filtered;
        combo.Text = text;
        if (editBox is not null) editBox.CaretIndex = Math.Min(caret, text.Length);
        combo.IsDropDownOpen = combo.IsKeyboardFocusWithin && filtered.Count > 0 && filtered.Count < options.Count;
        suppress = false;
    }

    private void ResetFilter()
    {
        suppress = true;
        var text = combo.Text;
        combo.ItemsSource = allOptions();
        combo.Text = text;
        suppress = false;
        combo.EditableTextBox?.SelectAll();
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (suppress) return;
        // Also fixes a second, related bug found while testing this: picking
        // an item from an already-*filtered* list (type a few letters, then
        // click one of the narrowed-down matches) left the text box blank
        // rather than showing the picked item's text -- the toolkit's own automatic
        // selection-to-text sync for an editable ComboBox apparently loses
        // track of what to display when ItemsSource gets swapped back to the
        // full list (by ResetFilter, below) while that sync is still
        // in-flight. Setting Text ourselves from the actual selected Option,
        // rather than trusting that sync to happen at all, sidesteps it
        // entirely instead of chasing the toolkit's own internal timing.
        if (combo.SelectedItem is Option selected)
        {
            suppress = true;
            combo.Text = selected.Display;
            suppress = false;
        }
        Committed?.Invoke();
        ResetFilter();
        Dispatcher.UIThread.Post(ResetFilter, DispatcherPriority.Background);
    }
}

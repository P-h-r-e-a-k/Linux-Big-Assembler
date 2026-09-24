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
    private bool replacing;      // (the list is being replaced: the selection changes that causes are not picks)
    private bool selecting;      // (a selection change is being handled: the list can't be replaced until it is over)

    public event Action? Committed;

    // The focus has left the combo box (for its owner to take a typed-in value). Its LostFocus isn't that in Avalonia: clicking an
    // entry of the drop-down takes the focus from the text box too, and a typed value taken then would undo the pick being made.
    public event Action? FocusLeft;

    public FilterableComboBox(ComboBox combo, Func<IReadOnlyList<Option>> allOptions)
    {
        this.combo = combo;
        this.allOptions = allOptions;
        combo.PropertyChanged += (_, e) => { if (e.Property == ComboBox.TextProperty) OnTextChanged(); };
        combo.GotFocus += (_, _) => { if (!combo.IsDropDownOpen) ResetFilter(); };      // (not an entry of the drop-down being clicked: that would lose the click)
        combo.SelectionChanged += OnSelectionChanged;
        combo.LostFocus += (_, _) => Dispatcher.UIThread.Post(() => { if (!combo.IsKeyboardFocusWithin && !combo.IsDropDownOpen) FocusLeft?.Invoke(); });
    }

    public void Refresh() => SetItems(allOptions());

    // Replaces the list. Avalonia can't do that from inside a selection change (one its own text-to-selection sync makes, when the
    // text is set in code, included): it takes the new list but throws before the selection has it ("Cannot change source while
    // update is in progress"), and the combo box then selects nothing that is only in the new list. So a replacement asked for
    // then waits, and the whole list is put back as soon as the change is over (with retry). False when it had to wait.
    private bool SetItems(IReadOnlyList<Option> items, bool retry = true)
    {
        if (!replacing && !selecting)
        {
            replacing = true;
            try { combo.ItemsSource = items; return true; }
            catch (InvalidOperationException)
            {
                // (a change Avalonia was still in the middle of: empty the list, so the one put back below goes in from scratch)
                try { combo.ItemsSource = null; } catch (InvalidOperationException) { }
                retry = true;
            }
            finally { replacing = false; }
        }
        if (retry) Dispatcher.UIThread.Post(() => ResetFilter(retry: false));
        return false;
    }

    private void OnTextChanged()
    {
        // Only typing filters. Avalonia also raises this when the combo box copies a selection made in code into its text (the
        // island chosen at startup, or through the Scenes menu); filtering then would narrow the list to that one entry, and a
        // later selection in code of anything else would find nothing to select.
        if (suppress || !combo.IsKeyboardFocusWithin) return;
        var options = allOptions();
        var text = combo.Text ?? "";
        var editBox = combo.EditableTextBox;
        var caret = editBox?.CaretIndex ?? text.Length;
        var filtered = string.IsNullOrWhiteSpace(text)
            ? options
            : options.Where(o => o.Display.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();

        suppress = true;
        if (!SetItems(filtered, retry: false)) { suppress = false; return; }      // (the text changed with a pick, not by typing: nothing to filter)
        combo.Text = text;
        if (editBox is not null) editBox.CaretIndex = Math.Min(caret, text.Length);
        combo.IsDropDownOpen = combo.IsKeyboardFocusWithin && filtered.Count > 0 && filtered.Count < options.Count;
        suppress = false;
    }

    private void ResetFilter() => ResetFilter(retry: true);

    private void ResetFilter(bool retry)
    {
        suppress = true;
        var text = combo.Text;
        SetItems(allOptions(), retry);
        combo.Text = text;
        suppress = false;
        if (combo.IsKeyboardFocusWithin) combo.EditableTextBox?.SelectAll();      // (Avalonia shows a selection in a box without the focus too)
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // (Avalonia empties the selection as soon as the typed text matches no entry: that's typing, not a pick, and treating it as
        // one would select the typed text, for the next key to replace)
        if (suppress || replacing || combo.SelectedItem is null) return;
        selecting = true;
        try { OnPicked(); }
        finally { selecting = false; }
        Dispatcher.UIThread.Post(ResetFilter, DispatcherPriority.Background);
    }

    private void OnPicked()
    {
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
        // Committed runs at once, as in WPF: callers chain on it (the Scenes menu selects an island, whose handler loads that
        // island's scene list, and then selects a scene from that list).
        Committed?.Invoke();
        ResetFilter();      // (waits for the change to be over, see SetItems)
    }
}

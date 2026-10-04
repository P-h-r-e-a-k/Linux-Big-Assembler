using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace LBAAssembler;

// One item hosted in a DockGroup: a title, its content, and what the user is allowed to do to it.
// Mirrors the handful of AvalonDock LayoutAnchorable/LayoutDocument members this app actually used
// (Title, IsVisible, CanClose, CanFloat) -- see the DockGroup class comment for why AvalonDock was
// replaced. SetVisible/Activate just forward to the owning group, so the MainWindow.axaml.cs fields that
// used to be LayoutAnchorable (ZoneDetailsTab, PlayTab, ...) can stay DockItem and keep every existing
// call site (Modes.cs/Zones.cs/Play.cs/Placement.cs) unchanged.
internal sealed class DockItem
{
    public readonly string Key;
    public readonly string Title;
    public readonly Control Content;          // Avalonia's Control is WPF's FrameworkElement here
    public readonly bool CanClose;
    public readonly bool CanFloat;
    public bool IsVisible = true;   // false: closed (not shown as a tab), brought back via SetVisible(true)
    public bool Floating;           // true: living in its own Window right now, not shown as a tab at all
    // false: still shown at its normal tab-strip position (unlike IsVisible=false, which removes the tab
    // entirely and lets its neighbours shift into the gap) but greyed out and not clickable -- for a tab
    // that only applies in some modes (MainWindow.Modes.cs's Details/Build/Script), so the strip's order
    // stays stable and Ctrl+1/2/3 muscle memory keeps landing in the same place regardless of which mode
    // was active before.
    public bool IsEnabled = true;
    internal DockGroup? Owner;
    internal DockFloatWindow? FloatWindow;

    public DockItem(string key, string title, Control content, bool canClose, bool canFloat)
    {
        Key = key; Title = title; Content = content; CanClose = canClose; CanFloat = canFloat;
    }

    public void SetVisible(bool visible) => Owner?.SetVisible(Key, visible);
    public void SetEnabled(bool enabled) => Owner?.SetEnabled(Key, enabled);
    public void Activate() => Owner?.Activate(Key);
    // AvalonDock had two names for "the shown tab in its pane" (IsActive: DockingManager-wide;
    // IsSelected: within its own pane); this app only ever has one DockingManager, so both meant the
    // same thing at every call site -- kept as two names so neither call site needed to change.
    public bool IsActive => Owner?.IsItemActive(this) ?? false;
    public bool IsSelected => IsActive;
}

// A dockable pane: one or more DockItems sharing a header (a tab strip once there's more than one),
// with a pin (auto-hide -- collapse to a thin strip, peek via a flyout) and, per item, close and float
// (pop out to its own Window; closing that window docks it back). This is a small, purpose-built
// replacement for AvalonDock (removed because the whole app only ever used a handful of its features --
// a fixed split layout with no runtime rearranging or saved layout, one tabbed group, show/hide, activate,
// and one "tab selected" event -- see the commit that introduced this file for the fuller inventory).
// Only float and auto-hide have no prior code driving them; everything else mirrors the exact API surface
// MainWindow.axaml.cs's own AvalonDock wrapper region already had (SetPanelVisible/ActivatePanel), so the
// call sites in MainWindow.Modes.cs/Zones.cs/Play.cs/Placement.cs did not need to change.
//
// (Avalonia has no AvalonDock either -- the Dock.Avalonia package is a much larger thing again -- so the
// port keeps this control rather than swapping one docking library for another.)
internal sealed class DockGroup : Grid
{
    private readonly List<DockItem> items = new();
    private readonly StackPanel tabStrip = new() { Orientation = Orientation.Horizontal };
    private readonly TextBlock soloTitle = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0), FontFamily = UiFonts.Mono, FontSize = 10 };
    // MinWidth/MinHeight 0 and CornerRadius: Avalonia's Fluent theme gives a button a minimum size and its own corner
    // radius, which WPF's replacement ControlTemplate (see HeaderButtonClass) used to leave behind with the rest of it.
    private readonly ToggleButton pinButton = new() { Width = 22, Height = 22, MinWidth = 0, MinHeight = 0, Padding = new Thickness(0), CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 0, 2, 0) };
    private readonly Button floatButton = new() { Width = 22, Height = 22, MinWidth = 0, MinHeight = 0, Padding = new Thickness(0), CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 0, 2, 0) };
    private readonly ContentControl body = new();
    private readonly Border headerBorder;
    private readonly RowDefinition bodyRow = new() { Height = new GridLength(1, GridUnitType.Star) };
    private Popup? flyout;
    private readonly ContentControl flyoutHost = new();
    private DockItem? active;
    private bool pinned;

    public double NormalSize = 200;              // the row/column height or width this group asks for while docked normally
    private const double CollapsedStripSize = 26; // header-only height/width while pinned off

    public event EventHandler<DockItem>? ActiveItemChanged;

    public DockGroup()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(bodyRow);

        var header = new DockPanel { LastChildFill = true, Height = 26 };
        header.SetResourceReference(Panel.BackgroundProperty, "ThemeHeaderBrush");
        Grid.SetRow(header, 0);
        DockPanel.SetDock(soloTitle, Dock.Left);
        tabStrip.Margin = new Thickness(2, 0, 0, 0);
        // A solo pane (one item, no tab strip) has nothing else to click to peek at it while pinned off --
        // reads `active` live (not captured), since RebuildHeader never re-subscribes this.
        soloTitle.PointerPressed += (_, e) => { if (!e.IsLeft) return; if (pinned && active is not null) ShowFlyout(active); e.Handled = true; };

        // Drawn as vector icons in the button's own foreground colour: the emoji pin and the box glyph they replace came out thin, greyish and
        // theme-blind (a colour emoji ignores Foreground), which is what made them hard to see.
        StyleHeaderButtons();
        pinButton.Classes.Add(HeaderButtonClass);
        floatButton.Classes.Add(HeaderButtonClass);
        pinButton.Content = Icon(PinOutline, pinButton);
        floatButton.Content = Icon(PopOut, floatButton);
        pinButton.SetResourceReference(TemplatedControl.ForegroundProperty, "ThemeTextBrush");
        floatButton.SetResourceReference(TemplatedControl.ForegroundProperty, "ThemeTextBrush");
        pinButton.ToolTip = "Auto-hide (collapse to a strip; click a tab to peek)";
        floatButton.ToolTip = "Float (pop out to its own window)";
        pinButton.Click += (_, _) => SetPinned(!pinned);
        floatButton.Click += (_, _) => { if (active is { CanFloat: true } a) Float(a); };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 2, 0) };
        buttons.Children.Add(pinButton);
        buttons.Children.Add(floatButton);
        DockPanel.SetDock(buttons, Dock.Right);

        header.Children.Add(buttons);
        header.Children.Add(soloTitle);
        header.Children.Add(tabStrip);
        headerBorder = new Border { Child = header, BorderThickness = new Thickness(0, 0, 0, 1) };
        headerBorder.SetResourceReference(Border.BorderBrushProperty, "ThemeBorderBrush");
        Grid.SetRow(headerBorder, 0);
        Children.Add(headerBorder);

        Grid.SetRow(body, 1);
        body.SetResourceReference(TemplatedControl.BackgroundProperty, "ThemeWindowBrush");
        Children.Add(body);
    }

    private const string PinFilled = "M16,12V4H17V2H7V4H8V12L6,14V16H11.2V22H12.8V16H18V14L16,12Z";
    private const string PinOutline = "M16,12V4H17V2H7V4H8V12L6,14V16H11.2V22H12.8V16H18V14L16,12M8.8,14L10,12.8V4H14V12.8L15.2,14H8.8Z";
    private const string PopOut = "M14,3V5H17.59L7.76,14.83L9.17,16.24L19,6.41V10H21V3M19,19H5V5H12V3H5C3.89,3 3,3.9 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V12H19V19Z";

    private const string HeaderButtonClass = "dockHeaderButton";

    // A small flat header button: no chrome until the pointer is over it, no padding (the themed button's padding left a 22-pixel button
    // 2 pixels of room for its icon), the icon's colour is the button's foreground. WPF got this by replacing the whole ControlTemplate
    // (parsed from markup, with IsMouseOver/IsEnabled triggers); Avalonia keeps the Fluent template and only repaints the part it draws
    // its chrome into -- PART_ContentPresenter -- because a style set here, on the group, outranks the control theme's own, so the stock
    // hover, pressed and (for the pin's ToggleButton) checked-accent fills never show. Built in code rather than parsed from a markup
    // string: Avalonia's runtime XAML loader, the counterpart of WPF's XamlReader.Parse, is a separate package this app doesn't reference.
    private void StyleHeaderButtons()
    {
        Styles.Add(ChromeStyle(x => x.Class(HeaderButtonClass), Brushes.Transparent, Brushes.Transparent));
        // Added after the flat one: both selectors match while the pointer is over the button, and the later style wins.
        Styles.Add(ChromeStyle(x => x.Class(HeaderButtonClass).Class(":pointerover"), new DynamicResourceExtension("ThemeHoverBrush"), new DynamicResourceExtension("ThemeBorderBrush")));
        var disabled = new Style(x => x.Class(HeaderButtonClass).Class(":disabled"));
        disabled.Setters.Add(new Setter(Visual.OpacityProperty, 0.4));
        Styles.Add(disabled);
    }

    // The background/border the themed button paints behind its content, for the buttons carrying HeaderButtonClass.
    // The values are plain brushes or DynamicResourceExtensions (Avalonia's {DynamicResource} is an IBinding, which a
    // Setter takes as its value just as SetResourceReference does elsewhere in the port).
    private static Style ChromeStyle(Func<Selector?, Selector> selector, object background, object borderBrush)
    {
        var style = new Style(x => selector(x).Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"));
        style.Setters.Add(new Setter(ContentPresenter.BackgroundProperty, background));
        style.Setters.Add(new Setter(ContentPresenter.BorderBrushProperty, borderBrush));
        return style;
    }

    // A 14-pixel vector icon painted in the given button's foreground colour (so it follows the theme and the pinned accent).
    private static Avalonia.Controls.Shapes.Path Icon(string data, TemplatedControl owner)
    {
        var path = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse(data), Stretch = Stretch.Uniform, Width = 14, Height = 14, IsHitTestVisible = false };
        path[!Avalonia.Controls.Shapes.Shape.FillProperty] = owner[!TemplatedControl.ForegroundProperty];
        return path;
    }

    public DockItem AddItem(string key, string title, Control content, bool canClose = true, bool canFloat = true)
    {
        if (content.Parent is Panel oldParent) oldParent.Children.Remove(content);   // e.g. MainWindow.axaml's hidden content pool
        var item = new DockItem(key, title, content, canClose, canFloat) { Owner = this };
        items.Add(item);
        RebuildHeader();
        if (active is null) Activate(key);
        return item;
    }

    // Documents (this app's one Viewport) don't auto-hide the way anchorables do -- AvalonDock didn't
    // offer it for LayoutDocumentPane either.
    public bool AllowPin { set => pinButton.Visibility = value ? Visibility.Visible : Visibility.Collapsed; }

    public DockItem? Find(string key) => items.FirstOrDefault(i => i.Key == key);
    internal bool IsItemActive(DockItem item) => active == item;

    // Shows or hides a closed panel -- the "Panels" menu's per-name reopen entries. Floating items are
    // left alone (visible means "back on the tab strip", not "stop floating"; SetVisible(false) on a
    // floating item just marks it non-visible for later).
    public void SetVisible(string key, bool visible)
    {
        var item = Find(key);
        if (item is null || item.IsVisible == visible) return;
        item.IsVisible = visible;
        RebuildHeader();
        if (visible) Activate(key);
        else if (active == item) Activate(items.FirstOrDefault(i => i.IsVisible && i.IsEnabled && !i.Floating)?.Key ?? "");
    }

    // Modes.cs's per-mode tab availability: keeps the tab in the strip at its normal position, greyed
    // out and unclickable, instead of removing it (see IsEnabled's own comment on DockItem).
    public void SetEnabled(string key, bool enabled)
    {
        var item = Find(key);
        if (item is null || item.IsEnabled == enabled) return;
        item.IsEnabled = enabled;
        if (!enabled && active == item)
        {
            var fallback = items.FirstOrDefault(i => i.IsVisible && i.IsEnabled && !i.Floating);
            if (fallback is not null) SetActive(fallback);
        }
        RebuildHeader();
    }

    // Shows (if closed) and selects a panel -- "bring this to the front," used when a zone is clicked,
    // Play mode starts, etc. Un-floats it first if it was floating, matching what a user would expect
    // clicking "Play" in the menu to do even if Play is currently sitting in its own window. A disabled
    // tab (wrong mode for it right now) can't be activated -- callers that want to jump to one of these
    // switch mode first (MainWindow.axaml.cs's ShowPanel_Click).
    public void Activate(string key)
    {
        var item = Find(key);
        if (item is null || !item.IsEnabled) return;
        if (item.Floating) Unfloat(item);
        if (!item.IsVisible) { item.IsVisible = true; RebuildHeader(); }
        if (pinned) { ShowFlyout(item); return; }
        SetActive(item);
    }

    private void SetActive(DockItem item)
    {
        active = item;
        Detach(item.Content);
        body.Content = item.Content;
        RebuildHeader();
        ActiveItemChanged?.Invoke(this, item);
    }

    // body and flyoutHost are two separate ContentControls that can each hold at most one of these items'
    // Content at a time; Avalonia throws ("The control already has a visual parent", WPF's "must
    // disconnect... before attaching to new parent") if the same element is still sitting as the other
    // one's Content when it's assigned here. Pinning doesn't clear body.Content (only its visibility), and
    // activating while pinned goes to flyoutHost instead of body -- so without this, unpinning (or
    // activating a second item while pinned, then coming back to the first) reuses a Content value the
    // other host never let go of.
    private void Detach(Control content)
    {
        if (ReferenceEquals(body.Content, content)) body.Content = null;
        if (ReferenceEquals(flyoutHost.Content, content)) flyoutHost.Content = null;
    }

    public void SetPinned(bool value)
    {
        pinned = value;
        pinButton.SetResourceReference(TemplatedControl.ForegroundProperty, pinned ? "ThemeAccentBrush" : "ThemeTextBrush");
        pinButton.Content = Icon(pinned ? PinFilled : PinOutline, pinButton);
        bodyRow.Height = pinned ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        body.Visibility = pinned ? Visibility.Collapsed : Visibility.Visible;
        Height = pinned ? CollapsedStripSize : double.NaN;
        flyout?.SetCurrentValue(Popup.IsOpenProperty, false);
    }

    // While pinned off, clicking a tab peeks at its content in a popup anchored under the header
    // instead of expanding the pane -- closes itself on any outside click (Avalonia's light dismiss,
    // WPF's StaysOpen=False).
    private void ShowFlyout(DockItem item)
    {
        if (flyout is null)
        {
            flyout = new Popup { PlacementTarget = headerBorder, Placement = PlacementMode.Bottom, IsLightDismissEnabled = true };
            var border = new Border { BorderThickness = new Thickness(1) };
            border.SetResourceReference(Border.BackgroundProperty, "ThemeWindowBrush");
            border.SetResourceReference(Border.BorderBrushProperty, "ThemeBorderStrongBrush");
            border.Child = flyoutHost;
            flyout.Child = border;
            // An Avalonia Popup is a control, not a free-floating object: it has to be in the tree to find the
            // window that hosts it. It measures to nothing, so sharing the header's row costs no space.
            Grid.SetRow(flyout, 0);
            Children.Add(flyout);
        }
        flyoutHost.Width = Bounds.Width > 0 ? Bounds.Width : NormalSize;
        flyoutHost.Height = NormalSize;
        Detach(item.Content);
        flyoutHost.Content = item.Content;
        active = item;
        RebuildHeader();
        // Deferred: a light-dismiss popup opened synchronously from inside a pointer-pressed handler closes
        // itself right back on that same click's release (the still-in-progress click reaches the dismiss
        // overlay before focus finishes moving into the popup). Opening it on the next dispatcher pass,
        // after the click has fully finished, avoids that.
        Dispatcher.UIThread.Post(() => flyout!.IsOpen = true, DispatcherPriority.Input);
    }

    public void Float(DockItem item)
    {
        if (!item.CanFloat || item.Floating) return;
        item.Floating = true;
        Detach(item.Content);   // might be sitting in body (active, docked) or flyoutHost (active, peeked while pinned)
        var window = new DockFloatWindow(item, this);
        item.FloatWindow = window;
        RebuildHeader();
        if (active == item) Activate(items.FirstOrDefault(i => i.IsVisible && !i.Floating)?.Key ?? "");
        window.ShowOwned();   // Avalonia takes the owner as Show()'s argument; see Compat/WindowCompat.cs
    }

    // Called by DockFloatWindow when its own window closes (the redock button, or the window X either way).
    internal void Unfloat(DockItem item)
    {
        if (!item.Floating) return;
        item.Floating = false;
        item.FloatWindow = null;
        RebuildHeader();
    }

    private void RebuildHeader()
    {
        var visible = items.Where(i => i.IsVisible && !i.Floating).ToList();
        tabStrip.Children.Clear();
        soloTitle.Visibility = Visibility.Collapsed;
        tabStrip.Visibility = Visibility.Collapsed;

        // Nothing left to show here (its one item floated out, or every item got closed): give the space
        // back rather than reserving a blank slot -- DockSplitter.Min/Max clamps normal-sized siblings, but
        // an empty group's own explicit size otherwise just sits there empty. (The port's Visibility shim is
        // an extension member, which needs a receiver, so the group sets its own IsVisible directly.)
        IsVisible = visible.Count != 0;
        if (visible.Count == 0) { pinButton.IsEnabled = floatButton.IsEnabled = false; return; }
        if (visible.Count == 1)
        {
            soloTitle.Visibility = Visibility.Visible;
            soloTitle.Text = visible[0].Title;
            soloTitle.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextMutedBrush");
        }
        else
        {
            tabStrip.Visibility = Visibility.Visible;
            foreach (var item in visible) tabStrip.Children.Add(BuildTab(item));
        }
        pinButton.IsEnabled = true;
        floatButton.IsEnabled = active?.CanFloat ?? false;
        floatButton.Visibility = (active?.CanFloat ?? false) ? Visibility.Visible : Visibility.Collapsed;
    }

    private Border BuildTab(DockItem item)
    {
        var isActive = item == active;
        var text = new TextBlock { Text = item.Title, FontFamily = UiFonts.Mono, FontSize = 10, VerticalAlignment = VerticalAlignment.Center };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(text);
        if (item.CanClose && item.IsEnabled)
        {
            var close = new Button
            {
                Content = "×", Width = 16, Height = 16, MinWidth = 0, MinHeight = 0, Padding = new Thickness(0), Margin = new Thickness(6, 0, 0, 0),
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            };
            close.SetResourceReference(TemplatedControl.ForegroundProperty, "ThemeTextMutedBrush");
            close.Click += (_, e) => { e.Handled = true; SetVisible(item.Key, false); };
            row.Children.Add(close);
        }
        var tab = new Border
        {
            Child = row, Padding = new Thickness(10, 4, 8, 4), Margin = new Thickness(0, 0, 2, 0), Cursor = item.IsEnabled ? Cursors.Hand : Cursors.Arrow,
            BorderThickness = new Thickness(1, 1, 1, 0),
            // Matches IslandEditorView's own disabled-control opacity (0.45) rather than inventing a second convention.
            Opacity = item.IsEnabled ? 1.0 : 0.45,
        };
        tab.SetResourceReference(Border.BackgroundProperty, isActive ? "ThemeWindowBrush" : "ThemeRaisedBrush");
        tab.SetResourceReference(Border.BorderBrushProperty, "ThemeBorderBrush");
        text.SetResourceReference(TextBlock.ForegroundProperty, isActive ? "ThemeTextBrush" : "ThemeTextMutedBrush");
        if (item.IsEnabled) tab.PointerPressed += (_, e) => { if (!e.IsLeft) return; if (pinned) ShowFlyout(item); else SetActive(item); e.Handled = true; };
        else tab.ToolTip = $"{item.Title} isn't available in the current mode";
        return tab;
    }
}

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LBAAssembler.Scenes;

namespace LBAAssembler;

// Choosing a behaviour for an actor (ActorBehaviours): no script, the templates, the game's own actors of its kind -- each with what it does
// and where it comes from, the kind's usual one marked -- and, for "Talks when spoken to", the line it says (a text of the scene's island).
// Built in code, like ListPickWindow.
internal sealed class BehaviourPickWindow : Window
{
    private readonly ListBox list = new();
    private readonly TextBox filter = new();
    private readonly TextBlock detail = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
    private readonly StackPanel textPanel = new() { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 8, 0, 0) };
    private readonly ListBox texts = new() { Height = 150 };
    private readonly TextBox textFilter = new() { Margin = new Thickness(0, 4, 0, 4), Padding = new Thickness(4, 2, 4, 2) };
    private readonly IReadOnlyList<ActorBehaviour> behaviours;
    private readonly IReadOnlyList<(int Id, string Text)> lines;
    private readonly string? usualId;

    public ActorBehaviour? Chosen { get; private set; }
    public int? Text { get; private set; }

    public BehaviourPickWindow(string title, IReadOnlyList<ActorBehaviour> behaviours, string? usualId, IReadOnlyList<(int Id, string Text)> lines, string? note)
    {
        this.behaviours = behaviours;
        this.lines = lines;
        this.usualId = usualId;
        Title = title;
        Width = 680; Height = 700;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(Control.BackgroundProperty, "ThemeWindowBrush");
        SetResourceReference(Control.ForegroundProperty, "ThemeTextBrush");
        ShowInTaskbar = false;

        var panel = new DockPanel { Margin = new Thickness(12) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        var ok = new Button { Content = "OK", Padding = new Thickness(18, 4, 18, 4), Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        var cancel = new Button { Content = "Cancel", Padding = new Thickness(14, 4, 14, 4), IsCancel = true };
        buttons.Children.Add(ok); buttons.Children.Add(cancel);
        DockPanel.SetDock(buttons, Dock.Bottom);
        panel.Children.Add(buttons);

        // the line it says (the talking template)
        var textLabel = new TextBlock { Text = "The line it says (a text of this scene's island):" };
        textLabel.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextMutedBrush");
        textPanel.Children.Add(textLabel);
        textPanel.Children.Add(textFilter);
        texts.SetResourceReference(Control.BackgroundProperty, "ThemeFieldBrush");
        texts.SetResourceReference(Control.ForegroundProperty, "ThemeTextBrush");
        textPanel.Children.Add(texts);
        DockPanel.SetDock(textPanel, Dock.Bottom);
        panel.Children.Add(textPanel);
        DockPanel.SetDock(detail, Dock.Bottom);
        detail.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextBrush");
        panel.Children.Add(detail);

        if (note is not null)
        {
            var text = new TextBlock { Text = note, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
            text.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextMutedBrush");
            DockPanel.SetDock(text, Dock.Top);
            panel.Children.Add(text);
        }
        DockPanel.SetDock(filter, Dock.Top);
        filter.Margin = new Thickness(0, 0, 0, 8);
        filter.Padding = new Thickness(4, 3, 4, 3);
        AutomationProperties.SetName(filter, "Filter behaviours");
        panel.Children.Add(filter);
        list.SetResourceReference(Control.BackgroundProperty, "ThemeFieldBrush");
        list.SetResourceReference(Control.ForegroundProperty, "ThemeTextBrush");
        list.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
        AutomationProperties.SetName(list, "Behaviours");
        AutomationProperties.SetName(texts, "Lines");
        panel.Children.Add(list);
        Content = panel;

        filter.TextChanged += (_, _) => Fill();
        textFilter.TextChanged += (_, _) => FillTexts();
        list.SelectionChanged += (_, _) => Selected();
        ok.Click += (_, _) => Accept();
        list.MouseDoubleClick += (_, _) => Accept();
        filter.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Down && list.SelectedIndex < list.Items.Count - 1) { list.SelectedIndex++; e.Handled = true; }
            if (e.Key == Key.Up && list.SelectedIndex > 0) { list.SelectedIndex--; e.Handled = true; }
        };
        Fill();
        FillTexts();
        Loaded += (_, _) => filter.Focus();
    }

    private void Fill()
    {
        var text = filter.Text.Trim();
        var keep = (list.SelectedItem as ListBoxItem)?.Tag as ActorBehaviour;
        list.Items.Clear();
        foreach (var b in behaviours.Where(b => text.Length == 0 || (b.Name + " " + b.Description + " " + b.Source).Contains(text, StringComparison.OrdinalIgnoreCase)))
        {
            var usual = b.Id == usualId;
            var name = new TextBlock { Text = b.Name + (usual ? "   (its kind's usual behaviour)" : ""), FontWeight = FontWeights.SemiBold };
            var what = new TextBlock { Text = b.Description + (b.Source.Length > 0 ? $"   [{b.Source}]" : ""), TextWrapping = TextWrapping.Wrap, FontSize = 11 };
            what.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextMutedBrush");
            var stack = new StackPanel { Margin = new Thickness(2, 3, 2, 3) };
            stack.Children.Add(name); stack.Children.Add(what);
            var item = new ListBoxItem { Content = stack, Tag = b };
            AutomationProperties.SetName(item, $"{b.Name}{(usual ? " (usual)" : "")}: {b.Description}" + (b.Source.Length > 0 ? $" [{b.Source}]" : ""));
            list.Items.Add(item);
        }
        var select = list.Items.OfType<ListBoxItem>().FirstOrDefault(i => i.Tag == keep)
                     ?? list.Items.OfType<ListBoxItem>().FirstOrDefault(i => ((ActorBehaviour)i.Tag).Id == usualId)
                     ?? list.Items.OfType<ListBoxItem>().FirstOrDefault();
        list.SelectedItem = select;
        if (select is not null) list.ScrollIntoView(select);
    }

    private void FillTexts()
    {
        var text = textFilter.Text.Trim();
        var keep = (texts.SelectedItem as ListBoxItem)?.Tag;
        texts.Items.Clear();
        foreach (var (id, line) in lines.Where(l => text.Length == 0 || l.Text.Contains(text, StringComparison.OrdinalIgnoreCase) || l.Id.ToString() == text))
        {
            var item = new ListBoxItem { Content = new TextBlock { Text = $"{id,5}  {line}", TextTrimming = TextTrimming.CharacterEllipsis }, Tag = id };
            AutomationProperties.SetName(item, $"{id}: {line}");
            texts.Items.Add(item);
        }
        texts.SelectedItem = texts.Items.OfType<ListBoxItem>().FirstOrDefault(i => Equals(i.Tag, keep)) ?? texts.Items.OfType<ListBoxItem>().FirstOrDefault();
    }

    private void Selected()
    {
        if ((list.SelectedItem as ListBoxItem)?.Tag is not ActorBehaviour b) { detail.Text = ""; textPanel.Visibility = Visibility.Collapsed; return; }
        detail.Text = $"{b.Name}: {b.Description}." + (b.Source.Length > 0 && b.Source != "template" ? $" From the game's {b.Source}{(b.Uses > 1 ? $", {b.Uses} actors in all" : "")}; what names its own scene (other actors, zones, track points, texts of another island, story flags) is remapped or left out." : "");
        textPanel.Visibility = b.NeedsText ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Accept()
    {
        if ((list.SelectedItem as ListBoxItem)?.Tag is not ActorBehaviour b) return;
        if (b.NeedsText)
        {
            if ((texts.SelectedItem as ListBoxItem)?.Tag is not int id)
            {
                MessageBox.Show(this, lines.Count == 0 ? "This scene's island has no dialogue texts to choose from." : "Choose the line it says.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            Text = id;
        }
        Chosen = b;
        DialogResult = true;
    }

    // The picker for actor `index` of `scene` (in `store`'s folder): every behaviour its kind can have; null when cancelled.
    public static (ActorBehaviour Behaviour, int? Text)? ForActor(Window owner, SceneStore store, SceneModel scene, int index, string? sceneLabel = null)
    {
        var actor = scene.Actors[index];
        if (actor.IsSprite || actor.Entity < 0)
        {
            MessageBox.Show(owner, "This actor is a sprite: behaviours are for 3D actors (a kind of actor's animations make them walk and attack).", "Apply a behaviour",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return null;
        }
        List<ActorBehaviour> all;
        ActorBehaviour usual;
        using (UiBusy.Cursor())
        {
            all = ActorBehaviours.For(store, scene, actor.Entity);
            usual = ActorBehaviours.DefaultFor(store, scene, actor.Entity);
        }
        var lines = ActorBehaviours.Texts(store.Directory, store.Game, scene.Island);
        var where = Maps.MapWorkspace.For(store.Directory) is { } map ? $"the map \"{map.Name}\"" : sceneLabel ?? "the scene";
        return Pick(owner, $"Behaviour of actor {index}", all, usual.Id, lines,
            $"Actor {index} of {where} (kind of actor {actor.Entity}). Its scripts and the settings they need (move, life points, collisions) are replaced; " +
            "its kind, body, place and facing stay. The game's own actors of its kind are listed with what they do; what their scripts name in their own scene " +
            "is remapped into this one or left out.");
    }

    // Opens the picker; null when cancelled.
    public static (ActorBehaviour Behaviour, int? Text)? Pick(Window owner, string title, IReadOnlyList<ActorBehaviour> behaviours, string? usualId,
        IReadOnlyList<(int Id, string Text)> lines, string? note = null)
    {
        var window = new BehaviourPickWindow(title, behaviours, usualId, lines, note) { Owner = owner };
        return window.ShowDialog() == true && window.Chosen is { } chosen ? (chosen, window.Text) : null;
    }
}

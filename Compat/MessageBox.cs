using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace LBAAssembler;

public enum MessageBoxButton { OK, OKCancel, YesNoCancel, YesNo }
public enum MessageBoxImage { None, Error, Question, Warning, Information, Hand = Error, Stop = Error, Exclamation = Warning, Asterisk = Information }
public enum MessageBoxResult { None, OK, Cancel, Yes, No }

// WPF's MessageBox, as a small themed Avalonia dialog. Modal and synchronous like the original (see DialogPump): the
// editor's flows are written straight through -- "ask, then act" -- and stay that way.
public static class MessageBox
{
    public static MessageBoxResult Show(string text) => Show(null, text, "", MessageBoxButton.OK, MessageBoxImage.None);
    public static MessageBoxResult Show(string text, string caption) => Show(null, text, caption, MessageBoxButton.OK, MessageBoxImage.None);
    public static MessageBoxResult Show(string text, string caption, MessageBoxButton button) => Show(null, text, caption, button, MessageBoxImage.None);
    public static MessageBoxResult Show(string text, string caption, MessageBoxButton button, MessageBoxImage image) => Show(null, text, caption, button, image);
    public static MessageBoxResult Show(Window? owner, string text) => Show(owner, text, "", MessageBoxButton.OK, MessageBoxImage.None);
    public static MessageBoxResult Show(Window? owner, string text, string caption) => Show(owner, text, caption, MessageBoxButton.OK, MessageBoxImage.None);
    public static MessageBoxResult Show(Window? owner, string text, string caption, MessageBoxButton button) => Show(owner, text, caption, button, MessageBoxImage.None);

    public static MessageBoxResult Show(Window? owner, string text, string caption, MessageBoxButton button, MessageBoxImage image)
    {
        owner ??= DialogPump.ActiveWindow;
        var result = MessageBoxResult.None;
        var window = new Window
        {
            Title = caption,
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.Parse("#E8F0FA")),
            MinWidth = 320, MaxWidth = 720,
        };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Margin = new Thickness(0, 18, 0, 0) };
        Button Add(string label, MessageBoxResult value, bool isDefault = false, bool isCancel = false)
        {
            var b = new Button { Content = label, MinWidth = 84, HorizontalContentAlignment = HorizontalAlignment.Center, IsDefault = isDefault, IsCancel = isCancel };
            b.Classes.Add(isDefault ? "PrimaryButton" : "QuietButton");
            b.Click += (_, _) => { result = value; window.Close(); };
            buttons.Children.Add(b);
            return b;
        }
        switch (button)
        {
            case MessageBoxButton.OK: Add("OK", MessageBoxResult.OK, isDefault: true, isCancel: true); break;
            case MessageBoxButton.OKCancel: Add("OK", MessageBoxResult.OK, isDefault: true); Add("Cancel", MessageBoxResult.Cancel, isCancel: true); break;
            case MessageBoxButton.YesNo: Add("Yes", MessageBoxResult.Yes, isDefault: true); Add("No", MessageBoxResult.No, isCancel: true); break;
            case MessageBoxButton.YesNoCancel: Add("Yes", MessageBoxResult.Yes, isDefault: true); Add("No", MessageBoxResult.No); Add("Cancel", MessageBoxResult.Cancel, isCancel: true); break;
        }
        var glyph = image switch
        {
            MessageBoxImage.Error => "✖",
            MessageBoxImage.Warning => "⚠",
            MessageBoxImage.Question => "?",
            MessageBoxImage.Information => "i",
            _ => "",
        };
        var glyphColor = image switch
        {
            MessageBoxImage.Error => "#A32C22",
            MessageBoxImage.Warning => "#A35A22",
            _ => "#1B6EC2",
        };
        var row = new DockPanel { LastChildFill = true };
        if (glyph.Length > 0)
        {
            var badge = new Border
            {
                Width = 36, Height = 36, CornerRadius = new CornerRadius(18), Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Top,
                Background = new SolidColorBrush(Color.Parse(glyphColor)),
                Child = new TextBlock { Text = glyph, Foreground = Brushes.White, FontSize = 18, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            };
            DockPanel.SetDock(badge, Dock.Left);
            row.Children.Add(badge);
        }
        row.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, MaxWidth = 560, VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Color.Parse("#10243E")) });
        var content = new StackPanel { Margin = new Thickness(22, 18, 22, 16) };
        content.Children.Add(row);
        content.Children.Add(buttons);
        window.Content = content;
        // Cancel / the [X] give the button that means "no change": Cancel where there is one, else No, else OK.
        var cancelResult = button switch { MessageBoxButton.OK => MessageBoxResult.OK, MessageBoxButton.YesNo => MessageBoxResult.No, _ => MessageBoxResult.Cancel };
        window.Closed += (_, _) => { if (result == MessageBoxResult.None) result = cancelResult; };
        DialogPump.ShowModal(window, owner);
        return result;
    }
}

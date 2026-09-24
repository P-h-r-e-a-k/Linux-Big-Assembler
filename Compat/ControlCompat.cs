using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace LBAAssembler;

// WPF names for Avalonia control members. ActualWidth/ActualHeight are Bounds; ToolTip is an attached property; the
// z-order is a plain property of the visual.
public static class ControlCompat
{
    extension(Layoutable element)
    {
        public double ActualWidth => element.Bounds.Width;
        public double ActualHeight => element.Bounds.Height;
    }

    extension(Control control)
    {
        public object? ToolTip
        {
            get => Avalonia.Controls.ToolTip.GetTip(control);
            set => Avalonia.Controls.ToolTip.SetTip(control, value);
        }
    }

    extension(InputElement element)
    {
        public bool IsMouseOver => element.IsPointerOver;
        public bool IsKeyboardFocused => element.IsFocused;
        public bool IsMouseCaptured => Mouse.Captured is not null && ReferenceEquals(Mouse.Captured, element);
        public void CaptureMouse() => Mouse.Capture(element);
        public void ReleaseMouseCapture() { if (element.IsMouseCaptured) Mouse.Capture(null); }
    }

    extension(Panel)
    {
        public static void SetZIndex(Control element, int value) => element.ZIndex = value;
        public static int GetZIndex(Control element) => element.ZIndex;
    }

    extension(ScrollViewer scroll)
    {
        public double VerticalOffset => scroll.Offset.Y;
        public double HorizontalOffset => scroll.Offset.X;
        public double ViewportWidth => scroll.Viewport.Width;
        public double ViewportHeight => scroll.Viewport.Height;
        public double ExtentWidth => scroll.Extent.Width;
        public double ExtentHeight => scroll.Extent.Height;
        public double ScrollableWidth => Math.Max(0, scroll.Extent.Width - scroll.Viewport.Width);
        public double ScrollableHeight => Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height);
        public void ScrollToVerticalOffset(double offset) => scroll.Offset = new Vector(scroll.Offset.X, Math.Max(0, offset));
        public void ScrollToHorizontalOffset(double offset) => scroll.Offset = new Vector(Math.Max(0, offset), scroll.Offset.Y);
        public void ScrollToTop() => scroll.Offset = new Vector(scroll.Offset.X, 0);
        public void ScrollToBottom() => scroll.ScrollToEnd();
    }

    extension(TextBox box)
    {
        public int SelectionLength
        {
            get => Math.Abs(box.SelectionEnd - box.SelectionStart);
            set => box.SelectionEnd = box.SelectionStart + value;
        }
        public void Select(int start, int length) { box.SelectionStart = start; box.SelectionEnd = start + length; box.CaretIndex = start + length; }
        public int LineCount => (box.Text ?? "").Split('\n').Length;
        public int GetCharacterIndexFromLineIndex(int line)
        {
            var text = box.Text ?? "";
            var index = 0;
            for (var l = 0; l < line; l++) { var next = text.IndexOf('\n', index); if (next < 0) return text.Length; index = next + 1; }
            return index;
        }
        public int GetLineIndexFromCharacterIndex(int index)
        {
            var text = box.Text ?? "";
            var line = 0;
            for (var i = 0; i < Math.Min(index, text.Length); i++) if (text[i] == '\n') line++;
            return line;
        }
        public int GetLineLength(int line)
        {
            var text = box.Text ?? "";
            var start = box.GetCharacterIndexFromLineIndex(line);
            var end = text.IndexOf('\n', start);
            return (end < 0 ? text.Length : end + 1) - start;
        }
        public string GetLineText(int line)
        {
            var text = box.Text ?? "";
            var start = box.GetCharacterIndexFromLineIndex(line);
            return text.Substring(start, box.GetLineLength(line));
        }
        public void ScrollToLine(int line)
        {
            // Put the caret there: the text box scrolls its caret into view on its own.
            var keepStart = box.SelectionStart; var keepEnd = box.SelectionEnd;
            box.CaretIndex = box.GetCharacterIndexFromLineIndex(line);
            box.SelectionStart = keepStart; box.SelectionEnd = keepEnd;
        }
        public void ScrollToHome() => box.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault()?.ScrollToHome();
        public void AppendText(string text) { box.Text = (box.Text ?? "") + text; box.CaretIndex = box.Text!.Length; }
        // Where the caret is, relative to the text box, for placing a popup under it (WPF's GetRectFromCharacterIndex).
        public Rect GetRectFromCharacterIndex(int index)
        {
            var presenter = box.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault();
            if (presenter is null) return new Rect(0, 0, 1, box.FontSize * 1.3);
            var rect = presenter.TextLayout.HitTestTextPosition(Math.Clamp(index, 0, (box.Text ?? "").Length));
            var origin = presenter.TranslatePoint(rect.TopLeft, box) ?? rect.TopLeft;
            return new Rect(origin, rect.Size);
        }
    }

    extension(ComboBox combo)
    {
        // The editable combo box's own text box (the template's PART_InputText), when it has one.
        public TextBox? EditableTextBox => combo.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
    }

    extension(ContextMenu menu)
    {
        public bool IsOpenMenu { get => menu.IsOpen; set { if (value) menu.Open(menu.PlacementTarget as Control); else menu.Close(); } }
    }
}

// WPF's freezable collections used by the shape properties.
public class PointCollection : AvaloniaList<Point>
{
    public PointCollection() { }
    public PointCollection(IEnumerable<Point> points) : base(points) { }
}

public class DoubleCollection : AvaloniaList<double>
{
    public DoubleCollection() { }
    public DoubleCollection(IEnumerable<double> values) : base(values) { }
}

public static class FontWeights
{
    public static readonly FontWeight Normal = FontWeight.Normal, Bold = FontWeight.Bold, SemiBold = FontWeight.SemiBold, Medium = FontWeight.Medium, Light = FontWeight.Light;
}

public static class FontStyles
{
    public static readonly FontStyle Normal = FontStyle.Normal, Italic = FontStyle.Italic, Oblique = FontStyle.Oblique;
}

// The editor's fonts. The Windows names come first so a Windows machine keeps its look; the generic names are what
// fontconfig resolves on Linux (Avalonia takes a comma-separated fallback list).
public static class UiFonts
{
    public static readonly FontFamily Text = new("Segoe UI, Inter, Noto Sans, DejaVu Sans, sans-serif");
    public static readonly FontFamily Mono = new("Consolas, DejaVu Sans Mono, Liberation Mono, Noto Sans Mono, monospace");
}

public static class CompatTransforms
{
    public static MatrixTransform Matrix(double m11, double m12, double m21, double m22, double offsetX, double offsetY)
        => new(new Matrix(m11, m12, m21, m22, offsetX, offsetY));
}

public static class RectCompat
{
    extension(Rect rect)
    {
        public bool IsEmpty => rect.Width <= 0 || rect.Height <= 0;
        public static Rect Empty => new(0, 0, 0, 0);
        public static Rect Intersect(Rect a, Rect b) => a.Intersect(b);
        public static Rect Union(Rect a, Rect b) => a.Union(b);
    }
}

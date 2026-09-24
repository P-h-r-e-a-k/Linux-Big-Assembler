using Avalonia;
using Avalonia.Media;

namespace LBAAssembler;

// WPF's Line has X1/Y1/X2/Y2; Avalonia's has StartPoint/EndPoint. This Line (which wins over Avalonia's for the editor's
// own code, being in its namespace) offers both, so the many `new Line { X1 = ..., Y1 = ... }` stay as they were.
public class Line : Avalonia.Controls.Shapes.Line
{
    public double X1 { get => StartPoint.X; set => StartPoint = new Point(value, StartPoint.Y); }
    public double Y1 { get => StartPoint.Y; set => StartPoint = new Point(StartPoint.X, value); }
    public double X2 { get => EndPoint.X; set => EndPoint = new Point(value, EndPoint.Y); }
    public double Y2 { get => EndPoint.Y; set => EndPoint = new Point(EndPoint.X, value); }
}


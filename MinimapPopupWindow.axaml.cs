using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace LBAAssembler;

// The minimap "popped out" into its own resizable window -- opened by double-clicking the docked minimap
// (MainWindow.xaml's own MinimapContent), closed like any other window (its own X, Alt+F4, or the owner
// closing). MainWindow keeps a single instance alive while open and pushes a fresh snapshot to it every time
// the docked minimap's own image or markers change (see MainWindow's own RefreshMinimapPopup), rather than
// this window rendering anything on its own -- it has no renderer/game-data access of its own, only what it's
// handed.
public partial class MinimapPopupWindow : Window
{
    // Raised with a click's position in the SAME pixel space as the bitmap last passed to SetImage (i.e. the
    // minimap's own full, unscrolled image) -- MainWindow reuses its own existing Minimap_MouseDown math on
    // this exactly as if the docked minimap itself had been clicked there.
    public event Action<Point>? ImageClicked;

    public MinimapPopupWindow(Window owner)
    {
        InitializeComponent();
        this.OwnerWindow = owner;
    }

    public void SetImage(IImage? source) => PopupImage.Source = source;

    // Sizes the window so its own content area matches the given aspect ratio (MainWindow's own
    // MinimapContent.ActualWidth/Height, i.e. the same box RefreshMinimapPopup snapshots into the image
    // this window shows) -- called once, right before the window is first shown. Without this the window
    // always opened at its fixed XAML default (520x520, square), and Stretch="Uniform" -- correct in itself
    // -- then letterboxed the image with black bars on whichever axis the content's own shape didn't happen
    // to match a square, which is most islands (few are square). Still just a starting point: the window
    // stays freely resizable (Avalonia's default, as it was under WPF's ResizeMode="CanResize"), so the user
    // can resize away from this afterward exactly as before.
    public void SizeToAspect(double contentWidth, double contentHeight)
    {
        if (contentWidth <= 0 || contentHeight <= 0) return;
        const double MaxDimension = 720, MinDimension = 320;
        var scale = MaxDimension / Math.Max(contentWidth, contentHeight);
        var targetWidth = contentWidth * scale;
        var targetHeight = contentHeight * scale;
        if (Math.Max(targetWidth, targetHeight) < MinDimension)
        {
            var upscale = MinDimension / Math.Max(targetWidth, targetHeight);
            targetWidth *= upscale; targetHeight *= upscale;
        }
        // PopupContent is what should actually match the aspect ratio; SizeToContent lets Avalonia work out
        // exactly how much window chrome (title bar, the Border's own Margin) that needs around it, rather
        // than this guessing at title-bar height itself.
        PopupContent.Width = targetWidth;
        PopupContent.Height = targetHeight;
        // Avalonia difference: WPF's SizeToContent simply overrode an explicit Window Width/Height, while
        // here the XAML's 520x520 would still constrain the measure -- clear them so the content's own size
        // is what the window is sized from. They stay cleared afterwards; the window keeps whatever size
        // this pass gave it, and resizing works from there as usual.
        Width = double.NaN;
        Height = double.NaN;
        SizeToContent = SizeToContent.WidthAndHeight;
        // One-shot: once this pass has actually resized the window (Loaded priority runs after the layout
        // pass SizeToContent needs), let PopupContent go back to filling whatever size the window is, so
        // the window staying resizable isn't fighting an explicit fixed content size on every later resize.
        Dispatcher.UIThread.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            SizeToContent = SizeToContent.Manual;
            PopupContent.Width = double.NaN;
            PopupContent.Height = double.NaN;
        });
    }

    private void PopupContent_MouseLeftButtonDown(object? sender, PointerPressedEventArgs e)
    { if (!e.IsLeft) return;
        if (PopupImage.Source is not Bitmap bitmap) return;
        // PopupImage (Stretch="Uniform") only fills part of PopupContent's own box when their aspect ratios
        // differ -- letterboxed on whichever axis has slack. Map the click back through that same fit (same
        // formula Image itself uses to decide where to actually draw the bitmap) to the source's own pixel
        // coordinates, not just scale blindly by the container size.
        var clickInContent = e.GetPosition(PopupContent);
        var containerWidth = PopupContent.ActualWidth;
        var containerHeight = PopupContent.ActualHeight;
        if (containerWidth <= 0 || containerHeight <= 0) return;
        var scale = Math.Min(containerWidth / bitmap.PixelWidth, containerHeight / bitmap.PixelHeight);
        if (scale <= 0) return;
        var drawnWidth = bitmap.PixelWidth * scale;
        var drawnHeight = bitmap.PixelHeight * scale;
        var originX = (containerWidth - drawnWidth) / 2;
        var originY = (containerHeight - drawnHeight) / 2;
        var px = (clickInContent.X - originX) / scale;
        var py = (clickInContent.Y - originY) / scale;
        if (px < 0 || py < 0 || px >= bitmap.PixelWidth || py >= bitmap.PixelHeight) return;
        ImageClicked?.Invoke(new Point(px, py));
    }
}

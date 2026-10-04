using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml.MarkupExtensions;

namespace LBAAssembler;

// WPF's FrameworkElement.SetResourceReference(property, key), which the themed windows call to bind a
// property to a theme brush from code rather than from XAML. Avalonia's equivalent of a {DynamicResource}
// set in code is binding the property to a DynamicResourceExtension, which is what this does, so the ~180
// call sites across the editor read the same on both toolkits (and a theme swap re-resolves them, which
// is the whole point of using a resource reference rather than assigning a brush).
public static class ResourceCompat
{
    extension(AvaloniaObject target)
    {
        public void SetResourceReference(AvaloniaProperty property, object key)
        {
            // WPF had one Background / Foreground / BorderBrush, declared high up the tree, for every control;
            // Avalonia registers its own on each of Border, Panel, TemplatedControl and TextBlock, and setting
            // one type's property on another type's object throws. The call sites name whichever type WPF had
            // them on, so the property is looked up again by name on the object actually being themed.
            var actual = AvaloniaPropertyRegistry.Instance.FindRegistered(target.GetType(), property.Name) ?? property;
            target[!actual] = new DynamicResourceExtension(key);
        }
    }

    // WPF's System.Windows.Controls.Control.{Background,Foreground,BorderBrush}Property, which about sixty of
    // the themed call sites name. Avalonia puts those on TemplatedControl (which derives from Control, the other
    // way round from WPF), so they are offered here under Control's name as well; SetResourceReference above
    // re-resolves them against whatever the target really is.
    extension(Control)
    {
        public static AvaloniaProperty BackgroundProperty => TemplatedControl.BackgroundProperty;
        public static AvaloniaProperty ForegroundProperty => TemplatedControl.ForegroundProperty;
        public static AvaloniaProperty BorderBrushProperty => TemplatedControl.BorderBrushProperty;
    }
}

using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using LBAAssembler;

namespace LbaBodyStudio;

// The 3D preview of Body Studio and Animation Studio: the renderer's picture (Renderer.Render, a FlatImage) converted to an
// Avalonia bitmap and drawn to fill the control, two status lines over it, and drag to turn the body.
public sealed class ModelView : Control
{
    public Generated? Model;
    public float Yaw;
    public bool Wire,Bones;
    public bool HeadOnly;
    // Overrides the body's own rest pose when set (e.g. AnimationStudioWindow's own posed-per-keyframe
    // preview, via Lba1Pose.World) -- null means "render the body's own neutral/modelled pose", the
    // original behaviour.
    public Vector3[]? Pose;
    Point? drag;
    Bitmap? current;   // the last frame's bitmap, kept until the next one is drawn (the drawing context uses it after Render returns)
    static readonly Typeface Face=new("sans-serif");
    static readonly IBrush Background=Brush(Renderer.ViewBackground),LightGray=Brush(Argb.Pack(211,211,211)),Silver=Brush(Argb.Pack(192,192,192));
    static IBrush Brush(uint argb)=>new SolidColorBrush(Color.FromUInt32(argb));
    static FormattedText Text(string text,IBrush brush)=>new(text,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,Face,12,brush);
    public ModelView(){ClipToBounds=true;}
    public void Invalidate()=>InvalidateVisual();
    protected override void OnSizeChanged(SizeChangedEventArgs e){base.OnSizeChanged(e);InvalidateVisual();}
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        int width=Math.Max(1,(int)Bounds.Width),height=Math.Max(1,(int)Bounds.Height);
        context.FillRectangle(Background,new Rect(0,0,Bounds.Width,Bounds.Height));
        if(Model==null){var hint=Text("Generate a body to preview it here",Silver);context.DrawText(hint,new Point((width-hint.Width)/2,(height-hint.Height)/2));return;}
        var image=Renderer.Render(Model.Body,Model.Palette,width,height,Yaw,Wire,Bones,HeadOnly,Pose,background:Renderer.ViewBackground,gridLine:Renderer.ViewGrid);
        var bitmap=BitmapFactory.FromBgra(image.Width,image.Height,image.Bgra);
        context.DrawImage(bitmap,new Rect(0,0,image.Width,image.Height));
        current?.Dispose();current=bitmap;
        context.DrawText(Text($"LBA{Model.Body.Game}  •  {Model.Body.Vertices.Count} points  •  {Model.Body.Faces.Count} polygons  •  {Model.Body.Bones.Count} bones",LightGray),new Point(16,16));
        context.DrawText(Text(Pose==null?"Drag to rotate  |  Neutral pose  |  Palette colours":"Drag to rotate  |  Animated pose  |  Palette colours",LightGray),new Point(16,height-32));
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e){base.OnPointerPressed(e);drag=e.GetPosition(this);e.Pointer.Capture(this);}
    protected override void OnPointerMoved(PointerEventArgs e){base.OnPointerMoved(e);if(drag is Point p){var q=e.GetPosition(this);Yaw+=(float)(q.X-p.X)*0.012f;drag=q;Invalidate();}}
    protected override void OnPointerReleased(PointerReleasedEventArgs e){base.OnPointerReleased(e);drag=null;e.Pointer.Capture(null);}
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e){base.OnDetachedFromVisualTree(e);current?.Dispose();current=null;}
}

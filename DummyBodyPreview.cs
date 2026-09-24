using System.IO;
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

// Renders Assets/DummyBody.lm2 -- a real LBA2 body payload, in the same raw
// format a BODY.HQR entry decodes to -- directly through the absorbed Body
// Studio code (LbaBodyStudio.Body/Renderer), entirely independent of the
// native game engine and BODY.HQR. Used only for ActorAttributesWindow's own
// preview panel while an actor is still a placeholder (RendererLibraryApi.
// IsActorPlaceholder) with no real body chosen yet: showing something here
// never requires installing anything into the user's actual game files, the
// way giving the actor this body for real (in the 3D world view) would.
internal static class DummyBodyPreview
{
    private static LbaBodyStudio.Body? cachedBody;
    private static uint[]? cachedPalette;   // packed ARGB, as Generator.Palette gives it
    private static bool loadFailed;

    // Forget any cached result (the game folders changed, so the palette source may have too).
    public static void Reset()
    {
        cachedBody = null;
        cachedPalette = null;
        cachedMarker = null;
        loadFailed = false;
    }

    private static bool TryLoad()
    {
        if (cachedBody is not null && cachedPalette is not null) return true;
        if (loadFailed) return false;
        try
        {
            // The body ships inside the exe. Its colours come from a game palette (RESS.HQR entry 0
            // is the same layout in both games), from whichever game folder is set.
            var folder = new[] { EditorSettings.Current.GameDirectory, EditorSettings.Current.Lba1Directory }
                .FirstOrDefault(f => !string.IsNullOrWhiteSpace(f) && File.Exists(Path.Combine(f, "RESS.HQR")));
            if (folder is null) return false; // no game folder yet: try again once one is set
            using var stream = typeof(DummyBodyPreview).Assembly.GetManifestResourceStream("DummyBody.lm2")
                ?? throw new FileNotFoundException("DummyBody.lm2 is not embedded.");
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            cachedBody = LbaBodyStudio.Body.Read(bytes.ToArray(), 2);
            cachedPalette = LbaBodyStudio.Generator.Palette(folder);
            return true;
        }
        catch
        {
            // The palette/body couldn't be read -- ActorAttributesWindow
            // falls back to its usual "no body to preview" message rather
            // than showing a broken image.
            loadFailed = true;
            return false;
        }
    }


    private static Bitmap? cachedMarker;
    private static double markerHeightUnits;

    // The dummy body on a transparent background, for marking where an invisible / body-less actor is in the main views (see MainWindow.AddDummyMarker). It is
    // rendered exactly as every other actor's body is (Lba1ActorImages: the same square frame, 80% high with its feet 90% down, the key background and the
    // renderer's ground grid line made transparent), so it can be drawn at the body's real size. Rendered once and cached.
    public static Bitmap? RenderMarker()
    {
        if (cachedMarker is not null) return cachedMarker;
        if (!TryLoad()) return null;
        var world = cachedBody!.World();
        markerHeightUnits = Math.Max(1, world.Max(v => v.Y) - world.Min(v => v.Y));
        var image = LbaBodyStudio.Renderer.Render(cachedBody!, cachedPalette!, Lba1.Lba1ActorImages.MarkerSize, Lba1.Lba1ActorImages.MarkerSize, 0.6f, wire: false);
        cachedMarker = Lba1.Lba1ActorImages.Transparent(image);
        return cachedMarker;
    }

    // Half the height, in view pixels at zoom 1, the dummy body has at its real size (the same scale as every other body marker: 15 pixels per 256 units, at
    // least 14 and at most 260 pixels tall); 16 while the body can't be loaded.
    public static int MarkerHalfHeight
    {
        get
        {
            RenderMarker();
            return markerHeightUnits <= 0 ? 16 : (int)(Math.Clamp(markerHeightUnits * 15 / 256, 14, 260) / 2);
        }
    }
    // The renderer's picture (a plain BGRA buffer) as an Avalonia bitmap.
    public static Bitmap? Render(int width, int height, float yawRadians)
    {
        if (!TryLoad() || width <= 0 || height <= 0) return null;
        var image = LbaBodyStudio.Renderer.Render(cachedBody!, cachedPalette!, width, height, yawRadians, wire: false);
        return BitmapFactory.FromBgra(image.Width, image.Height, image.Bgra);
    }
}

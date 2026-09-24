// TEMPORARY: stand-ins for the types being ported in parallel (Body Studio, the engine host, the audio players), with the
// agreed public surface, so the rest of the editor can be compiled meanwhile. Deleted once the real files land.
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using System.Diagnostics;

namespace LBAAssembler
{
    internal sealed class EmbeddedGameHost : NativeControlHost
    {
        public event Action? GameExited;
        public bool Running => false;
        public int ProcessId => 0;
        public (int Width, int Height) FitSize() => (640, 480);
        public Task<bool> AttachAsync(Process engine, int timeoutMilliseconds = 30000) { GameExited?.Invoke(); return Task.FromResult(false); }
        public void FocusGame() { }
        public void Stop() { }
    }

    internal static class DummyBodyPreview
    {
        public static void Reset() { }
        public static Bitmap? RenderMarker() => null;
        public static int MarkerHalfHeight => 16;
        public static Bitmap? Render(int width, int height, float yawRadians) => null;
    }

    internal static class BodyStudioLauncher { public static void Show(Window owner) { } }
    internal static class AnimationStudioLauncher { public static void Show(Window owner) { } }
}

namespace LBAAssembler.Lba1
{
    internal sealed class Lba1ActorImages
    {
        public sealed record Marker(Bitmap Image, double HeightUnits);
        internal const int MarkerSize = 128;
        public Lba1ActorImages(Lba1Game game) { }
        public Lba1ActorImages(byte[] rawPalette, Func<int, byte[]?> readBody, int version) { }
        public LbaBodyStudio.Body? Body(int bodyIndex) => null;
        public Marker? GetMarker(int bodyIndex) => null;
        public Bitmap? RenderPosed(int bodyIndex, IReadOnlyList<(int Type, double X, double Y, double Z)> bones, float yaw, (int ActorBeta, int AlphaLight, int BetaLight)? light = null) => null;
        public Bitmap? RenderPreview(int bodyIndex, int width, int height, float yaw, System.Numerics.Vector3[]? pose = null) => null;
    }
}

namespace LbaBodyStudio
{
    internal static class Generator { public static uint[] Palette(string folder) => new uint[256]; }
    internal static class Renderer
    {
        public const uint KeyBackground = 0xFF191E27, KeyGrid = 0xFF2C3440, ViewBackground = 0xFFE8F0FA, ViewGrid = 0xFFCBDDF0;
        public static FlatImage Render(Body model, uint[] palette, int width, int height, float yaw, bool wire, bool bones = false, bool headOnly = false, System.Numerics.Vector3[]? pose = null, object? shading = null, uint? background = null, uint? gridLine = null) => new(width, height);
    }
}

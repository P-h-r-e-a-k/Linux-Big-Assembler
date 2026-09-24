using Avalonia.Controls;

namespace LBAAssembler;

// Launches Animation Studio (BodyStudio/AnimationStudioWindow.cs) as its own top-level window, the
// exact same way BodyStudioLauncher.cs launches Body Studio's window -- see that file's own comment.
internal static class AnimationStudioLauncher
{
    private static LbaBodyStudio.AnimationStudioWindow? openWindow;

    public static void Show(Window owner)
    {
        if (openWindow is not null)
        {
            openWindow.WindowState = WindowState.Normal;
            openWindow.Activate();
            return;
        }

        openWindow = new LbaBodyStudio.AnimationStudioWindow();
        openWindow.Closed += (_, _) => openWindow = null;
        openWindow.Show(owner);
    }
}

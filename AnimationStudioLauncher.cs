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
            if (openWindow.WindowState == WindowState.Minimized) openWindow.WindowState = WindowState.Normal;
            openWindow.Activate();
            return;
        }

        openWindow = new LbaBodyStudio.AnimationStudioWindow();
        openWindow.Closed += (_, _) => openWindow = null;
        WindowLifecycle.Register(openWindow, "AnimationStudioWindow");
        // Avalonia takes the owner as an argument to Show() rather than through a settable Owner property.
        openWindow.Show(owner);
    }

    // Unlike BodyStudioLauncher.Close(), AnimationStudioWindow has no unsaved-changes prompt of its own
    // yet (Body Studio's was added separately) -- this closes it exactly as its own window-close button
    // already does today, with no new data-loss risk beyond that existing gap.
    public static void Close() => openWindow?.Close();
}

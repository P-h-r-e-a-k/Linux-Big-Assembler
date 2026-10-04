using Avalonia.Controls;

namespace LBAAssembler;

// Launches LBA Body Studio's window (BodyStudio/BodyStudioWindow.cs, absorbed from the standalone
// LbaBodyStudio project) as its own top-level window from inside this app. It is an ordinary
// Avalonia Window now, shown non-modally and owned by the window that launched it, matching this
// codebase's existing per-actor non-modal window pattern rather than leaving it floating unowned.
// One instance at a time (re-activates the existing one rather than opening a second) -- unchanged
// from before the port, not something the Interface Audit asked to change -- but now registered with
// WindowLifecycle like every other secondary window, closing the "different lifecycle... than every
// other window's per-instance position memory" half of that finding too.
internal static class BodyStudioLauncher
{
    private static LbaBodyStudio.BodyStudioWindow? openWindow;

    public static void Show(Window owner)
    {
        if (openWindow is not null)
        {
            if (openWindow.WindowState == WindowState.Minimized) openWindow.WindowState = WindowState.Normal;
            openWindow.Activate();
            return;
        }

        openWindow = new LbaBodyStudio.BodyStudioWindow();
        openWindow.Closed += (_, _) => openWindow = null;
        WindowLifecycle.Register(openWindow, "BodyStudioWindow");
        // Avalonia takes the owner as an argument to Show() rather than through a settable Owner property.
        openWindow.Show(owner);
    }

    // BodyStudioWindow.Closing already asks about unsaved changes; if the user cancels, the window (and
    // openWindow) simply stays as it is, same as clicking its own close button would.
    public static void Close() => openWindow?.Close();
}

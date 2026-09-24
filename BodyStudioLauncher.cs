using Avalonia.Controls;

namespace LBAAssembler;

// Launches LBA Body Studio's window (BodyStudio/BodyStudioWindow.cs, absorbed from the standalone
// LbaBodyStudio project) as its own top-level window from inside this app. It is an ordinary
// Avalonia Window now, shown non-modally and owned by the window that launched it, matching this
// codebase's existing per-actor non-modal window pattern rather than leaving it floating unowned.
// One instance at a time: launching again brings the open one to the front.
internal static class BodyStudioLauncher
{
    private static LbaBodyStudio.BodyStudioWindow? openWindow;

    public static void Show(Window owner)
    {
        if (openWindow is not null)
        {
            openWindow.WindowState = WindowState.Normal;
            openWindow.Activate();
            return;
        }

        openWindow = new LbaBodyStudio.BodyStudioWindow();
        openWindow.Closed += (_, _) => openWindow = null;
        openWindow.Show(owner);
    }
}

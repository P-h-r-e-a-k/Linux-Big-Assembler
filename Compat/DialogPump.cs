using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace LBAAssembler;

// WPF's ShowDialog() blocked the caller on a nested message loop until the dialog closed; Avalonia's ShowDialog is a
// Task. The editor's dialogs are used in the blocking style throughout ("if (dialog.ShowDialog() == true) ..."), so this
// runs the same nested loop (Dispatcher.PushFrame, exactly what WPF did underneath) until the Task completes.
public static class DialogPump
{
    public static IClassicDesktopStyleApplicationLifetime? Desktop => Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;

    // The window the user is working in (the active one, else the main window).
    public static Window? ActiveWindow => Desktop?.Windows.FirstOrDefault(w => w.IsActive) ?? Desktop?.MainWindow;

    public static T Wait<T>(Task<T> task)
    {
        if (!task.IsCompleted)
        {
            var frame = new DispatcherFrame();
            task.ContinueWith(_ => frame.Continue = false, TaskScheduler.FromCurrentSynchronizationContext());
            Dispatcher.UIThread.PushFrame(frame);
        }
        return task.GetAwaiter().GetResult();
    }

    public static void Wait(Task task)
    {
        if (!task.IsCompleted)
        {
            var frame = new DispatcherFrame();
            task.ContinueWith(_ => frame.Continue = false, TaskScheduler.FromCurrentSynchronizationContext());
            Dispatcher.UIThread.PushFrame(frame);
        }
        task.GetAwaiter().GetResult();
    }

    // Shows `window` modally over `owner` (the active window when null) and returns its Close(result) value.
    public static object? ShowModal(Window window, Window? owner)
    {
        owner ??= ActiveWindow;
        if (owner is null || ReferenceEquals(owner, window))
        {
            object? result = null;
            window.Closed += (_, _) => result = window.DialogResultValue;
            window.Show();
            var frame = new DispatcherFrame();
            window.Closed += (_, _) => frame.Continue = false;
            Dispatcher.UIThread.PushFrame(frame);
            return result;
        }
        return Wait(window.ShowDialog<object?>(owner));
    }
}

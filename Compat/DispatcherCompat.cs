using Avalonia;
using Avalonia.Threading;

namespace LBAAssembler;

// WPF put a Dispatcher on every control; Avalonia has the one UI thread dispatcher. BeginInvoke is WPF's name for
// Post (with the delegate last), kept so the many "run this after the current layout pass" calls stay as they were.
public static class DispatcherCompat
{
    extension(AvaloniaObject _)
    {
        public Dispatcher Dispatcher => Dispatcher.UIThread;
    }

    extension(Dispatcher dispatcher)
    {
        public void BeginInvoke(Delegate callback) => dispatcher.Post(() => callback.DynamicInvoke());
        public void BeginInvoke(Action callback) => dispatcher.Post(callback);
        public void BeginInvoke(DispatcherPriority priority, Delegate callback) => dispatcher.Post(() => callback.DynamicInvoke(), priority);
        public void BeginInvoke(DispatcherPriority priority, Action callback) => dispatcher.Post(callback, priority);
        // WPF's Dispatcher.Yield: let the queued work (a render, an input event) run before continuing.
        public static Task Yield(DispatcherPriority priority) => Dispatcher.UIThread.InvokeAsync(() => { }, priority).GetTask();
    }
}

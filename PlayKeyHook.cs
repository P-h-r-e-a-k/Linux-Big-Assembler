using System.Runtime.InteropServices;

namespace LBAAssembler;

// A key the editor takes while a scene plays, before the game sees it: the game's own window (another process's, embedded in the
// editor's, EmbeddedGameHost) has the keyboard, so the editor's own key handling never hears of it. A low-level keyboard hook, on while
// the play lasts: the key pressed while one of the editor's windows is the one in front calls `pressed` and goes no further (its release
// neither); anywhere else it is left alone -- one of the editor's windows or the game's own (`also`: its process; once the game has
// changed its size its window can be the one in front). F11: full screen and back (MainWindow.Play.cs) -- the engine's own F11, a debug
// build's benchmark, would have waited for a key with the game stopped.
// The hook runs on a thread of its own, its own message loop: Windows drops a low-level hook whose thread doesn't answer in time, and
// the editor's thread is busy for a moment as the game's window moves into the full-screen window and back -- on the editor's thread the
// first F11 worked and the hook was gone by the second.
internal sealed class PlayKeyHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13, WM_KEYDOWN = 0x100, WM_KEYUP = 0x101, WM_SYSKEYDOWN = 0x104, WM_SYSKEYUP = 0x105, WM_QUIT = 0x12;

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public int x, y; }
    private delegate IntPtr LowLevelKeyboardProc(int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, LowLevelKeyboardProc proc, IntPtr module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern int GetWindowThreadProcessId(IntPtr hwnd, out int processId);
    [DllImport("user32.dll")] private static extern int GetMessage(out MSG msg, IntPtr hwnd, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint thread, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);

    private readonly int key, also;
    private readonly Action pressed;
    private readonly LowLevelKeyboardProc proc;      // (kept: the hook calls it for as long as it is on)
    private readonly Thread thread;
    private volatile uint threadId;
    private IntPtr hook;
    private bool down;

    // `pressed` is called on the hook's own thread: it should only hand the work on (Dispatcher.BeginInvoke).
    public PlayKeyHook(int virtualKey, int alsoProcess, Action pressed)
    {
        key = virtualKey;
        also = alsoProcess;
        this.pressed = pressed;
        proc = Callback;
        var ready = new ManualResetEventSlim();
        thread = new Thread(() =>
        {
            threadId = GetCurrentThreadId();
            hook = SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(null), 0);
            if (hook == IntPtr.Zero) DebugLog.Log($"PlayKeyHook: no keyboard hook ({Marshal.GetLastWin32Error()}): key {virtualKey} goes to the game");
            ready.Set();
            while (GetMessage(out _, IntPtr.Zero, 0, 0) > 0) { }
            if (hook != IntPtr.Zero) UnhookWindowsHookEx(hook);
            hook = IntPtr.Zero;
        }) { IsBackground = true, Name = "PlayKeyHook" };
        thread.Start();
        ready.Wait(2000);
    }

    private IntPtr Callback(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && Marshal.ReadInt32(data) == key)
        {
            var m = message.ToInt32();
            // (its release ends the press wherever it is let go: let go as the full-screen window came to the front, it was the game's,
            // the press never ended, and every F11 after it was the first one held down)
            var released = m is WM_KEYUP or WM_SYSKEYUP;
            if (released) down = false;
            if (Ours())
            {
                if (m is WM_KEYDOWN or WM_SYSKEYDOWN)
                {
                    // (held down, it repeats: once a press)
                    if (!down) { down = true; DebugLog.Log($"PlayKeyHook: key {key}"); pressed(); }
                    return 1;
                }
                if (released) return 1;
            }
        }
        return CallNextHookEx(hook, code, message, data);
    }

    // Whether the window in front is one of the editor's (its main window, the game's full-screen one) or the game's own
    private bool Ours()
    {
        GetWindowThreadProcessId(GetForegroundWindow(), out var id);
        return id == Environment.ProcessId || also != 0 && id == also;
    }

    public void Dispose()
    {
        if (threadId != 0) PostThreadMessage(threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        threadId = 0;
    }
}

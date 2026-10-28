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
//
// The port: upstream's hook is Win32 to its bones, and it is kept as it is behind OperatingSystem.IsWindows() -- the same hook, the same
// thread, the same message loop. X11 has no low-level keyboard hook at all (a key goes to the window that holds the keyboard and no other
// client ever sees it), so Linux takes the key the way every hotkey daemon on X11 does: a passive grab of that one key on the root window,
// which hands it to us wherever it is pressed. Everything above still describes what happens; see "the Linux hook" below for how.
internal sealed class PlayKeyHook : IDisposable
{
    private static readonly bool IsWindows = OperatingSystem.IsWindows();

    // ---- Win32 (Windows) -----------------------------------------------------------------------------------------------------------

    private static class Win32
    {
        public const int WH_KEYBOARD_LL = 13, WM_KEYDOWN = 0x100, WM_KEYUP = 0x101, WM_SYSKEYDOWN = 0x104, WM_SYSKEYUP = 0x105, WM_QUIT = 0x12;
        public const int VK_F1 = 0x70, VK_F24 = 0x87;

        [StructLayout(LayoutKind.Sequential)]
        public struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public int x, y; }
        public delegate IntPtr LowLevelKeyboardProc(int code, IntPtr message, IntPtr data);
        [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr SetWindowsHookEx(int id, LowLevelKeyboardProc proc, IntPtr module, uint thread);
        [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr hwnd, out int processId);
        [DllImport("user32.dll")] public static extern int GetMessage(out MSG msg, IntPtr hwnd, uint min, uint max);
        [DllImport("user32.dll")] public static extern bool PostThreadMessage(uint thread, uint message, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string? name);
    }

    // ---- X11 (Linux) ---------------------------------------------------------------------------------------------------------------

    private static class X11
    {
        private const string Lib = "libX11.so.6";
        public const int KeyPress = 2, KeyRelease = 3;
        public const int GrabModeSync = 0, GrabModeAsync = 1;
        public const int AsyncKeyboard = 3, ReplayKeyboard = 5;   // XAllowEvents: "the key stops here" and "it was never grabbed"
        public const uint AnyModifier = 1 << 15;
        public const int PointerRoot = 1;                 // XGetInputFocus: no window holds the keyboard (no window manager, or one that follows the pointer)
        public const int BadAccess = 10;                  // XGrabKey's own error: another client has the key already
        public const int Success = 0;
        public const int KeysymF1 = 0xFFBE;               // XK_F1; XK_F1..XK_F24 run on from there, as VK_F1..VK_F24 do
        public const int EventSize = 192;                 // an XEvent is a union 24 machine words wide; only its head is read here

        [StructLayout(LayoutKind.Sequential)]
        public struct XErrorEvent
        {
            public int type; public IntPtr display; public nuint resourceid; public nuint serial;
            public byte error_code, request_code, minor_code;
        }
        public delegate int ErrorHandler(IntPtr display, ref XErrorEvent error);

        // The head of the XEvent union when the event is a key's (XKeyEvent). The server writes the whole union, so the buffer it is
        // read out of is EventSize wide; the fields past same_screen belong to other events and are never looked at.
        [StructLayout(LayoutKind.Sequential)]
        public struct XKeyEvent
        {
            public int type;
            public nuint serial;
            public int send_event;
            public IntPtr display, window, root, subwindow;
            public nuint time;
            public int x, y, x_root, y_root;
            public uint state, keycode;
            public int same_screen;
        }

        [DllImport(Lib)] public static extern int XInitThreads();
        [DllImport(Lib)] public static extern IntPtr XOpenDisplay(IntPtr name);
        [DllImport(Lib)] public static extern int XCloseDisplay(IntPtr display);
        [DllImport(Lib)] public static extern int XDefaultScreen(IntPtr display);
        [DllImport(Lib)] public static extern IntPtr XRootWindow(IntPtr display, int screen);
        [DllImport(Lib)] public static extern IntPtr XInternAtom(IntPtr display, string name, int onlyIfExists);
        [DllImport(Lib)] public static extern byte XKeysymToKeycode(IntPtr display, nuint keysym);
        [DllImport(Lib)] public static extern int XGrabKey(IntPtr display, int keycode, uint modifiers, IntPtr grabWindow, int ownerEvents, int pointerMode, int keyboardMode);
        [DllImport(Lib)] public static extern int XUngrabKey(IntPtr display, int keycode, uint modifiers, IntPtr grabWindow);
        [DllImport(Lib)] public static extern int XAllowEvents(IntPtr display, int eventMode, IntPtr time);
        [DllImport(Lib)] public static extern int XPending(IntPtr display);
        [DllImport(Lib)] public static extern int XNextEvent(IntPtr display, IntPtr eventReturn);
        [DllImport(Lib)] public static extern int XGetInputFocus(IntPtr display, out IntPtr window, out int revertTo);
        [DllImport(Lib)] public static extern int XQueryPointer(IntPtr display, IntPtr window, out IntPtr root, out IntPtr child, out int rootX, out int rootY, out int winX, out int winY, out uint mask);
        [DllImport(Lib)] public static extern int XQueryTree(IntPtr display, IntPtr window, out IntPtr root, out IntPtr parent, out IntPtr children, out uint count);
        [DllImport(Lib)] public static extern int XGetWindowProperty(IntPtr display, IntPtr window, IntPtr property, nint offset, nint length, int delete, IntPtr requestType,
                                                                     out IntPtr actualType, out int actualFormat, out nuint itemCount, out nuint bytesAfter, out IntPtr data);
        [DllImport(Lib)] public static extern int XFree(IntPtr data);
        [DllImport(Lib)] public static extern int XFlush(IntPtr display);
        [DllImport(Lib)] public static extern int XSync(IntPtr display, int discard);
        [DllImport(Lib)] public static extern IntPtr XSetErrorHandler(ErrorHandler? handler);
        [DllImport(Lib, EntryPoint = "XSetErrorHandler")] public static extern IntPtr XSetErrorHandlerPtr(IntPtr handler);
        [DllImport(Lib)] public static extern int XkbSetDetectableAutoRepeat(IntPtr display, int detectable, out int supported);
    }

    // ---- the hook ------------------------------------------------------------------------------------------------------------------

    private readonly int key, also;
    private readonly Action pressed;
    private readonly Win32.LowLevelKeyboardProc proc;      // (kept: the hook calls it for as long as it is on)
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
            if (IsWindows) RunWindows(ready);
            else RunX11(ready);
        }) { IsBackground = true, Name = "PlayKeyHook" };
        thread.Start();
        ready.Wait(2000);
    }

    private void RunWindows(ManualResetEventSlim ready)
    {
        threadId = Win32.GetCurrentThreadId();
        hook = Win32.SetWindowsHookEx(Win32.WH_KEYBOARD_LL, proc, Win32.GetModuleHandle(null), 0);
        if (hook == IntPtr.Zero) DebugLog.Log($"PlayKeyHook: no keyboard hook ({Marshal.GetLastWin32Error()}): key {key} goes to the game");
        ready.Set();
        while (Win32.GetMessage(out _, IntPtr.Zero, 0, 0) > 0) { }
        if (hook != IntPtr.Zero) Win32.UnhookWindowsHookEx(hook);
        hook = IntPtr.Zero;
    }

    private IntPtr Callback(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && Marshal.ReadInt32(data) == key)
        {
            var m = message.ToInt32();
            // (its release ends the press wherever it is let go: let go as the full-screen window came to the front, it was the game's,
            // the press never ended, and every F11 after it was the first one held down)
            var released = m is Win32.WM_KEYUP or Win32.WM_SYSKEYUP;
            if (released) down = false;
            if (Ours())
            {
                if (m is Win32.WM_KEYDOWN or Win32.WM_SYSKEYDOWN)
                {
                    // (held down, it repeats: once a press)
                    if (!down) { down = true; DebugLog.Log($"PlayKeyHook: key {key}"); pressed(); }
                    return 1;
                }
                if (released) return 1;
            }
        }
        return Win32.CallNextHookEx(hook, code, message, data);
    }

    // Whether the window in front is one of the editor's (its main window, the game's full-screen one) or the game's own
    private bool Ours()
    {
        if (!IsWindows) return OursX11();
        Win32.GetWindowThreadProcessId(Win32.GetForegroundWindow(), out var id);
        return id == Environment.ProcessId || also != 0 && id == also;
    }

    public void Dispose()
    {
        if (!IsWindows) { StopX11(); return; }
        if (threadId != 0) Win32.PostThreadMessage(threadId, Win32.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        threadId = 0;
    }

    // ---- the Linux hook: a passive grab of the one key on the root window ----------------------------------------------------------
    //
    // XGrabKey on the root window takes that one key for us wherever it is pressed, for as long as the play lasts, and the grab freezes
    // the keyboard (GrabModeSync) until the press is answered -- which is what lets both of Callback's answers be given here:
    // XAllowEvents(AsyncKeyboard) is its `return 1` (the key stops with us, nothing else sees it), XAllowEvents(ReplayKeyboard) is its
    // CallNextHookEx (the server sends the key on to whatever holds the keyboard as though it had never been grabbed), so a window that
    // is neither the editor's nor the game's gets its F11 exactly as it always did.
    //
    // On a connection of its own, on the thread upstream already gives the hook: the only X connection in this process that is reachable
    // is EmbeddedGameHost's private one, and a thread sitting on a connection waiting for a key cannot be sharing it with the UI thread.
    // Upstream's thread waits in GetMessage and is woken by a posted WM_QUIT; X has no thread message queue to post into, so this one
    // asks XPending every few milliseconds instead. That wait is also what a frozen keyboard costs a window that is not ours, which is
    // why it is kept short.
    private const int PollMilliseconds = 4;

    private IntPtr display, rootWindow, atomNetWmPid;
    private int keycode;
    private X11.ErrorHandler? errorHandler;                // kept referenced: Xlib holds the pointer for as long as it is the handler
    private IntPtr previousErrorHandler;
    private bool errorHandlerInstalled;
    private volatile bool grabRefused;
    private volatile bool stopping;

    private void RunX11(ManualResetEventSlim ready)
    {
        var grabbed = false;
        try { grabbed = GrabX11(); }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException)
        {
            DebugLog.Log($"PlayKeyHook: libX11 isn't usable ({error.Message}): key {key} goes to the game");
        }
        catch (Exception error)
        {
            DebugLog.Log($"PlayKeyHook: no key grab ({error.Message}): key {key} goes to the game");
        }
        ready.Set();
        if (!grabbed) { ReleaseX11(); return; }
        var buffer = Marshal.AllocHGlobal(X11.EventSize);
        try
        {
            while (!stopping)
            {
                if (X11.XPending(display) == 0) { Thread.Sleep(PollMilliseconds); continue; }
                X11.XNextEvent(display, buffer);
                var e = Marshal.PtrToStructure<X11.XKeyEvent>(buffer);
                // only the grab's own events are of any interest, and nothing else that arrives here has frozen the keyboard, so
                // nothing else is answered
                if ((e.type is X11.KeyPress or X11.KeyRelease) && (int)e.keycode == keycode) HandleX11(e.type == X11.KeyPress);
            }
        }
        catch (Exception error)
        {
            // the play goes on without the key rather than taking the editor with it, exactly as a hook Windows refused to install would
            DebugLog.Log($"PlayKeyHook: the key grab stopped ({error.Message}): key {key} goes to the game");
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
            ReleaseX11();
        }
    }

    private bool GrabX11()
    {
        var keysym = KeysymFor(key);
        if (keysym == 0) { DebugLog.Log($"PlayKeyHook: no X keysym for virtual key {key}: it goes to the game"); return false; }
        // XInitThreads before anything else: this connection is opened and used on the hook's thread while the editor's own thread is
        // in Avalonia's, and Xlib wants to know there is more than one thread in the process before either is touched.
        X11.XInitThreads();
        display = X11.XOpenDisplay(IntPtr.Zero);
        if (display == IntPtr.Zero) { DebugLog.Log($"PlayKeyHook: XOpenDisplay failed (no DISPLAY?): key {key} goes to the game"); return false; }
        // Xlib's default error handler ends the process on any protocol error, and one is expected right here: a client that already
        // holds this key makes XGrabKey BadAccess. EmbeddedGameHost puts a handler of its own in as soon as a game is embedded, but the
        // handler is one pointer for the whole process and nothing exposes whose it is, so this installs its own for as long as the hook
        // is on and puts back whatever was there when it goes. (An error of EmbeddedGameHost's own connection is logged under this name
        // meanwhile; both handlers only log.)
        errorHandler = OnXError;
        previousErrorHandler = X11.XSetErrorHandler(errorHandler);
        errorHandlerInstalled = true;
        rootWindow = X11.XRootWindow(display, X11.XDefaultScreen(display));
        atomNetWmPid = X11.XInternAtom(display, "_NET_WM_PID", 0);
        // Autorepeat: Windows repeats WM_KEYDOWN with no WM_KEYUP in between, so `down` stays set and a key held down stays the one
        // press Callback counts. X sends a KeyRelease before every repeated KeyPress unless the client asks it not to, and each of
        // those would clear `down` -- F11 held down would have gone full screen, back, and full screen again. Asked per connection,
        // so only this one's events change. (Unsupported, the key still works; holding it down repeats the toggle.)
        X11.XkbSetDetectableAutoRepeat(display, 1, out _);
        keycode = X11.XKeysymToKeycode(display, keysym);
        if (keycode == 0) { DebugLog.Log($"PlayKeyHook: the keyboard has no key for virtual key {key}: it goes to the game"); return false; }
        // AnyModifier: upstream's hook takes the key whatever is held down with it (WM_SYSKEYDOWN is the Alt one). The keyboard is
        // grabbed in sync mode -- that is what makes a replay possible -- and the pointer is left alone.
        X11.XGrabKey(display, keycode, X11.AnyModifier, rootWindow, 0, X11.GrabModeAsync, X11.GrabModeSync);
        X11.XSync(display, 0);                             // the grab's own error, if the key is already somebody else's, arrives here
        if (grabRefused) { DebugLog.Log($"PlayKeyHook: another program already has key {key}: it goes to the game"); return false; }
        DebugLog.Log($"PlayKeyHook: key {key} grabbed (keycode {keycode})");
        return true;
    }

    // Callback, with the two XAllowEvents answers in place of its return value.
    private void HandleX11(bool isDown)
    {
        // (its release ends the press wherever it is let go -- the grab keeps the key until it is let go, so it is heard of here even
        // when the window in front changed under it)
        if (!isDown) down = false;
        if (Ours())
        {
            // the key stops here. The keyboard is thawed first: `pressed` only hands the work to the UI thread, but nothing should be
            // typed anywhere while it does.
            X11.XAllowEvents(display, X11.AsyncKeyboard, IntPtr.Zero);
            X11.XFlush(display);
            // (held down, it repeats: once a press)
            if (isDown && !down) { down = true; DebugLog.Log($"PlayKeyHook: key {key}"); pressed(); }
            return;
        }
        X11.XAllowEvents(display, X11.ReplayKeyboard, IntPtr.Zero);
        X11.XFlush(display);
    }

    // Whether the window that holds the keyboard is one of the editor's or the game's. X11 has no window in front; what there is, is the
    // window the keyboard's events go to, and since that can be a window the client made for itself (SDL's own, the black child the game
    // sits in), its ancestors are walked until one says whose it is. _NET_WM_PID is what says so: SDL3 sets it on every window it makes
    // (EmbeddedGameHost finds the game's window by it) and Avalonia sets it on every top level of ours. PointerRoot means no window holds
    // the keyboard at all -- no window manager, or one that gives it to whatever the pointer is over -- so the window under the pointer
    // is the one that would have had the key.
    private bool OursX11()
    {
        if (display == IntPtr.Zero) return false;
        X11.XGetInputFocus(display, out var focus, out _);
        if (focus == IntPtr.Zero || focus.ToInt64() == X11.PointerRoot)
            X11.XQueryPointer(display, rootWindow, out _, out focus, out _, out _, out _, out _, out _);
        for (var window = focus; window != IntPtr.Zero && window != rootWindow;)
        {
            var id = WindowPid(window);
            if (id > 0) return id == Environment.ProcessId || also != 0 && id == also;
            if (X11.XQueryTree(display, window, out _, out var parent, out var children, out _) == 0) return false;
            if (children != IntPtr.Zero) X11.XFree(children);
            window = parent;
        }
        return false;
    }

    private int WindowPid(IntPtr window)
    {
        if (X11.XGetWindowProperty(display, window, atomNetWmPid, 0, 1, 0, IntPtr.Zero /* AnyPropertyType */, out _, out var format, out var items, out _, out var data) != X11.Success) return -1;
        try { return data != IntPtr.Zero && format == 32 && items >= 1 ? (int)Marshal.ReadIntPtr(data) : -1; }
        finally { if (data != IntPtr.Zero) X11.XFree(data); }
    }

    // The virtual key upstream's hook is given, as the X keysym of the same key. Only the function keys are here: F11 (0x7A) is the one
    // the editor takes while a scene plays, and a key with nothing to map to gets no grab at all rather than a grab of the wrong key.
    private static nuint KeysymFor(int virtualKey)
        => virtualKey is >= Win32.VK_F1 and <= Win32.VK_F24 ? (nuint)(X11.KeysymF1 + (virtualKey - Win32.VK_F1)) : (nuint)0;

    private int otherConnectionErrors;

    private int OnXError(IntPtr errorDisplay, ref X11.XErrorEvent error)
    {
        var ours = errorDisplay == display;
        if (ours && error.error_code == X11.BadAccess) grabRefused = true;
        // errors of another connection (EmbeddedGameHost's, whose handler this one stands in for while the hook is on) are noted a few
        // times and then dropped, the same rule that handler keeps for connections that aren't its own
        if (!ours && ++otherConnectionErrors > 5) return 0;
        DebugLog.Log($"PlayKeyHook: X error {error.error_code} on {(ours ? "our" : "another")} connection (request {error.request_code}.{error.minor_code}, resource 0x{error.resourceid:x})");
        return 0;
    }

    private void StopX11()
    {
        // Waited for, where Windows' hook is only asked to stop: the grab belongs to this connection and no other, so the next play's
        // PlayKeyHook -- a connection of its own, another client as far as the server is concerned -- would be refused the key until
        // this one has actually let go of it. The wait is a poll interval, since `pressed` only posts to the UI thread and never
        // waits on it.
        stopping = true;
        if (!thread.Join(1000)) DebugLog.Log($"PlayKeyHook: the grab of key {key} didn't let go");
    }

    private void ReleaseX11()
    {
        if (display != IntPtr.Zero)
        {
            if (keycode != 0) X11.XUngrabKey(display, keycode, X11.AnyModifier, rootWindow);
            // whatever state the grab was left in, the keyboard is thawed: a frozen keyboard with nobody left to answer for it is a
            // keyboard that has stopped working for the whole screen
            X11.XAllowEvents(display, X11.AsyncKeyboard, IntPtr.Zero);
            X11.XCloseDisplay(display);
            display = IntPtr.Zero;
        }
        // back to whatever was there before (EmbeddedGameHost's, or Xlib's own when nothing had one yet)
        if (errorHandlerInstalled) { X11.XSetErrorHandlerPtr(previousErrorHandler); errorHandlerInstalled = false; previousErrorHandler = IntPtr.Zero; }
    }
}

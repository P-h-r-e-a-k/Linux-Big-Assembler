using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Threading;

namespace LBAAssembler;

// Shows the LBA2 engine (lba2cc, a separate process with its own SDL window) inside the main window: the engine's window is
// taken over as a child of this control's own native window, sized to the largest 4:3 rectangle that fits, so playing a scene
// happens in the editor's view and not in a window of its own. Keyboard focus goes to the engine window when it is clicked,
// like it would to a child control; the game keeps its own input handling.
//
// Avalonia's NativeControlHost hands this control a native window of the platform (an X11 window on Linux, an HWND on
// Windows) that follows the control's bounds; a black child window of ours is created in it, and the engine's window is
// re-parented into that child:
//
//   Linux (X11, through libX11 directly -- Avalonia's own X11 backend is not public API): the engine's SDL window is a
//   top-level window with _NET_WM_PID set to the engine's process id (SDL3 sets it on every window). AttachAsync polls the
//   window tree for it, unmaps it, XReparentWindow's it under the host child, sizes it and maps it again. A window that is a
//   child of another client's window is not managed by the window manager, so it has no frame and can't be moved by the
//   user, exactly like a WS_CHILD window on Windows. Note that under a window manager the SDL window may be seen for an
//   instant before it is taken over (see Lba2Play.Launch for how the engine is asked to create it off screen). Wayland is not
//   supported: there is no foreign-window re-parenting there; run the editor under X11/XWayland (Avalonia does that by default).
//
//   Windows: the original WPF HwndHost approach, kept behind OperatingSystem.IsWindows(): the SDL window is found by process
//   id, its style is cut down to WS_CHILD and it is SetParent'ed into the host child.
internal sealed class EmbeddedGameHost : NativeControlHost
{
    // ---- X11 (Linux) ---------------------------------------------------------------------------------------------------------------

    private static class X11
    {
        private const string Lib = "libX11.so.6";
        public const int RevertToParent = 2;
        public const uint Button1Mask = 1 << 8;
        public const int Success = 0;

        [StructLayout(LayoutKind.Sequential)]
        public struct XErrorEvent
        {
            public int type; public IntPtr display; public nuint resourceid; public nuint serial;
            public byte error_code, request_code, minor_code;
        }
        public delegate int ErrorHandler(IntPtr display, ref XErrorEvent error);

        [DllImport(Lib)] public static extern int XInitThreads();
        [DllImport(Lib)] public static extern IntPtr XOpenDisplay(IntPtr name);
        [DllImport(Lib)] public static extern int XDefaultScreen(IntPtr display);
        [DllImport(Lib)] public static extern IntPtr XRootWindow(IntPtr display, int screen);
        [DllImport(Lib)] public static extern nuint XBlackPixel(IntPtr display, int screen);
        [DllImport(Lib)] public static extern IntPtr XCreateSimpleWindow(IntPtr display, IntPtr parent, int x, int y, uint width, uint height, uint borderWidth, nuint border, nuint background);
        [DllImport(Lib)] public static extern int XDestroyWindow(IntPtr display, IntPtr window);
        [DllImport(Lib)] public static extern int XMapWindow(IntPtr display, IntPtr window);
        [DllImport(Lib)] public static extern int XUnmapWindow(IntPtr display, IntPtr window);
        [DllImport(Lib)] public static extern int XReparentWindow(IntPtr display, IntPtr window, IntPtr parent, int x, int y);
        [DllImport(Lib)] public static extern int XMoveResizeWindow(IntPtr display, IntPtr window, int x, int y, uint width, uint height);
        [DllImport(Lib)] public static extern int XSetInputFocus(IntPtr display, IntPtr window, int revertTo, IntPtr time);
        [DllImport(Lib)] public static extern int XGetInputFocus(IntPtr display, out IntPtr window, out int revertTo);
        [DllImport(Lib)] public static extern int XQueryTree(IntPtr display, IntPtr window, out IntPtr root, out IntPtr parent, out IntPtr children, out uint count);
        [DllImport(Lib)] public static extern int XFree(IntPtr data);
        [DllImport(Lib)] public static extern IntPtr XInternAtom(IntPtr display, string name, int onlyIfExists);
        [DllImport(Lib)] public static extern int XGetWindowProperty(IntPtr display, IntPtr window, IntPtr property, nint offset, nint length, int delete, IntPtr requestType,
                                                                     out IntPtr actualType, out int actualFormat, out nuint itemCount, out nuint bytesAfter, out IntPtr data);
        [DllImport(Lib)] public static extern int XQueryPointer(IntPtr display, IntPtr window, out IntPtr root, out IntPtr child, out int rootX, out int rootY, out int winX, out int winY, out uint mask);
        [DllImport(Lib)] public static extern int XFlush(IntPtr display);
        [DllImport(Lib)] public static extern int XSync(IntPtr display, int discard);
        [DllImport(Lib)] public static extern IntPtr XSetErrorHandler(ErrorHandler? handler);
        [DllImport(Lib)] public static extern int XGetGeometry(IntPtr display, IntPtr drawable, out IntPtr root, out int x, out int y, out uint width, out uint height, out uint borderWidth, out uint depth);
    }

    // One connection of our own, opened on first use and kept for the life of the process (a Display can't be shared with
    // Avalonia's own one, which is not exposed). XInitThreads first: AttachAsync polls from a worker thread while the UI
    // thread uses the same connection.
    private static IntPtr display;
    private static IntPtr rootWindow;
    private static IntPtr atomNetWmPid;
    private static bool displayTried;
    private static X11.ErrorHandler? errorHandler;        // kept referenced: Xlib holds the pointer for good

    private static IntPtr Display
    {
        get
        {
            if (display != IntPtr.Zero || displayTried) return display;
            displayTried = true;
            try
            {
                X11.XInitThreads();
                display = X11.XOpenDisplay(IntPtr.Zero);
                if (display == IntPtr.Zero) { DebugLog.Log("EmbeddedGameHost: XOpenDisplay failed (no DISPLAY?)"); return display; }
                rootWindow = X11.XRootWindow(display, X11.XDefaultScreen(display));
                atomNetWmPid = X11.XInternAtom(display, "_NET_WM_PID", 0);
                // Xlib's default error handler ends the process on any protocol error, and errors are expected here (the game's
                // window can vanish between finding it and re-parenting it: BadWindow; focusing a not yet viewable window:
                // BadMatch). The handler is global to the process, so ours replaces Avalonia's own (which only writes the error
                // to its log) and logs errors of every connection. It can't be chained: Xlib hands back Avalonia's managed
                // delegate thunk, which .NET refuses to re-marshal as a delegate of another type.
                errorHandler = OnXError;
                X11.XSetErrorHandler(errorHandler);
            }
            catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException)
            {
                DebugLog.Log($"EmbeddedGameHost: libX11 isn't usable: {error.Message}");
                display = IntPtr.Zero;
            }
            return display;
        }
    }

    private static int otherConnectionErrors;

    private static int OnXError(IntPtr errorDisplay, ref X11.XErrorEvent error)
    {
        // errors of Avalonia's own connection (it raises a few BadAtom ones at start-up) are noted a few times, then dropped
        if (errorDisplay != display && ++otherConnectionErrors > 5) return 0;
        DebugLog.Log($"EmbeddedGameHost: X error {error.error_code} on {(errorDisplay == display ? "our" : "another")} connection (request {error.request_code}.{error.minor_code}, resource 0x{error.resourceid:x})");
        return 0;
    }

    // The engine's top-level window: one whose _NET_WM_PID is the engine's process id, and that isn't in `except` (a window taken
    // over already). Frames of a window manager sit between the root and the client window, so the tree is walked a few levels
    // deep (never as deep as a window already re-parented under the host). Zero when there is none.
    private static IntPtr FindEngineWindowX11(int processId, HashSet<IntPtr> except)
    {
        var d = Display;
        if (d == IntPtr.Zero) return IntPtr.Zero;
        return Walk(rootWindow, 0);

        IntPtr Walk(IntPtr window, int depth)
        {
            if (X11.XQueryTree(d, window, out _, out _, out var children, out var count) == 0 || children == IntPtr.Zero) return IntPtr.Zero;
            try
            {
                for (var i = 0; i < (int)count; i++)
                {
                    var child = Marshal.ReadIntPtr(children, i * IntPtr.Size);
                    if (!except.Contains(child) && WindowPid(d, child) == processId) return child;
                    if (depth < 2)
                    {
                        var below = Walk(child, depth + 1);
                        if (below != IntPtr.Zero) return below;
                    }
                }
            }
            finally { X11.XFree(children); }
            return IntPtr.Zero;
        }
    }

    private static int WindowPid(IntPtr d, IntPtr window)
    {
        if (X11.XGetWindowProperty(d, window, atomNetWmPid, 0, 1, 0, IntPtr.Zero /* AnyPropertyType */, out _, out var format, out var items, out _, out var data) != X11.Success) return -1;
        try { return data != IntPtr.Zero && format == 32 && items >= 1 ? (int)Marshal.ReadIntPtr(data) : -1; }
        finally { if (data != IntPtr.Zero) X11.XFree(data); }
    }

    // ---- Win32 (Windows) -----------------------------------------------------------------------------------------------------------

    private static class Win32
    {
        public const int WS_CHILD = 0x40000000, WS_VISIBLE = 0x10000000, WS_CLIPCHILDREN = 0x02000000, WS_CLIPSIBLINGS = 0x04000000;
        public const int WS_POPUP = unchecked((int)0x80000000), WS_CAPTION = 0x00C00000, WS_THICKFRAME = 0x00040000, WS_SYSMENU = 0x00080000;
        public const int WS_MINIMIZEBOX = 0x00020000, WS_MAXIMIZEBOX = 0x00010000, WS_BORDER = 0x00800000, WS_DLGFRAME = 0x00400000;
        public const int WS_EX_APPWINDOW = 0x00040000, WS_EX_WINDOWEDGE = 0x00000100, WS_EX_CLIENTEDGE = 0x00000200, WS_EX_DLGMODALFRAME = 0x00000001, WS_EX_TOOLWINDOW = 0x00000080;
        public const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
        public const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOZORDER = 0x4, SWP_FRAMECHANGED = 0x20, SWP_NOACTIVATE = 0x10;
        public const int SW_SHOW = 5, SW_HIDE = 0;
        public const int VK_LBUTTON = 1;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr CreateWindowEx(int exStyle, string className, string windowName, int style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);
        [DllImport("user32.dll")] public static extern bool DestroyWindow(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern IntPtr SetParent(IntPtr child, IntPtr parent);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] public static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
        [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr hwnd, int x, int y, int width, int height, bool repaint);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hwnd, out RECT rect);
        [DllImport("user32.dll")] public static extern IntPtr SetFocus(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern IntPtr GetFocus();
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int command);
        [DllImport("user32.dll")] public static extern IntPtr GetParent(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr param);
        [DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr hwnd, out int processId);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hwnd, StringBuilder text, int count);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT point);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
        public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr param);
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern ushort RegisterClassW(ref WNDCLASS windowClass);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string? name);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)] public static extern IntPtr GetProcAddress(IntPtr module, string name);
        [DllImport("gdi32.dll")] public static extern IntPtr GetStockObject(int index);
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WNDCLASS
        {
            public uint style; public IntPtr lpfnWndProc; public int cbClsExtra, cbWndExtra; public IntPtr hInstance, hIcon, hCursor, hbrBackground;
            public string? lpszMenuName; public string lpszClassName;
        }

        // A window class that paints itself black (the game's 4:3 picture sits in the middle of the control with black around it).
        public static string HostClass()
        {
            const string name = "LbaEditorGameHost";
            if (!classRegistered)
            {
                var windowClass = new WNDCLASS
                {
                    lpfnWndProc = GetProcAddress(GetModuleHandle("user32.dll"), "DefWindowProcW"),
                    hInstance = GetModuleHandle(null), hbrBackground = GetStockObject(4 /* BLACK_BRUSH */), lpszClassName = name,
                };
                RegisterClassW(ref windowClass);
                classRegistered = true;
            }
            return name;
        }
        private static bool classRegistered;

        public static IntPtr FindEngineWindow(int processId)
        {
            IntPtr best = IntPtr.Zero;
            EnumWindows((hwnd, _) =>
            {
                GetWindowThreadProcessId(hwnd, out var pid);
                if (pid != processId || GetParent(hwnd) != IntPtr.Zero) return true;      // (it may still be hidden: it is shown once embedded)
                var name = new StringBuilder(64);
                GetClassName(hwnd, name, name.Capacity);
                // the engine's window is an SDL one; any other visible top-level window of the process (a console, an error box) is not it
                if (name.ToString().StartsWith("SDL", StringComparison.OrdinalIgnoreCase)) { best = hwnd; return false; }
                return true;
            }, IntPtr.Zero);
            return best;
        }
    }

    // ---- the control ---------------------------------------------------------------------------------------------------------------

    private static readonly bool IsWindows = OperatingSystem.IsWindows();

    // How long AttachAsync keeps watching for a replacement window after it took one over. SDL3 destroys and re-creates its
    // window (at the same place, as a top-level one again) when SDL_CreateRenderer wants a visual the window wasn't created
    // with -- the OpenGL renderer on X11 does, and the engine creates its window without asking for OpenGL -- so the first
    // window found is often not the one the game ends up drawing in. Every replacement is taken over the same way, within a
    // couple of milliseconds of appearing.
    private const int SettleMilliseconds = 1500;

    private IntPtr host;                                   // our black child window (an X window / an HWND) inside the native window Avalonia gives us
    private IntPtr game;                                   // the engine's window once it has been re-parented into host
    private Process? process;
    private readonly HashSet<IntPtr> captured = new();     // every engine window taken over so far (a replaced one may not have been destroyed yet)
    private (int X, int Y, int W, int H) gameRect;         // where the game window sits inside host, in device pixels
    private readonly DispatcherTimer focusTimer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(120) };

    public event Action? GameExited;

    public bool Running => process is { HasExited: false } && game != IntPtr.Zero;
    public int ProcessId => process?.Id ?? 0;

    public EmbeddedGameHost()
    {
        focusTimer.Tick += FocusTick;
    }

    // The size in device pixels the game should be launched at: the largest 4:3 rectangle that fits this control.
    public (int Width, int Height) FitSize()
    {
        var (w, h) = DevicePixelSize();
        w = Math.Max(320, w);
        h = Math.Max(240, h);
        var fitW = Math.Min(w, h * 4 / 3);
        fitW -= fitW % 2;
        return (Math.Max(320, fitW), Math.Max(240, fitW * 3 / 4));
    }

    private (int Width, int Height) DevicePixelSize()
    {
        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        return ((int)Math.Round(Bounds.Width * scale), (int)Math.Round(Bounds.Height * scale));
    }

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        if (IsWindows)
        {
            host = Win32.CreateWindowEx(0, Win32.HostClass(), "", Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_CLIPCHILDREN | Win32.WS_CLIPSIBLINGS, 0, 0, 100, 100, parent.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            return new PlatformHandle(host, "HWND");
        }
        var d = Display;
        if (d == IntPtr.Zero || !string.Equals(parent.HandleDescriptor, "XID", StringComparison.Ordinal))
        {
            // not X11 (Wayland, or no display): nothing can be embedded; Avalonia's own default child keeps the layout intact
            DebugLog.Log($"EmbeddedGameHost: no X11 display to embed into (parent handle is '{parent.HandleDescriptor}')");
            return base.CreateNativeControlCore(parent);
        }
        var (w, h) = DevicePixelSize();
        var screen = X11.XDefaultScreen(d);
        var black = X11.XBlackPixel(d, screen);
        host = X11.XCreateSimpleWindow(d, parent.Handle, 0, 0, (uint)Math.Max(1, w), (uint)Math.Max(1, h), 0, black, black);
        X11.XMapWindow(d, host);
        X11.XFlush(d);
        return new PlatformHandle(host, "XID");
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        Stop();
        if (control.Handle == host && host != IntPtr.Zero)
        {
            if (IsWindows) Win32.DestroyWindow(host);
            else if (Display != IntPtr.Zero) { X11.XDestroyWindow(Display, host); X11.XFlush(Display); }
            host = IntPtr.Zero;
        }
        else base.DestroyNativeControlCore(control);
    }

    // Takes over the window of an engine process that was just started: waits for it to appear, then embeds it. False when it never
    // did (or the engine ended first). The wait is a tight loop on its own thread and the window is hidden and re-parented the
    // moment it exists, so it is (all but) never seen as a window of its own; the game is visible in the control as soon as its
    // first window has been taken over, while the loop keeps watching for a replacement window (see SettleMilliseconds) before
    // this returns.
    public async Task<bool> AttachAsync(Process engine, int timeoutMilliseconds = 30000)
    {
        process = engine;
        captured.Clear();
        engine.EnableRaisingEvents = true;
        engine.Exited += (_, _) => Dispatcher.UIThread.Post(() => { if (ReferenceEquals(process, engine)) { Release(); GameExited?.Invoke(); } });
        var parent = host;
        if (parent == IntPtr.Zero) { DebugLog.Log("EmbeddedGameHost: AttachAsync without a native host window (is the control in a shown window?)"); return false; }
        var seen = new HashSet<IntPtr>();
        IntPtr found;
        try
        {
            found = await Task.Run(() =>
            {
                var clock = Stopwatch.StartNew();
                IntPtr last = IntPtr.Zero;
                long lastAt = 0;
                while (clock.ElapsedMilliseconds < timeoutMilliseconds && !engine.HasExited)
                {
                    var window = FindEngineWindow(engine.Id, seen);
                    if (window != IntPtr.Zero)
                    {
                        seen.Add(window);
                        Capture(window, parent);
                        last = window;
                        lastAt = clock.ElapsedMilliseconds;
                        Dispatcher.UIThread.Post(() => Adopt(engine, window));
                    }
                    else if (last != IntPtr.Zero && clock.ElapsedMilliseconds - lastAt >= SettleMilliseconds) break;
                    Thread.Sleep(2);
                }
                return last;
            });
        }
        catch (Exception error)
        {
            DebugLog.Log($"EmbeddedGameHost: attaching to the engine's window failed: {error}");
            return false;
        }
        return found != IntPtr.Zero && !engine.HasExited && ReferenceEquals(process, engine) && game != IntPtr.Zero;
    }

    private IntPtr FindEngineWindow(int processId, HashSet<IntPtr> except)
        => IsWindows ? Win32.FindEngineWindow(processId) : FindEngineWindowX11(processId, except);

    // Any thread: the window is taken off the screen and made a child of ours in one go. X11: unmapping first withdraws it from the
    // window manager (which otherwise keeps a frame around it); XSync makes sure the manager has seen that before the re-parent.
    // Windows: hidden, its style cut down to a child window's, and SetParent'ed.
    private static void Capture(IntPtr window, IntPtr parent)
    {
        if (IsWindows)
        {
            Win32.ShowWindow(window, Win32.SW_HIDE);
            var style = Win32.GetWindowLongPtr(window, Win32.GWL_STYLE).ToInt64();
            style &= ~(long)(Win32.WS_POPUP | Win32.WS_CAPTION | Win32.WS_THICKFRAME | Win32.WS_SYSMENU | Win32.WS_MINIMIZEBOX | Win32.WS_MAXIMIZEBOX | Win32.WS_BORDER | Win32.WS_DLGFRAME);
            style |= Win32.WS_CHILD | Win32.WS_CLIPSIBLINGS;
            Win32.SetWindowLongPtr(window, Win32.GWL_STYLE, new IntPtr(style));
            var ex = Win32.GetWindowLongPtr(window, Win32.GWL_EXSTYLE).ToInt64();
            ex &= ~(long)(Win32.WS_EX_APPWINDOW | Win32.WS_EX_WINDOWEDGE | Win32.WS_EX_CLIENTEDGE | Win32.WS_EX_DLGMODALFRAME | Win32.WS_EX_TOOLWINDOW);
            Win32.SetWindowLongPtr(window, Win32.GWL_EXSTYLE, new IntPtr(ex));
            Win32.SetParent(window, parent);
            return;
        }
        var d = Display;
        X11.XUnmapWindow(d, window);
        X11.XSync(d, 0);
        X11.XReparentWindow(d, window, parent, 0, 0);
        X11.XSync(d, 0);
    }

    // UI thread: a window Capture made ours becomes the game window: fitted, shown and given the keyboard.
    private void Adopt(Process engine, IntPtr window)
    {
        if (!ReferenceEquals(process, engine) || engine.HasExited || host == IntPtr.Zero) return;
        captured.Add(window);
        game = window;
        try
        {
            if (IsWindows)
            {
                Win32.SetWindowPos(game, IntPtr.Zero, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_FRAMECHANGED | Win32.SWP_NOACTIVATE);
                Fit();
                Win32.ShowWindow(game, Win32.SW_SHOW);
                Win32.SetFocus(game);
            }
            else
            {
                Fit();
                X11.XMapWindow(Display, game);
                X11.XSetInputFocus(Display, game, X11.RevertToParent, IntPtr.Zero);
                X11.XFlush(Display);
            }
        }
        catch (Exception error)
        {
            DebugLog.Log($"EmbeddedGameHost: showing the engine's window failed: {error}");
            game = IntPtr.Zero;
            return;
        }
        focusTimer.Start();
    }

    // Is the game window still a child of ours? (It vanishes when the engine replaces its window, see SettleMilliseconds, or ends.)
    private bool GameStillHosted()
    {
        if (game == IntPtr.Zero || host == IntPtr.Zero) return false;
        if (IsWindows) return Win32.GetParent(game) == host;
        // asked of our own window, never of the game's: a request on a destroyed window would only raise BadWindow
        if (X11.XQueryTree(Display, host, out _, out _, out var children, out var count) == 0 || children == IntPtr.Zero) return false;
        try
        {
            for (var i = 0; i < (int)count; i++)
                if (Marshal.ReadIntPtr(children, i * IntPtr.Size) == game) return true;
            return false;
        }
        finally { X11.XFree(children); }
    }

    // Centres the largest 4:3 rectangle that fits in the control. Our host window is sized to the control here as well: Avalonia
    // moves the native window it gave us, but our child of it is our own to keep in step.
    private void Fit()
    {
        if (host == IntPtr.Zero) return;
        int w, h;
        if (IsWindows)
        {
            if (!Win32.GetClientRect(host, out var rect)) return;
            w = rect.Right - rect.Left; h = rect.Bottom - rect.Top;
        }
        else
        {
            var d = Display;
            if (d == IntPtr.Zero) return;
            (w, h) = DevicePixelSize();
            if (w < 1 || h < 1) return;
            X11.XMoveResizeWindow(d, host, 0, 0, (uint)w, (uint)h);
        }
        if (w >= 16 && h >= 16 && game != IntPtr.Zero)
        {
            var fitW = Math.Min(w, h * 4 / 3);
            var fitH = fitW * 3 / 4;
            gameRect = ((w - fitW) / 2, (h - fitH) / 2, fitW, fitH);
            if (IsWindows) Win32.MoveWindow(game, gameRect.X, gameRect.Y, gameRect.W, gameRect.H, true);
            else X11.XMoveResizeWindow(Display, game, gameRect.X, gameRect.Y, (uint)gameRect.W, (uint)gameRect.H);
        }
        if (!IsWindows) X11.XFlush(Display);
    }

    // The control's bounds changed (the window was resized, a panel opened ...): the game is re-fitted. Avalonia applies the new
    // bounds to its native window a little later (after the layout pass), so this is done once more from the dispatcher.
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        SafeFit();
        Dispatcher.UIThread.Post(SafeFit, DispatcherPriority.Background);
    }

    private void SafeFit()
    {
        try { Fit(); }
        catch (Exception error) { DebugLog.Log($"EmbeddedGameHost: fitting the game window failed: {error.Message}"); }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        FocusGame();
    }

    // Every 120 ms while a game is embedded:
    //  - a game window that is no longer ours (the engine replaced it after AttachAsync stopped watching) is looked for again
    //    among the engine's top-level windows and taken over;
    //  - clicking on the game gives it the keyboard (a click on a foreign child window doesn't move the focus by itself, and the
    //    native area of the control never delivers pointer events to Avalonia); clicking anywhere else in the editor's window
    //    while the game has the keyboard gives it back to the editor.
    private void FocusTick(object? sender, EventArgs e)
    {
        if (game == IntPtr.Zero || process is not { HasExited: false } engine) return;
        try
        {
            if (!GameStillHosted())
            {
                var replacement = FindEngineWindow(engine.Id, captured);
                if (replacement == IntPtr.Zero) return;
                DebugLog.Log($"EmbeddedGameHost: the engine replaced its window; taking over 0x{replacement:x}");
                Capture(replacement, host);
                Adopt(engine, replacement);
                return;
            }
            if (IsWindows)
            {
                if (Win32.GetFocus() == game || (Win32.GetAsyncKeyState(Win32.VK_LBUTTON) & 0x8000) == 0) return;
                if (Win32.GetCursorPos(out var p) && Win32.GetWindowRect(game, out var r) && p.X >= r.Left && p.X < r.Right && p.Y >= r.Top && p.Y < r.Bottom) Win32.SetFocus(game);
                return;
            }
            var d = Display;
            if (d == IntPtr.Zero) return;
            if (X11.XQueryPointer(d, host, out _, out _, out _, out _, out var x, out var y, out var mask) == 0 || (mask & X11.Button1Mask) == 0) return;
            X11.XGetInputFocus(d, out var focused, out _);
            var overGame = x >= gameRect.X && x < gameRect.X + gameRect.W && y >= gameRect.Y && y < gameRect.Y + gameRect.H;
            if (overGame)
            {
                if (focused != game) { X11.XSetInputFocus(d, game, X11.RevertToParent, IntPtr.Zero); X11.XFlush(d); }
            }
            else if (focused == game && TopLevel.GetTopLevel(this)?.TryGetPlatformHandle() is { } top && top.Handle != IntPtr.Zero)
            {
                // the button is down somewhere else in the editor's window (the X focus is inside it, so the window manager
                // doesn't move the focus itself): the editor's own window gets the keyboard back
                if (X11.XQueryPointer(d, top.Handle, out _, out _, out _, out _, out var tx, out var ty, out _) != 0
                    && X11.XGetGeometry(d, top.Handle, out _, out _, out _, out var tw, out var th, out _, out _) != 0
                    && tx >= 0 && ty >= 0 && tx < tw && ty < th)
                {
                    X11.XSetInputFocus(d, top.Handle, X11.RevertToParent, IntPtr.Zero);
                    X11.XFlush(d);
                }
            }
        }
        catch (Exception error) { DebugLog.Log($"EmbeddedGameHost: focus check failed: {error.Message}"); focusTimer.Stop(); }
    }

    public void FocusGame()
    {
        if (game == IntPtr.Zero) return;
        try
        {
            if (IsWindows) Win32.SetFocus(game);
            else if (Display != IntPtr.Zero) { X11.XSetInputFocus(Display, game, X11.RevertToParent, IntPtr.Zero); X11.XFlush(Display); }
        }
        catch (Exception error) { DebugLog.Log($"EmbeddedGameHost: focusing the game failed: {error.Message}"); }
    }

    private void Release()
    {
        focusTimer.Stop();
        game = IntPtr.Zero;
        captured.Clear();
    }

    // Ends the game (a running one is closed the hard way: it is a test session, nothing of it is kept).
    public void Stop()
    {
        var engine = process;
        process = null;
        Release();
        if (engine is null) return;
        try
        {
            if (!engine.HasExited)
            {
                engine.Kill(entireProcessTree: true);
                engine.WaitForExit(2000);
            }
        }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception) { DebugLog.Log($"EmbeddedGameHost: stopping the engine: {error.Message}"); }
        finally { engine.Dispose(); }
    }
}

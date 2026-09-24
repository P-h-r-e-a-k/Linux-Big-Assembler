using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace LBAAssembler;

public enum MouseButtonState { Released, Pressed }

// WPF's global keyboard state. Avalonia only hands the modifiers out with each input event, so a class handler on every
// top level (tunnelling, seeing handled events too) keeps them, and the keys that are down, current.
public static class Keyboard
{
    private static readonly HashSet<Key> down = new();
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<KeyEventArgs, object> repeats = new();
    private static bool installed;

    public static KeyModifiers Modifiers { get; private set; }

    public static void Install()
    {
        if (installed) return;
        installed = true;
        InputElement.KeyDownEvent.AddClassHandler<TopLevel>((_, e) => { Modifiers = e.KeyModifiers; if (!down.Add(e.Key)) repeats.AddOrUpdate(e, new object()); }, RoutingStrategies.Tunnel, handledEventsToo: true);
        InputElement.KeyUpEvent.AddClassHandler<TopLevel>((_, e) => { Modifiers = e.KeyModifiers; down.Remove(e.Key); }, RoutingStrategies.Tunnel, handledEventsToo: true);
        InputElement.PointerPressedEvent.AddClassHandler<TopLevel>((_, e) => { Modifiers = e.KeyModifiers; Mouse.LastPointer = e.Pointer; }, RoutingStrategies.Tunnel, handledEventsToo: true);
        InputElement.PointerMovedEvent.AddClassHandler<TopLevel>((_, e) => { Modifiers = e.KeyModifiers; Mouse.LastPointer = e.Pointer; }, RoutingStrategies.Tunnel, handledEventsToo: true);
        InputElement.PointerReleasedEvent.AddClassHandler<TopLevel>((_, e) => { Modifiers = e.KeyModifiers; Mouse.LastPointer = e.Pointer; }, RoutingStrategies.Tunnel, handledEventsToo: true);
        InputElement.LostFocusEvent.AddClassHandler<TopLevel>((_, _) => { }, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    // A key held while the window loses focus never sends its KeyUp: the set is cleared when a window deactivates.
    public static void Reset() { down.Clear(); Modifiers = KeyModifiers.None; }

    public static bool IsKeyDown(Key key) => down.Contains(key);

    // WPF's KeyEventArgs.IsRepeat: the key was already down when this KeyDown arrived (the class handler above saw it first).
    internal static bool IsRepeat(KeyEventArgs e) => repeats.TryGetValue(e, out _);

    public static IInputElement? FocusedElement => DialogPump.ActiveWindow?.FocusManager?.GetFocusedElement();

    public static void Focus(IInputElement? element) => element?.Focus();

    public static void ClearFocus() => DialogPump.ActiveWindow?.FocusManager?.ClearFocus();
}

// WPF's Mouse: the override cursor (applied to every open window) and pointer capture, which Avalonia ties to the
// pointer of the event being handled (the last one seen is kept for the WPF-style element.CaptureMouse()).
public static class Mouse
{
    internal static IPointer? LastPointer;
    private static Cursor? overrideCursor;

    public static IInputElement? Captured => LastPointer?.Captured;

    public static void Capture(IInputElement? element)
    {
        try { LastPointer?.Capture(element); } catch (InvalidOperationException) { }
    }

    public static Cursor? OverrideCursor
    {
        get => overrideCursor;
        set
        {
            overrideCursor = value;
            foreach (var window in Application.Current?.Windows ?? Array.Empty<Window>())
                window.Cursor = value ?? Cursor.Default;
        }
    }
}

public static class Cursors
{
    public static readonly Cursor Arrow = new(StandardCursorType.Arrow);
    public static readonly Cursor Hand = new(StandardCursorType.Hand);
    public static readonly Cursor Wait = new(StandardCursorType.Wait);
    public static readonly Cursor Cross = new(StandardCursorType.Cross);
    public static readonly Cursor SizeAll = new(StandardCursorType.SizeAll);
    public static readonly Cursor SizeWE = new(StandardCursorType.SizeWestEast);
    public static readonly Cursor SizeNS = new(StandardCursorType.SizeNorthSouth);
    public static readonly Cursor IBeam = new(StandardCursorType.Ibeam);
    public static readonly Cursor No = new(StandardCursorType.No);
    public static readonly Cursor None = new(StandardCursorType.None);
    public static readonly Cursor ScrollAll = new(StandardCursorType.SizeAll);
}

// The pieces of WPF's mouse event arguments the editor reads. PointerEventArgs is the base of both the pressed and the
// released arguments, so one handler can serve a WPF MouseLeftButtonDown and its MouseLeftButtonUp.
public static class PointerCompat
{
    extension(PointerEventArgs e)
    {
        // The button this event is about (a press or a release); None for a move.
        public MouseButton ChangedButton
        {
            get
            {
                if (e is PointerReleasedEventArgs released) return released.InitialPressMouseButton;
                var kind = e.GetCurrentPoint(null).Properties.PointerUpdateKind;
                return kind switch
                {
                    PointerUpdateKind.LeftButtonPressed or PointerUpdateKind.LeftButtonReleased => MouseButton.Left,
                    PointerUpdateKind.RightButtonPressed or PointerUpdateKind.RightButtonReleased => MouseButton.Right,
                    PointerUpdateKind.MiddleButtonPressed or PointerUpdateKind.MiddleButtonReleased => MouseButton.Middle,
                    PointerUpdateKind.XButton1Pressed or PointerUpdateKind.XButton1Released => MouseButton.XButton1,
                    PointerUpdateKind.XButton2Pressed or PointerUpdateKind.XButton2Released => MouseButton.XButton2,
                    _ => MouseButton.None,
                };
            }
        }
        public bool IsLeft => e.ChangedButton == MouseButton.Left;
        public bool IsRight => e.ChangedButton == MouseButton.Right;
        public bool IsMiddle => e.ChangedButton == MouseButton.Middle;
        public MouseButtonState LeftButton => e.GetCurrentPoint(null).Properties.IsLeftButtonPressed ? MouseButtonState.Pressed : MouseButtonState.Released;
        public MouseButtonState RightButton => e.GetCurrentPoint(null).Properties.IsRightButtonPressed ? MouseButtonState.Pressed : MouseButtonState.Released;
        public MouseButtonState MiddleButton => e.GetCurrentPoint(null).Properties.IsMiddleButtonPressed ? MouseButtonState.Pressed : MouseButtonState.Released;
        public int ClickCount => e is PointerPressedEventArgs pressed ? pressed.ClickCount : 0;
    }

    extension(PointerWheelEventArgs e)
    {
        // WPF's Delta: 120 per notch, positive away from the user.
        public int WheelDelta => (int)Math.Round(e.Delta.Y * 120);
    }
}

// WPF's MouseButtonEventArgs / MouseEventArgs / MouseWheelEventArgs names, for the handler signatures.
public static class RoutedCompat
{
    extension(KeyEventArgs e)
    {
        public bool IsRepeat => Keyboard.IsRepeat(e);
    }

    extension(RoutedEventArgs e)
    {
        public object? OriginalSource => e.Source;
    }
}

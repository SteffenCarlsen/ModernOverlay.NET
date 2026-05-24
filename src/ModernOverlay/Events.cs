namespace ModernOverlay;

public delegate void OverlayRenderHandler(DrawContext frame);

public delegate void OverlayLifecycleHandler(OverlayWindow overlay);

public delegate void OverlayDeviceHandler(OverlayWindow overlay, OverlayDeviceEventArgs args);

public delegate void OverlayWindowChangedHandler(OverlayWindow overlay, OverlayWindowChangedEventArgs args);

public delegate void OverlayTargetChangedHandler(OverlayWindow overlay, OverlayTargetChangedEventArgs args);

public delegate void OverlayPointerHandler(OverlayWindow overlay, OverlayPointerEventArgs args);

public delegate void OverlayKeyboardHandler(OverlayWindow overlay, OverlayKeyboardEventArgs args);

public delegate void OverlayTextInputHandler(OverlayWindow overlay, OverlayTextInputEventArgs args);

public enum OverlayPointerEventKind
{
    Moved,
    Pressed,
    Released,
    Wheel,
}

public enum OverlayPointerButton
{
    None,
    Left,
    Right,
    Middle,
}

[Flags]
public enum OverlayModifierKeys
{
    None = 0,
    Shift = 1 << 0,
    Control = 1 << 1,
    Alt = 1 << 2,
    Windows = 1 << 3,
}

public sealed class OverlayDeviceEventArgs : EventArgs
{
    public OverlayDeviceEventArgs(string reason)
    {
        Reason = reason;
    }

    public string Reason { get; }
}

public sealed class OverlayWindowChangedEventArgs : EventArgs
{
    public OverlayWindowChangedEventArgs(WindowBounds bounds)
    {
        Bounds = bounds;
    }

    public WindowBounds Bounds { get; }
}

public sealed class OverlayTargetChangedEventArgs : EventArgs
{
    public OverlayTargetChangedEventArgs(OverlayTarget? target)
        : this(target, null, null)
    {
    }

    public OverlayTargetChangedEventArgs(OverlayTarget? target, WindowHandle? targetHwnd, WindowBounds? bounds)
    {
        Target = target;
        TargetHwnd = targetHwnd;
        Bounds = bounds;
    }

    public OverlayTarget? Target { get; }

    public WindowHandle? TargetHwnd { get; }

    public WindowBounds? Bounds { get; }
}

public sealed class OverlayPointerEventArgs : EventArgs
{
    public OverlayPointerEventArgs(
        OverlayPointerEventKind kind,
        OverlayPointerButton button,
        PointF position,
        int pixelX,
        int pixelY,
        int wheelDelta = 0,
        bool isHorizontalWheel = false)
    {
        Kind = kind;
        Button = button;
        Position = position;
        PixelX = pixelX;
        PixelY = pixelY;
        WheelDelta = wheelDelta;
        IsHorizontalWheel = isHorizontalWheel;
    }

    public OverlayPointerEventKind Kind { get; }

    public OverlayPointerButton Button { get; }

    public PointF Position { get; }

    public int PixelX { get; }

    public int PixelY { get; }

    public int WheelDelta { get; }

    public bool IsHorizontalWheel { get; }
}

public sealed class OverlayKeyboardEventArgs : EventArgs
{
    public OverlayKeyboardEventArgs(
        int virtualKey,
        bool isSystemKey,
        int repeatCount,
        int scanCode,
        bool isExtendedKey,
        bool wasDown,
        bool isTransitionState,
        OverlayModifierKeys modifiers)
    {
        VirtualKey = virtualKey;
        IsSystemKey = isSystemKey;
        RepeatCount = repeatCount;
        ScanCode = scanCode;
        IsExtendedKey = isExtendedKey;
        WasDown = wasDown;
        IsTransitionState = isTransitionState;
        Modifiers = modifiers;
    }

    public int VirtualKey { get; }

    public bool IsSystemKey { get; }

    public int RepeatCount { get; }

    public int ScanCode { get; }

    public bool IsExtendedKey { get; }

    public bool WasDown { get; }

    public bool IsTransitionState { get; }

    public bool IsRepeat => WasDown || RepeatCount > 1;

    public OverlayModifierKeys Modifiers { get; }
}

public sealed class OverlayTextInputEventArgs : EventArgs
{
    public OverlayTextInputEventArgs(string text, bool isSystemCharacter)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
        IsSystemCharacter = isSystemCharacter;
    }

    public string Text { get; }

    public bool IsSystemCharacter { get; }
}

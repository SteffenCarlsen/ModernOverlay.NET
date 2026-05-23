namespace ModernOverlay;

public delegate void OverlayRenderHandler(DrawContext frame);

public delegate void OverlayLifecycleHandler(OverlayWindow overlay);

public delegate void OverlayDeviceHandler(OverlayWindow overlay, OverlayDeviceEventArgs args);

public delegate void OverlayWindowChangedHandler(OverlayWindow overlay, OverlayWindowChangedEventArgs args);

public delegate void OverlayTargetChangedHandler(OverlayWindow overlay, OverlayTargetChangedEventArgs args);

public delegate void OverlayPointerHandler(OverlayWindow overlay, OverlayPointerEventArgs args);

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

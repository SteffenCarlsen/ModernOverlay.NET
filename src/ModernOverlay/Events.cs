namespace ModernOverlay;

public delegate void OverlayRenderHandler(DrawContext frame);

public delegate void OverlayLifecycleHandler(OverlayWindow overlay);

public delegate void OverlayDeviceHandler(OverlayWindow overlay, OverlayDeviceEventArgs args);

public delegate void OverlayWindowChangedHandler(OverlayWindow overlay, OverlayWindowChangedEventArgs args);

public delegate void OverlayTargetChangedHandler(OverlayWindow overlay, OverlayTargetChangedEventArgs args);

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
    {
        Target = target;
    }

    public OverlayTarget? Target { get; }
}

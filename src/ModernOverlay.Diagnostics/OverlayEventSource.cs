using System.Diagnostics.Tracing;

namespace ModernOverlay.Diagnostics;

[EventSource(Name = "ModernOverlay")]
public sealed class OverlayEventSource : EventSource
{
    public static OverlayEventSource Log { get; } = new();

    private OverlayEventSource()
    {
    }

    [Event(1, Level = EventLevel.Informational)]
    public void OverlayCreated(long hwnd) => WriteEvent(1, hwnd);

    [Event(2, Level = EventLevel.Informational)]
    public void OverlayDestroyed(long hwnd) => WriteEvent(2, hwnd);

    [Event(3, Level = EventLevel.Warning)]
    public void RenderException(string exceptionType, string message) => WriteEvent(3, exceptionType, message);

    [Event(4, Level = EventLevel.Verbose)]
    public void FrameRendered(long frameNumber, double milliseconds, int renderThreadId)
        => WriteEvent(4, frameNumber, milliseconds, renderThreadId);
}

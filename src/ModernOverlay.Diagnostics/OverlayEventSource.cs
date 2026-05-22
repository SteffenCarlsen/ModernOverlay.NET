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
    public void FrameRendered(
        long frameNumber,
        double totalMilliseconds,
        double renderMilliseconds,
        double presentMilliseconds,
        int commandCount,
        int primitiveCount,
        int transientTextLayoutCount,
        int nativeResourceCount,
        int renderThreadId,
        double dpiScaleX,
        double dpiScaleY,
        int windowWidth,
        int windowHeight,
        bool hasTarget)
        => WriteEvent(4, frameNumber, totalMilliseconds, renderMilliseconds, presentMilliseconds, commandCount, primitiveCount, transientTextLayoutCount, nativeResourceCount, renderThreadId, dpiScaleX, dpiScaleY, windowWidth, windowHeight, hasTarget);

    [Event(5, Level = EventLevel.Verbose)]
    public void TargetResolved(long overlayHwnd, long targetHwnd, int x, int y, int width, int height)
        => WriteEvent(5, overlayHwnd, targetHwnd, x, y, width, height);

    [Event(6, Level = EventLevel.Warning)]
    public void TargetLost(long overlayHwnd) => WriteEvent(6, overlayHwnd);

    [Event(7, Level = EventLevel.Warning)]
    public void ResourceLeakDetected(int liveCount) => WriteEvent(7, liveCount);

    [Event(8, Level = EventLevel.Informational)]
    public void TargetReacquired(long overlayHwnd, long targetHwnd, int x, int y, int width, int height)
        => WriteEvent(8, overlayHwnd, targetHwnd, x, y, width, height);

    [Event(9, Level = EventLevel.Informational)]
    public void DpiChanged(long overlayHwnd, double dpiScaleX, double dpiScaleY, int width, int height)
        => WriteEvent(9, overlayHwnd, dpiScaleX, dpiScaleY, width, height);

    [Event(10, Level = EventLevel.Warning)]
    public void FrameOverBudget(long frameNumber, double actualMilliseconds, double targetMilliseconds)
        => WriteEvent(10, frameNumber, actualMilliseconds, targetMilliseconds);

    [Event(11, Level = EventLevel.Warning)]
    public void SkippedFrame(long frameNumber, string reason)
        => WriteEvent(11, frameNumber, reason);

    [Event(12, Level = EventLevel.Informational)]
    public void BackendInitialized(long overlayHwnd, string backendName, long generation)
        => WriteEvent(12, overlayHwnd, backendName, generation);

    [Event(13, Level = EventLevel.Informational)]
    public void BackendDisposed(long overlayHwnd, string backendName, long generation)
        => WriteEvent(13, overlayHwnd, backendName, generation);

    [Event(14, Level = EventLevel.Warning)]
    public void DeviceLost(long overlayHwnd, string reason)
        => WriteEvent(14, overlayHwnd, reason);

    [Event(15, Level = EventLevel.Informational)]
    public void DeviceRestored(long overlayHwnd, string reason, long backendGeneration, long resourceGeneration)
        => WriteEvent(15, overlayHwnd, reason, backendGeneration, resourceGeneration);

    [Event(16, Level = EventLevel.Warning)]
    public void ExcessiveTextLayoutCreation(long frameNumber, int transientTextLayoutCount, int threshold)
        => WriteEvent(16, frameNumber, transientTextLayoutCount, threshold);

    [Event(17, Level = EventLevel.Warning)]
    public void BackendFallback(string backendName, string feature, string requestedValue, string effectiveValue, string reason)
        => WriteEvent(17, backendName, feature, requestedValue, effectiveValue, reason);
}

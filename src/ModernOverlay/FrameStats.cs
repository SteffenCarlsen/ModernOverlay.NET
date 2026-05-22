namespace ModernOverlay;

public readonly record struct FrameStats(
    long FrameCount,
    TimeSpan LastFrameDuration,
    DateTimeOffset LastFrameUtc,
    int RenderThreadId);

namespace ModernOverlay;

public enum OverlayInputMode
{
    ClickThrough,
    Interactive,
}

public enum OverlayZOrder
{
    Normal,
    TopMost,
}

public enum PresentMode
{
    BackendDefault,
    VSync,
    Immediate,
}

public enum DpiMode
{
    System,
    PerMonitorV2,
}

public enum TransparencyMode
{
    Auto,
    DwmGlassFrame,
    LayeredWindowAttributes,
    UpdateLayeredWindow,
    DirectComposition,
}

public enum HiddenRenderPolicy
{
    Pause,
    Continue,
}

public enum RenderExceptionPolicy
{
    StopOverlay,
    IgnoreAndContinue,
    FailFast,
}

public sealed record FrameRateLimit
{
    public static FrameRateLimit DisplayDefault { get; } = new((int?)null);
    public static FrameRateLimit Unlimited { get; } = new(0);

    private FrameRateLimit(int? framesPerSecond)
    {
        FramesPerSecond = framesPerSecond;
    }

    public int? FramesPerSecond { get; }

    public static FrameRateLimit Fixed(int framesPerSecond)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(framesPerSecond);
        return new FrameRateLimit(framesPerSecond);
    }

    internal TimeSpan ToFrameInterval()
    {
        if (FramesPerSecond == 0)
        {
            return TimeSpan.Zero;
        }

        int fps = FramesPerSecond ?? 60;
        return TimeSpan.FromSeconds(1d / fps);
    }
}

public sealed record RenderQualityOptions
{
    public static RenderQualityOptions Default { get; } = new();

    public bool AntialiasPrimitives { get; init; } = true;

    public bool AntialiasText { get; init; } = true;
}

public sealed record WindowClassOptions
{
    public static WindowClassOptions Randomized { get; } = new();

    public string? ClassName { get; init; }

    public bool ShowInTaskbar { get; init; }
}

public sealed record OverlayTarget
{
    public WindowHandle Hwnd { get; init; }
}

public sealed record OverlayWindowOptions
{
    public string? Title { get; init; }

    public WindowBounds Bounds { get; init; } = new(100, 100, 800, 450);

    public bool IsVisible { get; init; } = true;

    public OverlayInputMode InputMode { get; init; } = OverlayInputMode.ClickThrough;

    public OverlayZOrder ZOrder { get; init; } = OverlayZOrder.TopMost;

    public FrameRateLimit FrameRateLimit { get; init; } = FrameRateLimit.DisplayDefault;

    public PresentMode PresentMode { get; init; } = PresentMode.BackendDefault;

    public RenderQualityOptions Quality { get; init; } = RenderQualityOptions.Default;

    public OverlayTarget? Target { get; init; }

    public DpiMode DpiMode { get; init; } = DpiMode.PerMonitorV2;

    public TransparencyMode TransparencyMode { get; init; } = TransparencyMode.Auto;

    public WindowClassOptions WindowClass { get; init; } = WindowClassOptions.Randomized;

    public HiddenRenderPolicy HiddenRenderPolicy { get; init; } = HiddenRenderPolicy.Pause;

    public RenderExceptionPolicy ExceptionPolicy { get; init; } = RenderExceptionPolicy.StopOverlay;

    public bool EnableBlurBehind { get; init; }
}

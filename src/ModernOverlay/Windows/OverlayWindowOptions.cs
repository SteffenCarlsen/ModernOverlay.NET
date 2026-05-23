namespace ModernOverlay.Windows;

public enum OverlayInputMode
{
    ClickThrough,
    Interactive,
}

public enum OverlayZOrder
{
    Normal,
    TopMost,
    FollowTarget,
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

public enum TargetMinimizedPolicy
{
    HideOverlay,
    PauseRendering,
}

public enum MatchMode
{
    Exact,
    Contains,
    StartsWith,
    EndsWith,
}

public enum RenderExceptionPolicy
{
    StopOverlay = 0,
    Continue = 1,
    FailFast = 2,
    PauseOverlay = 3,
}

public readonly record struct FrameRateLimit
{
    public static FrameRateLimit DisplayDefault { get; } = new(null);
    public static FrameRateLimit Unlimited { get; } = new(0);

    private FrameRateLimit(double? framesPerSecond)
    {
        FramesPerSecond = framesPerSecond;
    }

    public double? FramesPerSecond { get; }

    public static FrameRateLimit Fixed(double framesPerSecond)
    {
        return double.IsFinite(framesPerSecond) && framesPerSecond > 0d
            ? new FrameRateLimit(framesPerSecond)
            : throw new ArgumentOutOfRangeException(nameof(framesPerSecond), "Frame rate must be finite and greater than zero.");
    }

    internal TimeSpan ToFrameInterval(double displayDefaultFramesPerSecond = 60d)
    {
        return FramesPerSecond switch
        {
            0 => TimeSpan.Zero,
            double framesPerSecond => TimeSpan.FromSeconds(1d / framesPerSecond),
            _ when double.IsFinite(displayDefaultFramesPerSecond) && displayDefaultFramesPerSecond > 0d
                => TimeSpan.FromSeconds(1d / displayDefaultFramesPerSecond),
            _ => throw new ArgumentOutOfRangeException(nameof(displayDefaultFramesPerSecond), "Display default frame rate must be finite and greater than zero."),
        };
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

public enum TargetBoundsMode
{
    Window,
    ClientArea,
    Custom,
}

public sealed record OverlayTarget
{
    public WindowHandle Hwnd { get; init; }

    public TargetBoundsMode BoundsMode { get; init; } = TargetBoundsMode.Window;

    public bool Reacquire { get; init; }

    public TimeSpan? TrackingInterval { get; init; }

    internal TargetDiscoveryKind DiscoveryKind { get; init; } = TargetDiscoveryKind.Hwnd;

    internal string? DiscoveryValue { get; init; }

    internal int? DiscoveryProcessId { get; init; }

    internal MatchMode MatchMode { get; init; } = MatchMode.Exact;

    internal IWindowTargetProvider? Provider { get; init; }

    internal Func<WindowHandle, WindowBounds?>? CustomBoundsResolver { get; init; }

    public OverlayTarget WithBoundsMode(TargetBoundsMode boundsMode) => this with { BoundsMode = boundsMode };

    public OverlayTarget WithCustomBounds(Func<WindowHandle, WindowBounds?> resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        return this with { BoundsMode = TargetBoundsMode.Custom, CustomBoundsResolver = resolver };
    }

    public OverlayTarget WithReacquire(bool reacquire) => this with { Reacquire = reacquire };

    public OverlayTarget WithTrackingInterval(TimeSpan interval)
        => interval >= TimeSpan.Zero
            ? this with { TrackingInterval = interval }
            : throw new ArgumentOutOfRangeException(nameof(interval), "Target tracking interval cannot be negative.");
}

internal enum TargetDiscoveryKind
{
    Hwnd,
    ProcessId,
    ProcessName,
    WindowTitle,
    WindowClassName,
    ForegroundWindow,
    CustomProvider,
}

public interface IWindowTargetProvider
{
    bool TryResolve(out WindowHandle hwnd);
}

public static class WindowTarget
{
    public static OverlayTarget FromHwnd(WindowHandle hwnd)
    {
        return !hwnd.IsNull
            ? new OverlayTarget { Hwnd = hwnd }
            : throw new ArgumentException("Target HWND cannot be null.", nameof(hwnd));
    }

    public static OverlayTarget ByProcessName(string processName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);
        return new OverlayTarget
        {
            DiscoveryKind = TargetDiscoveryKind.ProcessName,
            DiscoveryValue = processName,
            Reacquire = true,
        };
    }

    public static OverlayTarget ByProcessId(int processId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processId);
        return new OverlayTarget
        {
            DiscoveryKind = TargetDiscoveryKind.ProcessId,
            DiscoveryProcessId = processId,
            Reacquire = true,
        };
    }

    public static OverlayTarget ByTitle(string title, MatchMode mode = MatchMode.Contains)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        return new OverlayTarget
        {
            DiscoveryKind = TargetDiscoveryKind.WindowTitle,
            DiscoveryValue = title,
            MatchMode = mode,
            Reacquire = true,
        };
    }

    public static OverlayTarget ByClassName(string className)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(className);
        return new OverlayTarget
        {
            DiscoveryKind = TargetDiscoveryKind.WindowClassName,
            DiscoveryValue = className,
            Reacquire = true,
        };
    }

    public static OverlayTarget ForegroundWindow()
        => new()
        {
            DiscoveryKind = TargetDiscoveryKind.ForegroundWindow,
            Reacquire = true,
        };

    public static OverlayTarget FromProvider(IWindowTargetProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return new OverlayTarget
        {
            DiscoveryKind = TargetDiscoveryKind.CustomProvider,
            Provider = provider,
            Reacquire = true,
        };
    }
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

    public TimeSpan TargetTrackingInterval { get; init; } = TimeSpan.FromMilliseconds(33);

    public DpiMode DpiMode { get; init; } = DpiMode.PerMonitorV2;

    public TransparencyMode TransparencyMode { get; init; } = TransparencyMode.Auto;

    public WindowClassOptions WindowClass { get; init; } = WindowClassOptions.Randomized;

    public HiddenRenderPolicy HiddenRenderPolicy { get; init; } = HiddenRenderPolicy.Pause;

    public TargetMinimizedPolicy TargetMinimizedPolicy { get; init; } = TargetMinimizedPolicy.HideOverlay;

    public RenderExceptionPolicy ExceptionPolicy { get; init; } = RenderExceptionPolicy.StopOverlay;

    public bool EnableBlurBehind { get; init; }

    public bool ExcludeFromCapture { get; init; }

    public bool RejectResourceCreationDuringRender { get; init; }

    public int ExcessiveTextLayoutCreationThreshold { get; init; } = 64;
}

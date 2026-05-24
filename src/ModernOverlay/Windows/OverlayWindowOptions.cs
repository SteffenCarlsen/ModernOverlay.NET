namespace ModernOverlay.Windows;

public enum OverlayInputMode
{
    ClickThrough,
    Interactive,
    SelectiveClickThrough,
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
    /// <summary>
    /// Gets the native window title used for diagnostics, window enumeration, and tools that inspect top-level windows.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets the initial overlay bounds in physical pixels when no <see cref="Target"/> is configured.
    /// Targeted overlays use these bounds until the first successful target synchronization.
    /// </summary>
    public WindowBounds Bounds { get; init; } = new(100, 100, 800, 450);

    /// <summary>
    /// Gets whether the overlay is shown immediately after creation.
    /// Hidden overlays can still be created and later shown by the platform window implementation.
    /// </summary>
    public bool IsVisible { get; init; } = true;

    /// <summary>
    /// Gets how the overlay participates in pointer and keyboard input.
    /// Use <see cref="OverlayInputMode.ClickThrough"/> for passive overlays,
    /// <see cref="OverlayInputMode.Interactive"/> for fully interactive overlays,
    /// or <see cref="OverlayInputMode.SelectiveClickThrough"/> when only hit UI elements should receive input.
    /// </summary>
    public OverlayInputMode InputMode { get; init; } = OverlayInputMode.ClickThrough;

    /// <summary>
    /// Gets how the overlay is ordered relative to other windows.
    /// <see cref="OverlayZOrder.TopMost"/> keeps it above normal windows, while
    /// <see cref="OverlayZOrder.FollowTarget"/> tracks the configured target window.
    /// </summary>
    public OverlayZOrder ZOrder { get; init; } = OverlayZOrder.TopMost;

    /// <summary>
    /// Gets the frame pacing policy used by the overlay frame loop.
    /// Display default follows the platform default cadence, fixed limits request a specific rate,
    /// and unlimited removes ModernOverlay's frame delay without disabling backend or compositor pacing.
    /// </summary>
    public FrameRateLimit FrameRateLimit { get; init; } = FrameRateLimit.DisplayDefault;

    /// <summary>
    /// Gets the presentation mode requested from the rendering backend.
    /// Use this to choose between backend defaults, VSync, or immediate presentation when supported.
    /// </summary>
    public PresentMode PresentMode { get; init; } = PresentMode.BackendDefault;

    /// <summary>
    /// Gets primitive and text rendering quality preferences applied when backend resources are created.
    /// </summary>
    public RenderQualityOptions Quality { get; init; } = RenderQualityOptions.Default;

    /// <summary>
    /// Gets the optional target window that the overlay should attach to or track.
    /// When set, target bounds and minimized-state policies can override the standalone <see cref="Bounds"/>.
    /// </summary>
    public OverlayTarget? Target { get; init; }

    /// <summary>
    /// Gets the default interval used to resynchronize with <see cref="Target"/> when the target does not provide its own tracking interval.
    /// Lower values react faster to target movement at the cost of more frequent native window queries.
    /// </summary>
    public TimeSpan TargetTrackingInterval { get; init; } = TimeSpan.FromMilliseconds(33);

    /// <summary>
    /// Gets the DPI awareness mode requested for the overlay window.
    /// Per-monitor DPI keeps overlay coordinates aligned when the window moves between displays with different scaling.
    /// </summary>
    public DpiMode DpiMode { get; init; } = DpiMode.PerMonitorV2;

    /// <summary>
    /// Gets the transparency implementation to request from the platform backend.
    /// <see cref="TransparencyMode.Auto"/> chooses the best supported mode for the current backend.
    /// </summary>
    public TransparencyMode TransparencyMode { get; init; } = TransparencyMode.Auto;

    /// <summary>
    /// Gets native window class settings, including whether the overlay should use a randomized class name and appear in the taskbar.
    /// </summary>
    public WindowClassOptions WindowClass { get; init; } = WindowClassOptions.Randomized;

    /// <summary>
    /// Gets whether rendering pauses or continues while the overlay itself is hidden.
    /// Pausing reduces background work; continuing keeps time-dependent render state advancing.
    /// </summary>
    public HiddenRenderPolicy HiddenRenderPolicy { get; init; } = HiddenRenderPolicy.Pause;

    /// <summary>
    /// Gets how the overlay behaves when its configured target window is minimized.
    /// </summary>
    public TargetMinimizedPolicy TargetMinimizedPolicy { get; init; } = TargetMinimizedPolicy.HideOverlay;

    /// <summary>
    /// Gets how exceptions thrown during rendering are handled by the frame loop.
    /// Choose a stricter policy for fail-fast diagnostics or a tolerant policy when an overlay should keep running after render failures.
    /// </summary>
    public RenderExceptionPolicy ExceptionPolicy { get; init; } = RenderExceptionPolicy.StopOverlay;

    /// <summary>
    /// Gets whether the platform backend should enable blur-behind effects for transparent overlay regions when supported.
    /// </summary>
    public bool EnableBlurBehind { get; init; }

    /// <summary>
    /// Gets whether the overlay requests exclusion from screen capture APIs that honor native capture-affinity settings.
    /// This is best-effort and depends on operating system and capture tool support.
    /// </summary>
    public bool ExcludeFromCapture { get; init; }

    /// <summary>
    /// Gets whether showing the overlay should avoid activating it. Set to <see langword="false"/> for overlays that need keyboard focus and text input.
    /// </summary>
    public bool NoActivate { get; init; } = true;

    /// <summary>
    /// Gets whether resource creation requests made from inside a render callback should be rejected.
    /// Enabling this helps catch lifetime bugs where backend resources are created at unsafe times.
    /// </summary>
    public bool RejectResourceCreationDuringRender { get; init; }

    /// <summary>
    /// Gets the diagnostic threshold for detecting excessive text layout creation during a frame.
    /// Higher values tolerate more dynamic text; lower values surface potential caching issues sooner.
    /// </summary>
    public int ExcessiveTextLayoutCreationThreshold { get; init; } = 64;
}

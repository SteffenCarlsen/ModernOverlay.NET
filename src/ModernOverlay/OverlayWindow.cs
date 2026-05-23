using ModernOverlay.Win32;
using ModernOverlay.Diagnostics;
using ModernOverlay.Rendering;
using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace ModernOverlay;

public sealed class OverlayWindow : IAsyncDisposable
{
    private const int FrameStatsWindowSize = 120;

    private readonly Lock runGate = new();
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private readonly Win32OverlayWindow nativeWindow;
    private readonly IRenderBackend renderBackend;
    private readonly DrawContext drawContext;
    private readonly Queue<TimeSpan> recentFrameDurations = new();
    private OverlayInputMode inputMode;
    private IOverlayInputRegionResolver? inputRegionResolver;
    private OverlayZOrder zOrder;
    private WindowBounds currentBounds;
    private WindowHandle currentTargetHwnd;
    private WindowBounds? currentTargetBounds;
    private DpiScale currentDpiScale;
    private bool desiredVisible;
    private bool hiddenByTargetMinimized;
    private bool targetRenderPaused;
    private bool targetWasMinimized;
    private bool targetWasLost;
    private bool paused;
    private bool disposed;
    private CancellationTokenSource? activeRunCancellation;
    private long frameCount;
    private long skippedFrameCount;
    private long droppedFrameCount;
    private long targetTrackingUpdateCount;
    private TimeSpan recentFrameDurationTotal;
    private DateTimeOffset firstFrameUtc;
    private DateTimeOffset lastFrameStartUtc;
    private DateTimeOffset lastTargetTrackingUtc;
    private TimeSpan currentTargetFrameInterval;
    private readonly TimeSpan targetTrackingInterval;

    private OverlayWindow(OverlayWindowOptions options, Win32OverlayWindow nativeWindow, WindowBounds initialBounds)
    {
        Options = options;
        this.nativeWindow = nativeWindow;
        Resources = new OverlayResourceManager
        {
            RejectCreationDuringRender = options.RejectResourceCreationDuringRender,
        };
        Hotkeys = new OverlayHotkeyManager(nativeWindow);
        nativeWindow.SetDpiChangedCallback(HandleDpiChanged);
        nativeWindow.SetPointerCallback(HandlePointerEvent);
        nativeWindow.SetKeyboardCallback(HandleKeyboardEvent);
        nativeWindow.SetTextInputCallback(HandleTextInputEvent);
        nativeWindow.SetInputRegionCallback(ResolveInputRegion);
        renderBackend = RenderBackendRegistry.CreateBackend(options);
        currentDpiScale = ToDpiScale(nativeWindow.GetDpiScale());
        currentBounds = initialBounds;
        nativeWindow.InvokeOnOwnerThread(() =>
        {
            renderBackend.Initialize(CreateBackendInitializeContext(currentBounds, currentDpiScale));
        });
        OverlayEventSource.Log.BackendInitialized(nativeWindow.Hwnd, BackendName, renderBackend.Generation.Value);
        drawContext = new DrawContext(renderBackend.CommandSink, enforceFrameScope: true);
        inputMode = options.InputMode;
        zOrder = options.ZOrder;
        currentTargetFrameInterval = options.FrameRateLimit.ToFrameInterval();
        targetTrackingInterval = options.Target?.TrackingInterval ?? options.TargetTrackingInterval;
        desiredVisible = options.IsVisible;
        OverlayEventSource.Log.OverlayCreated(nativeWindow.Hwnd);
    }

    public event OverlayRenderHandler? Render;

    public event OverlayLifecycleHandler? Loaded;

    public event OverlayLifecycleHandler? Unloaded;

    public event OverlayLifecycleHandler? Disposed;

    public event OverlayDeviceHandler? DeviceLost;

    public event OverlayDeviceHandler? DeviceRestored;

    public event OverlayWindowChangedHandler? BoundsChanged;

    public event OverlayWindowChangedHandler? VisibilityChanged;

    public event OverlayTargetChangedHandler? TargetChanged;

    public event OverlayTargetChangedHandler? TargetLost;

    public event OverlayTargetChangedHandler? TargetReacquired;

    public event OverlayPointerHandler? PointerMoved;

    public event OverlayPointerHandler? PointerPressed;

    public event OverlayPointerHandler? PointerReleased;

    public event OverlayPointerHandler? PointerWheel;

    public event OverlayKeyboardHandler? KeyPressed;

    public event OverlayKeyboardHandler? KeyReleased;

    public event OverlayTextInputHandler? TextInput;

    public OverlayWindowOptions Options { get; }

    public FrameStats FrameStats { get; private set; }

    public OverlayResourceManager Resources { get; }

    public OverlayHotkeyManager Hotkeys { get; }

    public WindowHandle Hwnd => new(nativeWindow.Hwnd);

    public string BackendName => renderBackend.Resources.BackendName;

    public WindowBounds BoundsPixels => currentBounds;

    public DpiScale DpiScale => currentDpiScale;

    public RectF BoundsDips => currentDpiScale.PixelsToDips(currentBounds);

    internal RenderBackendKind RenderBackendKind => renderBackend.Kind;

    internal Rendering.RenderBackendGeneration RenderBackendGeneration => renderBackend.Generation;

    public OverlayInputMode InputMode
    {
        get => inputMode;
        set
        {
            inputMode = value;
            nativeWindow.SetClickThrough(value == OverlayInputMode.ClickThrough);
        }
    }

    public void SetInputRegionResolver(IOverlayInputRegionResolver? resolver)
    {
        inputRegionResolver = resolver;
    }

    public OverlayZOrder ZOrder
    {
        get => zOrder;
        set
        {
            zOrder = value;
            if (value == OverlayZOrder.TopMost)
            {
                nativeWindow.SetTopMost(true);
                return;
            }

            nativeWindow.SetTopMost(false);
            if (value == OverlayZOrder.FollowTarget && !currentTargetHwnd.IsNull)
            {
                nativeWindow.PlaceRelativeTo(currentTargetHwnd.Value);
            }
        }
    }

    public static ValueTask<OverlayWindow> CreateAsync(OverlayWindowOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ct.ThrowIfCancellationRequested();

        if (options.Bounds.IsEmpty)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Overlay bounds must have positive width and height.");
        }

        if (options.TargetTrackingInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Target tracking interval cannot be negative.");
        }

        if (options.Target?.TrackingInterval is { } targetTrackingInterval && targetTrackingInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Target tracking interval cannot be negative.");
        }

        WindowBounds initialBounds = ResolveInitialBounds(options);

        var nativeOptions = new Win32OverlayWindowOptions(
            options.WindowClass.ClassName,
            options.Title ?? "ModernOverlay",
            initialBounds.X,
            initialBounds.Y,
            initialBounds.Width,
            initialBounds.Height,
            options.InputMode == OverlayInputMode.ClickThrough,
            options.ZOrder == OverlayZOrder.TopMost,
            !options.WindowClass.ShowInTaskbar,
            options.DpiMode == DpiMode.PerMonitorV2,
            options.ExcludeFromCapture);

        Win32OverlayWindow nativeWindow = Win32OverlayWindow.Create(nativeOptions);
        try
        {
            ApplyInitialTransparency(options, nativeWindow);
            if (options.IsVisible)
            {
                nativeWindow.Show();
            }
        }
        catch
        {
            nativeWindow.Dispose();
            throw;
        }

        return ValueTask.FromResult(new OverlayWindow(options, nativeWindow, initialBounds));
    }

    private static void ApplyInitialTransparency(OverlayWindowOptions options, Win32OverlayWindow nativeWindow)
    {
        const uint transparentBlackColorKey = 0x000000;

        switch (options.TransparencyMode)
        {
            case TransparencyMode.Auto:
            case TransparencyMode.DwmGlassFrame:
                nativeWindow.ExtendFrameIntoClientArea();
                nativeWindow.SetTransparentColorKey(transparentBlackColorKey);
                break;
            case TransparencyMode.LayeredWindowAttributes:
                nativeWindow.SetTransparentColorKey(transparentBlackColorKey);
                break;
            case TransparencyMode.UpdateLayeredWindow:
                ApplyTransparencyFallback(
                    nativeWindow,
                    options.TransparencyMode,
                    "UpdateLayeredWindow CPU-copy fallback backend is not implemented; using DWM glass frame extension.");
                break;
            case TransparencyMode.DirectComposition:
                ApplyTransparencyFallback(
                    nativeWindow,
                    options.TransparencyMode,
                    "DirectComposition backend is not implemented; using DWM glass frame extension.");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(options), $"Unsupported transparency mode: {options.TransparencyMode}.");
        }

        if (options.EnableBlurBehind && options.TransparencyMode != TransparencyMode.DwmGlassFrame)
        {
            nativeWindow.ExtendFrameIntoClientArea();
        }

        if (options.EnableBlurBehind)
        {
            nativeWindow.EnableBlurBehind();
        }
    }

    private static void ApplyTransparencyFallback(Win32OverlayWindow nativeWindow, TransparencyMode requestedMode, string reason)
    {
        const uint transparentBlackColorKey = 0x000000;

        nativeWindow.ExtendFrameIntoClientArea();
        nativeWindow.SetTransparentColorKey(transparentBlackColorKey);
        OverlayEventSource.Log.BackendFallback(
            "Win32 overlay window",
            nameof(TransparencyMode),
            requestedMode.ToString(),
            TransparencyMode.DwmGlassFrame.ToString(),
            reason);
    }

    public async ValueTask RunAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();

        CancellationTokenSource runCancellation = new();
        lock (runGate)
        {
            if (activeRunCancellation is not null)
            {
                runCancellation.Dispose();
                throw new InvalidOperationException("The overlay run loop is already active.");
            }

            activeRunCancellation = runCancellation;
        }

        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetimeCancellation.Token, runCancellation.Token);
        _ = ResolveAndStoreFrameInterval();

        Loaded?.Invoke(this);

        try
        {
            await Task.Run(
                () => nativeWindow.RunFrameLoop(ResolveAndStoreFrameInterval, RenderOneFrame, linked.Token),
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
        }
        finally
        {
            lock (runGate)
            {
                if (ReferenceEquals(activeRunCancellation, runCancellation))
                {
                    activeRunCancellation = null;
                }
            }

            runCancellation.Dispose();
            Unloaded?.Invoke(this);
        }
    }

    public ValueTask StopAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();
        lock (runGate)
        {
            activeRunCancellation?.Cancel();
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask ShowAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        desiredVisible = true;
        hiddenByTargetMinimized = false;
        nativeWindow.Show();
        VisibilityChanged?.Invoke(this, new OverlayWindowChangedEventArgs(currentBounds));
        return ValueTask.CompletedTask;
    }

    public ValueTask HideAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        desiredVisible = false;
        hiddenByTargetMinimized = false;
        nativeWindow.Hide();
        VisibilityChanged?.Invoke(this, new OverlayWindowChangedEventArgs(currentBounds));
        return ValueTask.CompletedTask;
    }

    public void Pause() => paused = true;

    public void Resume() => paused = false;

    public void SetBoundsPixels(WindowBounds bounds)
    {
        if (bounds.IsEmpty)
        {
            throw new ArgumentOutOfRangeException(nameof(bounds), "Overlay bounds must have positive width and height.");
        }

        ApplyBounds(bounds, raiseTargetChanged: false);
    }

    public void SetBoundsDips(RectF bounds, DpiScale dpi) => SetBoundsPixels(WindowBounds.FromDips(bounds, dpi));

    public void MovePixels(int x, int y) => SetBoundsPixels(currentBounds with { X = x, Y = y });

    public void ResizePixels(int width, int height) => SetBoundsPixels(currentBounds with { Width = width, Height = height });

    public ValueTask RecreateAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        const string lostReason = "Manual recreation requested.";
        const string restoredReason = "Backend recreated and resource generation advanced.";
        nativeWindow.InvokeOnOwnerThread(() => RecreateBackendOnOwnerThread(lostReason, restoredReason));
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return ValueTask.CompletedTask;
        }

        disposed = true;
        lifetimeCancellation.Cancel();
        Hotkeys.Dispose();
        nativeWindow.SetDpiChangedCallback(null);
        nativeWindow.SetKeyboardCallback(null);
        nativeWindow.SetTextInputCallback(null);
        nativeWindow.SetPointerCallback(null);
        nativeWindow.InvokeOnOwnerThread(renderBackend.Dispose);
        OverlayEventSource.Log.BackendDisposed(nativeWindow.Hwnd, BackendName, renderBackend.Generation.Value);
        OverlayEventSource.Log.OverlayDestroyed(nativeWindow.Hwnd);
        Disposed?.Invoke(this);
        nativeWindow.Dispose();
        lifetimeCancellation.Dispose();
        return ValueTask.CompletedTask;
    }

    private void RenderOneFrame()
    {
        if (paused)
        {
            return;
        }

        if (!desiredVisible && Options.HiddenRenderPolicy == HiddenRenderPolicy.Pause)
        {
            return;
        }

        SyncTargetBoundsIfDue(start: DateTimeOffset.UtcNow);
        if (targetRenderPaused)
        {
            return;
        }

        DateTimeOffset start = DateTimeOffset.UtcNow;
        drawContext.Reset();

        try
        {
            nativeWindow.InvokeOnOwnerThread(() => RenderOneFrameOnOwnerThread(start));
        }
        catch when (Options.ExceptionPolicy == RenderExceptionPolicy.Continue)
        {
            return;
        }
        catch when (Options.ExceptionPolicy == RenderExceptionPolicy.PauseOverlay)
        {
            paused = true;
            throw;
        }
    }

    private void RenderOneFrameOnOwnerThread(DateTimeOffset start)
    {
        DateTimeOffset previousFrameUtc = FrameStats.LastFrameUtc;
        if (firstFrameUtc == default)
        {
            firstFrameUtc = start;
        }

        TimeSpan frameStartInterval = lastFrameStartUtc == default ? TimeSpan.Zero : start - lastFrameStartUtc;
        lastFrameStartUtc = start;
        var frameInfo = new FrameInfo(
            frameCount + 1,
            start,
            start - firstFrameUtc,
            frameStartInterval,
            currentTargetFrameInterval,
            frameStartInterval,
            Environment.CurrentManagedThreadId,
            currentDpiScale,
            currentBounds,
            currentTargetBounds);
        BeginFrameResult beginFrame = renderBackend.BeginFrame(frameInfo);
        if (!beginFrame.CanRender)
        {
            skippedFrameCount++;
            OverlayEventSource.Log.SkippedFrame(frameInfo.FrameNumber, beginFrame.SkipReason ?? "Backend skipped frame.");
            return;
        }

        Exception? renderException = null;
        long renderStart = Stopwatch.GetTimestamp();
        TimeSpan renderDuration;
        try
        {
            drawContext.BeginFrame();
            using (Resources.EnterRenderCallback())
            {
                Render?.Invoke(drawContext);
            }

            drawContext.CompleteFrame();
        }
        catch (Exception exception)
        {
            renderException = exception;
            drawContext.UnwindFrameState();
            drawContext.EndFrame();
        }

        renderDuration = Stopwatch.GetElapsedTime(renderStart);
        long presentStart = Stopwatch.GetTimestamp();
        EndFrameResult endFrame = renderBackend.EndFrame();
        TimeSpan presentDuration = Stopwatch.GetElapsedTime(presentStart);
        if (renderException is not null)
        {
            OverlayEventSource.Log.RenderException(renderException.GetType().FullName ?? renderException.GetType().Name, renderException.Message);
            if (Options.ExceptionPolicy == RenderExceptionPolicy.FailFast)
            {
                Environment.FailFast("ModernOverlay render callback failed.", renderException);
            }

            ExceptionDispatchInfo.Capture(renderException).Throw();
        }

        if (endFrame.RequiresRecreate)
        {
            string lostReason = endFrame.RecreateReason ?? "Render backend requested device recreation.";
            RecreateBackendOnOwnerThread(lostReason, "Backend recreated after render-target recreation request.");
            return;
        }

        frameCount++;
        DateTimeOffset end = DateTimeOffset.UtcNow;
        TimeSpan frameDuration = end - start;
        TimeSpan completedFrameInterval = previousFrameUtc == default ? frameDuration : end - previousFrameUtc;
        (TimeSpan movingAverageFrameDuration, TimeSpan worstFrameDuration) = RecordFrameDuration(frameDuration);
        if (IsDroppedFrame(completedFrameInterval))
        {
            droppedFrameCount++;
            OverlayEventSource.Log.FrameOverBudget(
                frameCount,
                completedFrameInterval.TotalMilliseconds,
                currentTargetFrameInterval.TotalMilliseconds);
        }

        TimeSpan elapsedSinceFirstFrame = end - firstFrameUtc;
        FrameStats = new FrameStats(
            frameCount,
            ToFramesPerSecond(completedFrameInterval),
            elapsedSinceFirstFrame > TimeSpan.Zero ? frameCount / elapsedSinceFirstFrame.TotalSeconds : 0d,
            frameDuration,
            movingAverageFrameDuration,
            worstFrameDuration,
            end,
            Environment.CurrentManagedThreadId,
            currentTargetFrameInterval,
            completedFrameInterval,
            renderDuration,
            presentDuration,
            renderBackend.CommandSink.CommandCount,
            renderBackend.CommandSink.PrimitiveCount,
            renderBackend.CommandSink.TransientTextLayoutCount,
            renderBackend.CommandSink.NativeResourceCount,
            skippedFrameCount,
            droppedFrameCount,
            targetTrackingUpdateCount,
            renderBackend.Generation.Value,
            currentTargetHwnd,
            currentDpiScale,
            currentBounds,
            currentTargetBounds);
        OverlayEventSource.Log.FrameRendered(
            FrameStats.FrameCount,
            FrameStats.LastFrameDuration.TotalMilliseconds,
            FrameStats.RenderDuration.TotalMilliseconds,
            FrameStats.PresentDuration.TotalMilliseconds,
            FrameStats.CommandCount,
            FrameStats.PrimitiveCount,
            FrameStats.TransientTextLayoutCount,
            FrameStats.NativeResourceCount,
            FrameStats.RenderThreadId,
            FrameStats.DpiScale.X,
            FrameStats.DpiScale.Y,
            FrameStats.WindowBounds.Width,
            FrameStats.WindowBounds.Height,
            FrameStats.TargetBounds.HasValue);
        if (Options.ExcessiveTextLayoutCreationThreshold > 0
            && FrameStats.TransientTextLayoutCount > Options.ExcessiveTextLayoutCreationThreshold)
        {
            OverlayEventSource.Log.ExcessiveTextLayoutCreation(
                FrameStats.FrameCount,
                FrameStats.TransientTextLayoutCount,
                Options.ExcessiveTextLayoutCreationThreshold);
        }
    }

    private static WindowBounds ResolveInitialBounds(OverlayWindowOptions options)
    {
        return TryResolveTarget(options.Target, out _, out WindowBounds targetBounds)
            ? targetBounds
            : options.Bounds;
    }

    private static bool TryResolveTarget(OverlayTarget? target, out WindowHandle hwnd, out WindowBounds bounds)
    {
        hwnd = default;
        bounds = default;
        if (target is null || !TryResolveTargetHwnd(target, out hwnd))
        {
            return false;
        }

        if (target.BoundsMode == TargetBoundsMode.Custom)
        {
            bounds = target.CustomBoundsResolver?.Invoke(hwnd) ?? WindowBounds.Empty;
            return !bounds.IsEmpty;
        }

        bool clientArea = target.BoundsMode == TargetBoundsMode.ClientArea;
        if (!Win32WindowQuery.TryGetWindowBounds(hwnd.Value, clientArea, out Win32WindowBounds win32Bounds))
        {
            return false;
        }

        bounds = new WindowBounds(win32Bounds.X, win32Bounds.Y, win32Bounds.Width, win32Bounds.Height);
        return !bounds.IsEmpty;
    }

    private TimeSpan ResolveFrameInterval()
    {
        return Options.FrameRateLimit.FramesPerSecond is not null
            ? Options.FrameRateLimit.ToFrameInterval()
            : Win32DisplayQuery.TryGetRefreshRateForWindow(nativeWindow.Hwnd, out int displayFramesPerSecond)
            ? Options.FrameRateLimit.ToFrameInterval(displayFramesPerSecond)
            : Options.FrameRateLimit.ToFrameInterval();
    }

    private TimeSpan ResolveAndStoreFrameInterval()
    {
        TimeSpan interval = ResolveFrameInterval();
        currentTargetFrameInterval = interval;
        return interval;
    }

    private static bool TryResolveTargetHwnd(OverlayTarget target, out WindowHandle hwnd)
    {
        hwnd = default;
        nint resolved = target.DiscoveryKind switch
        {
            TargetDiscoveryKind.Hwnd => target.Hwnd.Value,
            TargetDiscoveryKind.ProcessId when target.DiscoveryProcessId is int processId
                => Win32WindowQuery.TryFindWindowByProcessId(processId, out nint processIdWindow) ? processIdWindow : 0,
            TargetDiscoveryKind.ProcessName when target.DiscoveryValue is not null
                => Win32WindowQuery.TryFindWindowByProcessName(target.DiscoveryValue, out nint processWindow) ? processWindow : 0,
            TargetDiscoveryKind.WindowTitle when target.DiscoveryValue is not null
                => Win32WindowQuery.TryFindWindowByTitle(target.DiscoveryValue, ToWin32MatchMode(target.MatchMode), out nint titleWindow) ? titleWindow : 0,
            TargetDiscoveryKind.WindowClassName when target.DiscoveryValue is not null
                => Win32WindowQuery.TryFindWindowByClassName(target.DiscoveryValue, out nint classWindow) ? classWindow : 0,
            TargetDiscoveryKind.ForegroundWindow
                => Win32WindowQuery.TryGetForegroundWindow(out nint foregroundWindow) ? foregroundWindow : 0,
            TargetDiscoveryKind.CustomProvider when target.Provider is not null
                => target.Provider.TryResolve(out WindowHandle providerHwnd) ? providerHwnd.Value : 0,
            _ => 0,
        };

        if (resolved == 0)
        {
            return false;
        }

        hwnd = new WindowHandle(resolved);
        return true;
    }

    private void SyncTargetBounds()
    {
        if (Options.Target is not null
            && TryResolveTargetHwnd(Options.Target, out WindowHandle minimizedTargetHwnd)
            && Win32WindowQuery.IsWindowMinimized(minimizedTargetHwnd.Value))
        {
            HandleTargetMinimized(minimizedTargetHwnd);
            return;
        }

        if (!TryResolveTarget(Options.Target, out WindowHandle targetHwnd, out WindowBounds targetBounds))
        {
            if (!currentTargetHwnd.IsNull)
            {
                OverlayEventSource.Log.TargetLost(nativeWindow.Hwnd);
                TargetLost?.Invoke(this, new OverlayTargetChangedEventArgs(Options.Target, currentTargetHwnd, currentTargetBounds));
                targetWasLost = true;
            }

            ClearTargetMinimizedState();
            currentTargetHwnd = default;
            currentTargetBounds = null;
            return;
        }

        ClearTargetMinimizedState();
        bool wasLost = targetWasLost;
        targetWasLost = false;
        currentTargetHwnd = targetHwnd;
        currentTargetBounds = targetBounds;
        OverlayEventSource.Log.TargetResolved(nativeWindow.Hwnd, targetHwnd.Value, targetBounds.X, targetBounds.Y, targetBounds.Width, targetBounds.Height);
        if (wasLost)
        {
            OverlayEventSource.Log.TargetReacquired(nativeWindow.Hwnd, targetHwnd.Value, targetBounds.X, targetBounds.Y, targetBounds.Width, targetBounds.Height);
            TargetReacquired?.Invoke(this, new OverlayTargetChangedEventArgs(Options.Target, targetHwnd, targetBounds));
        }

        if (zOrder == OverlayZOrder.FollowTarget)
        {
            nativeWindow.PlaceRelativeTo(targetHwnd.Value);
        }

        if (targetBounds == currentBounds)
        {
            return;
        }

        ApplyBounds(targetBounds, raiseTargetChanged: true);
    }

    private void SyncTargetBoundsIfDue(DateTimeOffset start)
    {
        if (Options.Target is null)
        {
            return;
        }

        TimeSpan interval = targetTrackingInterval;
        bool shouldTrack = interval == TimeSpan.Zero
            || lastTargetTrackingUtc == default
            || start - lastTargetTrackingUtc >= interval
            || currentTargetHwnd.IsNull
            || targetRenderPaused
            || targetWasLost;
        if (!shouldTrack)
        {
            return;
        }

        lastTargetTrackingUtc = start;
        targetTrackingUpdateCount++;
        SyncTargetBounds();
    }

    private void HandleTargetMinimized(WindowHandle targetHwnd)
    {
        currentTargetHwnd = targetHwnd;
        currentTargetBounds = null;
        targetRenderPaused = true;
        targetWasMinimized = true;

        if (Options.TargetMinimizedPolicy == TargetMinimizedPolicy.HideOverlay && !hiddenByTargetMinimized)
        {
            nativeWindow.Hide();
            hiddenByTargetMinimized = true;
            VisibilityChanged?.Invoke(this, new OverlayWindowChangedEventArgs(currentBounds));
        }
    }

    private void ClearTargetMinimizedState()
    {
        if (!targetWasMinimized)
        {
            return;
        }

        targetWasMinimized = false;
        targetRenderPaused = false;
        if (hiddenByTargetMinimized && desiredVisible)
        {
            nativeWindow.Show();
            VisibilityChanged?.Invoke(this, new OverlayWindowChangedEventArgs(currentBounds));
        }

        hiddenByTargetMinimized = false;
    }

    private void ApplyBounds(WindowBounds bounds, bool raiseTargetChanged)
    {
        nativeWindow.SetBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        currentDpiScale = ToDpiScale(nativeWindow.GetDpiScale());
        nativeWindow.InvokeOnOwnerThread(() => renderBackend.Resize(new PixelSize(bounds.Width, bounds.Height), currentDpiScale));
        currentBounds = bounds;
        BoundsChanged?.Invoke(this, new OverlayWindowChangedEventArgs(bounds));
        if (raiseTargetChanged)
        {
            TargetChanged?.Invoke(this, new OverlayTargetChangedEventArgs(Options.Target, currentTargetHwnd, currentTargetBounds));
        }
    }

    private void HandleDpiChanged(Win32DpiScale dpiScale, Win32WindowBounds bounds)
    {
        currentDpiScale = ToDpiScale(dpiScale);
        WindowBounds effectiveBounds = bounds.IsEmpty
            ? currentBounds
            : new WindowBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        currentBounds = effectiveBounds;
        renderBackend.Resize(new PixelSize(effectiveBounds.Width, effectiveBounds.Height), currentDpiScale);
        OverlayEventSource.Log.DpiChanged(nativeWindow.Hwnd, currentDpiScale.X, currentDpiScale.Y, effectiveBounds.Width, effectiveBounds.Height);
        BoundsChanged?.Invoke(this, new OverlayWindowChangedEventArgs(effectiveBounds));
    }

    private void RecreateBackendOnOwnerThread(string lostReason, string restoredReason)
    {
        DeviceLost?.Invoke(this, new OverlayDeviceEventArgs(lostReason));
        OverlayEventSource.Log.DeviceLost(nativeWindow.Hwnd, lostReason);
        Resources.AdvanceGeneration();
        currentDpiScale = ToDpiScale(nativeWindow.GetDpiScale());
        renderBackend.Recreate(CreateBackendInitializeContext(currentBounds, currentDpiScale));
        DeviceRestored?.Invoke(this, new OverlayDeviceEventArgs(restoredReason));
        OverlayEventSource.Log.DeviceRestored(nativeWindow.Hwnd, restoredReason, renderBackend.Generation.Value, Resources.CurrentGeneration);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    private static DpiScale ToDpiScale(Win32DpiScale dpiScale) => new(dpiScale.X, dpiScale.Y);

    private void HandlePointerEvent(Win32PointerEvent pointerEvent)
    {
        OverlayPointerEventKind kind = pointerEvent.Kind switch
        {
            Win32PointerEventKind.Moved => OverlayPointerEventKind.Moved,
            Win32PointerEventKind.Pressed => OverlayPointerEventKind.Pressed,
            Win32PointerEventKind.Released => OverlayPointerEventKind.Released,
            Win32PointerEventKind.Wheel => OverlayPointerEventKind.Wheel,
            _ => throw new ArgumentOutOfRangeException(nameof(pointerEvent), "Unsupported pointer event kind."),
        };
        OverlayPointerButton button = pointerEvent.Button switch
        {
            Win32PointerButton.None => OverlayPointerButton.None,
            Win32PointerButton.Left => OverlayPointerButton.Left,
            Win32PointerButton.Right => OverlayPointerButton.Right,
            Win32PointerButton.Middle => OverlayPointerButton.Middle,
            _ => throw new ArgumentOutOfRangeException(nameof(pointerEvent), "Unsupported pointer button."),
        };

        var position = new PointF(pointerEvent.X / currentDpiScale.X, pointerEvent.Y / currentDpiScale.Y);
        var args = new OverlayPointerEventArgs(
            kind,
            button,
            position,
            pointerEvent.X,
            pointerEvent.Y,
            pointerEvent.WheelDelta,
            pointerEvent.IsHorizontalWheel);
        switch (kind)
        {
            case OverlayPointerEventKind.Moved:
                PointerMoved?.Invoke(this, args);
                break;
            case OverlayPointerEventKind.Pressed:
                PointerPressed?.Invoke(this, args);
                break;
            case OverlayPointerEventKind.Released:
                PointerReleased?.Invoke(this, args);
                break;
            case OverlayPointerEventKind.Wheel:
                PointerWheel?.Invoke(this, args);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(pointerEvent), "Unsupported pointer event kind.");
        }
    }

    private void HandleKeyboardEvent(Win32KeyboardEvent keyboardEvent)
    {
        var modifiers = (OverlayModifierKeys)(int)keyboardEvent.Modifiers;
        var args = new OverlayKeyboardEventArgs(
            keyboardEvent.VirtualKey,
            keyboardEvent.IsSystemKey,
            keyboardEvent.RepeatCount,
            keyboardEvent.ScanCode,
            keyboardEvent.IsExtendedKey,
            keyboardEvent.WasDown,
            keyboardEvent.IsTransitionState,
            modifiers);

        if (keyboardEvent.IsPressed)
        {
            KeyPressed?.Invoke(this, args);
            return;
        }

        KeyReleased?.Invoke(this, args);
    }

    private void HandleTextInputEvent(Win32TextInputEvent textInputEvent)
    {
        TextInput?.Invoke(this, new OverlayTextInputEventArgs(textInputEvent.Text, textInputEvent.IsSystemCharacter));
    }

    private bool ResolveInputRegion(int pixelX, int pixelY)
    {
        if (inputMode != OverlayInputMode.SelectiveClickThrough)
        {
            return true;
        }

        if (inputRegionResolver is null)
        {
            return false;
        }

        var position = new PointF(pixelX / currentDpiScale.X, pixelY / currentDpiScale.Y);
        return inputRegionResolver.ResolveInputRegion(position) == OverlayInputRegionResult.Interactive;
    }

    private (TimeSpan MovingAverage, TimeSpan Worst) RecordFrameDuration(TimeSpan frameDuration)
    {
        recentFrameDurations.Enqueue(frameDuration);
        recentFrameDurationTotal += frameDuration;
        if (recentFrameDurations.Count > FrameStatsWindowSize)
        {
            recentFrameDurationTotal -= recentFrameDurations.Dequeue();
        }

        TimeSpan worst = TimeSpan.Zero;
        foreach (TimeSpan recentFrameDuration in recentFrameDurations)
        {
            if (recentFrameDuration > worst)
            {
                worst = recentFrameDuration;
            }
        }

        return (
            TimeSpan.FromTicks(recentFrameDurationTotal.Ticks / recentFrameDurations.Count),
            worst);
    }

    private bool IsDroppedFrame(TimeSpan frameInterval)
    {
        if (frameInterval <= TimeSpan.Zero || currentTargetFrameInterval <= TimeSpan.Zero)
        {
            return false;
        }

        long budgetTicks = currentTargetFrameInterval.Ticks + currentTargetFrameInterval.Ticks / 2;
        return frameInterval.Ticks > budgetTicks;
    }

    private static double ToFramesPerSecond(TimeSpan interval)
        => interval > TimeSpan.Zero ? 1d / interval.TotalSeconds : 0d;

    private static Win32WindowTitleMatchMode ToWin32MatchMode(MatchMode matchMode)
        => matchMode switch
        {
            MatchMode.Exact => Win32WindowTitleMatchMode.Exact,
            MatchMode.Contains => Win32WindowTitleMatchMode.Contains,
            MatchMode.StartsWith => Win32WindowTitleMatchMode.StartsWith,
            MatchMode.EndsWith => Win32WindowTitleMatchMode.EndsWith,
            _ => throw new ArgumentOutOfRangeException(nameof(matchMode), matchMode, "Unsupported match mode."),
        };

    private RenderBackendInitializeContext CreateBackendInitializeContext(WindowBounds bounds, DpiScale dpi)
        => new(
            new WindowHandle(nativeWindow.Hwnd),
            new PixelSize(bounds.Width, bounds.Height),
            dpi,
            Options.Quality,
            Options.PresentMode);
}

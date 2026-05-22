using ModernOverlay.Win32;

namespace ModernOverlay;

public sealed class OverlayWindow : IAsyncDisposable
{
    private readonly DrawContext drawContext = new();
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private readonly Win32OverlayWindow nativeWindow;
    private OverlayInputMode inputMode;
    private OverlayZOrder zOrder;
    private WindowBounds currentBounds;
    private bool paused;
    private bool disposed;
    private long frameCount;

    private OverlayWindow(OverlayWindowOptions options, Win32OverlayWindow nativeWindow)
    {
        Options = options;
        this.nativeWindow = nativeWindow;
        Resources = new OverlayResourceManager();
        inputMode = options.InputMode;
        zOrder = options.ZOrder;
        currentBounds = options.Bounds;
    }

    public event OverlayRenderHandler? Render;

    public event OverlayLifecycleHandler? Loaded;

    public event OverlayLifecycleHandler? Unloaded;

    public event OverlayDeviceHandler? DeviceLost;

    public event OverlayDeviceHandler? DeviceRestored;

    public event OverlayWindowChangedHandler? BoundsChanged;

    public event OverlayWindowChangedHandler? VisibilityChanged;

    public event OverlayTargetChangedHandler? TargetChanged;

    public OverlayWindowOptions Options { get; }

    public FrameStats FrameStats { get; private set; }

    public OverlayResourceManager Resources { get; }

    public WindowHandle Hwnd => new(nativeWindow.Hwnd);

    public OverlayInputMode InputMode
    {
        get => inputMode;
        set
        {
            inputMode = value;
            nativeWindow.SetClickThrough(value == OverlayInputMode.ClickThrough);
        }
    }

    public OverlayZOrder ZOrder
    {
        get => zOrder;
        set
        {
            zOrder = value;
            nativeWindow.SetTopMost(value == OverlayZOrder.TopMost);
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

        var nativeOptions = new Win32OverlayWindowOptions(
            options.WindowClass.ClassName,
            options.Title ?? "ModernOverlay",
            options.Bounds.X,
            options.Bounds.Y,
            options.Bounds.Width,
            options.Bounds.Height,
            options.InputMode == OverlayInputMode.ClickThrough,
            options.ZOrder == OverlayZOrder.TopMost,
            !options.WindowClass.ShowInTaskbar);

        Win32OverlayWindow nativeWindow = Win32OverlayWindow.Create(nativeOptions);
        ApplyInitialTransparency(options, nativeWindow);
        if (options.IsVisible)
        {
            nativeWindow.Show();
        }

        return ValueTask.FromResult(new OverlayWindow(options, nativeWindow));
    }

    private static void ApplyInitialTransparency(OverlayWindowOptions options, Win32OverlayWindow nativeWindow)
    {
        switch (options.TransparencyMode)
        {
            case TransparencyMode.Auto:
            case TransparencyMode.DwmGlassFrame:
                nativeWindow.ExtendFrameIntoClientArea();
                break;
            case TransparencyMode.LayeredWindowAttributes:
                nativeWindow.SetLayeredAlpha(byte.MaxValue);
                break;
            case TransparencyMode.UpdateLayeredWindow:
            case TransparencyMode.DirectComposition:
                throw new NotSupportedException($"{options.TransparencyMode} is reserved for a later renderer backend.");
            default:
                throw new ArgumentOutOfRangeException(nameof(options), $"Unsupported transparency mode: {options.TransparencyMode}.");
        }

        if (options.EnableBlurBehind && options.TransparencyMode != TransparencyMode.DwmGlassFrame)
        {
            nativeWindow.ExtendFrameIntoClientArea();
        }
    }

    public async ValueTask RunAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();

        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetimeCancellation.Token);
        TimeSpan interval = Options.FrameRateLimit.ToFrameInterval();

        Loaded?.Invoke(this);

        try
        {
            if (interval == TimeSpan.Zero)
            {
                while (!linked.IsCancellationRequested)
                {
                    RenderOneFrame();
                    await Task.Yield();
                }
            }
            else
            {
                using var timer = new PeriodicTimer(interval);
                while (await timer.WaitForNextTickAsync(linked.Token).ConfigureAwait(false))
                {
                    RenderOneFrame();
                }
            }
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
        }
        finally
        {
            Unloaded?.Invoke(this);
        }
    }

    public ValueTask StopAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lifetimeCancellation.Cancel();
        return ValueTask.CompletedTask;
    }

    public ValueTask ShowAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        nativeWindow.Show();
        VisibilityChanged?.Invoke(this, new OverlayWindowChangedEventArgs(currentBounds));
        return ValueTask.CompletedTask;
    }

    public ValueTask HideAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        nativeWindow.Hide();
        VisibilityChanged?.Invoke(this, new OverlayWindowChangedEventArgs(currentBounds));
        return ValueTask.CompletedTask;
    }

    public void Pause() => paused = true;

    public void Resume() => paused = false;

    public void SetBounds(WindowBounds bounds)
    {
        if (bounds.IsEmpty)
        {
            throw new ArgumentOutOfRangeException(nameof(bounds), "Overlay bounds must have positive width and height.");
        }

        nativeWindow.SetBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        currentBounds = bounds;
        BoundsChanged?.Invoke(this, new OverlayWindowChangedEventArgs(bounds));
    }

    public void MovePixels(int x, int y) => SetBounds(currentBounds with { X = x, Y = y });

    public void ResizePixels(int width, int height) => SetBounds(currentBounds with { Width = width, Height = height });

    public ValueTask RecreateAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        DeviceLost?.Invoke(this, new OverlayDeviceEventArgs("Manual recreation requested."));
        Resources.AdvanceGeneration();
        DeviceRestored?.Invoke(this, new OverlayDeviceEventArgs("Resource generation advanced."));
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
        nativeWindow.Dispose();
        lifetimeCancellation.Dispose();
        return ValueTask.CompletedTask;
    }

    private void RenderOneFrame()
    {
        if (paused && Options.HiddenRenderPolicy == HiddenRenderPolicy.Pause)
        {
            return;
        }

        DateTimeOffset start = DateTimeOffset.UtcNow;
        drawContext.Reset();

        try
        {
            nativeWindow.InvokeOnOwnerThread(() => RenderOneFrameOnOwnerThread(start));
        }
        catch when (Options.ExceptionPolicy == RenderExceptionPolicy.IgnoreAndContinue)
        {
            return;
        }
    }

    private void RenderOneFrameOnOwnerThread(DateTimeOffset start)
    {
        Render?.Invoke(drawContext);
        frameCount++;
        DateTimeOffset end = DateTimeOffset.UtcNow;
        FrameStats = new FrameStats(frameCount, end - start, end, Environment.CurrentManagedThreadId);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}

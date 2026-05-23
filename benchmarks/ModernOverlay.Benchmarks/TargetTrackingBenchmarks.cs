using BenchmarkDotNet.Attributes;
using ModernOverlay.Win32;

namespace ModernOverlay.Benchmarks;

[MemoryDiagnoser]
[BenchmarkCategory("TargetTracking")]
public class TargetTrackingBenchmarks : IDisposable
{
    private Win32OverlayWindow targetWindow = null!;
    private WindowHandle targetHandle;
    private OverlayTarget hwndTarget = null!;
    private FixedWindowTargetProvider provider = null!;
    private string targetTitle = null!;
    private int processId;
    private bool disposed;

    [GlobalSetup]
    public void Setup()
    {
        targetTitle = $"ModernOverlay target benchmark {Guid.NewGuid():N}";
        targetWindow = Win32OverlayWindow.Create(new Win32OverlayWindowOptions(
            ClassName: null,
            Title: targetTitle,
            X: 24,
            Y: 32,
            Width: 640,
            Height: 360,
            ClickThrough: false,
            TopMost: false,
            ToolWindow: true));
        targetHandle = new WindowHandle(targetWindow.Hwnd);
        hwndTarget = WindowTarget.FromHwnd(targetHandle);
        provider = new FixedWindowTargetProvider(targetHandle);
        processId = Environment.ProcessId;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        targetWindow.Dispose();
    }

    [Benchmark]
    public WindowBounds QueryWindowBoundsByHwnd()
    {
        _ = WindowQuery.TryGetWindowBounds(targetHandle, out WindowBounds bounds);
        return bounds;
    }

    [Benchmark]
    public WindowBounds QueryClientBoundsByHwnd()
    {
        _ = WindowQuery.TryGetClientBounds(targetHandle, out WindowBounds bounds);
        return bounds;
    }

    [Benchmark]
    public WindowHandle FindTargetByTitleContains()
    {
        _ = WindowQuery.TryFindWindowByTitle(targetTitle, MatchMode.Contains, out WindowHandle hwnd);
        return hwnd;
    }

    [Benchmark]
    public WindowHandle FindTargetByProcessId()
    {
        _ = WindowQuery.TryFindWindowByProcessId(processId, out WindowHandle hwnd);
        return hwnd;
    }

    [Benchmark]
    public WindowHandle ResolveCustomProvider()
    {
        _ = provider.TryResolve(out WindowHandle hwnd);
        return hwnd;
    }

    [Benchmark]
    public async ValueTask<WindowHandle> CreateHiddenOverlayWithHwndTarget()
    {
        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            Title = "ModernOverlay targeted benchmark overlay",
            IsVisible = false,
            Bounds = WindowBounds.FromPixels(0, 0, 320, 180),
            Target = hwndTarget,
            FrameRateLimit = FrameRateLimit.Fixed(60),
        }).ConfigureAwait(false);

        return overlay.Hwnd;
    }

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }

    private sealed class FixedWindowTargetProvider(WindowHandle hwnd) : IWindowTargetProvider
    {
        public bool TryResolve(out WindowHandle resolvedHwnd)
        {
            resolvedHwnd = hwnd;
            return true;
        }
    }
}

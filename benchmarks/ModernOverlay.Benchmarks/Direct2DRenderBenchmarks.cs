using BenchmarkDotNet.Attributes;
using ModernOverlay.Direct2D;
using ModernOverlay.Rendering;
using ModernOverlay.Win32;

namespace ModernOverlay.Benchmarks;

[MemoryDiagnoser]
[BenchmarkCategory("Direct2D")]
public class Direct2DRenderBenchmarks : IDisposable
{
    private Direct2DRenderBackend backend = null!;
    private bool disposed;
    private FrameInfo frameInfo;
    private OverlayResourceManager resources = null!;
    private SolidBrushHandle brush = null!;
    private Win32OverlayWindow window = null!;

    [GlobalSetup]
    public void Setup()
    {
        window = Win32OverlayWindow.Create(new Win32OverlayWindowOptions(
            ClassName: null,
            Title: "ModernOverlay Direct2D benchmark",
            X: 0,
            Y: 0,
            Width: 640,
            Height: 360,
            ClickThrough: true,
            TopMost: false,
            ToolWindow: true));
        resources = new OverlayResourceManager();
        brush = resources.CreateSolidBrush(ColorRgba.White);
        backend = new Direct2DRenderBackend();
        window.InvokeOnOwnerThread(() =>
        {
            backend.Initialize(new RenderBackendInitializeContext(
                new WindowHandle(window.Hwnd),
                new PixelSize(640, 360),
                DpiScale.Default,
                RenderQualityOptions.Default,
                PresentMode.BackendDefault));
        });
        frameInfo = new FrameInfo(
            1,
            DateTimeOffset.UtcNow,
            TimeSpan.Zero,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1d / 60d),
            TimeSpan.Zero,
            window.OwnerThreadId,
            DpiScale.Default,
            WindowBounds.FromPixels(0, 0, 640, 360),
            null);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        window.InvokeOnOwnerThread(backend.Dispose);
        brush.Dispose();
        window.Dispose();
    }

    [Benchmark]
    public bool ClearAndPresent()
    {
        bool presented = false;
        window.InvokeOnOwnerThread(() =>
        {
            _ = backend.BeginFrame(frameInfo);
            backend.Clear(ColorRgba.Transparent);
            presented = backend.EndFrame().Presented;
        });
        return presented;
    }

    [Benchmark]
    public bool DrawPrimitiveBatchAndPresent()
    {
        bool presented = false;
        window.InvokeOnOwnerThread(() =>
        {
            _ = backend.BeginFrame(frameInfo);
            backend.CommandSink.DrawLine(new PointF(8, 8), new PointF(220, 80), brush, 2f, null);
            backend.CommandSink.DrawRectangle(new RectF(32, 32, 180, 96), brush, 2f, null);
            backend.CommandSink.FillCircle(new PointF(280, 120), 32, brush);
            presented = backend.EndFrame().Presented;
        });
        return presented;
    }

    [Benchmark]
    public long ResizeRenderTarget()
    {
        long generation = 0;
        window.InvokeOnOwnerThread(() =>
        {
            backend.Resize(new PixelSize(640, 360), DpiScale.Default);
            generation = backend.Generation.Value;
        });
        return generation;
    }

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }
}

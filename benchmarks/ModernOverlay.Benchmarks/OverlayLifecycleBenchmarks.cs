using BenchmarkDotNet.Attributes;

namespace ModernOverlay.Benchmarks;

[MemoryDiagnoser]
[BenchmarkCategory("Lifecycle")]
public class OverlayLifecycleBenchmarks
{
    private int sequence;

    [Benchmark]
    public async ValueTask<WindowHandle> CreateAndDisposeHiddenOverlay()
    {
        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            Title = $"ModernOverlay benchmark {sequence++}",
            IsVisible = false,
            Bounds = WindowBounds.FromPixels(0, 0, 320, 180),
            FrameRateLimit = FrameRateLimit.Fixed(60),
        });

        return overlay.Hwnd;
    }

    [Benchmark]
    public async ValueTask<long> CreateRecreateAndDisposeHiddenOverlay()
    {
        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            Title = $"ModernOverlay recreate benchmark {sequence++}",
            IsVisible = false,
            Bounds = WindowBounds.FromPixels(0, 0, 320, 180),
            FrameRateLimit = FrameRateLimit.Fixed(60),
        });

        await overlay.RecreateAsync();
        return overlay.Resources.CurrentGeneration;
    }
}

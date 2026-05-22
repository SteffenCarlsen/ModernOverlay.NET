using BenchmarkDotNet.Attributes;

namespace ModernOverlay.Benchmarks;

[MemoryDiagnoser]
public class DrawContextBenchmarks
{
    private readonly DrawContext context = new();
    private readonly OverlayResourceManager resources = new();
    private SolidBrushHandle brush = null!;
    private FontHandle font = null!;
    private string text = null!;

    [GlobalSetup]
    public void Setup()
    {
        brush = resources.CreateSolidBrush(ColorRgba.White);
        font = resources.CreateFont(new FontOptions("Segoe UI", 18));
        text = "Benchmark overlay text";
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        brush.Dispose();
        font.Dispose();
    }

    [Benchmark]
    public ColorRgba? Clear()
    {
        context.Clear(ColorRgba.Transparent);
        return context.LastClearColor;
    }

    [Benchmark]
    public void DrawLine()
    {
        context.Draw.Line(new PointF(4, 4), new PointF(128, 96), brush);
    }

    [Benchmark]
    public void FillRectangle()
    {
        context.Fill.Rectangle(new RectF(8, 8, 96, 48), brush);
    }

    [Benchmark]
    public void ClipScope()
    {
        using ScopedClip _ = context.Clip(new RectF(0, 0, 320, 180));
    }

    [Benchmark]
    public SizeF MeasureText()
    {
        return context.Measure.Text(text, font);
    }
}

using ModernOverlay;
using ModernOverlay.Direct2D;

Direct2DOverlayBackend.Register();

await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
{
    Title = "ModernOverlay Shapes Sample",
    Bounds = new WindowBounds(120, 120, 720, 420),
    FrameRateLimit = FrameRateLimit.Fixed(60),
});

using SolidBrushHandle white = overlay.Resources.CreateSolidBrush(ColorRgba.White);
using SolidBrushHandle cyan = overlay.Resources.CreateSolidBrush(new ColorRgba(0.1f, 0.8f, 1f, 0.9f));
using LinearGradientBrushHandle gradient = overlay.Resources.CreateLinearGradientBrush(new LinearGradientBrushOptions(
    new PointF(40, 40),
    new PointF(680, 360),
    [
        new GradientStop(0f, new ColorRgba(0.9f, 0.2f, 0.35f, 0.85f)),
        new GradientStop(1f, new ColorRgba(0.15f, 0.75f, 0.45f, 0.85f)),
    ]));
using StrokeStyleHandle dash = overlay.Resources.CreateStrokeStyle(new StrokeStyleOptions { DashStyle = StrokeDashStyle.DashDot });
using GeometryPath path = overlay.Resources.CreateGeometry(builder => builder
    .MoveTo(new PointF(120, 310))
    .BezierTo(new PointF(210, 150), new PointF(340, 470), new PointF(430, 250))
    .ArcTo(new PointF(570, 310), new SizeF(80, 50), arcSize: GeometryArcSize.Large)
    .LineTo(new PointF(120, 310))
    .Close());

overlay.Render += frame =>
{
    frame.Clear(ColorRgba.Transparent);
    frame.Fill.RoundedRectangle(new RectF(36, 36, 648, 320), 24, 24, gradient);
    frame.Draw.RoundedRectangle(new RectF(36, 36, 648, 320), 24, 24, white, dash, 3f);
    frame.Draw.Geometry(path, white, 3f);
    frame.Draw.Circle(new PointF(560, 130), 48, cyan, 4f);

    using (frame.Transform(Matrix3x2F.CreateTranslation(26, -8)))
    {
        frame.Draw.Arrow(new PointF(86, 112), new PointF(244, 86), white, 3f);
    }
};

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
await overlay.RunAsync(cts.Token);
